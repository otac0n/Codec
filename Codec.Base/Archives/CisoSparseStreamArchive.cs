// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec.Streams;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    internal class CisoSparseStreamArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<string>
    {
        private readonly string fileName = PathExtensions.ChangeExtension(parent.Path.GetFileName(parentRelativePath), ".iso");

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem(
                "*.ciso",
                (serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
                {
                    // TODO: Also verify the space bitmap.
                    using var file = parent.File.OpenRead(parentRelativePath);
                    var header = file.ReadLittleEndian<CisoHeader>();
                    return Encoding.ASCII.GetString(header.Signature) == "CISO";
                },
                static (fullPath, parentRelativePath, parent, parentPath) => new CisoSparseStreamArchive(parentRelativePath, parent));
        }

        protected override string GetEntryName(string entry) =>
            entry;

        protected override IEnumerable<string> ReadIndex() =>
            [this.fileName];

        protected override Stream Open(string entry, FileStreamOptions parentOptions)
        {
            Debug.Assert(entry == this.fileName, "Entry does not match the file name.");
            FileBase.EnsureReadOnly(parentOptions, "Recompressing .ciso streams is not supported.");
            var file = parent.File.OpenRead(parentRelativePath);
            var header = file.ReadLittleEndian<CisoHeader>();
            var headerSize = Marshal.SizeOf<CisoHeader>();
            var bitmap = (Span<byte>)header.Map;
            var blockSize = header.BlockSize;

            var streams = new List<SparseStream>();

            var count = 0;
            var zeroBlocks = 0;
            for (var i = 0; i < bitmap.Length; i++)
            {
                if (bitmap[i] == 1)
                {
                    if (zeroBlocks != 0)
                    {
                        streams.Add(new ZeroStream(blockSize * zeroBlocks));
                        zeroBlocks = 0;
                    }

                    var physicalAddress = count++ * blockSize + headerSize;
                    streams.Add(new OffsetStreamSpan(file, physicalAddress, blockSize, Ownership.Dispose));
                }
                else
                {
                    zeroBlocks++;
                }
            }

            return new ConcatStream(Ownership.Dispose, [.. streams]);
        }

        [InlineArray(4)]
        private struct Name4
        {
            public byte Char0;
        }

        [InlineArray(0x8000 - sizeof(uint) - 4)]
        private struct FreeSpaceBitMap
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CisoHeader
        {
            public Name4 Signature;
            public uint BlockSize;
            public FreeSpaceBitMap Map;
        }
    }
}
