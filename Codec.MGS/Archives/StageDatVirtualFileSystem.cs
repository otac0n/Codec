// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Archives;
    using Codec.MGS.Streams;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Section = (long Offset, long Length, bool Encoded);
    using Entry = (string Folder, uint Group, uint Id, byte Ext, (long Offset, long Length, bool Encoded) Section, long Offset, long Length);

    public class StageDatVirtualFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly uint SectorSize = 0x800;

        private static readonly ImmutableDictionary<byte, string> Extensions = new Dictionary<byte, string>
        {
            [0x01] = "bin",
            [0x02] = "cv2",
            [0x04] = "evm",
            [0x05] = "far",
            [0x06] = "gcx",
            [0x07] = "hzx",
            [0x0A] = "kms",
            [0x0B] = "lt2",
            [0x0C] = "mar",
            [0x0E] = "o2d",
            [0x11] = "row",
            [0x12] = "sar",
            [0x13] = "tri",
            [0x15] = "var",
            [0x19] = "zms",
            [0x7D] = "face",
        }.ToImmutableDictionary();

        private static readonly ImmutableDictionary<uint, string> Groups = new Dictionary<uint, string>
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
            var glob = PathExtensions.GlobToRegex("*STAGE*.DAT");
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (glob.IsMatch(parent.Path.GetFileName(parentRelativePath)))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new StageDatVirtualFileSystem(parentRelativePath, parent);
                }

                return null;
            });
        }

        protected override string GetEntryName(Entry entry) =>
            $"{entry.Folder}/{Groups[entry.Group]}/{entry.Id:x6}.{Extensions[entry.Ext]}";

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            var iv = source.ReadUInt32LittleEndian();
            Header header;
            FolderEntry[] folders;
            using (var decoded = new DecodingStream(iv, iv ^ 0xF0F0u, source))
            {
                header = decoded.ReadLittleEndian<Header>();
                folders = decoded.ReadArrayLittleEndian<FolderEntry>(header.FolderCount);
            }

            var entries = new List<Entry>();
            foreach (var folder in folders)
            {
                var folderName = Encoding.ASCII.GetString(folder.Name).TrimEnd('\0');
                source.Position = folder.Offset * SectorSize;

                FileEntry[] files;
                using (var decoded = new DecodingStream(MakeKey(folderName, iv), MakeSalt(folderName), source))
                {
                    var fileCount = decoded.ReadUInt32LittleEndian();
                    files = decoded.ReadArrayLittleEndian<FileEntry>(fileCount);
                }

                var group = 0U;
                var sectionSize = 0U;
                var dataPtr = (uint)(sizeof(uint) + Marshal.SizeOf<FileEntry>() * files.Length);
                var encodedSize = 0U;
                Section? section = null;
                for (var f = 0; f < files.Length; f++)
                {
                    var file = files[f];
                    var id = file.Filename & 0xFFFFFF;
                    var ext = (byte)(file.Filename >> 24);
                    switch (ext)
                    {
                        case 0x7F:
                            group = id;
                            if (id != 0)
                            {
                                sectionSize = file.Offset;
                                dataPtr = StreamExtensions.Align(dataPtr, SectorSize);
                                section = (folder.Offset * SectorSize + dataPtr, sectionSize, false);
                            }
                            else
                            {
                                if (encodedSize != 0)
                                {
                                    dataPtr += encodedSize;
                                    encodedSize = 0;
                                    section = null;
                                }
                                else
                                {
                                    dataPtr += file.Offset;
                                }
                            }

                            break;

                        case 0x7E:
                            encodedSize = id;
                            var offset = file.Offset;
                            section = (section.Value.Offset, encodedSize, true);

                            break;

                        case 0x00:
                            break;

                        default:
                            entries.Add((folderName, group, id, ext, section.Value, file.Offset, files[f + 1].Offset - file.Offset));
                            break;
                    }
                }
            }

            return entries;
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    var source = parent.File.Open(parentRelativePath, options);
                    if (entry.Section.Encoded)
                    {
                        source.Position = entry.Section.Offset;
                        var key = source.ReadUInt16LittleEndian() ^ 0x9385;
                        var keyB = unchecked((uint)(key * 0x0116));
                        var keyA = (uint)(((key ^ 0x6576) << 0x10) | key);
                        Stream section = new OffsetStreamSpan(source, entry.Section.Offset, entry.Section.Length, Ownership.Dispose);
                        section = new DecodingStream(keyA, keyB, section, Ownership.Dispose);
                        section = new CachingSeekableStream(section);
                        section.Write([0x78, 0x9C]);
                        section = new DeflateStream(section, CompressionMode.Decompress);
                        section = new CachingSeekableStream(section);
                        return new OffsetStreamSpan(section, entry.Offset, entry.Length, Ownership.Dispose);
                    }
                    else
                    {
                        return new OffsetStreamSpan(source, entry.Section.Offset + entry.Offset, entry.Length, Ownership.Dispose);
                    }
                },
                updated => throw new NotImplementedException());
        }

        private static uint MakeKey(string key, uint iv) =>
            MakeKey(StringCode.Hash24(key), iv);

        private static uint MakeKey(uint key, uint iv) =>
            Mix(0xA78925D9, key) + iv;

        private static uint MakeSalt(string iv) =>
            MakeSalt(StringCode.Hash24(iv));

        private static uint MakeSalt(uint iv) =>
            Mix(0x7A88FB59, iv);

        private static uint Mix(uint a, uint b) =>
            a + b + (b << 0x07);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public ushort Version;
            public ushort PageSize;
            public ushort FolderCount;
            public ushort UnknownA;
            public uint UnknownB;
        }

        [InlineArray(8)]
        private struct Name8
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FolderEntry
        {
            public Name8 Name;
            public uint Offset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileEntry
        {
            public uint Filename;
            public uint Offset;
        }
    }
}
