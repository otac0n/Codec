namespace Codec.Archives
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using Microsoft.Extensions.DependencyInjection;

    public class ZipArchiveFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<ZipArchiveEntry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.zip", static (fullPath, parentRelativePath, parent, parentPath) => new ZipArchiveFileSystem(parentRelativePath, parent));
        }

        protected override string GetEntryName(ZipArchiveEntry entry) => entry.FullName;

        protected override Stream Open(ZipArchiveEntry entry, FileStreamOptions parentOptions)
        {
            return CreateStreamWrapper(
                parentOptions,
                options =>
                {
                    var file = parent.File.Open(parentRelativePath, options);
                    var archive = new ZipArchive(file, ZipArchiveMode.Read);
                    entry = archive.GetEntry(entry.FullName)!;
                    return new CachingSeekableStream(new DisposingStream(entry.Open(), archive));
                },
                updated =>
                {
                    using var file = parent.File.Open(parentRelativePath, FileMode.Open, FileAccess.ReadWrite);
                    using var archive = new ZipArchive(file, ZipArchiveMode.Update);

                    var zipEntry = archive.GetEntry(entry.FullName)
                        ?? throw new FileNotFoundException($"Entry '{entry.FullName}' not found.");

                    using var destination = zipEntry.Open();
                    destination.SetLength(0);
                    updated.Position = 0;
                    updated.CopyTo(destination);
                });
        }

        protected override IEnumerable<ZipArchiveEntry> ReadIndex()
        {
            using var file = parent.File.OpenRead(parentRelativePath);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            return archive.Entries;
        }
    }
}
