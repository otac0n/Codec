namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using Codec.MGS.Files;
    using Codec.MGS.Streams;
    using Codec.Streams;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long Length);

    public class StageDatPdtArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly string[] KnownFolders =
        [
            "browser",
            "demoops",
            "ending_flow",
            "epigram",
            "extra",
            "flashdemo",
            "init",
            "ms_lobby",
            "my_outer",
            "my_outer_ap",
            "my_outer_tfr",
            "my_outer_trade",
            "n02pw",
            "n18pw",
            "n22pw",
            "ntconnect",
            "pre_extra",
            "r_coop_demo",
            "r_date_mission",
            "r_lobby",
            "r_main01",
            "r_main02",
            "r_net_coop2",
            "r_net_coop2_weapon",
            "r_net_coop4",
            "r_net_coop4_ai",
            "r_net_prison",
            "r_net01",
            "r_netcv",
            "r_prologue",
            "r_title",
            "result",
            "tfr",
            "title",
            "vs_lobby",
            "vs_result",
            "w00s01a",
            "w00s01apscan",
            "w01s01a",
            "w01s01b",
            "w01s02a",
            "w01s02n",
            "w01s03a",
            "w01s04a",
            "w01s05a",
            "w01s06a",
            "w01s07a",
            "w01s07b",
            "w01s07c",
            "w01s07n",
            "w02s01a",
            "w02s02a",
            "w02s02n",
            "w02s03a",
            "w02s03b",
            "w02s03c",
            "w02s03n",
            "w02s04a",
            "w02s05a",
            "w02s06a",
            "w02s06b",
            "w02s07a",
            "w03s01a",
            "w03s02a",
            "w03s02b",
            "w03s03a",
            "w03s04a",
            "w03s04n",
            "w03s05a",
            "w04s01a",
            "w04s01n",
            "w04s02a",
            "w04s02b",
            "w04s02c",
            "w04s03a",
            "w04s04a",
            "w04s05a",
            "w04s05n",
            "w04s06a",
            "w04s06b",
            "w05s01a",
            "w05s02a",
            "w05s03a",
            "w05s04a",
            "w05s05a",
            "w05s06a",
            "w05s07a",
            "w06s01a",
            "w06s02a",
            "w06s02b",
            "w06s02c",
            "w07s01a",
            "w08s01a",
        ];

        private static readonly Dictionary<string, byte> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".bgp",   0x38 },
            { ".bin",   0x01 },
            { ".cap",   0x35 },
            { ".cddl",  0x34 },
            { ".cmf",   0x63 },
            { ".cnf",   0xF2 },
            { ".cv2",   0x07 },
            { ".cvd",   0x10 },
            { ".dar",   0xF0 },
            { ".dcd",   0x1B },
            { ".eft",   0x11 },
            { ".eqp",   0x64 },
            { ".fcx",   0x17 },
            { ".gcx",   0x02 },
            { ".geom",  0x0C },
            { ".img",   0x69 },
            { ".kms",   0x15 },
            { ".la2",   0x5F },
            { ".la3",   0x5E },
            { ".lst",   0x66 },
            { ".lt2",   0x06 },
            { ".mdb",   0x13 },
            { ".mdc",   0x13 },
            { ".mdh",   0x04 },
            { ".mdl",   0x13 },
            { ".mdp",   0x13 },
            { ".mdpb",  0x19 },
            { ".mdpe",  0x1A },
            { ".mds",   0x05 },
            { ".mgm",   0x30 },
            { ".mmd",   0x1F },
            { ".mtar",  0x08 },
            { ".mtcm",  0x0B },
            { ".mtfa",  0x0A },
            { ".mtra",  0x6F },
            { ".mtsq",  0x09 },
            { ".mtst",  0x18 },
            { ".nav",   0x0F },
            { ".ohd",   0x1E },
            { ".ola",   0x6D },
            { ".olang", 0x5D },
            { ".pcmp",  0x36 },
            { ".png",   0x68 },
            { ".prx",   0x31 },
            { ".psq",   0xFF },
            { ".ptcp",  0x33 },
            { ".qar",   0xF1 },
            { ".rat",   0x6B },
            { ".rcm",   0x6C },
            { ".rlc",   0x32 },
            { ".row",   0x6E },
            { ".rpd",   0x16 },
            { ".sep",   0x37 },
            { ".slot",  0x60 },
            { ".spk",   0x1D },
            { ".tri",   0x03 },
            { ".txp",   0x14 },
            { ".vcp",   0x23 },
            { ".vcpg",  0x24 },
            { ".vib",   0x6A },
            { ".vlm",   0x65 },
            { ".vram",  0x61 },
            { ".vrd",   0x20 },
            { ".vrdt",  0x22 },
            { ".vrdv",  0x21 },
            { ".ypk",   0x1C },
            { ".zon",   0x12 },
        };

        private PdtKeys keyHeader;

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("stagedat.pdt", static (fullPath, parentRelativePath, parent, parentPath) => new StageDatPdtArchive(parentRelativePath, parent));
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            this.keyHeader = source.ReadLittleEndian<PdtKeys>();
            var (iv, salt) = DecodingStream.MakeKey(this.keyHeader.SaltA, this.keyHeader.SaltB, this.keyHeader.SaltC);
            using var decoded = new DecodingStream(iv, salt, source, Ownership.None);
            var header = decoded.ReadLittleEndian<PdtHeader>();
            var table = decoded.ReadArrayLittleEndian<PdtTable>(header.PageCount);
            var lookup = decoded.ReadArrayLittleEndian<PdtLookupBST>(header.PageCount);

            void FindFile(string folder, string fileNameWithExtension, Action<uint, (long Offset, long Length)> found)
            {
                var key = StringCode.Combine32(StringCode.Hash24(folder), this.HashFileNameWithExtension(fileNameWithExtension));
                var ix = Array.FindIndex(lookup, item => item.Key == key);
                if (ix != -1)
                {
                    var entry = table[lookup[ix].Index];
                    found(key, (entry.Offset, entry.Length));
                }
            }

            var handled = new HashSet<uint>();
            var entries = new List<Entry>();
            foreach (var knownFolder in KnownFolders)
            {
                FindFile(knownFolder, "data.cnf", (dataCnfKey, chunk) =>
                {
                    if (handled.Add(dataCnfKey))
                    {
                        var dataCnfEntry = (this.Path.Combine(knownFolder, "data.cnf"), chunk.Offset, chunk.Length);
                        entries.Add(dataCnfEntry);

                        string section = null;
                        using var dataCnfFile = this.ReadFile(source, dataCnfEntry, Ownership.None);
                        DataCnfFile.WalkFile(dataCnfFile, s => section = s, f =>
                        {
                            var fileName = f.TrimStart('@');
                            FindFile(knownFolder, fileName, (key, fileChunk) =>
                            {
                                if (handled.Add(key))
                                {
                                    entries.Add((this.Path.Combine(knownFolder, fileName), fileChunk.Offset, fileChunk.Length));
                                }
                            });
                        });
                    }
                });
            }

            foreach (var item in lookup)
            {
                var entry = table[item.Index];
                var hash = item.Key;
                if (handled.Add(item.Key))
                {
                    entries.Add(($"{item.Key:x8}.bin", entry.Offset, entry.Length));
                }
            }

            return entries;
        }

        protected override string GetEntryName(Entry entry) =>
            entry.FileName;

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    return this.ReadFile(parent.File.Open(parentRelativePath, options), entry, Ownership.Dispose);
                },
                updated =>
                {
                    throw new NotImplementedException();
                });
        }

        private static uint HashFileNameWithExtension(string fileName, string extension)
        {
            return StringCode.Hash24(fileName) | (uint)(Extensions[extension] << 24);
        }

        private uint HashFileNameWithExtension(string fileNameWithExtension) =>
            HashFileNameWithExtension(this.Path.GetFileNameWithoutExtension(fileNameWithExtension), this.Path.GetExtension(fileNameWithExtension));

        private Stream ReadFile(Stream source, Entry entry, Ownership ownership)
        {
            var section = new OffsetStreamSpan(source, entry.Offset, entry.Length, ownership);
            var (iv, salt) = DecodingStream.MakeKey(this.keyHeader.SaltA, this.keyHeader.SaltB, this.keyHeader.SaltC);
            var decoded = new DecodingStream(iv, salt, section, Ownership.Dispose);
            var header = decoded.ReadLittleEndian<PdtCompressedHeader>();
            var compressed = new OffsetStreamSpan(decoded, decoded.Position, decoded.Length - decoded.Position - 1, Ownership.Dispose);
            var decompressed = new ZLibStream(compressed, CompressionMode.Decompress);
            return new CachingSeekableStream(decompressed, header.DecompressedLength);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct PdtKeys
        {
            public uint SaltA;
            public uint SaltB;
            public uint SaltC;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PdtHeader
        {
            public uint UnknownA;
            public uint UnknownB;
            public uint UnknownC;
            public ushort PageCount;
            public ushort UnknownD;
            public uint LookupOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PdtTable
        {
            public uint Length;
            public uint Key;
            public uint Offset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PdtLookupBST
        {
            public uint Key;
            public uint Index;
            public uint LessThanBranchOffset;
            public uint GreaterThanBranchOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct PdtCompressedHeader
        {
            public uint DecompressedLength;
        }
    }
}
