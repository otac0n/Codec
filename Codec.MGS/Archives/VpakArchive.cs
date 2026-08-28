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
    using DiscUtils.Streams;
    using K4os.Compression.LZ4;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long CompressedLength, long DecompressedLength);

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
                source.Position += count * sizeof(ulong);

                entries.Add((Encoding.ASCII.GetString(fileName), offset, compressedLength, decompressedLength));
            }

            return entries;
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                    new BareLz4Stream(
                        new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.CompressedLength, Ownership.Dispose),
                        entry.DecompressedLength),
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

        private class BareLz4Stream(Stream compressedSource, long uncompressedLength) :
            MemoryStream(Decompress(compressedSource, uncompressedLength))
        {
            private static byte[] Decompress(Stream compressedSource, long uncompressedLength)
            {
                using (compressedSource)
                {
                    var compressed = new byte[compressedSource.Length];
                    compressedSource.ReadExactly(compressed);

                    var target = new byte[uncompressedLength];
                    var decoded = LZ4Codec.Decode(compressed, target);
                    if (decoded < 0)
                    {
                        throw new InvalidDataException();
                    }
                    else if (decoded != target.Length)
                    {
                        Array.Resize(ref target, decoded);
                    }

                    return target;
                }
            }
        }
    }
}
