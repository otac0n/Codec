// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using Codec.MGS.Services;
    using Codec.MGS.Streams;
    using Codec.Streams;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, (long Offset, long Length, long EncodedLength) Section, long Offset, long Length);
    using Section = (long Offset, long Length, long EncodedLength);

    public class DirArchive(string fullPath, string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly ImmutableDictionary<Variant, ImmutableDictionary<byte, string>> Extensions = new Dictionary<Variant, ImmutableDictionary<byte, string>>()
        {
            [Variant.MGS2] = new Dictionary<byte, string>()
            {
                { 0x01, "bin" },
                { 0x02, "cv2" },
                { 0x04, "evm" },
                { 0x05, "far" },
                { 0x06, "gcx" },
                { 0x07, "hzx" },
                { 0x0A, "kms" },
                { 0x0B, "lt2" },
                { 0x0C, "mar" },
                { 0x0E, "o2d" },
                { 0x11, "row" },
                { 0x12, "sar" },
                { 0x13, "tri" },
                { 0x15, "var" },
                { 0x19, "zms" },
                { 0x7D, "face" },
            }.ToImmutableDictionary(),
            [Variant.MGSPW] = new Dictionary<byte, string>()
            {
                { 0x01, "bin" },
                { 0x02, "gcx" },
                { 0x03, "tri" },
                { 0x04, "mdh" },
                { 0x05, "mds" },
                { 0x06, "lt2" },
                { 0x07, "cv2" },
                { 0x08, "mtar" },
                { 0x09, "mtsq" },
                { 0x0A, "mtfa" },
                { 0x0B, "mtcm" },
                { 0x0C, "geom" },
                { 0x0F, "nav" },
                { 0x10, "cvd" },
                { 0x11, "eft" },
                { 0x12, "zon" },
                { 0x13, "mdp" }, // "mdb", "mdc", "mdl"
                { 0x14, "txp" },
                { 0x15, "kms" },
                { 0x16, "rpd" },
                { 0x17, "fcx" },
                { 0x18, "mtst" },
                { 0x19, "mdpb" },
                { 0x1A, "mdpe" },
                { 0x1B, "dcd" },
                { 0x1C, "ypk" },
                { 0x1D, "spk" },
                { 0x1E, "ohd" },
                { 0x1F, "mmd" },
                { 0x20, "vrd" },
                { 0x21, "vrdv" },
                { 0x22, "vrdt" },
                { 0x23, "vcp" },
                { 0x24, "vcpg" },
                { 0x30, "mgm" },
                { 0x31, "prx" },
                { 0x32, "rlc" },
                { 0x33, "ptcp" },
                { 0x34, "cddl" },
                { 0x35, "cap" },
                { 0x36, "pcmp" },
                { 0x37, "sep" },
                { 0x38, "bgp" },
                { 0x5D, "olang" },
                { 0x5E, "la3" },
                { 0x5F, "la2" },
                { 0x60, "slot" },
                { 0x61, "vram" },
                { 0x63, "cmf" },
                { 0x64, "eqp" },
                { 0x65, "vlm" },
                { 0x66, "lst" },
                { 0x68, "png" },
                { 0x69, "img" },
                { 0x6A, "vib" },
                { 0x6B, "rat" },
                { 0x6C, "rcm" },
                { 0x6D, "ola" },
                { 0x6E, "row" },
                { 0x6F, "mtra" },
                { 0xF0, "dar" },
                { 0xF1, "qar" },
                { 0xF2, "cnf" },
                { 0xFF, "psq" },
            }.ToImmutableDictionary(),
            [Variant.MGS4] = new Dictionary<byte, string>()
            {
                { 0x01, "bin" },
                { 0x02, "gcx" },
                { 0x03, "txn" }, // "tri"
                { 0x04, "mdh" },
                { 0x05, "mds" },
                { 0x06, "lt2" }, // "lt3"
                { 0x07, "cv2" },
                { 0x08, "mtar" },
                { 0x09, "mtsq" },
                { 0x0A, "mtfa" }, // "far"
                { 0x0B, "mtcm" },
                { 0x0C, "geom" },
                { 0x0D, "mdn" }, // "mdl", "mdb", "mdc"
                { 0x0F, "nav" },
                { 0x10, "cvd" }, // "van"
                { 0x11, "cnp" }, // "eft"
                { 0x12, "zon" },
                { 0x13, "rpd" },
                { 0x14, "abc" },
                { 0x15, "nv2" },
                { 0x16, "spu" },
                { 0x17, "fcv" },
                { 0x18, "phs" },
                { 0x19, "eqpp" },
                { 0x1A, "phpr" },
                { 0x1B, "phes" },
                { 0x1C, "sds" },
                { 0x1D, "vab" },
                { 0x1E, "ssp" },
                { 0x1F, "rvb" },
                { 0x20, "gsp" },
                { 0x21, "dlz" }, // "dld"
                { 0x22, "rdv" },
                { 0x23, "octt" },
                { 0x24, "octl" },
                { 0x25, "vfp" },
                { 0x26, "octs" },
                { 0x27, "bpef" },
                { 0x28, "sfp" },
                { 0x29, "pdl" },
                { 0x2A, "ptl" },
                { 0x2B, "cpef" },
                { 0x2C, "dlp" },
                { 0x4F, "at3" },
                { 0x5A, "png" },
                { 0x5B, "pam" },
                { 0x5C, "dbd" },
                { 0x5D, "jpg" },
                { 0x5E, "ico" },
                { 0x5F, "la2" },
                { 0x60, "slot" },
                { 0x61, "vpo" },
                { 0x62, "fpo" },
                { 0x63, "cv4" },
                { 0x64, "mcl" },
                { 0x65, "vlm" },
                { 0x66, "lh4" },
                { 0x67, "csr" },
                { 0x68, "var" },
                { 0x69, "img" },
                { 0x6A, "vib" },
                { 0x6B, "rat" },
                { 0x6C, "rcm" },
                { 0x6D, "ola" },
                { 0x6E, "raw" }, // "row"
                { 0x6F, "mtra" },
                { 0xFF, "psq" },
            }.ToImmutableDictionary(),
            [Variant.MGSTTS] = new Dictionary<byte, string>()
            {
                [0x0A] = "kmy",
                [0x13] = "tpl",
            }.ToImmutableDictionary(),
        }.ToImmutableDictionary();

        internal static readonly ImmutableDictionary<uint, string> Groups = new Dictionary<uint, string>()
        {
            [0x00000002] = "cache",
            [0x00000003] = "resident",
            [0x00000004] = "delayload",
            [0x00000005] = "delayload_w",
            [0x00000010] = "sound",
            [0x00010000] = "nocache",
        }.ToImmutableDictionary();

        private Variant variant;

        public enum Variant
        {
            Unknown = 0,
            MGS2,
            MGSTTS,
            MGS4,
            MGSPW,
        }

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem(
                "*.dir",
                static (serviceProvider, fullPath, parentRelativePath, parent, parentPath) => Validate(parentRelativePath, parent),
                static (fullPath, parentRelativePath, parent, parentPath) => new DirArchive(fullPath, parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry) => entry.FileName;

        private static bool Validate(string parentRelativePath, IFileSystem parent)
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            if (source.Length <= sizeof(uint) + 2 * Marshal.SizeOf<DirEntryInfo>())
            {
                return false;
            }

            return ProcessArchive(source, validate: true);
        }

        private static bool ProcessArchive(FileSystemStream source, Action<Endianness, bool, uint>? setup = null, Action<uint, DirEntryInfo, Section, long>? handleFile = null, Action<uint, DirEntryInfoWide, Section, long>? handleFileWide = null, bool validate = false)
        {
            var entryCountLE = source.ReadUInt32LittleEndian();
            var entryCountBE = BinaryPrimitives.ReverseEndianness(entryCountLE);
            var (entryCount, endianness) = entryCountBE < entryCountLE
                ? (entryCountBE, Endianness.BigEndian)
                : (entryCountLE, Endianness.LittleEndian);

            var wideIndexEntries = source.ReadUInt32LittleEndian() == 0;
            if (!wideIndexEntries)
            {
                source.Position -= sizeof(uint);
            }

            if (entryCount == 0)
            {
                return false;
            }

            bool ContinueWithKnonWidth<THeader, TIndex>(Action<uint, TIndex, Section, long>? handleFile)
                where THeader : struct
                where TIndex : struct, IDirEntryInfo
            {
                if (validate && source.Length <= Marshal.SizeOf<THeader>() + (entryCount * Marshal.SizeOf<TIndex>()))
                {
                    return false;
                }

                var dirEntries = source.ReadArrayWithEndianness<TIndex>(entryCount, endianness);
                var dataPtr = source.Position;
                DetermineSectorSize<THeader, TIndex>(dirEntries, source.Length, out var alignment);
                setup?.Invoke(endianness, wideIndexEntries, alignment);
                return WalkEntries<TIndex>(dirEntries, alignment, ref dataPtr, handleFile, validate);
            }

            return wideIndexEntries
                ? ContinueWithKnonWidth<DirHeaderWide, DirEntryInfoWide>(handleFileWide)
                : ContinueWithKnonWidth<DirHeader, DirEntryInfo>(handleFile);
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);

            var extensions = ImmutableDictionary<byte, string>.Empty;

            var entries = new List<Entry>();

            void Process<TIndex>(uint group, TIndex entry, Section section, long length)
                where TIndex : IDirEntryInfo
            {
                var variantString = this.variant.ToString().ToLowerInvariant();

                if (!Groups.TryGetValue(group, out var groupName))
                {
                    groupName = group.ToString("x6", CultureInfo.InvariantCulture);
                }

                var archiveName = parent.Path.GetFileNameWithoutExtension(fullPath);
                var parentFolder = parent.Path.GetFileNameWithoutExtension(parent.Path.GetDirectoryName(fullPath));

                if (variantString == "mgstts" ||
                    !(JoyDictService.TryGetOriginalFileName(variantString, "stage.dat", null, archiveName, entry.FileName, entry.Extension, out var fileName) ||
                    JoyDictService.TryGetOriginalFileName(variantString, "stage.dat", null, parentFolder, entry.FileName, entry.Extension, out fileName)))
                {
                    if (!extensions.TryGetValue(entry.Extension, out var ext))
                    {
                        ext = entry.Extension.ToString("x2", CultureInfo.InvariantCulture);
                    }

                    fileName = $"{entry.FileName:x6}.{ext}";
                }

                entries.Add(($"{groupName}/{fileName}", section, entry.Offset, length));
            }

            ProcessArchive(
                source,
                (endianness, wideIndexEntries, sectorSize) =>
                {
                    this.variant = DetermineVariant(endianness, wideIndexEntries, sectorSize);
                    extensions = Extensions.GetValueOrDefault(this.variant, extensions);
                },
                Process,
                Process);

            return entries;
        }

        public static long GetFileSize<THeader, TIndex>(TIndex[] dirEntries, long alignment)
            where THeader : struct
            where TIndex : struct, IDirEntryInfo
        {
            long dataPtr = Marshal.SizeOf<THeader>() + Marshal.SizeOf<TIndex>() * dirEntries.Length;
            WalkEntries(dirEntries, alignment, ref dataPtr);
            return dataPtr;
        }

        public static bool WalkEntries<TIndex>(TIndex[] dirEntries, long alignment, ref long dataPtr, Action<uint, TIndex, Section, long>? handleFile = null, bool validate = false)
            where TIndex : struct, IDirEntryInfo
        {
            var group = 0U;
            var sectionSize = 0U;
            uint? compressedSize = null;
            var entryCount = dirEntries.Length;
            for (var i = 0; i < entryCount; i++)
            {
                var entry = dirEntries[i];

                if (validate && i == entryCount - 1)
                {
                    return entry.Id == 0;
                }

                switch (entry.Extension)
                {
                    case 0x00:
                        if (entry.FileName == 0)
                        {
                            if (validate)
                            {
                                return entry.Offset == 0;
                            }

                            break;
                        }

                        goto default;

                    case 0x7D:
                        break;

                    case 0x7E:
                        compressedSize = entry.FileName;
                        break;

                    case 0x7F:
                        if (entry.FileName != 0)
                        {
                            dataPtr = StreamExtensions.Align(dataPtr, alignment);
                            sectionSize = (uint)entry.Offset;
                            group = entry.FileName;
                        }
                        else
                        {
                            dataPtr += compressedSize ?? entry.Offset;
                            compressedSize = null;
                            sectionSize = 0;
                            group = 0;
                        }

                        break;

                    default:
                        if (validate)
                        {
                            var start = entry.Offset;
                            var end = dirEntries[i + 1].Offset;
                            if (end < start || start >= sectionSize || end > sectionSize)
                            {
                                return false;
                            }
                        }

                        handleFile?.Invoke(group, entry, (dataPtr, sectionSize, compressedSize ?? sectionSize), dirEntries[i + 1].Offset - entry.Offset);
                        break;
                }
            }

            return !validate;
        }

        private static Variant DetermineVariant(Endianness endianness, bool wideIndexEntries, uint sectorSize) =>
            (endianness, wideIndexEntries, sectorSize) switch
            {
                (Endianness.LittleEndian, false, 0x800) => Variant.MGS2,
                (Endianness.BigEndian, true, 0x800) => Variant.MGS4,
                (Endianness.BigEndian, false, 0x800) => Variant.MGSTTS,
                (Endianness.LittleEndian, true, 0x1000) => Variant.MGSPW,
                _ => Variant.Unknown,
            };

        private static bool DetermineSectorSize<THeader, TIndex>(TIndex[] dirEntries, long length, out uint alignment)
            where THeader : struct
            where TIndex : struct, IDirEntryInfo
        {
            for (var bit = 11; bit <= 12; bit++)
            {
                alignment = (uint)(1 << bit);
                var sum = GetFileSize<THeader, TIndex>(dirEntries, alignment);
                if (sum == length)
                {
                    return true;
                }
            }

            alignment = 0x800;
            return false;
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    var source = parent.File.Open(parentRelativePath, options);
                    if (entry.Section.Length != entry.Section.EncodedLength)
                    {
                        if (this.variant == Variant.MGS2)
                        {
                            source.Position = entry.Section.Offset;
                            var keyA = source.ReadUInt16LittleEndian();
                            var (iv, salt) = DecodingStream.MakeKey(keyA, 0x9385U, 0x0116U);
                            Stream section = new OffsetStreamSpan(source, entry.Section.Offset, entry.Section.EncodedLength, Ownership.Dispose);
                            section = new DecodingStream(iv, salt, section, Ownership.Dispose);
                            section = new CachingSeekableStream(section);
                            section.Write([0x78, 0x9C]);
                            section = new DeflateStream(section, CompressionMode.Decompress);
                            section = new CachingSeekableStream(section);
                            return new OffsetStreamSpan(section, entry.Offset, entry.Length, Ownership.Dispose);
                        }
                        else if (this.variant == Variant.MGSTTS)
                        {
                            Stream section = new OffsetStreamSpan(source, entry.Section.Offset, entry.Section.EncodedLength, Ownership.Dispose);
                            section = new ZLibStream(section, CompressionMode.Decompress);
                            section = new CachingSeekableStream(section, entry.Section.Length);
                            return new OffsetStreamSpan(section, entry.Offset, entry.Length, Ownership.Dispose);
                        }

                        throw new NotSupportedException();
                    }
                    else
                    {
                        return new OffsetStreamSpan(source, entry.Section.Offset + entry.Offset, entry.Length, Ownership.Dispose);
                    }
                },
                updated =>
                {
                    throw new NotImplementedException();
                });
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DirHeader
        {
            public uint EntryCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DirHeaderWide
        {
            public uint EntryCount;
            public uint Padding;
        }

        public interface IDirEntryInfo
        {
            public uint Id { get; }

            public long Offset { get; set; }

            public uint FileName { get; }

            public byte Extension { get; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DirEntryInfo : IDirEntryInfo
        {
            public uint Id { get; set; }

            public uint Offset { get; set; }

            public uint FileName => this.Id & 0xFFFFFF;

            public byte Extension => (byte)((this.Id >> 24) & 0xFF);

            long IDirEntryInfo.Offset
            {
                readonly get => this.Offset;
                set => this.Offset = (uint)value;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DirEntryInfoWide : IDirEntryInfo
        {
            public uint Id { get; set; }

            public uint PaddingA { get; set; }

            public ulong Offset { get; set; }

            public uint FileName => this.Id & 0xFFFFFF;

            public byte Extension => (byte)((this.Id >> 24) & 0xFF);

            long IDirEntryInfo.Offset
            {
                readonly get => (long)this.Offset;
                set => this.Offset = (ulong)value;
            }
        }
    }
}
