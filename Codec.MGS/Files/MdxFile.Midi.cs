namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text;
    using Codec.Text;
    using Melanchall.DryWetMidi.Common;
    using Melanchall.DryWetMidi.Composing;
    using Melanchall.DryWetMidi.Core;
    using Melanchall.DryWetMidi.Interaction;
    using Melanchall.DryWetMidi.MusicTheory;
    using MidiFile = Melanchall.DryWetMidi.Core.MidiFile;
    using Note = Melanchall.DryWetMidi.MusicTheory.Note;

    internal partial class MdxFile
    {
        private const short TicksPerQuarterNote = 96;
        private const byte VolumeController = 7;
        private const byte PanController = 10;
        private const int MaxRampSteps = 16;

        private const string TempoPrefix = "tempo";
        private const string TempoMovePrefix = "tempoMove";
        private const string VolumeMovePrefix = "volumeMove";
        private const string PanMovePrefix = "panMove";
        private const string TiePrefix = "tie";
        private const string PercussionPrefix = "percussion";

        private static string MakeMarker(string marker, params int[] args) => marker + string.Concat(args.Select(a => $":{a}"));

        public static MemoryStream ConvertToMIDIStream(string path, IFileSystem parent, uint index)
        {
            Packet[][][] songs;
            using (var stream = parent.File.OpenRead(path))
            {
                songs = ReadSongs(stream);
            }

            if (index >= songs.Length)
            {
                throw new FileNotFoundException(nameof(index), $"The index {index} is out of range for the .mdx file, which contains {songs.Length} chunks.");
            }

            var tracks = songs[index];
            var (melodicPre, percussionPre) = GetInstrumentIndexes(songs, path, parent);
            var melodic = melodicPre.ToDictionary(x => x.WaveTableIndex, x => x.Patch);
            var percussion = percussionPre.ToDictionary(x => x.WaveTableIndex, x => x.Key);

            var midi = new MidiFile
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision(TicksPerQuarterNote),
            };

            var nextChannelIndex = 0;
            var tempoChanges = new SortedDictionary<long, Tempo>();
            var tempoMap = TempoMap.Create(Tempo.FromBeatsPerMinute(30));
            midi.ReplaceTempoMap(tempoMap);

            var indent = new Indent("  ");
            var sb = new StringBuilder();

            for (var t = 0; t < tracks.Length; t++)
            {
                sb.AppendLine($"{indent}Track {t}");
                indent++;

                var track = tracks[t];
                var pattern = new PatternBuilder();
                var target = pattern;

                var transpose = 0;
                var detuneSet = false;
                var (lp1Pattern, lp1Target) = (default(PatternBuilder), default(PatternBuilder));
                var (lp2Pattern, lp2Target) = (default(PatternBuilder), default(PatternBuilder));
                var (recallPattern, recallTarget) = (default(PatternBuilder), default(PatternBuilder));
                var hasMelodicNotes = false;

                var end = false;
                for (var i = 0; !end && i < track.Length; i++)
                {
                    var (cmd, p1, p2, p3) = track[i];
                    switch (cmd)
                    {
                        case MdxCommand.Tempo:
                            sb.AppendLine($"{indent}{cmd} tempo=0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                            target.Marker(MakeMarker(TempoPrefix, p1));
                            break;

                        case MdxCommand.TempoMove:
                            sb.AppendLine($"{indent}{cmd} time=0x{p1:x2} tempo=0x{p2:x2} 0x{p3:x2}");

                            target.Marker(MakeMarker(TempoMovePrefix, p1, p2));
                            break;

                        case MdxCommand.SoundBank1:
                        case MdxCommand.SoundBank2:
                        case MdxCommand.SoundBank3:
                            {
                                var (bank, program) = melodic[p1];

                                sb.AppendLine($"{indent}{cmd} patch=0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                                target.ControlChange((SevenBitNumber)0, (SevenBitNumber)bank);
                                target.ProgramChange((SevenBitNumber)program);
                                break;
                            }

                        case MdxCommand.Volume:
                            sb.AppendLine($"{indent}{cmd} vol=0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                            target.ControlChange((SevenBitNumber)VolumeController, (SevenBitNumber)ConvertVolume(p1));
                            break;

                        case MdxCommand.VolumeMove:
                            sb.AppendLine($"{indent}{cmd} time=0x{p1:x2} vol=0x{p2:x2} 0x{p3:x2}");

                            target.Marker(MakeMarker(VolumeMovePrefix, p1, ConvertVolume(p2)));
                            break;

                        case MdxCommand.Pan:
                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} pan=0x{p2:x2} 0x{p3:x2}");

                            target.ControlChange((SevenBitNumber)PanController, (SevenBitNumber)ConvertPan(p2));
                            break;

                        case MdxCommand.PanMove:
                            sb.AppendLine($"{indent}{cmd} time=0x{p1:x2} pan=0x{p2:x2} 0x{p3:x2}");

                            target.Marker(MakeMarker(PanMovePrefix, p1, ConvertPan(p2)));
                            break;

                        case MdxCommand.Transpose:
                            sb.AppendLine($"{indent}{cmd} semi=0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                            transpose = (sbyte)p1;
                            break;

                        case MdxCommand.Detune:
                            sb.AppendLine($"{indent}{cmd} tune=0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                            if (!detuneSet)
                            {
                                pattern.ControlChange((SevenBitNumber)101, (SevenBitNumber)0);
                                pattern.ControlChange((SevenBitNumber)100, (SevenBitNumber)0);
                                pattern.ControlChange((SevenBitNumber)6, (SevenBitNumber)2);
                                pattern.ControlChange((SevenBitNumber)38, (SevenBitNumber)0);
                                pattern.ControlChange((SevenBitNumber)101, (SevenBitNumber)127);
                                pattern.ControlChange((SevenBitNumber)100, (SevenBitNumber)127);
                                detuneSet = true;
                            }

                            target.PitchBend(ConvertDetune(p1));
                            break;

                        case MdxCommand.Portamento:
                            {
                                var time = (int)p1;

                                sb.AppendLine($"{indent}{cmd} time=0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                                target.ControlChange((SevenBitNumber)65, (SevenBitNumber)(time == 0 ? 0 : 127));
                                target.ControlChange((SevenBitNumber)5, (SevenBitNumber)Math.Clamp(time, 0, 127));
                                break;
                            }

                        case MdxCommand.Loop1Start:
                            lp1Target = target;
                            target = lp1Pattern = new PatternBuilder();

                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");
                            indent++;

                            break;

                        case MdxCommand.Loop1End:
                            if (lp1Pattern != null)
                            {
                                target = lp1Target!;
                                var loopCount = (int)p1;
                                var volume = (sbyte)p2;
                                var freq = (sbyte)p3 * 8;

                                indent--;
                                sb.AppendLine($"{indent}{cmd} n=0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                                target.Pattern(lp1Pattern.Build()).Repeat(1, loopCount - 1);
                                lp1Pattern = null;
                            }

                            break;

                        case MdxCommand.Loop2Start:
                            lp2Target = target;
                            target = lp2Pattern = new PatternBuilder();

                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");
                            indent++;
                            break;

                        case MdxCommand.Loop2End:
                            if (lp2Pattern != null)
                            {
                                target = lp2Target!;
                                var loopCount = (int)p1;
                                var volume = p2;
                                var freq = p3 * 8;

                                if (loopCount == 0)
                                {
                                    loopCount = 1;
                                }

                                indent--;
                                sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                                target.Pattern(lp2Pattern.Build()).Repeat(1, loopCount - 1);
                                lp2Pattern = null;
                            }

                            break;

                        case MdxCommand.Loop3Start:
                            target.Marker("loopStart");

                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");
                            indent++;

                            break;

                        case MdxCommand.Loop3End:
                            target.Marker("loopEnd");

                            indent--;
                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                            break;

                        case MdxCommand.BracketStart:
                            recallTarget = target;
                            target = recallPattern = new PatternBuilder();

                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");
                            indent++;

                            break;

                        case MdxCommand.BracketEnd:
                            if (recallPattern != null)
                            {
                                if (recallTarget != null)
                                {
                                    target = recallTarget;
                                    recallTarget = null;
                                    indent--;
                                }

                                sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                                target.Pattern(recallPattern.Build());
                            }

                            break;

                        case MdxCommand.Rest:
                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                            target.StepForward(new MidiTimeSpan(p1));
                            break;

                        case MdxCommand.Tie:
                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                            target.Marker(MakeMarker(TiePrefix, p1));
                            target.StepForward(new MidiTimeSpan(p1));
                            break;

                        case MdxCommand.End:
                            end = true;
                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");
                            break;

                        case < (MdxCommand)PercussionCutOff:
                            {
                                var (octave, note) = Math.DivRem((int)cmd + transpose, 12);
                                var len = (int)p1;
                                var vel = (SevenBitNumber)p3;
                                var midiNote = Note.Get((NoteName)note, octave + 1);

                                sb.AppendLine($"{indent}{midiNote} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                                hasMelodicNotes = true;
                                target.Note(midiNote, new MidiTimeSpan(len), vel);

                                break;
                            }

                        case < (MdxCommand)NoteCutOff:
                            {
                                var note = (byte)(PercussionSampleBase + (int)cmd - PercussionCutOff);
                                var len = (int)p1;
                                var vel = p3;

                                sb.AppendLine($"{indent}Percussion{note} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2}");

                                // Patch 103 is missing?
                                if (!percussion.TryGetValue(note, out var key))
                                {
                                    var newKey = ConvertPercussionNote(note);
                                    if (newKey < 0)
                                    {
                                        newKey = ~newKey - PercussionSampleBase;
                                    }

                                    percussion[note] = key = (byte)newKey;
                                }

                                target.Marker(MakeMarker(PercussionPrefix, key, vel, len));
                                target.StepForward(new MidiTimeSpan(len));

                                break;
                            }

                        default:
                            sb.AppendLine($"{indent}{cmd} 0x{p1:x2} 0x{p2:x2} 0x{p3:x2} (UNHANDLED)");
                            break;
                    }
                }

                if (hasMelodicNotes && nextChannelIndex > 15)
                {
                    throw new NotImplementedException();
                }

                var chunk = pattern.Build().ToTrackChunk(tempoMap, (FourBitNumber)(!hasMelodicNotes ? 9 : nextChannelIndex));
                if (chunk.Events.Count > 0)
                {
                    if (hasMelodicNotes)
                    {
                        nextChannelIndex++;
                        if (nextChannelIndex == 9)
                        {
                            nextChannelIndex++;
                        }
                    }

                    var rampEvents = new List<TimedEvent>();
                    var tieExtensions = new List<(long Time, long DurationTicks)>();
                    var percussionHits = new List<(long Time, byte Note, byte Velocity, int Length)>();
                    var currentVolume = (SevenBitNumber)127;
                    var currentPan = (SevenBitNumber)64;
                    byte currentTempoRaw = 126; // 30 * 4.2

                    var orderedEvents = chunk.GetTimedEvents().OrderBy(e => e.Time).ToList();
                    for (var i = 0; i < orderedEvents.Count; i++)
                    {
                        var timedEvent = orderedEvents[i];

                        if (timedEvent.Event is ControlChangeEvent cc)
                        {
                            byte controlNumber = cc.ControlNumber;
                            if (controlNumber == VolumeController)
                            {
                                currentVolume = cc.ControlValue;
                            }
                            else if (controlNumber == PanController)
                            {
                                currentPan = cc.ControlValue;
                            }

                            continue;
                        }

                        if (timedEvent.Event is not MarkerEvent marker || !marker.Text.Contains(':'))
                        {
                            continue;
                        }

                        var parts = marker.Text.Split(':');
                        var args = parts.Skip(1).Select(int.Parse).ToArray();
                        switch (parts[0])
                        {
                            case TempoPrefix:
                                tempoChanges[timedEvent.Time] = Tempo.FromBeatsPerMinute(ConvertTempo(currentTempoRaw = (byte)args[0]));
                                break;

                            case TempoMovePrefix:
                                {
                                    var nextIndex = i + 1;

                                    BuildTempoChangeRamp(tempoChanges, timedEvent.Time, args[0], ref currentTempoRaw, (byte)args[1], orderedEvents, nextIndex, TempoPrefix, TempoMovePrefix);
                                    break;
                                }

                            case VolumeMovePrefix:
                                {
                                    rampEvents.AddRange(BuildControlChangeRamp(timedEvent.Time, args[0], ref currentVolume, (SevenBitNumber)(byte)args[1], orderedEvents, i + 1, (SevenBitNumber)VolumeController, VolumeMovePrefix));
                                    break;
                                }

                            case PanMovePrefix:
                                {
                                    rampEvents.AddRange(BuildControlChangeRamp(timedEvent.Time, args[0], ref currentPan, (SevenBitNumber)(byte)args[1], orderedEvents, i + 1, (SevenBitNumber)PanController, PanMovePrefix));
                                    break;
                                }

                            case TiePrefix:
                                {
                                    var durationTicks = args[0];
                                    tieExtensions.Add((timedEvent.Time, durationTicks));
                                    break;
                                }

                            case PercussionPrefix:
                                {
                                    percussionHits.Add((timedEvent.Time, (byte)args[0], (byte)args[1], args[2]));
                                    break;
                                }
                        }
                    }

                    chunk.RemoveTimedEvents(e => e.Event is MarkerEvent marker && marker.Text.Contains(':'));

                    if (rampEvents.Count > 0)
                    {
                        using var timedEventsManager = chunk.ManageTimedEvents();
                        foreach (var rampEvent in rampEvents)
                        {
                            timedEventsManager.Objects.Add(rampEvent);
                        }
                    }

                    if (tieExtensions.Count > 0)
                    {
                        using var notesManager = chunk.ManageNotes();
                        foreach (var (time, durationTicks) in tieExtensions)
                        {
                            var note = notesManager.Objects
                                .Where(n => n.Time + n.Length == time)
                                .OrderByDescending(n => n.Time)
                                .FirstOrDefault();

                            note?.Length += durationTicks;
                        }
                    }

                    if (percussionHits.Count > 0)
                    {
                        using var timedEventsManager = chunk.ManageTimedEvents();

                        var percussionChannel = (FourBitNumber)9;
                        timedEventsManager.Objects.Add(new TimedEvent(new ControlChangeEvent((SevenBitNumber)0, (SevenBitNumber)1) { Channel = percussionChannel }, 0));
                        timedEventsManager.Objects.Add(new TimedEvent(new ProgramChangeEvent((SevenBitNumber)0) { Channel = percussionChannel }, 0));

                        foreach (var hit in percussionHits)
                        {
                            timedEventsManager.Objects.Add(new TimedEvent(new NoteOnEvent((SevenBitNumber)hit.Note, (SevenBitNumber)hit.Velocity) { Channel = percussionChannel }, hit.Time));
                            timedEventsManager.Objects.Add(new TimedEvent(new NoteOffEvent((SevenBitNumber)hit.Note, (SevenBitNumber)0) { Channel = percussionChannel }, hit.Time + hit.Length));
                        }
                    }

                    midi.Chunks.Add(chunk);
                }

                indent--;
                sb.AppendLine($"{indent}End Track {t}");
            }

            Debug.Write(sb.ToString());

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

        private static long? FindRampCutoffTime(List<TimedEvent> orderedEvents, int startIndex, SevenBitNumber? controlNumber, params string[] prefixes)
        {
            for (var i = startIndex; i < orderedEvents.Count; i++)
            {
                var @event = orderedEvents[i].Event;

                if (controlNumber is SevenBitNumber n && @event is ControlChangeEvent cc && (byte)cc.ControlNumber == (byte)n)
                {
                    return orderedEvents[i].Time;
                }

                if (@event is MarkerEvent marker && prefixes.Any(p => marker.Text.StartsWith(p + ":", StringComparison.Ordinal)))
                {
                    return orderedEvents[i].Time;
                }
            }

            return null;
        }

        private static void BuildTempoChangeRamp(SortedDictionary<long, Tempo> tempoChanges, long startTime, int durationTicks, ref byte currentValue, byte targetTempoRaw, List<TimedEvent> orderedEvents, int nextIndex, params string[] prefixes)
        {
            var cutoffTime = FindRampCutoffTime(orderedEvents, nextIndex, null, prefixes);
            if (durationTicks <= 0)
            {
                if (cutoffTime is null || startTime < cutoffTime)
                {
                    currentValue = targetTempoRaw;
                    return;
                }

                return;
            }

            var steps = Math.Clamp(durationTicks, 1, MaxRampSteps);

            var start = currentValue;
            var target = targetTempoRaw;

            for (var i = 1; i <= steps; i++)
            {
                var time = startTime + (durationTicks * i / steps);
                if (cutoffTime is not null && time >= cutoffTime)
                {
                    break;
                }

                var value = start + ((target - start) * i / steps);
                tempoChanges[time] = Tempo.FromBeatsPerMinute(ConvertTempo(currentValue = (byte)Math.Clamp(value, 0, 255)));
            }

            return;
        }

        private static IEnumerable<TimedEvent> BuildControlChangeRamp(long startTime, long durationTicks, ref SevenBitNumber currentValue, SevenBitNumber targetValue, List<TimedEvent> orderedEvents, int startIndex, SevenBitNumber controlNumber, params string[] prefixes)
        {
            var cutoffTime = FindRampCutoffTime(orderedEvents, startIndex, controlNumber, prefixes);
            if (durationTicks <= 0)
            {
                if (cutoffTime is null || startTime < cutoffTime)
                {
                    currentValue = targetValue;
                    return [new TimedEvent(new ControlChangeEvent(controlNumber, targetValue), startTime)];
                }

                return [];
            }

            var steps = (int)Math.Clamp(durationTicks, 1, MaxRampSteps);

            int start = currentValue;
            int target = targetValue;

            var events = new List<TimedEvent>(steps);
            for (var i = 1; i <= steps; i++)
            {
                var time = startTime + (durationTicks * i / steps);
                if (cutoffTime is not null && time >= cutoffTime)
                {
                    break;
                }

                var value = start + ((target - start) * i / steps);
                events.Add(new TimedEvent(new ControlChangeEvent(controlNumber, currentValue = (SevenBitNumber)(byte)Math.Clamp(value, 0, 127)), time));
            }

            return events;
        }

        private static float ConvertTempo(byte p1) => p1 / 4.2f;

        private static ushort ConvertDetune(byte p1) => (ushort)(((sbyte)p1 << 2) / 256.0);

        private static byte ConvertVolume(byte volume) => (SevenBitNumber)(byte)(volume / 2);

        private static byte ConvertPan(byte pan) => (SevenBitNumber)(byte)Math.Clamp((sbyte)pan * 127f / 40 + 64, 0, 127);

        private static int ConvertPercussionNote(byte patch)
        {
            switch (patch)
            {
                case 79: return 35; // Low Bass Drum
                case 80: return 36; // High Bass Drum
                case 85: return 41; // Low Floor Tom
                case 87: return 42; // Closed Hi-Hat
                case 84: return 43; // High Floor Tom
                case 86: return 44; // Pedal Hi-Hat
                case 83: return 45; // Low Tom
                case 89: return 46; // Open Hi-Hat
                case 82: return 47; // Low-Mid Tom
                case 81: return 48; // High-Mid Tom
                case 88: return 49; // Crash Cymbal 1
                case 101: return 61; // Low Bongo
                case 90: return 75; // Claves
                case 91: return 76; // High Woodblock
                case 92: return 77; // Low Woodblock

                case 102: return 126; // (door slam)
                case 103: return 127; // (missing?)

                default:
                    Debug.WriteLine($"No Drum #{patch}");
                    return ~patch;
            }
        }

        private static int ConvertSoundPatch(string container, byte patch)
        {
            switch (container)
            {
                case "wv00007f":
                    switch (patch)
                    {
                        case 6: return 0; // Default
                        case 5: return 17; // Percussive Organ
                        case 14: return 18; // Rock Organ
                        case 27: return 30; // Distortion Guitar
                        case 24: return 32; // Acoustic Bass
                        case 53: return 38; // Synth Bass 1
                        case 29: return 39; // Synth Bass 2
                        case 55: return 40; // Violin
                        case 54: return 41; // Viola
                        case 23: return 42; // Cello
                        case 31: return 47; // Timpani
                        case 38: return 55; // Orchestra Hit
                        case 45: return 56; // Trumpet
                        case 46: return 57; // Trombone
                        case 47: return 58; // Tuba
                        case 19: return 59; // Muted Trumpet
                        case 0: return 60; // French Horn
                        case 52: return 61; // Brass Section
                        case 26: return 62; // Synth Brass 1
                        case 44: return 63; // Synth Brass 2
                        case 9: return 73; // Flute
                        case 17: return 80; // Lead 1 (Square)
                        case 2: return 81; // Lead 2 (Sawtooth)
                        case 21: return 81; // Lead 2 (Sawtooth)
                        case 10: return 81; // Lead 2 (Sawtooth)
                        case 25: return 85; // Lead 6 (Voice)
                        case 28: return 87; // Lead 8 (Bass Lead)
                        case 50: return 93; // Pad 6 (Metallic)
                        case 30: return 113; // Agogo
                        case 33: return 116; // Taiko Drum
                        case 35: return 116; // Taiko Drum
                        case 37: return 119; // Reverse Cymbal
                        case 4: return 120; // Guitar Fret Noise
                        case 3: return 122; // Seashore
                        case 75: return 122; // Seashore
                        case 76: return 122; // Seashore
                        case 39: return 127; // Gunshot
                    }

                    break;

                case "wv00000a":
                    switch (patch)
                    {
                        case 144: return 122; // Seashore
                        case 148: return 122; // Seashore
                        case 147: return 123; // Bird Tweet
                        case 150: return 127; // Gunshot
                    }

                    break;

                case "wv00000d":
                    switch (patch)
                    {
                        case 144: return 122; // Seashore
                        case 145: return 122; // Seashore
                    }

                    break;

                case "wv000010":
                    switch (patch)
                    {
                        case 152: return 91; // Pad 4 (Choir)
                        case 153: return 103; // FX 8 (Sci-fi)
                    }

                    break;

                case "wv000001":
                case "wv00000b":
                case "wv00000f":
                case "wv00001a":
                case "wv00001e":
                case "wv000014":
                case "wv000015":
                    switch (patch)
                    {
                        case 144: return 122; // Seashore
                    }

                    break;

                case "wv00001c":
                    switch (patch)
                    {
                        case 145: return 122; // Seashore *lowered pitch
                    }

                    break;

                case "wv000002":
                case "wv000003":
                    switch (patch)
                    {
                        case 147: return 122; // Seashore
                    }

                    break;

                case "wv000007":
                    switch (patch)
                    {
                        case 159: return 42; // Cello
                        case 160: return 41; // Viola
                        case 161: return 40; // Violin
                    }

                    break;

                case "wv000008":
                    switch (patch)
                    {
                        case 149: return 73; // Flute
                        case 150: return 100; // FX 5 (Brightness)
                        case 144: return 127; // Gunshot *white? noise
                        case 148: return 127; // Gunshot *pink? noise
                    }

                    break;

                case "wv000009":
                case "wv00000c":
                    switch (patch)
                    {
                        // TODO: Barks / howls -> Percussion
                        case 145: return 0;
                        case 146: return 1;
                        case 147: return 2;
                        case 148: return 3;
                        case 149: return 4;

                        case 144: return 122; // Seashore
                    }

                    break;

                case "wv00000e":
                case "wv000016":
                    switch (patch)
                    {
                        // TODO: Crows -> Percussion
                        case 147: return 0;
                        case 148: return 1;
                        case 150: return 2;
                        case 151: return 3;
                        case 152: return 4;

                        case 153: return 122; // Seashore *
                        case 155: return 122; // Seashore *

                        case 145: return 127; // Gunshot *
                        case 156: return 127; // Gunshot *creaky door open
                        case 157: return 127; // Gunshot *heavy door close
                    }

                    break;

                case "wv000018":
                    switch (patch)
                    {
                        case 152: return 122; // Seashore
                        case 156: return 124; // Telephone Ring
                    }

                    break;

                case "wv000019":
                    switch (patch)
                    {
                        case 154: return 48; // String Ensemble 1
                        case 153: return 49; // String Ensemble 2
                        case 155: return 50; // Synth Strings 1
                    }

                    break;

                case "wv00001b":
                    switch (patch)
                    {
                        // TODO: Ninja ATGC voices -> Percussion
                        case 149: return 0;
                        case 150: return 1;
                        case 151: return 2;
                        case 154: return 3;

                        case 152: return 120; // Guitar Fret Noise
                        case 155: return 122; // Seashore
                        case 156: return 125; // Helicopter
                    }

                    break;

                case "wv00001f":
                    switch (patch)
                    {
                        case 147: return 122; // Seashore
                        case 155: return 122; // Seashore
                        case 145: return 127; // Gunshot *creak & slam
                        case 146: return 127; // Gunshot *explosion
                        case 154: return 127; // Gunshot *mortar shot
                    }

                    break;

                case "wv00002d":
                    switch (patch)
                    {
                        // TODO: Ninja voices -> Percussion
                        case 192: return 5;
                        case 193: return 6;
                        case 194: return 7;
                        case 195: return 8;
                        case 196: return 9;
                        case 197: return 10;
                        case 198: return 11;
                        case 199: return 12;
                        case 200: return 13;
                        case 201: return 14;
                        case 202: return 15;
                        case 203: return 16;
                        case 204: return 17;
                        case 205: return 18;
                    }

                    break;

                case "wv00002e":
                    switch (patch)
                    {
                        // TODO: Barks / howls -> Percussion
                        case 196: return 6;
                        case 209: return 7;
                    }

                    break;

                case "wv000030":
                case "wv000031":
                    switch (patch)
                    {
                        case 128: return 48; // String Ensemble 1
                        case 192: return 49; // String Ensemble 2
                    }

                    break;

                case "wv000034":
                    switch (patch)
                    {
                        // TODO: Ninja voices -> Percussion
                        case 144: return 5;
                        case 145: return 6;
                        case 147: return 7;
                        case 148: return 8;
                        case 155: return 9;
                        case 156: return 10;
                        case 157: return 11;
                        case 158: return 12;
                        case 159: return 13;
                        case 160: return 14;
                        case 161: return 15;
                        case 162: return 16;
                        case 163: return 17;
                        case 164: return 18;
                    }

                    break;
            }

            Debug.WriteLine($"No Patch #{patch} (from {container})");
            return ~patch;
        }
    }
}
