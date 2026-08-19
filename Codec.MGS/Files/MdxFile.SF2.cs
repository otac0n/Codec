namespace Codec.MGS.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using static Codec.Files.SoundFont;
    using Patch = (byte Bank, byte Program);
    using SourceLocation = (string File, int Index);
    using WaveInfo = (WvxFile.WaveTableEntry Info, (string File, int Index) Source, byte WaveTableIndex);
    using MelodicPlacement = ((string File, int Index) Source, byte WaveTableIndex, (byte Bank, byte Program) Patch);
    using PercussivePlacement = ((string File, int Index) Source, byte WaveTableIndex, byte Key);

    internal partial class MdxFile
    {
        private static List<string> FindWvxFiles(string path, IFileSystem parent)
        {
            var directoryName = parent.Path.GetDirectoryName(path) ?? string.Empty;
            var searchPaths = new[]
            {
                PathExtensions.RemoveRelativeSegments(parent.Path.Combine(directoryName, "../../init/sound/wv00007f.wvx")),
                parent.Path.Combine(directoryName, "*.wvx"),
            };

            var matches = new List<string>();

            foreach (var searchPath in searchPaths)
            {
                matches.AddRange(parent.Directory.EnumerateFiles(
                    parent.Path.GetDirectoryName(searchPath) ?? string.Empty,
                    parent.Path.GetFileName(searchPath)));
            }

            if (matches.Count == 0)
            {
                throw new InvalidDataException();
            }

            return matches;
        }

        private static (HashSet<byte> Melodic, HashSet<byte> Percussive) GetDistinctInstruments(Packet[][][] songs)
        {
            var melodic = new HashSet<byte>();
            var percussive = new HashSet<byte>();
            for (var s = 0; s < songs.Length; s++)
            {
                var tracks = songs[s];
                for (var t = 0; t < 24; t++)
                {
                    var track = tracks[t];

                    var end = false;
                    for (var i = 0; !end && i < track.Length; i++)
                    {
                        var (cmd, p1, _, _) = track[i];
                        switch (cmd)
                        {
                            case MdxCommand.SoundBank1:
                            case MdxCommand.SoundBank2:
                            case MdxCommand.SoundBank3:
                                melodic.Add(p1);
                                break;

                            case < (MdxCommand)NoteCutOff and >= (MdxCommand)PercussionCutOff:
                                percussive.Add((byte)(PercussionSampleBase + cmd - PercussionCutOff));
                                break;

                            case MdxCommand.End:
                                end = true;
                                break;

                            default:
                                break;
                        }
                    }
                }
            }

            return (melodic, percussive);
        }

        private static (List<MelodicPlacement> Melodic, List<PercussivePlacement> Percussive) GetInstrumentIndexes(Packet[][][] songs, IList<WaveInfo> sourceInfo, IFileSystem parent)
        {
            var (melodic, percussive) = GetDistinctInstruments(songs);
            var (melodicOut, percussiveOut) = (new List<MelodicPlacement>(), new List<PercussivePlacement>());
            var melodicCount = new Dictionary<byte, int>();
            for (var i = 0; i < sourceInfo.Count; i++)
            {
                var info = sourceInfo[i];
                var ix = info.WaveTableIndex;
                var isMelodic = melodic.Contains(ix);
                var isPercussive = percussive.Contains(ix);

                if (isMelodic)
                {
                    var program = ConvertSoundPatch(parent.Path.GetFileNameWithoutExtension(info.Source.File), ix);
                    if (program < 0)
                    {
                        var (newBank, newProgram) = Math.DivRem(~program, 128);
                        melodicOut.Add((info.Source, ix, ((byte)(127 - newBank), (byte)newProgram))); // Bank 128 is reserved for percussion.
                    }
                    else
                    {
                        var newProgram = (byte)program;
                        melodicCount.TryGetValue(newProgram, out var count);
                        melodicOut.Add((info.Source, ix, ((byte)count, newProgram)));
                        melodicCount[newProgram] = count + 1;
                    }
                }

                if (isPercussive)
                {
                    var key = ConvertPercussionNote(ix);
                    if (key < 0)
                    {
                        key = (byte)(~key - PercussionSampleBase);
                    }

                    percussiveOut.Add((info.Source, ix, (byte)key));
                }
            }

            return (melodicOut, percussiveOut);
        }

        private static (IList<MelodicPlacement> Melodic, IList<PercussivePlacement> Percussive) GetInstrumentIndexes(Packet[][][] songs, string path, IFileSystem parent)
        {
            var wvxFiles = FindWvxFiles(path, parent);

            var sources = new List<WaveInfo>();
            foreach (var wvx in wvxFiles)
            {
                using var input = parent.File.OpenRead(wvx);
                var indexRange = WvxFile.PopulateWaveTable(input, out var wvxSources, out _);
                for (var i = 0; i < wvxSources.Length; i++)
                {
                    sources.Add((wvxSources[i], (wvx, i), checked((byte)(i + indexRange.Start.Value))));
                }
            }

            return GetInstrumentIndexes(songs, sources, parent);
        }

        private static MemoryStream BuildSoundFont(string path, IFileSystem parent)
        {
            var wvxFiles = FindWvxFiles(path, parent);

            var sources = new List<WaveInfo>();
            var streamAndLoopPoint = new List<(short[] Stream, Range? Loop)>();
            foreach (var wvx in wvxFiles)
            {
                using var input = parent.File.OpenRead(wvx);
                var indexRange = WvxFile.PopulateWaveTable(input, out var wvxSources, out var wvxStreams, out var wvxLoopPoints);
                for (var i = 0; i < wvxSources.Length; i++)
                {
                    sources.Add((wvxSources[i], (wvx, i), checked((byte)(i + indexRange.Start.Value))));
                    streamAndLoopPoint.Add((wvxStreams[i], wvxLoopPoints[i]));
                }
            }

            IList<MelodicPlacement> instruments;
            IList<PercussivePlacement> percussionInstruments;
            using (var mdx = parent.File.OpenRead(path))
            {
                var songs = ReadSongs(mdx);
                (instruments, percussionInstruments) = GetInstrumentIndexes(songs, sources, parent);
            }

            var percussionZones = new List<InstrumentZone>();
            var document = new SoundFontDocument
            {
                Name = parent.Path.GetFileNameWithoutExtension(path),
            };

            for (var ix = 0; ix < sources.Count; ix++)
            {
                var info = sources[ix];
                var melodicPatches = instruments.Where(i => i.Source == info.Source).ToList();
                var percussivePatches = percussionInstruments.Where(i => i.Source == info.Source).ToList();
                if (melodicPatches.Count == 0 && percussivePatches.Count == 0)
                {
                    continue;
                }

                var (stream, loopPoint) = streamAndLoopPoint[ix];
                var wav = info.Info;
                var sampleSource = new SampleSource
                {
                    Name = $"{Path.GetFileNameWithoutExtension(info.Source.File)}_{info.Source.Index} @ {info.WaveTableIndex}",
                    Pcm = stream,
                    SampleRate = (int)WvxFile.SampleRate,
                    OriginalKey = (byte)(47 - wav.SampleNote),
                    PitchCorrectionCents = (sbyte)Math.Round(wav.SampleTune / 10f),
                    Loops = loopPoint != null,
                    LoopStartSample = loopPoint != null ? loopPoint.Value.Start.Value : 0,
                    LoopEndSample = loopPoint != null ? loopPoint.Value.End.Value : 0,
                };

                document.Samples.Add(sampleSource);
                var sampleIndex = document.Samples.Count - 1;
                var volumeEnvelope = new VolumeEnvelope
                {
                    AttackTime = ConvertAdsrTime(~wav.AttackRate & 0x7F, hasStepField: true),
                    DecayTime = ConvertAdsrTime(~wav.DecayRate & 0xF, hasStepField: false, totalLevelChange: 0x8000 - ((wav.SustainLevel & 0xF) + 1) * 0x800),
                    // NOTE: the writer scales this by *10 to get SF2 centibels (0-1000), so this
                    // property itself needs to land in 0-100, not 0-1000.
                    SustainAttenuation = (15 - (wav.SustainLevel & 0xF)) * 100f / 15f,
                    ReleaseTime = ConvertAdsrTime(~wav.ReleaseRate & 0x1F, hasStepField: false),
                };

                var attenuation = ConvertDecVolAttenuation(wav.Volume);

                foreach (var (_, _, key) in percussivePatches)
                {
                    percussionZones.Add(new InstrumentZone
                    {
                        SampleIndex = sampleIndex,
                        KeyLow = key,
                        KeyHigh = key,
                        RootKeyOverride = (byte)(key - wav.SampleNote),
                        InitialAttenuationCentibels = attenuation,
                        VolumeEnvelope = volumeEnvelope,
                    });
                }

                foreach (var (source, _, melodicPatch) in melodicPatches)
                {
                    var (bank, program) = melodicPatch;
                    var instrument = new Instrument
                    {
                        Name = $"WAVE {info.WaveTableIndex}",
                        Zones = [new InstrumentZone { SampleIndex = sampleIndex, InitialAttenuationCentibels = attenuation, VolumeEnvelope = volumeEnvelope }],
                    };
                    document.Instruments.Add(instrument);

                    var preset = new Preset
                    {
                        Name = $"MIDI {program}",
                        MidiProgram = program,
                        MidiBank = bank,
                    };
                    preset.Zones.Add(new PresetZone { InstrumentIndex = document.Instruments.Count - 1 });
                    document.Presets.Add(preset);
                }
            }

            if (percussionZones.Count > 0)
            {
                document.Instruments.Add(new Instrument { Name = "Percussion", Zones = percussionZones });

                var percussionPreset = new Preset { Name = "Percussion", MidiProgram = 0, MidiBank = 128 };
                percussionPreset.Zones.Add(new PresetZone { InstrumentIndex = document.Instruments.Count - 1 });
                document.Presets.Add(percussionPreset);
            }

            var output = new MemoryStream();
            Sf2Writer.Write(document, output);
            output.Position = 0;
            return output;
        }

        /// <summary>
        /// Converts a PSX SPU ADSR rate register value into a real duration, per the timing formula at
        /// https://psx-spx.consoledev.net/soundprocessingunitspu/#envelope-operation-depending-on-shiftstepmodedirection.
        /// The register packs a 5-bit Shift (0..1Fh = fast..slow) and, for Attack/Sustain only, a 2-bit
        /// Step (0..3, magnitude 7..4); Decay/Release have no Step field — the hardware step is fixed
        /// at magnitude 8 for those, so pass <paramref name="hasStepField"/> = false and just the Shift.
        /// </summary>
        /// <summary>
        /// Converts the driver's per-sample dec_vol correction (a linear subtraction, in the same raw
        /// 0-127-ish units as the Volume command, applied as `vol -= dec_vol` before playback) into SF2
        /// centibel attenuation. This assumes dec_vol shares a linear-amplitude scale with Volume — an
        /// assumption, not a confirmed curve, so worth tuning by ear if a sample still sounds off.
        /// </summary>
        private static float ConvertDecVolAttenuation(byte decVol)
        {
            if (decVol <= 0)
            {
                return 0;
            }

            var ratio = Math.Clamp((127 - decVol) / 127.0, 0.0001, 1.0);
            return (float)Math.Clamp(-2000.0 * Math.Log10(ratio), 0, 1440);
        }

        private static TimeSpan ConvertAdsrTime(int rate, bool hasStepField, int totalLevelChange = 0x8000)
        {
            var shift = hasStepField ? rate >> 2 : rate;
            var stepMagnitude = hasStepField ? 7 - (rate & 0x3) : 8;

            if (totalLevelChange <= 0)
            {
                return TimeSpan.Zero;
            }

            if (stepMagnitude <= 0)
            {
                // All-ones rate register: hardware never steps (an intentional "hold forever" case).
                return TimeSpan.MaxValue;
            }

            // samples_per_step and steps_needed both fold into the same closed form regardless of
            // whether Shift is in the "sub-sample" range (<=11, steps every sample) or the "slow"
            // range (>11, multiple samples between steps) — see the linked formula's CounterIncrement
            // and AdsrStep derivations.
            var totalSamples = (double)totalLevelChange / stepMagnitude * Math.Pow(2, shift - 11);
            return TimeSpan.FromSeconds(totalSamples / 44100.0);
        }
    }
}
