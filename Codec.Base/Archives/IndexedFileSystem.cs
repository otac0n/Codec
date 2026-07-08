namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;

    public abstract class IndexedFileSystem<TEntry> : FileSystemBase
        where TEntry : notnull
    {
        private readonly StringComparison comparison;

        protected Dictionary<string, TEntry> Index => field ??= this.ReadIndex().ToDictionary(e => this.CanonicalizePath(this.GetEntryName(e)), StringComparer.FromComparison(this.comparison));

        protected IndexedFileSystem(StringComparison comparison = StringComparison.Ordinal)
        {
            this.Directory = new IndexedDirectoryBase(this);
            this.File = new IndexedFileBase(this);
            this.comparison = comparison;
        }

        protected abstract IEnumerable<TEntry> ReadIndex();

        protected abstract Stream Open(TEntry entry, FileStreamOptions parentOptions);

        protected abstract string GetEntryName(TEntry entry);

        private string CanonicalizePath(string? path) =>
            string.Join(this.Path.DirectorySeparatorChar, PathExtensions.SplitPath(path));

        private class IndexedDirectoryBase(IndexedFileSystem<TEntry> parent) : DirectoryBase(parent)
        {
            public override bool Exists([NotNullWhen(true)] string? path)
            {
                if (path is null)
                {
                    return false;
                }

                var prefix = parent.CanonicalizePath(path);
                if (prefix != string.Empty)
                {
                    prefix += parent.Path.DirectorySeparatorChar;
                }

                return parent.Index.Keys.Any(key => key.StartsWith(prefix, parent.comparison));
            }

            protected override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption, bool files = false, bool directories = false)
            {
                var prefix = parent.CanonicalizePath(path);
                if (prefix != string.Empty)
                {
                    prefix += parent.Path.DirectorySeparatorChar;
                }

                var prefixFound = false;
                var glob = PathExtensions.GlobToRegex(searchPattern);

                var seenDirectories = new HashSet<string>(StringComparer.FromComparison(parent.comparison));
                foreach (var key in parent.Index.Keys)
                {
                    if (key.StartsWith(prefix, parent.comparison))
                    {
                        prefixFound = true;

                        var nextSlash = key.IndexOf(parent.Path.DirectorySeparatorChar, prefix.Length);

                        if (directories && nextSlash != -1)
                        {
                            var slash = nextSlash;
                            do
                            {
                                var dirPath = key[..slash];
                                if (seenDirectories.Add(dirPath) && glob.IsMatch(parent.Path.GetFileName(dirPath)))
                                {
                                    yield return dirPath;
                                }

                                if (searchOption != SearchOption.AllDirectories)
                                {
                                    break;
                                }

                                slash = key.IndexOf(parent.Path.DirectorySeparatorChar, slash + 1);
                            }
                            while (slash != -1);
                        }

                        if (files)
                        {
                            if (nextSlash == -1 || searchOption == SearchOption.AllDirectories)
                            {
                                if (glob.IsMatch(parent.Path.GetFileName(key)))
                                {
                                    yield return key;
                                }
                            }
                        }
                    }
                }

                if (!prefixFound && !string.IsNullOrEmpty(prefix))
                {
                    throw new DirectoryNotFoundException(path);
                }
            }
        }

        private class IndexedFileBase(IndexedFileSystem<TEntry> parent) : FileBase(parent)
        {
            private readonly FileLockManager<TEntry> locks = new();

            public override bool Exists([NotNullWhen(true)] string? path) =>
                !PathExtensions.EndsWithSlash(path) && parent.Index.ContainsKey(parent.CanonicalizePath(path));

            public override FileSystemStream Open(string path, FileStreamOptions options)
            {
                if (!PathExtensions.EndsWithSlash(path))
                {
                    var canonicalPath = parent.CanonicalizePath(path);

                    if (parent.Index.TryGetValue(canonicalPath, out var entry))
                    {
                        var @lock = this.locks.Acquire(entry, options.Access, options.Share);
                        var parentOptions = GetParentOptions(options);
                        return new StreamWrapper(
                            new DisposingStream(
                                parent.Open(entry, parentOptions),
                                @lock),
                            canonicalPath,
                            (options.Options & FileOptions.Asynchronous) == FileOptions.Asynchronous);
                    }
                }

                throw new FileNotFoundException($"File '{path}' not found in archive.");
            }
        }
    }
}
