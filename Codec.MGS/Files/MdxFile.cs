// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using Codec.Archives;
    using Melanchall.DryWetMidi.Common;
    using Melanchall.DryWetMidi.Composing;
    using Melanchall.DryWetMidi.Core;
    using Melanchall.DryWetMidi.Interaction;
    using Melanchall.DryWetMidi.MusicTheory;
    using Microsoft.Extensions.DependencyInjection;
    using Note = Melanchall.DryWetMidi.MusicTheory.Note;

    internal class MdxFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddFileSystem("*.mdx", static (fullPath, parentRelativePath, parent, parentPath) => new MdxFileFileSystem(parentRelativePath, parent));
        }

        private class MdxFileFileSystem(string path, IFileSystem parent) : IndexedFileSystem<uint>
        {
            private readonly IFileSystem parent = parent ?? new FileSystem();
            private readonly string path = path;

            protected override IEnumerable<uint> ReadIndex()
            {
                using var stream = this.parent.File.OpenRead(this.path);
                var chunkCount = stream.ReadUInt32LittleEndian();
                return Enumerable.Range(0, (int)chunkCount).Select(x => (uint)x);
            }

            protected override string GetEntryName(uint entry) =>
                $"{entry}.midi";

            protected override Stream Open(uint entry, FileStreamOptions parentOptions)
            {
                FileBase.EnsureReadOnly(parentOptions, "Writing to sub songs in .mdx files is not supported.");
                using var input = this.parent.File.Open(this.path, parentOptions);
                return ConvertToMIDIStream(input, entry);
            }
        }

        public static MemoryStream ConvertToMIDIStream(Stream stream, uint index)
        {
            var chunkCount = stream.ReadUInt32LittleEndian();
            if (index >= chunkCount)
            {
                throw new FileNotFoundException(nameof(index), $"The index {index} is out of range for the .mdx file, which contains {chunkCount} chunks.");
            }

            const short TicksPerQuarterNote = 96;
            const string TempoPrefix = "tempo:";

            stream.Position = index * sizeof(uint) + sizeof(uint);
            var chunkPosition = stream.ReadUInt32LittleEndian();
            stream.Position = chunkPosition;
            var tracks = stream.ReadArrayLittleEndian<uint>(24);

            var midi = new MidiFile
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote),
            };

            var tempoChanges = new SortedDictionary<long, Tempo>();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(30));
            midi.ReplaceTempoMap(tempoMap);

            for (var track = 0; track < 24; track++)
            {
                var pattern = new PatternBuilder();
                stream.Position = tracks[track];
                var end = false;
                var target = pattern;
                var (c, l) = (default(PatternBuilder), default(PatternBuilder));
                var lastVoice = -1;
                while (!end)
                {
                    var packet = stream.ReadUInt32LittleEndian();
                    var cmd = (packet >> 24) & 0xFF;
                    var p1 = (packet >> 16) & 0xFF;
                    var p2 = (packet >> 8) & 0xFF;
                    var p3 = (packet >> 0) & 0xFF;
                    var mdxCmd = (MdxCommand)cmd;
                    switch (mdxCmd)
                    {
                        case MdxCommand.End:
                            end = true;
                            break;

                        case MdxCommand.Bank:
                            target.ProgramChange((SevenBitNumber)((int)p1 & 0x7F));
                            break;

                        case MdxCommand.RepStart:
                            target = c = new PatternBuilder();
                            break;

                        case MdxCommand.RepEnd:
                            target = pattern;
                            target.Pattern(c.Build()).Repeat(1, (int)p1 - 1);
                            c = null;
                            break;

                        case MdxCommand.LoopR:
                            if (l != null)
                            {
                                target.Pattern(l.Build());
                                l = null;
                            }

                            break;

                        case MdxCommand.LoopStart:
                            target = l = new PatternBuilder();
                            break;

                        case MdxCommand.LoopEnd:
                            target = pattern;
                            if (l != null)
                            {
                                target.Pattern(l.Build());
                                l = null;
                            }

                            break;

                        case MdxCommand.Tempo:
                            target.Marker(TempoPrefix + (int)p1);
                            break;

                        case MdxCommand.Rest:
                            target.StepForward(new MidiTimeSpan((int)p1));
                            break;

                        case < (MdxCommand)97:
                            {
                                var (octave, note) = Math.DivRem((int)cmd, 12);
                                var len = (int)p1;
                                var unk = (int)p2;
                                var vel = (SevenBitNumber)p3;

                                target.Note(Note.Get((NoteName)note, octave + 1), new MidiTimeSpan(len), vel);
                                break;
                            }

                        default:
                            Debug.WriteLine(Enum.IsDefined(mdxCmd)
                                ? $"UNHANDLED {mdxCmd} ({p1}, {p2}, {p3})"
                                : $"UNKNOWN {cmd:x2} ({p1}, {p2}, {p3})");
                            break;
                    }
                }

                var chunk = pattern.Build().ToTrackChunk(tempoMap);

                foreach (var timedEvent in chunk.GetTimedEvents())
                {
                    if (timedEvent.Event is MarkerEvent marker && marker.Text.StartsWith(TempoPrefix, StringComparison.Ordinal))
                    {
                        var bpm = int.Parse(marker.Text.AsSpan(TempoPrefix.Length), CultureInfo.InvariantCulture);
                        tempoChanges[timedEvent.Time] = Tempo.FromBeatsPerMinute(bpm / 4.5);
                    }
                }

                chunk.RemoveTimedEvents(e => e.Event is MarkerEvent);
                midi.Chunks.Add(chunk);
            }

            using (var tempoMapManager = new TempoMapManager(midi.TimeDivision))
            {
                foreach (var (ticks, tempo) in tempoChanges)
                {
                    tempoMapManager.SetTempo(ticks, tempo);
                }

                midi.ReplaceTempoMap(tempoMapManager.TempoMap);
            }

            var midiStream = new MemoryStream();
            midi.Write(midiStream);
            midiStream.Position = 0;
            return midiStream;
        }

        public enum MdxCommand : byte
        {
            Tempo = 0xD0,
            Bank = 0xD2,
            MasterVolume = 0xD5,
            Volume = 0xD7,
            Pan0 = 0xDD,
            Pan1 = 0xDE,
            RepStart = 0xE7,
            RepEnd = 0xE8,
            LoopL = 0xEB,
            LoopR = 0xEC,
            LoopStart = 0xED,
            LoopEnd = 0xEE,
            Rest = 0xF2,
            End = 0xFF,
        }
    }
}
