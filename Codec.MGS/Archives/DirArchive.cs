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
    using System.Linq;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long Length);

    public class DirArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly ImmutableDictionary<Variant, ImmutableDictionary<byte, string>> Extensions = new Dictionary<Variant, ImmutableDictionary<byte, string>>()
        {
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
        }.ToImmutableDictionary();

        private static readonly ImmutableDictionary<uint, string> Groups = new Dictionary<uint, string>()
        {
            [0x00000002] = "cache",
            [0x00000003] = "resident",
            [0x00000004] = "delayload",
            [0x00000005] = "delayload_w",
            [0x00000010] = "sound",
            [0x00010000] = "nocache",
        }.ToImmutableDictionary();

        public enum Variant
        {
            Unknown = 0,
            MGS4,
            MGSPW,
        }

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem(
                "*.dir",
                static (serviceProvider, fullPath, parentRelativePath, parent, parentPath) => Validate(parentRelativePath, parent),
                static (fullPath, parentRelativePath, parent, parentPath) => new DirArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry) => entry.FileName;

        private static bool Validate(string parentRelativePath, IFileSystem parent)
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            if (source.Length <= sizeof(uint) + 2 * Marshal.SizeOf<DirEntryInfo>())
            {
                return false;
            }

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

            if (entryCount == 0 || source.Length <= sizeof(uint) + (entryCount * (wideIndexEntries ? Marshal.SizeOf<DirEntryInfoWide>() : Marshal.SizeOf<DirEntryInfo>())))
            {
                return false;
            }

            var dirEntries = wideIndexEntries
                ? [.. source.ReadArrayWithEndianness<DirEntryInfoWide>(entryCount, endianness).Select(e => new DirEntryInfo { Id = e.Id, Offset = (uint)e.Offset })]
                : source.ReadArrayWithEndianness<DirEntryInfo>(entryCount, endianness);

            var sectorSize = DetermineSectorSize(dirEntries, source.Length);

            var dataStart = StreamExtensions.Align(source.Position, sectorSize);
            for (var i = 0; i < entryCount; i++)
            {
                var last = i == entryCount - 1;
                var entry = dirEntries[i];
                switch (entry.Extension)
                {
                    case 0x00:
                        return last;

                    case 0x7D:
                    case 0x7E:
                    case 0x7F:
                        if (last)
                        {
                            return false;
                        }

                        if (entry.Id == 0x7F000000)
                        {
                            dataStart = StreamExtensions.Align(dataStart + entry.Offset, sectorSize);
                        }

                        break;

                    default:
                        if (last)
                        {
                            return false;
                        }

                        var start = dataStart + entry.Offset;
                        var end = dataStart + dirEntries[i + 1].Offset;
                        if (end < start || start >= source.Length || end > source.Length)
                        {
                            return false;
                        }

                        break;
                }
            }

            return false;
        }

        private static Variant DetermineVariant(Endianness endianness, uint sectorSize) => (endianness, sectorSize) switch
        {
            (Endianness.BigEndian, 0x800) => Variant.MGS4,
            (Endianness.LittleEndian, 0x1000) => Variant.MGSPW,
            _ => Variant.Unknown,
        };

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);

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

            var dirEntries = wideIndexEntries
                ? [.. source.ReadArrayWithEndianness<DirEntryInfoWide>(entryCount, endianness).Select(e => new DirEntryInfo { Id = e.Id, Offset = (uint)e.Offset })]
                : source.ReadArrayWithEndianness<DirEntryInfo>(entryCount, endianness);

            var sectorSize = DetermineSectorSize(dirEntries, source.Length);
            var variant = DetermineVariant(endianness, sectorSize);
            var extensions = Extensions.GetValueOrDefault(variant, ImmutableDictionary<byte, string>.Empty);

            var dataStart = StreamExtensions.Align(source.Position, sectorSize);
            var group = "unknown";
            for (var i = 0; i < entryCount; i++)
            {
                var entry = dirEntries[i];
                switch (entry.Extension)
                {
                    case 0x00:
                        yield break;

                    case 0x7D:
                        break;

                    case 0x7E:
                        break;

                    case 0x7F:
                        if (entry.FileName == 0)
                        {
                            group = "unknown";
                            dataStart = StreamExtensions.Align(dataStart + entry.Offset, sectorSize);
                        }
                        else if (!Groups.TryGetValue(entry.FileName, out group))
                        {
                            group = entry.FileName.ToString("x6", CultureInfo.InvariantCulture);
                        }

                        break;

                    default:
                        if (!extensions.TryGetValue(entry.Extension, out var ext))
                        {
                            ext = entry.Extension.ToString("x2", CultureInfo.InvariantCulture);
                        }

                        yield return ($"{group}/{entry.FileName:x6}.{ext}", dataStart + entry.Offset, dirEntries[i + 1].Offset - entry.Offset);
                        break;
                }
            }
        }

        private static uint DetermineSectorSize(DirEntryInfo[] dirEntries, long length)
        {
            uint[] sections = [.. dirEntries.Where(e => e.Id == 0x7F000000).Select(e => e.Offset)];
            var sectionsSum = sections.Sum(x => x);
            for (var bit = 11; bit <= 12; bit++)
            {
                var align = (uint)(1 << bit);
                var paddingSum = Enumerable.Range(0, sections.Length).Sum(i => i == sections.Length - 1 ? align : StreamExtensions.GetPadding(sections[i], align));
                var sum = sectionsSum + paddingSum;
                if (sum == length)
                {
                    return align;
                }
            }

            return 0x800;
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options => new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.Length, Ownership.Dispose),
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DirEntryInfo
        {
            public uint Id;
            public uint Offset;

            public readonly uint FileName => this.Id & 0xFFFFFF;

            public readonly byte Extension => (byte)((this.Id >> 24) & 0xFF);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct DirEntryInfoWide
        {
            public uint Id;
            public uint PaddingA;
            public ulong Offset;
        }
    }
}
