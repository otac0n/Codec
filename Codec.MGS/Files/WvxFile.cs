// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using Codec.Audio;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (int Index, long Offset, long Length);

    internal class WvxFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.wvx", static (fullPath, parentRelativePath, parent, parentPath) => new WvxFileFileSystem(parentRelativePath, parent));
        }

        private class WvxFileFileSystem(string path, IFileSystem parent) : IndexedFileSystem<Entry>
        {
            private readonly IFileSystem parent = parent ?? new FileSystem();
            private readonly string path = path;

            protected override IEnumerable<Entry> ReadIndex()
            {
                using var stream = this.parent.File.OpenRead(this.path);
                var header = stream.ReadBigEndian<Header>();
                var count = (int)(header.DataOffset / Marshal.SizeOf<Row>());
                var rows = stream.ReadArrayLittleEndian<Row>(count);

                // No idea where this 32 comes from, but it seems to be correct. It does skip the first valid entry,
                // but without this every entry is only 1 line long (i.e. the tail of the previous entry).
                var baseOffset = Marshal.SizeOf<Header>() + header.DataOffset + 32;

                for (var i = 0; i < count; i++)
                {
                    var row = rows[i];
                    var offset = baseOffset + (row.Offset - rows[0].Offset);
                    long length = 16;
                    while (true)
                    {
                        stream.Position = offset + length;
                        if (stream.Position >= stream.Length || stream.PeekAllZeros(16))
                        {
                            break;
                        }

                        length += 16;
                    }

                    yield return (i, offset, length);
                }
            }

            protected override string GetEntryName(Entry entry) =>
                $"{entry.Index}.vag";

            protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub patches in .wvx files is not supported.");
                var source = this.parent.File.Open(this.path, parentOptions);

                var headerStream = new MemoryStream();
                var vag = new VagHeader
                {
                    Version = 0,
                    DataSize = (uint)entry.Length,
                    SamplingFreq = 11025,
                };

                headerStream.WriteBigEndian(vag);

                return new ConcatStream(
                    Ownership.Dispose,
                    MappedStream.FromStream(headerStream, Ownership.Dispose),
                    new OffsetStreamSpan(source, entry.Offset, entry.Length, Ownership.Dispose));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public uint UnknownA;
            public uint DataOffset;
            public uint UnknownB;
            public uint UnknownC;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Row
        {
            public uint Offset;
            public uint UnknownA;
            public uint UnknownB;
            public uint UnknownC;
        }
    }
}
