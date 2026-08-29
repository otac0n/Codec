// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (uint Id, uint Index, long Offset, uint Size);

    public class DldArchive(string path, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private readonly IFileSystem parent = parent ?? new FileSystem();
        private readonly string path = path;

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.dld", static (fullPath, parentRelativePath, parent, parentPath) => new DldArchive(parentRelativePath, parent));
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var stream = this.parent.File.OpenRead(this.path);
            while (stream.Position < stream.Length)
            {
                var header = stream.ReadBigEndian<DldHeader>();
                if (header.Type == 0 && header.DataSize == 0 && header.Id == 0)
                {
                    break;
                }

                yield return (header.Id, header.Index, stream.Position, header.DataSize);
                stream.Position = StreamExtensions.Align(stream.Position + header.DataSize, header.Alignment);
            }
        }

        protected override string GetEntryName(Entry entry) =>
            $"{entry.Id:x8}_{entry.Index}.data";

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            FileBase.EnsureReadOnly(parentOptions, "Writing to sub images in .dld files is not currently supported.");
            return new OffsetStreamSpan(this.parent.File.Open(this.path, parentOptions), entry.Offset, entry.Size, Ownership.Dispose);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DldHeader
        {
            public byte Type;
            public byte Priority;
            public byte Alignment;
            public byte Pad0;
            public uint Pad1;
            public uint Id;
            public uint ParentDataSize;
            public uint DataSize;
            public uint MipMapCount;
            public uint Index;
            public uint Pad2;
        }
    }
}
