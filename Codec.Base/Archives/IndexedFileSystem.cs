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
        protected Dictionary<string, TEntry>? index;

        private readonly StringComparison comparison;

        protected Dictionary<string, TEntry> Index => index ??= this.ReadIndex().ToDictionary(e => this.CanonicalizePath(this.GetEntryName(e)), StringComparer.FromComparison(this.comparison));

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
                ValidateArguments(options.Mode, options.Access, options.Share, options.BufferSize, options.Options, options.PreallocationSize);

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

        protected static void ValidateArguments(FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options, long preallocationSize)
        {
            var tempshare = share & ~FileShare.Inheritable;
            ArgumentOutOfRangeException.ThrowIfLessThan((int)mode, (int)FileMode.CreateNew, nameof(mode));
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)mode, (int)FileMode.Append, nameof(mode));
            ArgumentOutOfRangeException.ThrowIfLessThan((int)access, (int)FileAccess.Read, nameof(access));
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)access, (int)FileAccess.ReadWrite, nameof(access));
            ArgumentOutOfRangeException.ThrowIfLessThan((int)tempshare, (int)FileShare.None, nameof(share));
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)tempshare, (int)(FileShare.ReadWrite | FileShare.Delete), nameof(share));
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferSize, 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(preallocationSize, 0);

            const FileOptions NoBuffering = (FileOptions)0x20000000;
            const FileOptions BackupOrRestore = (FileOptions)0x02000000;
            const FileOptions ValidFileOptions = FileOptions.WriteThrough | FileOptions.Asynchronous | FileOptions.RandomAccess
                | FileOptions.DeleteOnClose | FileOptions.SequentialScan | FileOptions.Encrypted
                | NoBuffering | BackupOrRestore;

            if (options != FileOptions.None && (options & ~ValidFileOptions) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Invalid file options.");
            }

            if ((access & FileAccess.Write) == 0)
            {
                if (mode is FileMode.Truncate or FileMode.CreateNew or FileMode.Create or FileMode.Append)
                {
                    throw new ArgumentException("Invalid file mode and file access combination.", nameof(access));
                }
            }

            if ((access & FileAccess.Read) != 0 && mode == FileMode.Append)
            {
                throw new ArgumentException("Invalid append mode with read access.", nameof(access));
            }

            if (preallocationSize > 0)
            {
                if ((access & FileAccess.Write) == 0)
                {
                    throw new ArgumentException("Invalid preallocation size with write access.", nameof(access));
                }

                if (mode is not FileMode.Create and not FileMode.CreateNew)
                {
                    throw new ArgumentException("Invalid preallocation size with existing file.", nameof(mode));
                }
            }
        }

        protected static Stream CreateStreamWrapper(FileStreamOptions parentOptions, Func<FileStreamOptions, Stream> value, Action<Stream> onClose)
        {
            // Assumes the file exists. Assume valid combinations of FileMode, FileAccess, and FileShare are passed in.
            Stream? stream = null;
            switch (parentOptions.Mode)
            {
                case FileMode.CreateNew:
                    throw new IOException("File already exists.");

                case FileMode.Open:
                case FileMode.OpenOrCreate:
                    stream = value(MakeReadOnly(parentOptions));
                    if ((parentOptions.Access & FileAccess.Write) != 0)
                    {
                        var cached = CachingSeekableStream.Wrap(stream);
                        stream = new DisposingStream(cached, x =>
                        {
                            cached.ReleaseInnerStream();
                            onClose(x);
                        });
                    }

                    break;

                case FileMode.Create:
                case FileMode.Truncate:
                    stream = new MemoryStream((int)parentOptions.PreallocationSize);
                    stream = new DisposingStream(stream, onClose);
                    break;

                case FileMode.Append:
                    stream = value(parentOptions);
                    stream.Position = stream.Length;
                    break;
            }

            return stream!;
        }

        private static FileStreamOptions MakeReadOnly(FileStreamOptions parentOptions)
        {
            var newOptions = new FileStreamOptions()
            {
                Access = FileAccess.Read, // Even for write-only files we need to restore any unwritten byte ranges.
                Mode = parentOptions.Mode,
                Share = parentOptions.Share,
                BufferSize = parentOptions.BufferSize,
                Options = parentOptions.Options,
                PreallocationSize = parentOptions.PreallocationSize,
            };

            if (!OperatingSystem.IsWindows())
            {
                newOptions.UnixCreateMode = parentOptions.UnixCreateMode;
            }

            return newOptions;
        }
    }
}
