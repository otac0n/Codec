// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Archives;
    using Codec.MGS.Services;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string Folder, byte Group, ushort Id, byte Ext, long Offset, long Length);

    public class StageDirArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly uint SectorSize = 0x800;

        private static readonly ImmutableDictionary<byte, string> Extensions = new Dictionary<byte, string>
        {
            [0x61] = "azm", // aar
            [0x62] = "bin",
            [0x63] = "con",
            [0x64] = "dar",
            [0x65] = "efx",
            [0x67] = "gcx",
            [0x68] = "hzm",
            [0x69] = "img",
            [0x6b] = "kmd",
            [0x6c] = "lit",
            [0x6d] = "mdx", // mt3
            [0x6e] = "nar",
            [0x6f] = "oar",
            [0x70] = "pcx", // pcc, which == pcx
            [0x72] = "res", // rar, rpk
            [0x73] = "sgt",
            [0x77] = "wvx",
            [0x7a] = "zmd",
        }.ToImmutableDictionary();

        private static readonly ImmutableDictionary<byte, string> Groups = new Dictionary<byte, string>
        {
            [0x63] = "cache",
            [0x6e] = "nocache",
            [0x72] = "resident",
            [0x73] = "sound",
        }.ToImmutableDictionary();

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*STAGE*.DIR", static (fullPath, parentRelativePath, parent, parentPath) => new StageDirArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry)
        {
            if (!(JoyDictService.TryGetOriginalFileName("mgs1", "stage.dir", null, entry.Folder, $"{entry.Id:x4}.{(char)entry.Ext}", out var filename) ||
                JoyDictService.TryGetOriginalFileName("mgs1", "stage.dar", null, entry.Folder, $"{entry.Id:x4}.{(char)entry.Ext}", out filename)))
            {
                filename = $"{entry.Id:x4}.{Extensions[entry.Ext]}";
            }

            return $"{entry.Folder}/{Groups[entry.Group]}/{filename}";
        }

        private static IEnumerable<Entry> ReadDar(Stream source, string folder, byte group, long offset, long length)
        {
            var headerSize = (uint)Marshal.SizeOf<DarHeader>();
            var relative = 0u;
            while (relative < length - 7)
            {
                source.Position = offset + relative;
                var header = source.ReadLittleEndian<DarHeader>();
                yield return (folder, group, header.Id, header.Ext, offset + relative + headerSize, header.Size);
                relative += headerSize + header.Size;
            }
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            var dataOffset = source.ReadUInt32LittleEndian();
            var folders = source.ReadArrayLittleEndian<FolderEntry>((int)dataOffset / Marshal.SizeOf<FolderEntry>());
            var entries = new List<Entry>();
            foreach (var folder in folders)
            {
                var folderName = Encoding.ASCII.GetString(folder.Name).TrimEnd('\0');
                var offset = folder.Size * SectorSize;
                source.Position = offset + 4;

                var rawEntries = new List<(DirItemEntry Entry, bool Packed)>();
                while (true)
                {
                    var entry = source.ReadLittleEndian<DirItemEntry>();
                    if (entry.Filename == 0)
                    {
                        break;
                    }

                    if (entry.Ext == byte.MaxValue)
                    {
                        var notLast = false;
                        for (var i = rawEntries.Count - 1; i >= 0; i--)
                        {
                            var prev = rawEntries[i];
                            if (prev.Entry.Group != entry.Group)
                            {
                                break;
                            }

                            var nextSize = prev.Entry.Size;
                            prev.Packed = notLast;
                            prev.Entry.Size = entry.Size - prev.Entry.Size;
                            rawEntries[i] = prev;
                            entry.Size = nextSize;
                            notLast = true;
                        }
                    }
                    else
                    {
                        rawEntries.Add((entry, false));
                    }
                }

                var relative = SectorSize;
                foreach (var (entry, packed) in rawEntries)
                {
                    if (entry.Ext == 0x64)
                    {
                        entries.AddRange(ReadDar(source, folderName, entry.Group, offset + relative, entry.Size));
                    }
                    else
                    {
                        entries.Add((folderName, entry.Group, entry.Id, entry.Ext, offset + relative, entry.Size));
                    }

                    relative += entry.Size;
                    if (!packed)
                    {
                        relative = StreamExtensions.Align(relative, SectorSize);
                    }
                }
            }

            return entries;
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            new OffsetStreamSpan(parent.File.Open(parentRelativePath, parentOptions), entry.Offset, entry.Length, Ownership.Dispose);

        [InlineArray(8)]
        public struct Name8
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FolderEntry
        {
            public Name8 Name;
            public uint Size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DirItemEntry
        {
            public uint Filename;
            public uint Size;

            public readonly ushort Id => (ushort)this.Filename;

            public readonly byte Group => (byte)(this.Filename >> 16);

            public readonly byte Ext => (byte)(this.Filename >> 24);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct DarHeader
        {
            public ushort Id;
            public byte Ext;
            public uint Size;
        }
    }
}
