namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Runtime.InteropServices;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using Entry = (string Path, long Offset, long Length);

    public class SdxArchive(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<Entry>
    {
        private static readonly uint SectorSize = 0x800;

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.sdx", static (fullPath, parentRelativePath, parent, parentPath) => new SdxArchive(parentRelativePath, parent));
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            using var stream = parent.File.OpenRead(parentRelativePath);

            var result = new List<Entry>();

            var offsets = Array.ConvertAll(stream.ReadArrayLittleEndian<ItemOffset>(4), i => i.BaseAddress * SectorSize);

            result.Add(("0.wvx", offsets[0], offsets[1] - offsets[0]));
            result.Add(("1.wvx", offsets[1], offsets[2] - offsets[1]));
            result.Add(("2.dat", offsets[2], offsets[3] - offsets[2]));
            result.Add(("3.mdx", offsets[3], stream.Length - offsets[3]));

            return result;
        }

        protected override string GetEntryName(Entry entry) =>
            entry.Path;

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions)
        {
            FileBase.EnsureReadOnly(parentOptions, "Writing to sub entries in .sdx archives is not currently supported.");
            return new OffsetStreamSpan(parent.File.Open(parentRelativePath, parentOptions), entry.Offset, entry.Length, Ownership.Dispose);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ItemOffset
        {
            public uint BaseAddress;
            public uint Unknown;
        }
    }
}
