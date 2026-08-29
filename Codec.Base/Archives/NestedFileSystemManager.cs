namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class NestedFileSystemManager
    {
        private readonly PathComparer comparer;
        private readonly Dictionary<string, FileSystemFactory?> nestedFactories;
        private readonly Dictionary<string, IFileSystem> fileSystems;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<NestedFileSystemManager> logger;

        public NestedFileSystemManager(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            this.logger = serviceProvider.GetRequiredService<ILogger<NestedFileSystemManager>>();
            this.comparer = new();
            this.nestedFactories = new(this.comparer);
            this.fileSystems = new(this.comparer)
            {
                [string.Empty] = serviceProvider.GetRequiredService<RootEnumerableFileSystem>(),
            };
            this.RootEntry = new(string.Empty, false, false);
        }

        public Entry RootEntry { get; }

        /// <summary>
        /// Returns the parent filesystem that contains the specified path.
        /// </summary>
        /// <remarks>
        ///   "G:\Data\Foo.zip/a/b/c.txt" -> ("a/b/c.txt", ZipFileSystem, "G:\Data\Foo.zip")
        ///   "G:\Data\Foo.zip/" -> ("", ZipFileSystem, "G:\Data\Foo.zip")
        ///   "G:\Data\Foo.zip" -> ("G:\Data\Foo.zip", FileSystem, "")
        /// </remarks>
        private bool TryFindParentFileSystem(string path, out string parentRelativePath, [NotNullWhen(true)] out IFileSystem? parent, [NotNullWhen(true)] out string? parentPath, bool asFile = true)
        {
            var found = this.fileSystems.TryGetValue(path, out parent);
            if (found && parent != null && !asFile)
            {
                parentPath = path;
                parentRelativePath = string.Empty;
                return true;
            }

            if (PathExtensions.GetDirectoryName(path) is string directoryName && this.TryFindParentFileSystem(directoryName, out var relativePath, out parent, out parentPath, asFile: false))
            {
                parentRelativePath = parent.Path.Combine(relativePath, directoryName == string.Empty ? path : parent.Path.GetRelativePath(directoryName, path));
                if (!found && this.GetOrAddFactory(path, parentRelativePath, parent, parentPath, out var factory))
                {
                    if (parent.File.Exists(parentRelativePath))
                    {
                        IFileSystem newParent = null;
                        try
                        {
                            newParent = factory(path, parentRelativePath, parent, parentPath);
                        }
                        catch (Exception ex)
                        {
                            this.logger.CouldNotOpenFileSystem(ex, parentRelativePath);
                        }

                        this.fileSystems.Add(path, newParent);
                        this.nestedFactories.Remove(path);
                        if (newParent != null && !asFile)
                        {
                            parent = newParent;
                            parentPath = path;
                            parentRelativePath = string.Empty;
                        }
                    }
                }

                return true;
            }

            parent = null;
            parentPath = null;
            parentRelativePath = path;
            return false;
        }

        public IEnumerable<Entry> EnumerateEntries(string path, bool recursive = false)
        {
            var stack = new Stack<string>();
            stack.Push(path);

            while (stack.Count > 0)
            {
                IEnumerator<Entry> enumerator;
                try
                {
                    enumerator = this.EnumerateEntries(stack.Pop()).GetEnumerator();
                }
                catch
                {
                    // TODO: Log the error.
                    continue;
                }

                while (true)
                {
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // TODO: Log the error.
                        break;
                    }

                    var entry = enumerator.Current;
                    yield return entry;

                    if (recursive && entry.CanEnumerateEntries)
                    {
                        stack.Push(entry.Path);
                    }
                }
            }
        }

        public IEnumerable<Entry> EnumerateFiles(string path, string searchPattern, bool recursive = false)
        {
            var glob = PathExtensions.GlobToRegex(searchPattern);
            foreach (var entry in this.EnumerateEntries(path, recursive))
            {
                if (!entry.CanOpen || !glob.IsMatch(Path.GetFileName(entry.Path)))
                {
                    continue;
                }

                yield return entry;
            }
        }

        public bool FileExists(string path)
        {
            return this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out _) && parent.File.Exists(parentRelativePath);
        }

        public FileSystemStream OpenRead(string path) => this.Open(path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read });

        public FileSystemStream Open(string path, FileStreamOptions options)
        {
            if (!this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out _))
            {
                throw new FileNotFoundException(path);
            }

            return parent.File.Open(parentRelativePath, options);
        }

        public string GetFileName(string path)
        {
            if (!this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out _))
            {
                return PathExtensions.GetFileName(path);
            }

            return parent.Path.GetFileName(parentRelativePath);
        }

        public string GetExtension(string path)
        {
            if (!this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out _))
            {
                return PathExtensions.GetExtension(path);
            }

            return parent.Path.GetExtension(parentRelativePath);
        }

        public T? Resolve<T>(string path)
        {
            if (!this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out var parentPath))
            {
                return default;
            }

            return this.serviceProvider.Resolve<T>(path, parentRelativePath, parent, parentPath);
        }

        public Action<T>? ResolveWriter<T>(string path)
        {
            if (!this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out var parentPath))
            {
                return default;
            }

            return this.serviceProvider.ResolveWriter<T>(path, parentRelativePath, parent, parentPath);
        }

        private IEnumerable<Entry> EnumerateEntries(string path)
        {
            if (this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out var parentPath, asFile: false))
            {
                if (string.IsNullOrEmpty(parentRelativePath) || parent.Directory.Exists(parentRelativePath))
                {
                    foreach (var d in parent.Directory.EnumerateDirectories(parentRelativePath))
                    {
                        yield return new(parent.Path.CombineIgnoringAbsolute(parentPath, d), false, true);
                    }

                    foreach (var f in parent.Directory.EnumerateFiles(parentRelativePath))
                    {
                        var p = parent.Path.CombineIgnoringAbsolute(parentPath, f);
                        yield return new(p, true, this.IsNestedFileSystem(p, f, parent, parentPath));
                    }
                }
            }
        }

        private bool IsNestedFileSystem(string file, string parentRelativePath, IFileSystem parent, string parentPath)
        {
            if (this.fileSystems.ContainsKey(file))
            {
                return true;
            }

            return this.GetOrAddFactory(file, parentRelativePath, parent, parentPath, out _);
        }

        private bool GetOrAddFactory(string file, string parentRelativePath, IFileSystem parent, string parentPath, [NotNullWhen(true)] out FileSystemFactory? factory)
        {
            if (!this.nestedFactories.TryGetValue(file, out factory))
            {
                this.nestedFactories[file] = factory = this.GetNestedFactory(file, parentRelativePath, parent, parentPath);
            }

            return factory is not null;
        }

        private FileSystemFactory? GetNestedFactory(string fullPath, string parentRelativePath, IFileSystem parent, string parentPath) =>
            this.serviceProvider.GetDeferredFactory(fullPath, parentRelativePath, parent, parentPath, this.logger);

        public bool TryGetEntry(string path, out Entry entry)
        {
            if (this.TryFindParentFileSystem(path, out var parentRelativePath, out var parent, out var parentPath))
            {
                if (parentRelativePath == string.Empty)
                {
                    entry = new Entry(parentPath, true, true);
                }
                else
                {
                    path = parent.Path.CombineIgnoringAbsolute(parentPath, parentRelativePath);
                    entry = new Entry(path, parent.File.Exists(parentRelativePath), parent.Directory.Exists(parentRelativePath) || this.IsNestedFileSystem(path, parentRelativePath, parent, parentPath));
                }

                return true;
            }

            entry = null!;
            return false;
        }

        public string GetFullPath(string path, string basePath)
        {
            if (!this.TryFindParentFileSystem(basePath, out var _, out var parent, out _))
            {
                throw new InvalidOperationException();
            }

            return PathExtensions.GetFullPath(parent.Path, path, basePath);
        }

        public string CombinePaths(string parentFolder, string filePath)
        {
            if (!this.TryFindParentFileSystem(parentFolder, out var _, out var parent, out _))
            {
                throw new InvalidOperationException();
            }

            return parent.Path.Combine(parentFolder, filePath);
        }

        public string GetRelativePath(string parentFolder, string newPath)
        {
            if (!this.TryFindParentFileSystem(parentFolder, out var _, out var parent, out _))
            {
                throw new InvalidOperationException();
            }

            return PathExtensions.GetRelativePath(parent.Path, parentFolder, newPath);
        }

        public bool IsPathUnder(string possibleChildPath, string parentPath)
        {
            var parentParts = PathExtensions.Split(parentPath);
            var childParts = PathExtensions.Split(possibleChildPath);
            if (childParts.Length < parentParts.Length)
            {
                // TODO: Perhaps this function should normalize via `CombinePaths(parent, possibleChildPath)`? Add test coverage to determine.
                return false;
            }

            // TODO: Per-segment case sensitivity.
            var compare = StringComparison.OrdinalIgnoreCase;

            for (var i = 0; i < parentParts.Length; i++)
            {
                if (string.Compare(parentParts[i], childParts[i], compare) is not 0)
                {
                    return false;
                }
            }

            return true;
        }

        public class PathComparer : IComparer<string?>, IEqualityComparer<string?>
        {
            public bool Equals(string? x, string? y) => this.Compare(x, y) == 0;

            public int Compare(string? x, string? y)
            {
                if (ReferenceEquals(x, y) || string.Equals(x, y, StringComparison.Ordinal))
                {
                    return 0;
                }
                else if (x is null)
                {
                    return -1;
                }
                else if (y is null)
                {
                    return 1;
                }

                var xParts = PathExtensions.Split(x);
                var yParts = PathExtensions.Split(y);

                for (var i = 0; i < xParts.Length && i < yParts.Length; i++)
                {
                    if (xParts.Length != yParts.Length)
                    {
                        if (i == xParts.Length - 1)
                        {
                            return 1;
                        }
                        else if (i == yParts.Length - 1)
                        {
                            return -1;
                        }
                    }

                    // TODO: Per-segment case sensitivity.
                    var compare = StringComparison.OrdinalIgnoreCase;
                    if (string.Compare(xParts[i], yParts[i], compare) is not 0 and var num)
                    {
                        return num;
                    }
                }

                return PathExtensions.EndsWithSlash(x).CompareTo(PathExtensions.EndsWithSlash(y));
            }

            /// <inheritdoc/>
            public int GetHashCode(string? obj)
            {
                var hash = default(HashCode);
                if (obj != null)
                {
                    var parts = PathExtensions.Split(obj);
                    foreach (var part in parts)
                    {
                        // TODO: Per-segment case sensitivity.
                        var compare = StringComparison.OrdinalIgnoreCase;
                        hash.Add(string.GetHashCode(part, compare));
                    }

                    if (parts.Length == 0 || PathExtensions.EndsWithSlash(obj))
                    {
                        hash.Add('/');
                    }
                }

                return hash.ToHashCode();
            }
        }
    }
}
