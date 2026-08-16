// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Archives;
    using Codec.MGS.Streams;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long Length);

    public class StageDatArchive(string parentRelativePath, IFileSystem parent, DirArchive.Variant variant = DirArchive.Variant.Unknown) : IndexedFileSystem<Entry>
    {
        private static readonly uint SectorSize = 0x800;

        public static void Register(IServiceCollection services)
        {
            var glob = PathExtensions.GlobToRegex("*STAGE*.DAT");
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (glob.IsMatch(parent.Path.GetFileName(parentRelativePath)))
                {
                    var variant = DetectVariant(parent, parentRelativePath);
                    if (variant != DirArchive.Variant.Unknown)
                    {
                        return (fullPath, parentRelativePath, parent, parentPath) =>
                            new StageDatArchive(parentRelativePath, parent, variant);
                    }
                }

                return null;
            });
        }

        private static DirArchive.Variant DetectVariant(IFileSystem parent, string relativePath)
        {
            using var source = parent.File.OpenRead(relativePath);
            if (source.Length < 12)
            {
                return DirArchive.Variant.Unknown;
            }

            source.Position = 10;
            var b0 = source.ReadByte();
            var b1 = source.ReadByte();
            return b0 == 0xCC && b1 == 0xCC ? DirArchive.Variant.TTS : DirArchive.Variant.MGS2;
        }

        protected override string GetEntryName(Entry entry) =>
            entry.FileName;

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            Header header;
            FolderEntry[] folders;

            var iv = source.ReadUInt32LittleEndian();
            if (variant == DirArchive.Variant.MGS2)
            {
                using var decoded = new DecodingStream(iv, iv ^ 0xF0F0u, source);
                header = decoded.ReadLittleEndian<Header>();
                folders = decoded.ReadArrayLittleEndian<FolderEntry>(header.FolderCount);
            }
            else if (variant == DirArchive.Variant.TTS)
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

                DirArchive.DirEntryInfo[] files = null!;
                if (variant == DirArchive.Variant.MGS2)
                {
                    using var decoded = new DecodingStream(MakeKey(folderName, iv), MakeSalt(folderName), source);
                    var fileCount = decoded.ReadUInt32LittleEndian();
                    files = decoded.ReadArrayLittleEndian<DirArchive.DirEntryInfo>(fileCount);
                }
                else if (variant == DirArchive.Variant.TTS)
                {
                    var fileCount = source.ReadUInt32BigEndian();
                    files = source.ReadArrayBigEndian<DirArchive.DirEntryInfo>(fileCount);
                }

                var folderSize = DirArchive.GetFileSize(files, SectorSize);

                entries.Add(($"{folderName}.dir", folder.Offset * SectorSize, folderSize));
            }

            return entries;
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    if (variant == DirArchive.Variant.MGS2)
                    {
                        var source = parent.File.Open(parentRelativePath, options);
                        var iv = source.ReadUInt32LittleEndian();
                        var folderName = parent.Path.GetFileNameWithoutExtension(entry.FileName);
                        var contentStream = new OffsetStreamSpan(source, entry.Offset, entry.Length, Ownership.Dispose);
                        var decoded = new CachingSeekableStream(new DecodingStream(MakeKey(folderName, iv), MakeSalt(folderName), contentStream));
                        var fileCount = decoded.ReadUInt32LittleEndian();
                        var headerSize = sizeof(uint) + Marshal.SizeOf<DirArchive.DirEntryInfo>() * fileCount;
                        var headerStream = new OffsetStreamSpan(decoded, 0, headerSize, Ownership.Dispose);
                        var tailStream = new OffsetStreamSpan(source, entry.Offset + headerSize, entry.Length - headerSize, Ownership.Dispose);
                        return new ConcatStream(Ownership.Dispose, headerStream, tailStream);
                    }
                    else
                    {
                        return new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.Length, Ownership.Dispose);
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
