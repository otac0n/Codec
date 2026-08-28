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
    using Entry = (string FileName, bool Compressed, long DecompressedLength, (long Offset, long Length)[] Chunks);

    public class VpakArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.pak", static (fullPath, parentRelativePath, parent, parentPath) => new VpakArchive(parentRelativePath, parent));
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
                source.Position += sizeof(uint);
                var count = source.ReadUInt32LittleEndian();
                var chunks = source.ReadArrayLittleEndian<long>(count);

                entries.Add((Encoding.ASCII.GetString(fileName), compressedLength != 0, decompressedLength, chunks.Select((c, i) => (offset + c, i >= count - 1 ? compressedLength : chunks[i + 1] - c)).ToArray()));
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
                    if (!entry.Compressed)
                    {
                        var chunk = entry.Chunks.Single();
                        return new OffsetStreamSpan(source, chunk.Offset, chunk.Length, Ownership.Dispose);
                    }

                    var compressedSize = entry.Chunks.Sum(c => c.Length);
                    var compressed = new byte[compressedSize];
                    var baseOffset = entry.Chunks[0].Offset;
                    source.Position = baseOffset;
                    source.ReadExactly(compressed);

                    var target = new byte[entry.DecompressedLength];

                    var targetOffset = 0;
                    foreach (var (offset, length) in entry.Chunks)
                    {
                        var start = offset - baseOffset;
                        var decoded = LZ4Codec.Decode(compressed.AsSpan()[(int)start..(int)(start + length)], target.AsSpan()[targetOffset..]);
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
