// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using Codec.Archives;
    using Codec.Audio;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Chunk = (long Offset, long Size);
    using Entry = (string Path, (long Offset, long Size)[] Chunks);

    public class DemoDatArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly byte EndCode = 240;

        private static readonly ImmutableDictionary<byte, string> extensions = new Dictionary<byte, string>
        {
            [0x01] = "vag",
            [0x05] = "frm",
            [0x10] = "idx",
        }.ToImmutableDictionary();

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("DEMO.DAT", static (fullPath, parentRelativePath, parent, parentPath) => new DemoDatArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry) => entry.Path;

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            var headerSize = sizeof(uint);
            var length = source.Length;
            var entries = new List<Entry>();
            var audioChunks = new List<Chunk>();
            var offset = 0L;
            var index = 0;

            var folder = 0;
            while (offset + headerSize <= length)
            {
                source.Position = offset;
                var header = source.ReadUInt32LittleEndian();
                var code = (byte)(header & 0xff);
                var size = header >> 8;

                if (code == EndCode)
                {
                    if (audioChunks.Count > 0)
                    {
                        entries.Add(($"{folder}/audio.{extensions[0x01]}", audioChunks.ToArray()));
                        audioChunks.Clear();
                    }

                    offset += size;
                    offset = StreamExtensions.Align(offset, 0x800);
                    folder++;
                    index = 0;

                    continue;
                }

                if (size <= 0 || offset + size > length)
                {
                    break;
                }

                var segment = (offset + headerSize, size - headerSize);
                if (code == 0x01)
                {
                    audioChunks.Add(segment);
                }
                else
                {
                    var ext = extensions.TryGetValue(code, out var extension) ? extension : $"{code:x2}";
                    entries.Add(($"{folder}/{index:d4}.{ext}", [segment]));
                }

                offset += size;
                index++;
            }

            return entries;
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            var parentFile = parent.File.Open(parentRelativePath, parentOptions);
            if (entry.Chunks is [var segment])
            {
                return new OffsetStreamSpan(parentFile, segment.Offset, segment.Size, Ownership.Dispose);
            }
            else
            {
                var streams = new List<SparseStream>();

                var headerStream = new MemoryStream();
                var vag = new VagHeader
                {
                    // see: https://github.com/vgmstream/vgmstream/blob/master/src/meta/vag.c#L94
                    Signature = 0x56414769, // VAGi
                    Reserved1 = BinaryPrimitives.ReverseEndianness(0x1000u),
                    Version = 0,
                    DataSize = (uint)entry.Chunks.Sum(e => e.Size) / 2,
                    SamplingFreq = 33075,
                };

                headerStream.WriteBigEndian(vag);
                headerStream.SetLength(0x800);

                streams.Add(MappedStream.FromStream(headerStream, Ownership.Dispose));
                streams.AddRange(entry.Chunks.Select(c => new OffsetStreamSpan(parentFile, c.Offset, c.Size, Ownership.Dispose)));

                return new ConcatStream(Ownership.Dispose, [.. streams]);
            }
        }
    }
}
