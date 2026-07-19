// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
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

    public enum StageDatVariant
    {
        Unknown = 0,
        Mgs2,
        Tts,
    }

    public class StageDatVirtualFileSystem(string parentRelativePath, IFileSystem parent, StageDatVariant variant = StageDatVariant.Unknown) : IndexedFileSystem<Entry>
    {
        private static readonly uint SectorSize = 0x800;

        private static readonly Dictionary<StageDatVariant, Dictionary<byte, string>> Extensions = new()
        {
            [StageDatVariant.Mgs2] = new()
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
            },
            [StageDatVariant.Tts] = new()
            {
                [0x0A] = "kmy",
                [0x13] = "tpl",
            },
        };

        private static readonly Dictionary<StageDatVariant, Dictionary<uint, string>> Groups = new()
        {
            [StageDatVariant.Mgs2] = new()
            {
                [0x00000002] = "cache",
                [0x00000003] = "resident",
                [0x00000004] = "delayload",
                [0x00000005] = "delayload_w",
                [0x00000010] = "sound",
                [0x00010000] = "nocache",
            },
            [StageDatVariant.Tts] = new()
            {
                [0x00000002] = "cache",
                [0x00000003] = "resident",
                [0x00000004] = "tts-04",
                [0x00000005] = "tts-05",
                [0x00000010] = "sound",
                [0x00010000] = "nocache",
            },
        };

        public static void Register(IServiceCollection services)
        {
            var glob = PathExtensions.GlobToRegex("*STAGE*.DAT");
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (glob.IsMatch(parent.Path.GetFileName(parentRelativePath)))
                {
                    var variant = DetectVariant(parent, parentRelativePath);
                    if (variant != StageDatVariant.Unknown)
                    {
                        return (fullPath, parentRelativePath, parent, parentPath) =>
                            new StageDatVirtualFileSystem(parentRelativePath, parent, variant);
                    }
                }

                return null;
            });
        }

        private static StageDatVariant DetectVariant(IFileSystem parent, string relativePath)
        {
            using var source = parent.File.OpenRead(relativePath);
            if (source.Length < 12)
            {
                return StageDatVariant.Unknown;
            }

            source.Position = 10;
            var b0 = source.ReadByte();
            var b1 = source.ReadByte();
            return b0 == 0xCC && b1 == 0xCC ? StageDatVariant.Tts : StageDatVariant.Mgs2;
        }

        protected override string GetEntryName(Entry entry)
        {
            string groupName;
            if (!(Groups.TryGetValue(variant, out var groupNames) && groupNames.TryGetValue(entry.Group, out groupName!)))
            {
                groupName = $"{entry.Group:x8}";
            }

            string extName;
            if (!(Extensions.TryGetValue(variant, out var extensions) && extensions.TryGetValue(entry.Ext, out extName!)))
            {
                extName = $"{entry.Ext:x2}";
            }

            return $"{entry.Folder}/{groupName}/{entry.Id:x6}.{extName}";
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            Header header;
            FolderEntry[] folders;

            var iv = source.ReadUInt32LittleEndian();
            if (variant == StageDatVariant.Mgs2)
            {
                using var decoded = new DecodingStream(iv, iv ^ 0xF0F0u, source);
                header = decoded.ReadLittleEndian<Header>();
                folders = decoded.ReadArrayLittleEndian<FolderEntry>(header.FolderCount);
            }
            else if (variant == StageDatVariant.Tts)
            {
                header = source.ReadBigEndian<Header>();
                folders = source.ReadArrayBigEndian<FolderEntry>(header.FolderCount);
            }
            else
            {
                return [];
            }

            var entries = new List<Entry>();
            foreach (var folder in folders)
            {
                var folderName = Encoding.ASCII.GetString(folder.Name).TrimEnd('\0');
                source.Position = folder.Offset * SectorSize;

                FileEntry[] files = null!;
                if (variant == StageDatVariant.Mgs2)
                {
                    using var decoded = new DecodingStream(MakeKey(folderName, iv), MakeSalt(folderName), source);
                    var fileCount = decoded.ReadUInt32LittleEndian();
                    files = decoded.ReadArrayLittleEndian<FileEntry>(fileCount);
                }
                else if (variant == StageDatVariant.Tts)
                {
                    var fileCount = source.ReadUInt32BigEndian();
                    files = source.ReadArrayBigEndian<FileEntry>(fileCount);
                }

                BuildFolderEntries(entries, folder.Offset, folderName, files);
            }

            return entries;
        }

        private static void BuildFolderEntries(List<Entry> entries, uint folderOffset, string folderName, FileEntry[] files)
        {
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
                            section = (folderOffset * SectorSize + dataPtr, sectionSize, false);
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

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    var source = parent.File.Open(parentRelativePath, options);
                    if (entry.Section.Encoded)
                    {
                        if (variant == StageDatVariant.Mgs2)
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
                        else if (variant == StageDatVariant.Tts)
                        {
                            Stream section = new OffsetStreamSpan(source, entry.Section.Offset, entry.Section.Length, Ownership.Dispose);
                            section = new ZLibStream(section, CompressionMode.Decompress);
                            section = new CachingSeekableStream(section);
                            return new OffsetStreamSpan(section, entry.Offset, entry.Length, Ownership.Dispose);
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
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
