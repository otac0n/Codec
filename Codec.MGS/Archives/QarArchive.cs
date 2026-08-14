// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string FileName, long Offset, long Length);

    public class QarArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.qar", static (fullPath, parentRelativePath, parent, parentPath) => new QarArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(Entry entry) => entry.FileName;

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var source = parent.File.OpenRead(parentRelativePath);
            source.Position = source.Length - sizeof(uint);
            var addressLE = source.ReadUInt32LittleEndian();
            var addressBE = BinaryPrimitives.ReverseEndianness(addressLE);

            if (addressBE >= source.Length && addressLE >= source.Length)
            {
                throw new InvalidDataException();
            }

            var (address, endianness) = addressLE >= source.Length || (addressBE < source.Length && addressBE > addressLE)
                ? (addressBE, Endianness.BigEndian)
                : (addressLE, Endianness.LittleEndian);
            source.Position = address;

            var index = source.ReadWithEndianness<IndexHeader>(endianness);

            var info = source.ReadArrayWithEndianness<EntryInfo>(index.EntryCount, endianness);
            var names = new string[index.EntryCount];
            for (var i = 0; i < index.EntryCount; i++)
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

                names[i] = nameBuilder.ToString();
            }

            var offset = 0U;
            for (var i = 0; i < index.EntryCount; i++)
            {
                yield return (names[i], offset, info[i].Size);
                offset += info[i].Size;
                offset = StreamExtensions.Align<uint>(offset, 0x80);
            }
        }

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options => new OffsetStreamSpan(parent.File.Open(parentRelativePath, options), entry.Offset, entry.Length, Ownership.Dispose),
                updated =>
                {
                    throw new NotImplementedException();
                });
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct IndexHeader
        {
            public ushort EntryCount;
            public ushort Unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct EntryInfo
        {
            public uint Unknown;
            public uint Size;
        }
    }
}
