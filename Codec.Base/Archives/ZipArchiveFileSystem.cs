namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.IO.Compression;
    using Microsoft.Extensions.DependencyInjection;

    public class ZipArchiveFileSystem(string parentRelativePath, IFileSystem parent) : IndexedFileSystem<ZipArchiveEntry>
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((servicProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new ZipArchiveFileSystem(parentRelativePath, parent);
                }

                return null;
            });
        }

        protected override string GetEntryName(ZipArchiveEntry entry) => entry.FullName;

        protected override Stream Open(ZipArchiveEntry entry, FileStreamOptions parentOptions)
        {
            if (parentOptions.Mode != FileMode.Open)
            {
                throw new NotSupportedException("ZipArchiveFileSystem currently only supports opening files in read-only mode.");
            }

            var file = parent.File.OpenRead(parentRelativePath);
            var archive = new ZipArchive(file, ZipArchiveMode.Read);
            entry = archive.GetEntry(entry.FullName)!;
            return new CachingSeekableStream(new DisposingStream(entry.Open(), archive));
        }

        protected override IEnumerable<ZipArchiveEntry> ReadIndex()
        {
            using var file = parent.File.OpenRead(parentRelativePath);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read);
            return archive.Entries;
        }
    }
}
