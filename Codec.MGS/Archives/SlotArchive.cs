namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    internal class SlotArchive : IndexedFileSystem<SlotArchive.Entry>
    {
        public static readonly ImmutableDictionary<uint, string> Regions = new Dictionary<uint, string>
        {
            { 0x00000002, "cache" },
            { 0x00000003, "resident" },
            { 0x00000004, "delayload" },
            { 0x00000005, "delayload_w" },
            { 0x00000010, "sound" },
            { 0x00010000, "nocache" },
        }.ToImmutableDictionary();

        public static readonly (string Extension, uint Id)[] Extensions =
        {
            ("rdv",  0x22),
            ("mtfa", 0x0A),
            ("mds",  0x05),
            ("raw",  0x6E),
            ("dlz",  0x21),
            ("rat",  0x6B),
            ("far",  0x0A),
            ("at3",  0x4F),
            ("vab",  0x1D),
            ("rvb",  0x1F),
            ("eqpp", 0x19),
            ("csr",  0x67),
            ("psq",  0xFF),
            ("gcx",  0x02),
            ("cvd",  0x10),
            ("ico",  0x5E),
            ("gsp",  0x20),
            ("txn",  0x03),
            ("nv2",  0x15),
            ("sds",  0x1C),
            ("fpo",  0x62),
            ("rpd",  0x13),
            ("dld",  0x21),
            ("vlm",  0x65),
            ("var",  0x68),
            ("phs",  0x18),
            ("pdl",  0x29),
            ("ptl",  0x2A),
            ("bpef", 0x27),
            ("spu",  0x16),
            ("la2",  0x5F),
            ("dbd",  0x5C),
            ("octl", 0x24),
            ("rcm",  0x6C),
            ("ola",  0x6D),
            ("lt2",  0x06),
            ("mtsq", 0x09),
            ("vfp",  0x25),
            ("row",  0x6E),
            ("mdh",  0x04),
            ("fcv",  0x17),
            ("bin",  0x01),
            ("cnp",  0x11),
            ("lt3",  0x06),
            ("img",  0x69),
            ("octt", 0x23),
            ("vib",  0x6A),
            ("zon",  0x12),
            ("octs", 0x26),
            ("nav",  0x0F),
            ("png",  0x5A),
            ("mdn",  0x0D),
            ("pam",  0x5B),
            ("abc",  0x14),
            ("tri",  0x03),
            ("cv4",  0x63),
            ("cpef", 0x2B),
            ("mtra", 0x6F),
            ("geom", 0x0C),
            ("phpr", 0x1A),
            ("dlp",  0x2C),
            ("sfp",  0x28),
            ("vpo",  0x61),
            ("cv2",  0x07),
            ("mcl",  0x64),
            ("lh4",  0x66),
            ("mtar", 0x08),
            ("jpg",  0x5D),
            ("eft",  0x11),
            ("slot", 0x60),
            ("ssp",  0x1E),
            ("mdl",  0x0D),
            ("mtcm", 0x0B),
            ("phes", 0x1B),
            ("mdb",  0x0D),
            ("van",  0x10),
            ("mdc",  0x0D),
        };

        public static readonly ILookup<uint, string> ExtensionsLookup = Extensions.ToLookup(e => e.Id, e => e.Extension);

        private static readonly int SectorSize = 0x800;

        private readonly string filePath;
        private readonly IFileSystem fileSystem;

        public SlotArchive(string filePath, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            this.filePath = filePath;
            this.fileSystem = fileSystem;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.slot", static (fullPath, parentRelativePath, parent, parentPath) => new SlotArchive(parentRelativePath, parent));
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            SlotHeader header;
            using var slotDat = this.fileSystem.File.OpenRead(this.filePath);
            header = slotDat.ReadBigEndian<SlotHeader>();

            slotDat.Seek(SectorSize, SeekOrigin.Begin);
            var entries = new List<Entry>();
            for (var i = 0; i < header.PageCount; i++)
            {
                SlotPageInfo pageInfo;
                pageInfo.Offset = slotDat.Position;
                var cnf = slotDat.ReadBigEndian<DataCNF>();

                var size = 0L;
                size += Marshal.SizeOf<DataCNF>() + Marshal.SizeOf<DataCNFTag>() * cnf.NumTags;
                size = StreamExtensions.Align(size, SectorSize);

                var tags = new DataCNFTag[cnf.NumTags];
                for (var j = 0; j < cnf.NumTags; j++)
                {
                    var tag = slotDat.ReadBigEndian<DataCNFTag>();
                    if (tag.Id == 0x7F000000)
                    {
                        size += StreamExtensions.Align(tag.Offset, SectorSize);
                    }

                    tags[j] = tag;
                }

                var sectionOffset = pageInfo.Offset + Marshal.SizeOf<DataCNF>();
                var encodedSectionSize = 0L;
                var currentSectionSize = 0L;
                var currentRegion = string.Empty;
                for (var j = 0; j < cnf.NumTags; j++)
                {
                    var tag = tags[j];
                    var filename = tag.Id & 0xFFFFFF;
                    var ext = tag.Id >> 24;
                    switch (ext)
                    {
                        case 0x00:
                            Debug.Assert(j == cnf.NumTags - 1);
                            break;

                        case 0x7F:
                            if (filename != 0)
                            {
                                currentRegion = GetFileName(filename);
                                currentSectionSize = tag.Offset;
                                sectionOffset = StreamExtensions.Align(sectionOffset, SectorSize);
                            }
                            else
                            {
                                if (encodedSectionSize != 0)
                                {
                                    sectionOffset += encodedSectionSize;
                                    encodedSectionSize = 0;
                                }
                                else
                                {
                                    sectionOffset += tag.Offset;
                                }
                            }

                            break;

                        case 0x7E:
                            encodedSectionSize = tag.Size;
                            throw new NotImplementedException();
                            break;

                        case 0x7D:
                            break;

                        default:
                            Debug.Assert(j < cnf.NumTags - 1);
                            entries.Add(new($"{i}/{currentRegion}/{GetFileName(filename)}.{GetExtension(ext)}", sectionOffset + tag.Offset, Math.Max(tags[j + 1].Offset - tag.Offset, 0)));
                            break;
                    }
                }

                pageInfo.Size = size;
                slotDat.Seek(pageInfo.Offset + size, SeekOrigin.Begin);
            }

            return entries;
        }

        protected override string GetEntryName(Entry entry) =>
            entry.Path;

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            new OffsetStreamSpan(this.fileSystem.File.Open(this.filePath, parentOptions), entry.Offset, entry.Size, Ownership.Dispose);

        private static string GetExtension(uint ext) =>
            ExtensionsLookup[ext].FirstOrDefault() ?? ext.ToString("x2");

        private static string GetFileName(uint filename) =>
            Regions.TryGetValue(filename, out var region) ? region : filename.ToString("x6");

        public record struct Entry(string Path, long Offset, long Size);

        struct SlotHeader
        {
            public uint Timestamp;
            public ushort Version;
            public ushort PageSize;
            public ushort PageCount;
            public ushort UnknownA;
            public uint UnknownB;
        }

        struct SlotPageInfo
        {
            public long Offset;
            public long Size;
        }

        struct DataCNF
        {
            public uint NumTags;
            public uint Pad;
        }

        struct DataCNFTag
        {
            public uint Id;
            public int Size;
            public long Offset;
        }
    }
}
