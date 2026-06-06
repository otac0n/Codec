namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text.RegularExpressions;
    using CueSharp;
    using DiscUtils.Iso9660;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    internal partial class CDReaderVFSAdapter : FileSystemBase
    {
        private readonly CDReader cdReader;

        public CDReaderVFSAdapter(CDReader cdReader)
        {
            this.cdReader = cdReader;
            this.Directory = new DirectoryProvider(this);
            this.File = new FileProvider(this);
            this.Path = new PathProvider(this);
        }

        public static void Register(IServiceCollection services)
        {

            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {

                if (parent is CueSheetVFS cueFS &&
                    TrackMatcher().Match(parentRelativePath) is { Success: true } match &&
                    int.TryParse(match.Groups[1].Value, out var trackNumber) &&
                    cueFS.CueSheet.Tracks[trackNumber - 1] is Track track &&
                    track.TrackDataType.ToString().StartsWith("MODE", StringComparison.Ordinal))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                        CreateCueTrackFileSystem(cueFS.CueSheetPath, cueFS.Parent, track);
                }

                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".cue", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var cueStream = parent.File.OpenRead(parentRelativePath);
                        using var reader = new StreamReader(cueStream);
                        var cue = new CueSheet(reader);
                        if (cue.Tracks is [Track track] && track.TrackDataType.ToString().StartsWith("MODE", StringComparison.Ordinal))
                        {
                            return CreateCueTrackFileSystem(parentRelativePath, parent, track);
                        }
                        else
                        {
                            return new CueSheetVFS(parentRelativePath, parent, cue);
                        }
                    };
                }

                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".iso", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        var file = parent.File.OpenRead(parentRelativePath);
                        var cdReader = new CDReader(file, joliet: true);
                        return new CDReaderVFSAdapter(cdReader);
                    };
                }

                return null;
            });
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

        private static CDReaderVFSAdapter CreateCueTrackFileSystem(string parentRelativePath, IFileSystem parent, Track track)
        {
            var stream = parent.File.OpenRead(GetTrackFileName(parentRelativePath, parent, track));
            var cdReader = new CDReader(
                track.TrackDataType switch
                {
                    DataType.MODE1_2048 => stream,
                    DataType.MODE1_2352 => new CDSectorStream(stream, CDSectorStream.Mode1),
                    DataType.MODE2_2336 => new CDSectorStream(stream, CDSectorStream.Mode2),
                    DataType.MODE2_2352 => new CDSectorStream(stream, CDSectorStream.XAForm1),
                },
                joliet: true);
            return new CDReaderVFSAdapter(cdReader);
        }

        private static string GetTrackFileName(string parentRelativePath, IFileSystem parent, Track track) =>
            parent.Path.Combine(
                parent.Path.GetDirectoryName(parentRelativePath)!,
                track.DataFile.Filename);

        [GeneratedRegex(@"Track (\d+)")]
        private static partial Regex TrackMatcher();

        private class CueSheetVFS : IndexedFileSystem<Track>
        {
            public CueSheet CueSheet { get; }

            public string CueSheetPath { get; }

            public IFileSystem Parent { get; }

            public CueSheetVFS(string path, IFileSystem parent, CueSheet cue)
            {
                this.Parent = parent ?? new FileSystem();
                this.CueSheetPath = path;
                this.CueSheet = cue;
            }

            protected override IEnumerable<Track> ReadIndex() => this.CueSheet.Tracks;

            protected override string GetEntryName(Track entry) =>
                $"Track {entry.TrackNumber}.{(entry.TrackDataType == DataType.AUDIO ? "cdda" : "bin")}";

            protected override Stream Open(Track entry, FileStreamOptions parentOptions)
            {
                if (entry.TrackDataType.ToString().StartsWith("MODE", StringComparison.Ordinal))
                {
                    return this.Parent.File.OpenRead(GetTrackFileName(this.CueSheetPath, this.Parent, entry));
                }

                if (entry.TrackDataType == DataType.AUDIO)
                {
                    static int MsfToLba(CueSharp.Index index)
                    {
                        return ((index.Minutes * 60) + index.Seconds) * 75 + index.Frames;
                    }

                    static int GetLba(Track entry)
                    {
                        var startIndex = entry.Indices.Single(x => x.Number == 1);
                        var startLba = MsfToLba(startIndex);
                        return startLba;
                    }

                    var trackIndex = entry.TrackNumber - 1;
                    var fileEntry = entry;
                    if (fileEntry.DataFile.Filename == null)
                    {
                        for (var i = trackIndex - 1; i >= 0; i--)
                        {
                            fileEntry = this.CueSheet.Tracks[i];
                            if (fileEntry.DataFile.Filename != null)
                            {
                                break;
                            }
                        }
                    }

                    var binStream = this.Parent.File.OpenRead(GetTrackFileName(this.CueSheetPath, this.Parent, fileEntry));
                    var startLba = GetLba(entry);
                    var endLba = trackIndex + 1 < this.CueSheet.Tracks.Length
                        ? GetLba(this.CueSheet.Tracks[trackIndex + 1])
                        : binStream.Length / 2352;
                    return new OffsetStreamSpan(
                        binStream,
                        startLba * 2352L,
                        (endLba - startLba) * 2352L,
                        Ownership.Dispose);
                }

                throw new FileNotFoundException();
            }
        }

        private class DirectoryProvider(CDReaderVFSAdapter parent) : DirectoryBase(parent)
        {
            public override void Delete(string path) => parent.cdReader.DeleteDirectory(path);

            public override void Delete(string path, bool recursive) => parent.cdReader.DeleteDirectory(path, recursive);

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

            public override bool Exists([NotNullWhen(true)] string? path) => parent.cdReader.Exists(path);

            public override DateTime GetCreationTime(string path) => parent.cdReader.GetCreationTime(path);

            public override DateTime GetCreationTimeUtc(string path) => parent.cdReader.GetCreationTimeUtc(path);

            public override string[] GetDirectories(string path) => parent.cdReader.GetDirectories(path);

            public override string[] GetDirectories(string path, string searchPattern) => parent.cdReader.GetDirectories(path, searchPattern);

            public override string[] GetDirectories(string path, string searchPattern, SearchOption searchOption) => parent.cdReader.GetDirectories(path, searchPattern, searchOption);

            public override string[] GetFiles(string path) => FilterNames(parent.cdReader.GetFiles(path));

            public override string[] GetFiles(string path, string searchPattern) => FilterNames(parent.cdReader.GetFiles(path, searchPattern));

            public override string[] GetFiles(string path, string searchPattern, SearchOption searchOption) => FilterNames(parent.cdReader.GetFiles(path, searchPattern, searchOption));

            public override string[] GetFileSystemEntries(string path) => FilterNames(parent.cdReader.GetFileSystemEntries(path));

            public override string[] GetFileSystemEntries(string path, string searchPattern) => FilterNames(parent.cdReader.GetFileSystemEntries(path, searchPattern));

            public override string[] GetFileSystemEntries(string path, string searchPattern, SearchOption searchOption) => searchOption == SearchOption.TopDirectoryOnly ? FilterNames(parent.cdReader.GetFileSystemEntries(path, searchPattern)) : throw new NotSupportedException();

            public override DateTime GetLastAccessTime(string path) => parent.cdReader.GetLastAccessTime(path);

            public override DateTime GetLastAccessTimeUtc(string path) => parent.cdReader.GetLastAccessTimeUtc(path);

            public override DateTime GetLastWriteTime(string path) => parent.cdReader.GetLastWriteTime(path);

            public override DateTime GetLastWriteTimeUtc(string path) => parent.cdReader.GetLastWriteTimeUtc(path);

            public override void SetCreationTime(string path, DateTime creationTime) => parent.cdReader.SetCreationTime(path, creationTime);

            public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => parent.cdReader.SetCreationTimeUtc(path, creationTimeUtc);

            public override void SetLastAccessTime(string path, DateTime lastAccessTime) => parent.cdReader.SetLastAccessTime(path, lastAccessTime);

            public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => parent.cdReader.SetLastAccessTimeUtc(path, lastAccessTimeUtc);

            public override void SetLastWriteTime(string path, DateTime lastWriteTime) => parent.cdReader.SetLastWriteTime(path, lastWriteTime);

            public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => parent.cdReader.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        private class FileProvider(CDReaderVFSAdapter parent) : FileBase(parent)
        {
            private readonly FileLockManager<string> locks = new();

            public override bool Exists([NotNullWhen(true)] string? path) => parent.cdReader.Exists(path);

            public override FileAttributes GetAttributes(string path) => parent.cdReader.GetAttributes(path);

            public override DateTime GetCreationTime(string path) => parent.cdReader.GetCreationTime(path);

            public override DateTime GetCreationTimeUtc(string path) => parent.cdReader.GetCreationTimeUtc(path);

            public override DateTime GetLastAccessTime(string path) => parent.cdReader.GetLastAccessTime(path);

            public override DateTime GetLastAccessTimeUtc(string path) => parent.cdReader.GetLastAccessTimeUtc(path);

            public override DateTime GetLastWriteTime(string path) => parent.cdReader.GetLastWriteTime(path);

            public override DateTime GetLastWriteTimeUtc(string path) => parent.cdReader.GetLastWriteTimeUtc(path);

            public override FileSystemStream Open(string path, FileStreamOptions options)
            {
                return new StreamWrapper(
                    parent.cdReader.OpenFile(path, options.Mode, options.Access),
                    path,
                    this.locks.Acquire(path, options.Access, options.Share),
                    (options.Options & FileOptions.Asynchronous) == FileOptions.Asynchronous);
            }

            public override void SetAttributes(string path, FileAttributes fileAttributes) => parent.cdReader.SetAttributes(path, fileAttributes);

            public override void SetCreationTime(string path, DateTime creationTime) => parent.cdReader.SetCreationTime(path, creationTime);

            public override void SetCreationTimeUtc(string path, DateTime creationTimeUtc) => parent.cdReader.SetCreationTimeUtc(path, creationTimeUtc);

            public override void SetLastAccessTime(string path, DateTime lastAccessTime) => parent.cdReader.SetLastAccessTime(path, lastAccessTime);

            public override void SetLastAccessTimeUtc(string path, DateTime lastAccessTimeUtc) => parent.cdReader.SetLastAccessTimeUtc(path, lastAccessTimeUtc);

            public override void SetLastWriteTime(string path, DateTime lastWriteTime) => parent.cdReader.SetLastWriteTime(path, lastWriteTime);

            public override void SetLastWriteTimeUtc(string path, DateTime lastWriteTimeUtc) => parent.cdReader.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        }

        private class PathProvider(CDReaderVFSAdapter parent) : PathBase(parent)
        {
            public override char DirectorySeparatorChar => '\\';
        }
    }
}
