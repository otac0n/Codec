// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
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
        private static readonly ImmutableDictionary<byte, string> Extensions = new Dictionary<byte, string>()
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

            source.Position = sizeof(uint);
            var wideIndexEntries = source.ReadUInt32LittleEndian() == 0;
            source.Position = 0;

            var entryCount = wideIndexEntries
                ? (uint)source.ReadUInt64LittleEndian()
                : source.ReadUInt32LittleEndian();

            if (entryCount == 0 || source.Length <= sizeof(uint) + (entryCount * (wideIndexEntries ? Marshal.SizeOf<DirEntryInfoWide>() : Marshal.SizeOf<DirEntryInfo>())))
            {
                return false;
            }

            var dirEntries = wideIndexEntries
                ? source.ReadArrayLittleEndian<DirEntryInfoWide>(entryCount).Select(e => new DirEntryInfo { Id = (uint)e.Id, Offset = (uint)e.Offset }).ToArray()
                : source.ReadArrayLittleEndian<DirEntryInfo>(entryCount);
            var dataStart = StreamExtensions.Align(source.Position, 0x1000);
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

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);

            source.Position = sizeof(uint);
            var wideIndexEntries = source.ReadUInt32LittleEndian() == 0;
            source.Position = 0;

            var entryCount = wideIndexEntries
                ? (uint)source.ReadUInt64LittleEndian()
                : source.ReadUInt32LittleEndian();

            var dirEntries = wideIndexEntries
                ? source.ReadArrayLittleEndian<DirEntryInfoWide>(entryCount).Select(e => new DirEntryInfo { Id = (uint)e.Id, Offset = (uint)e.Offset }).ToArray()
                : source.ReadArrayLittleEndian<DirEntryInfo>(entryCount);

            var dataStart = StreamExtensions.Align(source.Position, 0x1000);
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
                        if (!Groups.TryGetValue(entry.FileName, out group))
                        {
                            group = entry.FileName.ToString("x6", CultureInfo.InvariantCulture);
                        }

                        break;

                    default:
                        if (!Extensions.TryGetValue(entry.Extension, out var ext))
                        {
                            ext = entry.Extension.ToString("x2", CultureInfo.InvariantCulture);
                        }

                        yield return ($"{group}/{entry.FileName:x6}.{ext}", dataStart + entry.Offset, dirEntries[i + 1].Offset - entry.Offset);
                        break;
                }
            }
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
        private struct DirEntryInfo
        {
            public uint Id;
            public uint Offset;

            public readonly uint FileName => this.Id & 0xFFFFFF;

            public readonly byte Extension => (byte)((this.Id >> 24) & 0xFF);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DirEntryInfoWide
        {
            public ulong Id;
            public ulong Offset;
        }
    }
}
