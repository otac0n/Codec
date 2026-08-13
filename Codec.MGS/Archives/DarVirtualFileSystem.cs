// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Text;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long Length);

    public class DarVirtualFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.dar", static (fullPath, parentRelativePath, parent, parentPath) => new DarVirtualFileSystem(parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry) => entry.FileName;

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            var fileCountLE = source.ReadUInt32LittleEndian();

            var (fileCount, endianness) = fileCountLE > 0x0000FFFF
                ? (BinaryPrimitives.ReverseEndianness(fileCountLE), Endianness.BigEndian)
                : (fileCountLE, Endianness.LittleEndian);

            bool? alignData = null;
            for (var i = 0; i < fileCount; i++)
            {
                var nameBuilder = new StringBuilder();
                while (true)
                {
                    var b = source.ReadByte();
                    if (b <= 0)
                    {
                        break;
                    }

                    nameBuilder.Append((char)b);
                }

                source.Align(4);

                var name = nameBuilder.ToString();
                var length = source.ReadWithEndianness<uint>(endianness);

                ApplyAlignment(source, length, ref alignData);

                yield return (name, source.Position, length);
                source.Position += length + 1;
            }
        }

        private static void ApplyAlignment(FileSystemStream source, uint length, ref bool? alignData)
        {
            const int Alignment = 16;
            if (alignData is bool value)
            {
                if (value)
                {
                    source.Align(Alignment);
                }

                return;
            }

            var padding = StreamExtensions.GetPadding(source.Position, Alignment);
            if (padding > 0)
            {
                if (source.Position + padding + length >= source.Length || !source.PeekAllZeros(padding))
                {
                    alignData = false;
                    return;
                }

                var position = source.Position;
                source.Position += padding + length;
                (source.Position, alignData) = source.ReadByte() == 0
                        ? (position + padding, true)
                        : (position, false);
            }
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options => new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.Length, Ownership.Dispose),
                updated =>
                {
                    using var parentStream = parent.File.Open(parentRelativePath, FileMode.Open, FileAccess.ReadWrite);

                    // Read everything after this entry into tail.
                    using var tail = new MemoryStream();
                    var oldEnd = entry.Offset + entry.Length + 1;
                    parentStream.Position = oldEnd;
                    parentStream.CopyTo(tail);

                    // Restore entry and tail at current entry location.
                    parentStream.Position = entry.Offset - sizeof(uint);
                    parentStream.Write(BitConverter.GetBytes(checked((uint)updated.Length)));
                    updated.Position = 0;
                    updated.CopyTo(parentStream);
                    parentStream.WriteByte(0);
                    tail.Position = 0;
                    tail.CopyTo(parentStream);
                    parentStream.SetLength(parentStream.Position);

                    this.index = null;
                });
        }
    }
}
