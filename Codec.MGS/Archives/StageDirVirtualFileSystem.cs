// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Codec;
    using Codec.Archives;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;
    using FileEntry = (string name, long offset, long size);
    using FileSpan = (long offset, long size);

    public sealed class StageDirVirtualFileSystem : Codec.Archives.FileSystemBase
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
            [0x72] = "player",
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

        [InlineArray(8)]
        public struct Name8
        {
            public byte Char0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StageDirEntry
        {
            public Name8 Name;
            public uint Size;
        }

        private record struct DirEntry(string Name, long Offset);

        private static DirEntry[] ReadIndex(Stream source)
        {
            var dataOffset = source.ReadUInt32LittleEndian();
            var entries = source.ReadArrayLittleEndian<StageDirEntry>((int)dataOffset / Marshal.SizeOf<StageDirEntry>());
            return Array.ConvertAll(entries, entry =>
                new DirEntry(
                    Encoding.ASCII.GetString(entry.Name).TrimEnd('\0'),
                    entry.Size * SectorSize));
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct DarHeader
        {
            public ushort Id;
            public byte FileType;
            public uint Size;
        }

        private static FileEntry[] ReadDar(Stream source, string group, long offset, long length)
        {
            var entries = new List<FileEntry>();
            var headerSize = (uint)Marshal.SizeOf<DarHeader>();
            var relative = 0u;
            while (relative < length - 7)
            {
                source.Position = offset + relative;
                var header = source.ReadLittleEndian<DarHeader>();
                var key = $"{group}/{header.Id:x4}.{GetExtension(header.FileType)}";
                entries.Add((key, offset + relative + headerSize, header.Size));
                relative += headerSize + header.Size;
            }

            return [.. entries];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DarItemEntry
        {
            public uint Filename;
            public uint Size;

            public readonly ushort Id => (ushort)Filename;

            public readonly byte Group => (byte)(Filename >> 16);

            public readonly byte Ext => (byte)(Filename >> 24);
        }

        private static FileEntry[] ReadList(Stream source, long offset)
        {
            source.Position = offset + 2;
            var totalSize = source.ReadUInt16LittleEndian() * SectorSize;

            var rawEntries = new List<(DarItemEntry Entry, bool Packed)>();
            while (true)
            {
                var entry = source.ReadLittleEndian<DarItemEntry>();
                if (entry.Filename == 0)
                {
                    break;
                }

                if (entry.Ext == byte.MaxValue)
                {
                    var notLast = false;
                    for (var i = rawEntries.Count - 1; i >= 0; i--)
                    {
                        var prev = rawEntries[i];
                        if (prev.Entry.Group != entry.Group)
                        {
                            break;
                        }

                        var nextSize = prev.Entry.Size;
                        prev.Packed = notLast;
                        prev.Entry.Size = entry.Size - prev.Entry.Size;
                        rawEntries[i] = prev;
                        entry.Size = nextSize;
                        notLast = true;
                    }
                }
                else
                {
                    rawEntries.Add((entry, false));
                }
            }

            var entries = new List<FileEntry>();
            var counts = new Dictionary<string, int>();
            var relative = SectorSize;
            foreach (var (entry, packed) in rawEntries)
            {
                var group = GetGroup(entry.Group);

                if (entry.Ext == 0x64)
                {
                    entries.AddRange(ReadDar(source, group, offset + relative, entry.Size));
                }
                else
                {
                    var id = entry.Id.ToString("x4", CultureInfo.CurrentCulture);
                    var ext = GetExtension(entry.Ext);
                    var key = $"{group}/{id}.{ext}";

                    counts.TryGetValue(key, out var ix);
                    counts[key] = ix + 1;
                    if (ix > 0)
                    {
                        key = $"{group}/{id}.{ix}.{ext}";
                    }

                    entries.Add((key, offset + relative, entry.Size));
                }

                relative += entry.Size;
                if (!packed)
                {
                    relative = StreamExtensions.Align(relative, SectorSize);
                }
            }

            return entries.OrderBy(e => e.name).ToArray();
        }

        private FileEntry[] GetFileIndex(string path)
        {
            var ix = Array.FindIndex(this.index, e => e.Name == path);
            if (ix < 0)
            {
                throw new DirectoryNotFoundException();
            }

            if (!this.fileEntries.TryGetValue(path, out var files))
            {
                var entry = this.index[ix];
                using var stream = this.parent.File.OpenRead(this.parentRelativePath);
                this.fileEntries[path] = files = ReadList(stream, entry.Offset);
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
                var ix = Array.FindIndex(parent.index, e => e.Name == root);
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
                        return parent.index.Select(i => i.Name).Where(f => glob.IsMatch(f));
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
                            parent.GetFileIndex(i.Name)
                                .Where(f =>
                                    glob.IsMatch(System.IO.Path.GetFileName(f.name)))
                                .Select(f => $"{i.Name}/{f.name}"));
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
