namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using Codec.MGS.Streams;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string Path, long Offset, long Length);

    internal class SlotDatArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly uint SectorSize = 0x1000;
        private SlotKeyHeader keyHeader;

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem(
                "slot.dat",
                static (serviceProvider, fullPath, parentRelativePath, parent, parentPath) => parent.File.Exists(parent.Path.ChangeExtension(parentRelativePath, ".key")),
                static (fullPath, parentRelativePath, parent, parentPath) => new SlotDatArchive(parentRelativePath, parent));
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var slotDat = parent.File.OpenRead(parentRelativePath);
            var header = slotDat.ReadLittleEndian<SlotHeader>();

            using var slotKey = parent.File.OpenRead(parent.Path.ChangeExtension(parentRelativePath, ".key"));
            this.keyHeader = slotKey.ReadLittleEndian<SlotKeyHeader>();
            var slotKeyEntries = slotKey.ReadArrayLittleEndian<SlotKeyEntryHD>(header.EntryCount);

            for (var i = 0; i < header.EntryCount; i++)
            {
                var keyEntry = slotKeyEntries[i];
                var start = (keyEntry.FirstPage & 0xFFFFF) * SectorSize;
                var end = (keyEntry.LastPage & 0xFFFFF) * SectorSize;
                yield return ($"{i}.dir", start, end - start);
            }
        }

        protected override string GetEntryName(Entry entry) =>
            entry.Path;

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    var section = new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.Length, Ownership.Dispose);
                    var pageKey = this.keyHeader.SaltA ^ this.keyHeader.SaltB;
                    var key = MakeKey(pageKey);
                    var pageKeyB = MakeKey(pageKey, this.keyHeader.SaltC);
                    var decoded = new DecodingStream(key, pageKeyB, section, Ownership.Dispose);
                    var header = decoded.ReadLittleEndian<SlotCompressedHeader>();
                    var compressed = new OffsetStreamSpan(decoded, decoded.Position, decoded.Length - decoded.Position, Ownership.Dispose);
                    var decompressed = new ZLibStream(decoded, CompressionMode.Decompress);
                    return new CachingSeekableStream(decompressed, header.DecompressedSize);
                },
                updated =>
                {
                    throw new NotImplementedException();
                });
        }

        private static uint MakeKey(uint iv) =>
            ((iv ^ 0x00006576) << 0x10) | iv;

        private static uint MakeKey(uint key, uint iv) =>
            key * iv;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SlotHeader
        {
            public uint Timestamp;
            public ushort Version;
            public ushort PageSize;
            public ushort EntryCount;
            public ushort UnknownA;
            public uint UnknownB;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SlotKeyHeader
        {
            public uint SaltA;
            public uint SaltB;
            public uint SaltC;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SlotKeyEntry
        {
            public uint FirstPage;
            public uint LastPage;
            public uint Hash;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SlotKeyEntryHD
        {
            public uint FirstPage;
            public uint LastPage;
            public int Hash;
            public uint UnknownA;
            public uint UnknownB;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SlotCompressedHeader
        {
            public ushort UnknownA;
            public ushort UnknownB;
            public uint Padding;
            public uint CompressedSize;
            public uint DecompressedSize;
        }
    }
}
