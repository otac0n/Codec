// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using Codec.Archives;
    using Microsoft.Extensions.DependencyInjection;

    internal partial class MdxFile
    {
        private const int PercussionSampleBase = 0x4F;
        private const int PercussionCutOff = 0x48;
        private const int NoteCutOff = 0x80;

        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.mdx", static (fullPath, parentRelativePath, parent, parentPath) => new MdxFileFileSystem(parentRelativePath, parent));
        }

        private class MdxFileFileSystem(string path, IFileSystem parent) : IndexedFileSystem<int>
        {
            private readonly IFileSystem parent = parent ?? new FileSystem();
            private readonly string path = path;

            protected override IEnumerable<int> ReadIndex()
            {
                using var stream = this.parent.File.OpenRead(this.path);
                var chunkCount = stream.ReadUInt32LittleEndian();
                return [-1, .. Enumerable.Range(0, (int)chunkCount)];
            }

            protected override string GetEntryName(int entry) =>
                entry is >= 0
                    ? $"{entry}.midi"
                    : "samples.sf2";

            protected override Stream Open(int entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub songs in .mdx files is not supported.");
                if (entry is >= 0)
                {
                    return ConvertToMIDIStream(this.path, this.parent, (uint)entry);
                }
                else
                {
                    return BuildSoundFont(this.path, this.parent);
                }
            }
        }

        public static Packet[][][] ReadSongs(Stream stream)
        {
            var songCount = stream.ReadUInt32LittleEndian();
            var songPositions = stream.ReadArrayLittleEndian<uint>(songCount);

            var songs = new Packet[songCount][][];
            for (var s = 0; s < songCount; s++)
            {
                stream.Position = songPositions[s];
                var trackPositions = stream.ReadArrayLittleEndian<uint>(24);

                var song = new Packet[24][];
                for (var t = 0; t < 24; t++)
                {
                    stream.Position = trackPositions[t];

                    var packets = new List<Packet>();
                    while (true)
                    {
                        var line = stream.ReadUInt32LittleEndian();
                        var packet = new Packet(
                            (MdxCommand)((line >> 24) & 0xFF),
                            (byte)((line >> 16) & 0xFF),
                            (byte)((line >> 8) & 0xFF),
                            (byte)((line >> 0) & 0xFF));
                        packets.Add(packet);
                        if (packet.Command == MdxCommand.End)
                        {
                            break;
                        }
                    }

                    song[t] = [.. packets];
                }

                songs[s] = song;
            }

            return songs;
        }

        public record class Packet(MdxCommand Command, byte P1, byte P2, byte P3);

        public enum MdxCommand : byte
        {
            Tempo = 0xd0,
            TempoMove = 0xd1,
            SoundBank1 = 0xd2,
            SoundBank2 = 0xd3,
            SoundBank3 = 0xd4,
            Volume = 0xd5,
            VolumeMove = 0xd6,
            AttackDecaySustain = 0xd7,
            SustainRate = 0xd8,
            ReleaseRate = 0xd9,
            Pan = 0xdd,
            PanMove = 0xde,
            Transpose = 0xdf,
            Detune = 0xe0,
            Vibrato = 0xe1,
            VibratoMove = 0xe2,
            RandomPitchModulation = 0xe3,
            Sweep = 0xe4,
            SweepSettings = 0xe5,
            Portamento = 0xe6,
            Loop1Start = 0xe7,
            Loop1End = 0xe8,
            Loop2Start = 0xe9,
            Loop2End = 0xea,
            Loop3Start = 0xeb,
            Loop3End = 0xec,
            BracketStart = 0xed,
            BracketEnd = 0xee,
            Use = 0xf1,
            Rest = 0xf2,
            Tie = 0xf3,
            Echo1 = 0xf4,
            Echo2 = 0xf5,
            EffectOn = 0xf6,
            EffectOff = 0xf7,
            End = 0xff,
        }
    }
}
