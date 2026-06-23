namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;

    public class NestedFileSystemManager
    {
        private readonly PathComparer comparer;
        private readonly Dictionary<string, FileSystemFactory?> nestedFactories;
        private readonly Dictionary<string, IFileSystem> fileSystems;
        private readonly IServiceProvider serviceProvider;
        private readonly FileSystemHandler[] handlers;

        public NestedFileSystemManager(IServiceProvider serviceProvider, IFileSystem fs, params FileSystemHandler[] handlers)
        {
            this.serviceProvider = serviceProvider;
            this.handlers = handlers;
            this.comparer = new();
            this.nestedFactories = new(this.comparer);
            this.fileSystems = new(this.comparer)
            {
                [string.Empty] = fs,
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
                        var newParent = factory(path, parentRelativePath, parent, parentPath);
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
                foreach (var entry in this.EnumerateEntries(stack.Pop()))
                {
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

        public Stream OpenRead(string path) => this.Open(path, new FileStreamOptions { Mode = FileMode.Open, Access = FileAccess.Read, Share = FileShare.Read });

        public Stream Open(string path, FileStreamOptions options)
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
            this.handlers.Select(h => h(fullPath, parentRelativePath, parent, parentPath)).FirstOrDefault(f => f is not null);

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
