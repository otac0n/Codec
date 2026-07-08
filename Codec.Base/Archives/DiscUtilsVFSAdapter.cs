namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;

    public partial class DiscUtilsVFSAdapter : FileSystemBase
    {
        private readonly DiscUtils.IFileSystem underlying;

        public DiscUtilsVFSAdapter(DiscUtils.IFileSystem underlying)
        {
            this.underlying = underlying;
            this.Directory = new DirectoryProvider(this);
            this.File = new FileProvider(this);
            this.Path = new PathProvider(this);
        }

        private static string[] FilterNames(string[] names)
        {
            return Array.ConvertAll(names, name =>
            {
                var lastSemicolon = name.LastIndexOf(";", StringComparison.OrdinalIgnoreCase);
                if (lastSemicolon >= 0)
                {
                    return name[..lastSemicolon];
                }
                else
                {
                    return name;
                }
            });
        }

        private class DirectoryProvider(DiscUtilsVFSAdapter parent) : DirectoryBase(parent)
        {
            public override void Delete(string path) => parent.underlying.DeleteDirectory(path);

            public override void Delete(string path, bool recursive) => parent.underlying.DeleteDirectory(path, recursive);

            public override IEnumerable<string> EnumerateDirectories(string path) => this.GetDirectories(path);

            public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern) => this.GetDirectories(path, searchPattern);

            public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => this.GetDirectories(path, searchPattern, searchOption);

            public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions) => this.GetDirectories(path, searchPattern, enumerationOptions);

            public override IEnumerable<string> EnumerateFiles(string path) => this.GetFiles(path);

            public override IEnumerable<string> EnumerateFiles(string path, string searchPattern) => this.GetFiles(path, searchPattern);

            public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => this.GetFiles(path, searchPattern, searchOption);

            public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions) => this.GetFiles(path, searchPattern, enumerationOptions);

            public override IEnumerable<string> EnumerateFileSystemEntries(string path) => this.GetFileSystemEntries(path);

            public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern) => this.GetFileSystemEntries(path, searchPattern);

            public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => this.GetFileSystemEntries(path, searchPattern, searchOption);

            public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, EnumerationOptions enumerationOptions) => this.GetFileSystemEntries(path, searchPattern, enumerationOptions);

            public override bool Exists([NotNullWhen(true)] string? path) => parent.underlying.Exists(path);

            public override DateTime GetCreationTime(string path) => parent.underlying.GetCreationTime(path);

            public override DateTime GetCreationTimeUtc(string path) => parent.underlying.GetCreationTimeUtc(path);

            public override string[] GetDirectories(string path) => parent.underlying.GetDirectories(path);

            public override string[] GetDirectories(string path, string searchPattern) => parent.underlying.GetDirectories(path, searchPattern);

            public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => parent.underlying.GetDirectories(path, searchPattern, searchOption);

            public override string[] GetFiles(string path) => FilterNames(parent.underlying.GetFiles(path));

            public override string[] GetFiles(string path, string searchPattern) => FilterNames(parent.underlying.GetFiles(path, searchPattern));

            public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => FilterNames(parent.underlying.GetFiles(path, searchPattern, searchOption));

            public override string[] GetFileSystemEntries(string path) => FilterNames(parent.underlying.GetFileSystemEntries(path));

            public override string[] GetFileSystemEntries(string path, string searchPattern) => FilterNames(parent.underlying.GetFileSystemEntries(path, searchPattern));

            public override string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => searchOption == SearchOption.TopDirectoryOnly ? FilterNames(parent.underlying.GetFileSystemEntries(path, searchPattern)) : throw new NotSupportedException();

            public override DateTime GetLastAccessTime(string path) => parent.underlying.GetLastAccessTime(path);

            public override DateTime GetLastAccessTimeUtc(string path) => parent.underlying.GetLastAccessTimeUtc(path);

            public override DateTime GetLastWriteTime(string path) => parent.underlying.GetLastWriteTime(path);

            public override DateTime GetLastWriteTimeUtc(string path) => parent.underlying.GetLastWriteTimeUtc(path);

            public override void SetCreationTime(string path, DateTime creationTime) => parent.underlying.SetCreationTime(path, creationTime);

            public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => parent.underlying.SetCreationTimeUtc(path, creationTimeUtc);

            public override void SetLastAccessTime(string path, DateTime lastAccessTime) => parent.underlying.SetLastAccessTime(path, lastAccessTime);

            public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => parent.underlying.SetLastAccessTimeUtc(path, lastAccessTimeUtc);

            public override void SetLastWriteTime(string path, DateTime lastWriteTime) => parent.underlying.SetLastWriteTime(path, lastWriteTime);

            public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => parent.underlying.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        private class FileProvider(DiscUtilsVFSAdapter parent) : FileBase(parent)
        {
            private readonly FileLockManager<string> locks = new();

            public override bool Exists([NotNullWhen(true)] string? path) => parent.underlying.Exists(path);

            public override FileAttributes GetAttributes(string path) => parent.underlying.GetAttributes(path);

            public override DateTime GetCreationTime(string path) => parent.underlying.GetCreationTime(path);

            public override DateTime GetCreationTimeUtc(string path) => parent.underlying.GetCreationTimeUtc(path);

            public override DateTime GetLastAccessTime(string path) => parent.underlying.GetLastAccessTime(path);

            public override DateTime GetLastAccessTimeUtc(string path) => parent.underlying.GetLastAccessTimeUtc(path);

            public override DateTime GetLastWriteTime(string path) => parent.underlying.GetLastWriteTime(path);

            public override DateTime GetLastWriteTimeUtc(string path) => parent.underlying.GetLastWriteTimeUtc(path);

            public override FileSystemStream Open(string path, FileStreamOptions options) =>
                new StreamWrapper(
                    new DisposingStream(
                        parent.underlying.OpenFile(path, options.Mode, options.Access),
                        this.locks.Acquire(path, options.Access, options.Share)),
                    path,
                    (options.Options & FileOptions.Asynchronous) == FileOptions.Asynchronous);

            public override void SetAttributes(string path, FileAttributes fileAttributes) => parent.underlying.SetAttributes(path, fileAttributes);

            public override void SetCreationTime(string path, DateTime creationTime) => parent.underlying.SetCreationTime(path, creationTime);

            public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => parent.underlying.SetCreationTimeUtc(path, creationTimeUtc);

            public override void SetLastAccessTime(string path, DateTime lastAccessTime) => parent.underlying.SetLastAccessTime(path, lastAccessTime);

            public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => parent.underlying.SetLastAccessTimeUtc(path, lastAccessTimeUtc);

            public override void SetLastWriteTime(string path, DateTime lastWriteTime) => parent.underlying.SetLastWriteTime(path, lastWriteTime);

            public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => parent.underlying.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        private class PathProvider(DiscUtilsVFSAdapter parent) : PathBase(parent)
        {
            public override char DirectorySeparatorChar => '\\';
        }
    }
}
