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
    using Codec.Streams;
    using DiscUtils.Streams;
    using K4os.Compression.LZ4;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long CompressedLength, long DecompressedLength, uint ChunkDecompressedSize, long[] ChunksOffsets, ushort UnknownA);

    public class VpakArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly uint DefaultChunkSize = 0x1000;
        private static readonly uint ReservedHeaderSize = 0x1000;

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
            return ReadIndexFrom(source, out _);
        }

        private static List<Entry> ReadIndexFrom(Stream source, out VpakHeader header)
        {
            header = source.ReadLittleEndian<VpakHeader>();
            source.Position = source.Length - header.TailSize;

            var entries = new List<Entry>();
            while (source.Position < source.Length)
            {
                var p1 = source.ReadLittleEndian<IndexEntryPart1>();
                var fileName = new byte[p1.NameSize];
                source.ReadExactly(fileName);
                var p2 = source.ReadLittleEndian<IndexEntryPart2>();
                var chunks = source.ReadArrayLittleEndian<long>(p2.ChunkCount);

                entries.Add((Encoding.ASCII.GetString(fileName), p2.Offset, p2.CompressedLength, p2.DecompressedLength, p2.ChunkDecompressedSize, chunks, p1.UnknownA));
            }

            return entries;
        }

        private static uint WriteIndexTo(Stream destination, List<Entry> entries)
        {
            var start = destination.Position;

            foreach (var entry in entries)
            {
                destination.WriteLittleEndian(new IndexEntryPart1
                {
                    NameSize = (uint)entry.FileName.Length,
                    UnknownA = entry.UnknownA,
                });
                destination.Write(Encoding.ASCII.GetBytes(entry.FileName));

                destination.WriteLittleEndian(new IndexEntryPart2
                {
                    DecompressedLength = entry.DecompressedLength,
                    CompressedLength = entry.CompressedLength,
                    Offset = entry.Offset,
                    ChunkDecompressedSize = entry.ChunkDecompressedSize,
                    ChunkCount = (uint)entry.ChunksOffsets.Length,
                });

                destination.WriteArrayLittleEndian(entry.ChunksOffsets);
            }

            var size = (uint)(destination.Position - start);
            destination.SetLength(destination.Position);
            destination.Position = 12;
            destination.WriteLittleEndian(size);
            return size;
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
                    source.Dispose();

                    var target = new byte[entry.DecompressedLength];

                    var targetOffset = 0;
                    var chunks = entry.ChunksOffsets.Length;
                    for (var i = 0; i < chunks; i++)
                    {
                        var start = entry.ChunksOffsets[i];
                        var end = i >= chunks - 1 ? entry.CompressedLength : entry.ChunksOffsets[i + 1];
                        var length = end - start;
                        var targetLength = (int)Math.Min(Math.Max(entry.ChunkDecompressedSize, entry.DecompressedLength), entry.DecompressedLength - targetOffset);
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
                    using var compressed = new SpoolingStream();

                    updated.Position = 0;
                    var totalSize = updated.Length;
                    var compressedSize = 0;
                    var relativeChunkStarts = new long[(totalSize + DefaultChunkSize - 1) / DefaultChunkSize];
                    var rawData = new byte[DefaultChunkSize];
                    var compressedData = new byte[DefaultChunkSize * 2];
                    var remaining = totalSize;
                    for (var i = 0; i < relativeChunkStarts.Length; i++)
                    {
                        var count = (int)Math.Min(remaining, DefaultChunkSize);
                        updated.ReadExactly(rawData, count);
                        relativeChunkStarts[i] = compressed.Position;
                        var chunkSize = LZ4Codec.Encode(rawData.AsSpan()[..count], compressedData.AsSpan(), LZ4Level.L10_OPT);
                        compressed.Write(compressedData.AsSpan()[..chunkSize]);
                        compressedSize += chunkSize;
                        remaining -= count;
                    }

                    using var source = parent.File.Open(parentRelativePath, FileMode.Open, FileAccess.ReadWrite);
                    var index = ReadIndexFrom(source, out var header);
                    var entryIndex = index.FindIndex(e => e.FileName == entry.FileName);

                    var (bestStream, physicalSize, bestCompressedLength, bestChunkSize, bestChunks) = compressedSize < totalSize
                        ? ((Stream)compressed, compressed.Length, compressed.Length, relativeChunkStarts.Length > 1 ? DefaultChunkSize : 0, relativeChunkStarts)
                        : (updated, totalSize, 0, 0, [0]);

                    var placement = ByteRange.FindFreeSpace(
                        physicalSize,
                        [new ByteRange(0, ReservedHeaderSize), .. index.Where((e, i) => i != entryIndex).Select(e => new ByteRange(e.Offset, e.CompressedLength == 0 ? e.DecompressedLength : e.CompressedLength))],
                        out var indexPosition);

                    source.Position = placement.Offset;
                    bestStream.Position = 0;
                    bestStream.CopyTo(source);

                    index[entryIndex] = (entry.FileName, placement.Offset, bestCompressedLength, totalSize, bestChunkSize, bestChunks, entry.UnknownA);

                    source.Position = indexPosition;
                    WriteIndexTo(source, index);

                    this.index = null;
                });
        }

        [InlineArray(4)]
        private struct Name4
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct IndexEntryPart1
        {
            public uint NameSize;
            public ushort UnknownA;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct IndexEntryPart2
        {
            public ulong Padding;
            public long DecompressedLength;
            public long CompressedLength;
            public long Offset;
            public uint ChunkDecompressedSize;
            public uint ChunkCount;
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
