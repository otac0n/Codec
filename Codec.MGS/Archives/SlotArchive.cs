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
    using Entry = (string Path, long Offset, long Size);

    internal class SlotArchive : IndexedFileSystem<Entry>
    {
        private static readonly uint SectorSize = 0x800;

        private readonly string filePath;
        private readonly IFileSystem fileSystem;

        public SlotArchive(string filePath, IFileSystem? fileSystem = null)
        {
            fileSystem ??= new FileSystem();
            this.filePath = filePath;
            this.fileSystem = fileSystem;
        }

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.slot", static (fullPath, parentRelativePath, parent, parentPath) => new SlotArchive(parentRelativePath, parent));
        }

        protected override IEnumerable<Entry> ReadIndex()
        {
            SlotHeader header;
            using var slotDat = this.fileSystem.File.OpenRead(this.filePath);
            header = slotDat.ReadBigEndian<SlotHeader>();

            slotDat.Seek(SectorSize, SeekOrigin.Begin);
            var entries = new List<Entry>();
            for (var i = 0; i < header.PageCount; i++)
            {
                var start = slotDat.Position;
                var cnf = slotDat.ReadBigEndian<DirArchive.DirHeaderWide>();

                var tags = slotDat.ReadArrayBigEndian<DirArchive.DirEntryInfoWide>(cnf.EntryCount);
                var size = DirArchive.GetFileSize(tags, SectorSize);

                entries.Add(($"{i}.dir", start, size));

                slotDat.Position = start + size;
            }

            return entries;
        }

        protected override string GetEntryName(Entry entry) =>
            entry.Path;

        protected override Stream Open(Entry entry, FileStreamOptions parentOptions) =>
            new OffsetStreamSpan(this.fileSystem.File.Open(this.filePath, parentOptions), entry.Offset, entry.Size, Ownership.Dispose);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SlotHeader
        {
            public uint Timestamp;
            public ushort Version;
            public ushort PageSize;
            public ushort PageCount;
            public ushort UnknownA;
            public uint UnknownB;
        }
    }
}
