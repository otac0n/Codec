// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Archives;
    using DiscUtils.Streams;
    using K4os.Compression.LZ4;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long CompressedLength, long DecompressedLength, int ChunkDecompressedSize, long[] ChunksOffsets);

    public class VpakArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem(
                "*.pak",
                static (a, fullPath, parentRelativePath, parent, parentPath) =>
                {
                    Span<byte> signature = stackalloc byte[4];
                    using var file = parent.File.OpenRead(parentRelativePath);
                    file.ReadExactly(signature);
                    return Encoding.ASCII.GetString(signature) == "VPAK";
                },
                static (fullPath, parentRelativePath, parent, parentPath) => new VpakArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry) => entry.FileName;

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            var header = source.ReadLittleEndian<VpakHeader>();
            source.Position = source.Length - header.TailSize;

            var entries = new List<Entry>();
            while (source.Position < source.Length)
            {
                var nameSize = source.ReadUInt32LittleEndian();
                var fileName = new byte[nameSize];
                source.Position += sizeof(ushort);
                source.ReadExactly(fileName);
                source.Position += sizeof(ulong);
                var decompressedLength = source.ReadInt64LittleEndian();
                var compressedLength = source.ReadInt64LittleEndian();
                var offset = source.ReadInt64LittleEndian();
                var chunkDecompressedSize = (int)source.ReadUInt32LittleEndian();
                var count = source.ReadUInt32LittleEndian();
                var chunks = source.ReadArrayLittleEndian<long>(count);

                if (chunkDecompressedSize == 0)
                {
                    chunkDecompressedSize = (int)decompressedLength;
                }

                entries.Add((Encoding.ASCII.GetString(fileName), offset, compressedLength, decompressedLength, chunkDecompressedSize, chunks));
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
                    if (entry.CompressedLength == 0)
                    {
                        return new OffsetStreamSpan(source, entry.Offset, entry.DecompressedLength, Ownership.Dispose);
                    }

                    var compressed = new byte[entry.CompressedLength];
                    source.Position = entry.Offset;
                    source.ReadExactly(compressed);

                    var target = new byte[entry.DecompressedLength];

                    var targetOffset = 0;
                    var chunks = entry.ChunksOffsets.Length;
                    for (var i = 0; i < chunks; i++)
                    {
                        var start = entry.ChunksOffsets[i];
                        var end = i >= chunks - 1 ? entry.CompressedLength : entry.ChunksOffsets[i + 1];
                        var length = end - start;
                        var targetLength = (int)Math.Min(entry.ChunkDecompressedSize, entry.DecompressedLength - targetOffset);
                        var decoded = LZ4Codec.Decode(compressed.AsSpan()[(int)start..(int)(start + length)], target.AsSpan()[targetOffset..(targetOffset + targetLength)]);

                        if (decoded < 0)
                        {
                            throw new InvalidDataException();
                        }

                        targetOffset += decoded;
                    }

                    if (targetOffset != target.Length)
                    {
                        Array.Resize(ref target, targetOffset);
                    }

                    return new MemoryStream(target);
                },
                updated =>
                {
                    throw new NotImplementedException();
                });
        }

        [InlineArray(4)]
        private struct Name4
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct VpakHeader
        {
            public Name4 Signature;
            public ushort VersionMajor;
            public ushort VersionMinor;
            public uint UnknownA;
            public uint TailSize;
        }
    }
}
