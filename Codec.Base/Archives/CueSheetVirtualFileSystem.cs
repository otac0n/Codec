namespace Codec.Archives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text.RegularExpressions;
    using CueSharp;
    using DiscUtils.Iso9660;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    public partial class CueSheetVirtualFileSystem : IndexedFileSystem<Track>
    {
        public CueSheet CueSheet { get; }

        public string CueSheetPath { get; }

        public IFileSystem Parent { get; }

        public CueSheetVirtualFileSystem(string path, IFileSystem parent, CueSheet? cue = null)
        {
            this.Parent = parent ?? new FileSystem();
            this.CueSheetPath = path;
            this.CueSheet = cue ?? ReadCueSheet(this.CueSheetPath, this.Parent);
        }

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileSystemResolver>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                if (parent is CueSheetVirtualFileSystem cueFS &&
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
                        var cue = ReadCueSheet(parentRelativePath, parent);
                        if (cue.Tracks is [Track track] && track.TrackDataType.ToString().StartsWith("MODE", StringComparison.Ordinal))
                        {
                            return CreateCueTrackFileSystem(parentRelativePath, parent, track);
                        }
                        else
                        {
                            return new CueSheetVirtualFileSystem(parentRelativePath, parent, cue);
                        }
                    };
                }

                if (string.Equals(parent.Path.GetExtension(parentRelativePath), ".iso", StringComparison.OrdinalIgnoreCase))
                {
                    return static (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        var file = parent.File.OpenRead(parentRelativePath);
                        try
                        {
                            var cdReader = new CDReader(file, joliet: true);
                            return new DiscUtilsVFSAdapter(cdReader);
                        }
                        catch
                        {
                            file.Dispose();
                            throw;
                        }
                    };
                }

                return null;
            });
        }

        private static CueSheet ReadCueSheet(string parentRelativePath, IFileSystem parent)
        {
            using var cueStream = parent.File.OpenRead(parentRelativePath);
            using var reader = new StreamReader(cueStream);
            return new CueSheet(reader);
        }

        protected override IEnumerable<Track> ReadIndex() => this.CueSheet.Tracks;

        protected override string GetEntryName(Track entry) =>
            $"Track {entry.TrackNumber}.{(entry.TrackDataType == DataType.AUDIO ? "cdda" : "bin")}";

        [GeneratedRegex(@"Track (\d+)")]
        private static partial Regex TrackMatcher();

        private static string GetTrackFileName(string parentRelativePath, IFileSystem parent, Track track) =>
            parent.Path.Combine(
                parent.Path.GetDirectoryName(parentRelativePath)!,
                track.DataFile.Filename);

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

        private static DiscUtilsVFSAdapter CreateCueTrackFileSystem(string parentRelativePath, IFileSystem parent, Track track)
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
            return new DiscUtilsVFSAdapter(cdReader);
        }
    }
}
