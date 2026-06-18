// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text;
    using Codec;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using DirEntry = (string name, long offset);
    using FileEntry = (string name, long offset, long size);
    using FileSpan = (long offset, long size);

    public sealed class StageDirVirtualFileSystem : FileSystemBase
    {
        private static readonly long SectorSize = 2048L;
        private static readonly ImmutableDictionary<byte, string> extensions = new Dictionary<byte, string>
        {
            [0x61] = "azm",
            [0x62] = "bin",
            [0x63] = "con",
            [0x64] = "dar",
            [0x65] = "efx",
            [0x67] = "gcx",
            [0x68] = "hzm",
            [0x69] = "img",
            [0x6b] = "kmd",
            [0x6c] = "lit",
            [0x6d] = "mdx",
            [0x6f] = "oar",
            [0x70] = "pcx",
            [0x72] = "res",
            [0x73] = "sgt",
            [0x77] = "wvx",
            [0x7a] = "zmd",
        }.ToImmutableDictionary();

        private static readonly ImmutableDictionary<byte, string> groups = new Dictionary<byte, string>
        {
            [0x63] = "model",
            [0x6e] = "texture",
            [0x73] = "sound",
        }.ToImmutableDictionary();

        private readonly DirEntry[] index;
        private readonly Dictionary<string, FileEntry[]> fileEntries = [];
        private readonly FileLockManager<FileSpan> locks = new();
        private readonly string parentRelativePath;
        private readonly IFileSystem parent;

        public StageDirVirtualFileSystem(string parentRelativePath, IFileSystem parent)
        {
            this.parentRelativePath = parentRelativePath;
            this.parent = parent;
            using var source = parent.File.OpenRead(parentRelativePath);
            this.index = ReadIndex(source);

            this.Directory = new DirectoryProvider(this);
            this.File = new FileProvider(this);
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (string.Equals(parent.Path.GetFileName(parentRelativePath), "STAGE.DIR", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                        new StageDirVirtualFileSystem(parentRelativePath, parent);
                }

                return null;
            });
        }

        private static string GetExtension(byte id) =>
            extensions.TryGetValue(id, out var extension) ? extension : $"x{id:x2}";

        private static string GetGroup(byte id) =>
            groups.TryGetValue(id, out var group) ? group : $"x{id:x2}";

        private static DirEntry[] ReadIndex(Stream source)
        {
            var buffer = new byte[12];
            source.ReadExactly(buffer, 4);

            var dataOffset = BitConverter.ToUInt32(buffer, 0);
            var entries = new DirEntry[dataOffset / 12];
            for (var i = 0; i < entries.Length; i++)
            {
                source.ReadExactly(buffer, 12);

                var name = Encoding.ASCII.GetString(buffer, 0, 8).TrimEnd('\0');
                var offset = BitConverter.ToUInt32(buffer, 8) * SectorSize;

                entries[i] = (name, offset);
            }

            return entries;
        }

        private static FileEntry[] ReadDar(Stream source, string group, long offset, long length)
        {
            var buffer = new byte[8];

            var entries = new List<FileEntry>();
            var relative = 0u;
            while (relative < length - 7)
            {
                source.Seek(offset + relative, SeekOrigin.Begin);
                source.ReadExactly(buffer, 8);

                var id = string.Concat(buffer[..2].AsEnumerable().Reverse().Select(b => b.ToString("x2"))); // TODO: Use Span.
                var ext = GetExtension(buffer[2]);
                var size = BitConverter.ToUInt32(buffer, 4);

                var key = $"{group}/{id}.{ext}";

                entries.Add((key, offset + relative + 8, size));

                relative += 8 + size;
            }

            return entries.ToArray();
        }

        private static FileEntry[] ReadList(Stream source, long offset)
        {
            source.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[8];
            source.ReadExactly(buffer, 4);
            var totalSize = BitConverter.ToUInt16(buffer, 2) * SectorSize;

            var rawEntries = new List<(ushort id, byte group, byte ext, uint size, bool packed)>();
            while (true)
            {
                source.ReadExactly(buffer, 8);
                if (BitConverter.ToUInt32(buffer, 0) == 0)
                {
                    break;
                }

                var id = BitConverter.ToUInt16(buffer, 0);
                var group = buffer[2];
                var ext = buffer[3];
                var size = BitConverter.ToUInt32(buffer, 4);

                if (ext == byte.MaxValue)
                {
                    var notLast = false;
                    for (var i = rawEntries.Count - 1; i >= 0; i--)
                    {
                        var prev = rawEntries[i];
                        if (prev.group != group)
                        {
                            break;
                        }

                        var nextSize = prev.size;
                        rawEntries[i] = prev with { packed = notLast, size = size - prev.size };
                        size = nextSize;
                        notLast = true;
                    }
                }
                else
                {
                    rawEntries.Add((id, group, ext, size, false));
                }
            }

            var entries = new List<FileEntry>();
            var counts = new Dictionary<string, int>();
            var relative = SectorSize;
            foreach (var entry in rawEntries)
            {
                var group = GetGroup(entry.group);

                if (entry.ext == 0x64)
                {
                    entries.AddRange(ReadDar(source, group, offset + relative, entry.size));
                }
                else
                {
                    var id = entry.id.ToString("x4");
                    var ext = GetExtension(entry.ext);

                    var key = $"{group}/{id}.{ext}";
                    counts.TryGetValue(key, out var ix);
                    counts[key] = ix + 1;

                    if (ix > 0)
                    {
                        key = $"{group}/{id}.{ix}.{ext}";
                    }

                    entries.Add((key, offset + relative, entry.size));
                }

                relative += entry.size;
                if (!entry.packed)
                {
                    relative = StreamExtensions.Align(relative, SectorSize);
                }
            }

            return entries.OrderBy(e => e.name).ToArray();
        }

        private FileEntry[] GetFileIndex(string path)
        {
            var ix = Array.FindIndex(this.index, e => e.name == path);
            if (ix < 0)
            {
                throw new DirectoryNotFoundException();
            }

            if (!this.fileEntries.TryGetValue(path, out var files))
            {
                var entry = this.index[ix];
                using var stream = this.parent.File.OpenRead(this.parentRelativePath);
                this.fileEntries[path] = files = ReadList(stream, entry.offset);
            }

            return files;
        }

        private FileSpan? GetStreamSpanRange(string path)
        {
            var ix = path.AsSpan().IndexOfAny(PathExtensions.Separators);
            if (ix >= 0)
            {
                var name = path[(ix + 1)..];
                var dir = path[..ix].TrimEnd(PathExtensions.Separators);

                var files = this.GetFileIndex(dir);
                ix = Array.FindIndex(files, e => e.name == name);
                if (ix >= 0)
                {
                    var file = files[ix];
                    return (file.offset, file.size);
                }
            }

            return null;
        }

        private FileSystemStream Open(string path, FileStreamOptions options)
        {
            path = string.Join("/", path.Split(PathExtensions.Separators, StringSplitOptions.RemoveEmptyEntries));

            if (this.GetStreamSpanRange(path) is FileSpan fileSpan)
            {
                var @lock = this.locks.Acquire(fileSpan, options.Access, options.Share);
                var parentOptions = FileBase.GetParentOptions(options);
                return new StreamWrapper(
                    new OffsetStreamSpan(this.parent.File.Open(this.parentRelativePath, parentOptions), fileSpan.offset, fileSpan.size, Ownership.Dispose),
                    path,
                    @lock,
                    (options.Options & FileOptions.Asynchronous) == FileOptions.Asynchronous);
            }

            throw new FileNotFoundException(new FileNotFoundException().Message, path);
        }

        private class DirectoryProvider(StageDirVirtualFileSystem parent) : DirectoryBase(parent)
        {
            public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption) =>
                this.EnumerateDirectories(path, searchPattern, searchOption).Concat(this.EnumerateFiles(path, searchPattern, searchOption));

            public override bool Exists([NotNullWhen(true)] string? path)
            {
                if (path == string.Empty)
                {
                    return true;
                }

                var parts = path?.Split(PathExtensions.Separators, StringSplitOptions.RemoveEmptyEntries);
                if (parts is null or [])
                {
                    return false;
                }

                var root = parts[0];
                var ix = Array.FindIndex(parent.index, e => e.name == root);
                if (ix < 0)
                {
                    return false;
                }

                if (parts.Length == 1)
                {
                    return true;
                }

                var files = parent.GetFileIndex(root);
                var dir = string.Concat(parts.Skip(1).Select(p => p + "/"));
                return files.Any(f => f.name.StartsWith(dir));
            }

            public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
            {
                var glob = PathExtensions.GlobToRegex(searchPattern);
                if (path == string.Empty)
                {
                    if (searchOption == SearchOption.TopDirectoryOnly)
                    {
                        return parent.index.Select(i => i.name).Where(f => glob.IsMatch(f));
                    }
                }

                var parts = path.Split(PathExtensions.Separators, StringSplitOptions.RemoveEmptyEntries);
                var root = parts[0];

                var index = parent.GetFileIndex(root);
                if (index.Length == 0)
                {
                    throw new DirectoryNotFoundException();
                }

                var indexDirs = index.Select(f => f.name[0..f.name.IndexOf('/')]).Distinct();
                if (parts.Length == 1)
                {
                    if (searchOption == SearchOption.TopDirectoryOnly)
                    {
                        return indexDirs.Where(d => glob.IsMatch(d)).Select(d => $"{path}/{d}");
                    }
                }
                else if (!indexDirs.Contains(parts[1]) || parts.Length > 2)
                {
                    throw new DirectoryNotFoundException();
                }

                var dir = string.Concat(parts.Skip(1).Select(p => p + "/"));
                if (searchOption == SearchOption.TopDirectoryOnly)
                {
                    return [];
                }

                throw new NotImplementedException();
            }

            public override IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
            {
                var glob = PathExtensions.GlobToRegex(searchPattern);
                if (path == string.Empty)
                {
                    if (searchOption == SearchOption.TopDirectoryOnly)
                    {
                        return Enumerable.Empty<string>();
                    }
                    else
                    {
                        return parent.index.SelectMany(i =>
                            parent.GetFileIndex(i.name)
                                .Where(f =>
                                    glob.IsMatch(System.IO.Path.GetFileName(f.name)))
                                .Select(f => $"{i.name}/{f.name}"));
                    }
                }
                else
                {
                    var parts = path.Split(PathExtensions.Separators, StringSplitOptions.RemoveEmptyEntries);
                    var root = parts[0];
                    var dir = string.Concat(parts.Skip(1).Select(p => p + "/"));
                    if (searchOption == SearchOption.TopDirectoryOnly)
                    {
                        return parent.GetFileIndex(root)
                            .Where(f =>
                                f.name.StartsWith(dir) &&
                                f.name.IndexOf('/', dir.Length) == -1 &&
                                glob.IsMatch(System.IO.Path.GetFileName(f.name)))
                            .Select(f => $"{root}/{f.name}");
                    }
                    else
                    {
                        return parent.GetFileIndex(root)
                            .Where(f =>
                                f.name.StartsWith(dir) &&
                                glob.IsMatch(System.IO.Path.GetFileName(f.name)))
                            .Select(f => $"{root}/{f.name}");
                    }
                }
            }
        }

        private class FileProvider(StageDirVirtualFileSystem parent) : FileBase(parent)
        {
            public override bool Exists([NotNullWhen(true)] string? path) => parent.GetStreamSpanRange(path) is not null;

            public override FileSystemStream Open(string path, FileStreamOptions options) => parent.Open(path, options);
        }
    }
}
