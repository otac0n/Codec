namespace Codec.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    public class SoundFont
    {
        /// <summary>
        /// Decoded mono 16-bit PCM audio plus the metadata a SoundFont sample header needs.
        /// </summary>
        public sealed class SampleSource
        {
            public required string Name { get; init; }

            public required short[] Pcm { get; init; }

            public required int SampleRate { get; init; }

            /// <summary>
            /// MIDI note number the sample was recorded/tuned at. Most MGS VAG samples
            /// are tuned to C3 — MIDI note 60 in the "middle C = 60" convention.
            /// </summary>
            public byte OriginalKey { get; init; } = 60;

            /// <summary>Fine-tune offset in cents (-99..99), layered on top of <see cref="OriginalKey"/>.</summary>
            public sbyte PitchCorrectionCents { get; init; }

            public bool Loops { get; init; }

            /// <summary>Loop points, in sample frames, relative to the start of this sample's own data.</summary>
            public int LoopStartSample { get; init; }

            public int LoopEndSample { get; init; }
        }

        public sealed class VolumeEnvelope
        {
            public TimeSpan DelayTime { get; init; }

            public TimeSpan AttackTime { get; init; }

            public TimeSpan HoldTime { get; init; }

            public TimeSpan DecayTime { get; init; }

            public float SustainAttenuation { get; init; }

            public TimeSpan ReleaseTime { get; init; }
        }

        /// <summary>
        /// One key/velocity region within an <see cref="Instrument"/>, pointing at a single sample.
        /// </summary>
        public sealed class InstrumentZone
        {
            public required int SampleIndex { get; init; }

            public byte KeyLow { get; init; }

            public byte KeyHigh { get; init; } = 127;

            public byte VelLow { get; init; }

            public byte VelHigh { get; init; } = 127;

            public VolumeEnvelope? VolumeEnvelope { get; init; }

            /// <summary>
            /// Play the sample as though its root key were this note, without altering the sample
            /// header itself. Leave null to use the sample's own <see cref="SampleSource.OriginalKey"/>.
            /// </summary>
            public byte? RootKeyOverride { get; init; }
        }

        /// <summary>
        /// A playable sound: one or more sample zones. Not addressable by MIDI on its own —
        /// it needs a <see cref="Preset"/> to expose it via bank/program.
        /// </summary>
        public sealed class Instrument
        {
            public required string Name { get; init; }

            public List<InstrumentZone> Zones { get; init; } = [];
        }

        /// <summary>One key/velocity region within a <see cref="Preset"/>, pointing at a single instrument.</summary>
        public sealed class PresetZone
        {
            public required int InstrumentIndex { get; init; }

            public byte KeyLow { get; init; }

            public byte KeyHigh { get; init; } = 127;

            public byte VelLow { get; init; }

            public byte VelHigh { get; init; } = 127;
        }

        /// <summary>
        /// The thing a MIDI Bank Select / Program Change actually selects.
        /// </summary>
        public sealed class Preset
        {
            public required string Name { get; init; }

            public ushort MidiProgram { get; init; }

            public ushort MidiBank { get; init; }

            public List<PresetZone> Zones { get; init; } = [];
        }

        /// <summary>
        /// A complete, in-memory description of a .sf2 file: samples, the instruments built from
        /// them, and the presets that expose those instruments to MIDI. Hand this to <see cref="Sf2Writer"/>.
        /// </summary>
        public sealed class SoundFontDocument
        {
            public string Name { get; init; }

            public List<SampleSource> Samples { get; } = [];

            public List<Instrument> Instruments { get; } = [];

            public List<Preset> Presets { get; } = [];
        }

        /// <summary>
        /// Writes one RIFF chunk header, then backpatches its size once the caller is done writing
        /// the chunk's contents (via <c>using</c>). Also pads to an even byte count, as RIFF requires.
        /// </summary>
        internal sealed class RiffChunk : IDisposable
        {
            private readonly BinaryWriter _writer;
            private readonly long _sizeFieldPosition;
            private readonly long _startPosition;

            public RiffChunk(BinaryWriter writer, string fourCC)
            {
                _writer = writer;
                writer.Write(Encoding.ASCII.GetBytes(fourCC));
                _sizeFieldPosition = writer.BaseStream.Position;
                writer.Write(0u); // placeholder, patched in Dispose
                _startPosition = writer.BaseStream.Position;
            }

            public void Dispose()
            {
                var end = _writer.BaseStream.Position;
                var size = end - _startPosition;

                if (size % 2 != 0)
                {
                    _writer.Write((byte)0);
                    end++;
                }

                _writer.BaseStream.Position = _sizeFieldPosition;
                _writer.Write((uint)size);
                _writer.BaseStream.Position = end;
            }
        }

        /// <summary>
        /// The subset of SF2 2.01 generator operators this writer needs. See the spec for the full list.
        /// </summary>
        internal enum SfGenerator : ushort
        {
            DelayVolEnv = 33,
            AttackVolEnv = 34,
            HoldVolEnv = 35,
            DecayVolEnv = 36,
            SustainVolEnv = 37,
            ReleaseVolEnv = 38,
            KeyRange = 43,
            VelRange = 44,
            Instrument = 41,
            SampleModes = 54,
            OverridingRootKey = 58,
            SampleID = 53,
        }

        /// <summary>
        /// One entry in a pgen/igen array. <see cref="Amount"/> is either a plain value or, for
        /// range generators, a low/high byte pair packed into one 16-bit field (spec: genAmountType.ranges).
        /// </summary>
        internal readonly struct GeneratorRecord
        {
            public SfGenerator Operator { get; }

            public ushort Amount { get; }

            public GeneratorRecord(SfGenerator op, ushort amount)
            {
                Operator = op;
                Amount = amount;
            }

            public static GeneratorRecord Range(SfGenerator op, byte low, byte high) =>
                new(op, (ushort)(low | (high << 8)));
        }

        /// <summary>
        /// Flattened, index-resolved form of a <see cref="SoundFontDocument"/>'s instrument and preset
        /// graphs — the exact shape the pdta sub-chunks (phdr/pbag/pgen/inst/ibag/igen) are written from.
        /// </summary>
        internal sealed class SfLayout
        {
            public List<(string Name, ushort Program, ushort Bank, ushort BagIndex)> PresetHeaders { get; } = [];

            public List<(ushort GenIndex, ushort ModIndex)> PresetBags { get; } = [];

            public List<GeneratorRecord> PresetGenerators { get; } = [];

            public List<(string Name, ushort BagIndex)> InstrumentHeaders { get; } = [];

            public List<(ushort GenIndex, ushort ModIndex)> InstrumentBags { get; } = [];

            public List<GeneratorRecord> InstrumentGenerators { get; } = [];
        }

        /// <summary>
        /// Turns the friendly <see cref="Instrument"/>/<see cref="Preset"/> zone lists into the flat,
        /// index-linked arrays the binary format actually stores, including the required terminal
        /// ("EOI"/"EOP") records at each level.
        /// </summary>
        internal static class SfLayoutBuilder
        {
            public static SfLayout Build(SoundFontDocument doc)
            {
                var layout = new SfLayout();
                BuildInstruments(doc, layout);
                BuildPresets(doc, layout);
                return layout;
            }

            private static void BuildInstruments(SoundFontDocument doc, SfLayout layout)
            {
                foreach (var instrument in doc.Instruments)
                {
                    layout.InstrumentHeaders.Add((instrument.Name, (ushort)layout.InstrumentBags.Count));

                    foreach (var zone in instrument.Zones)
                    {
                        layout.InstrumentBags.Add(((ushort)layout.InstrumentGenerators.Count, 0));

                        // Spec requires keyRange, then velRange, to be the first generators in a zone.
                        if (zone.KeyLow != 0 || zone.KeyHigh != 127)
                        {
                            layout.InstrumentGenerators.Add(GeneratorRecord.Range(SfGenerator.KeyRange, zone.KeyLow, zone.KeyHigh));
                        }

                        if (zone.VelLow != 0 || zone.VelHigh != 127)
                        {
                            layout.InstrumentGenerators.Add(GeneratorRecord.Range(SfGenerator.VelRange, zone.VelLow, zone.VelHigh));
                        }

                        if (zone.RootKeyOverride is byte rootKey)
                        {
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.OverridingRootKey, rootKey));
                        }

                        if (zone.VolumeEnvelope is VolumeEnvelope envelope)
                        {
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.DelayVolEnv, (ushort)TimeToTimecents(envelope.DelayTime)));
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.AttackVolEnv, (ushort)TimeToTimecents(envelope.AttackTime)));
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.HoldVolEnv, (ushort)TimeToTimecents(envelope.HoldTime)));
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.DecayVolEnv, (ushort)TimeToTimecents(envelope.DecayTime)));
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.SustainVolEnv, (ushort)Math.Clamp(Math.Round(envelope.SustainAttenuation * 10), 0, 1000)));
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.ReleaseVolEnv, (ushort)TimeToTimecents(envelope.ReleaseTime)));
                        }

                        var sample = doc.Samples[zone.SampleIndex];
                        if (sample.Loops)
                        {
                            layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.SampleModes, 1)); // 1 = loop continuously
                        }

                        // Spec requires sampleID to be the last generator in an instrument zone.
                        layout.InstrumentGenerators.Add(new GeneratorRecord(SfGenerator.SampleID, (ushort)zone.SampleIndex));
                    }
                }

                layout.InstrumentHeaders.Add(("EOI", (ushort)layout.InstrumentBags.Count));
                layout.InstrumentBags.Add(((ushort)layout.InstrumentGenerators.Count, 0));
            }

            private static void BuildPresets(SoundFontDocument doc, SfLayout layout)
            {
                foreach (var preset in doc.Presets)
                {
                    layout.PresetHeaders.Add((preset.Name, preset.MidiProgram, preset.MidiBank, (ushort)layout.PresetBags.Count));

                    foreach (var zone in preset.Zones)
                    {
                        layout.PresetBags.Add(((ushort)layout.PresetGenerators.Count, 0));

                        if (zone.KeyLow != 0 || zone.KeyHigh != 127)
                        {
                            layout.PresetGenerators.Add(GeneratorRecord.Range(SfGenerator.KeyRange, zone.KeyLow, zone.KeyHigh));
                        }

                        if (zone.VelLow != 0 || zone.VelHigh != 127)
                        {
                            layout.PresetGenerators.Add(GeneratorRecord.Range(SfGenerator.VelRange, zone.VelLow, zone.VelHigh));
                        }

                        // Spec requires instrument to be the last generator in a preset zone.
                        layout.PresetGenerators.Add(new GeneratorRecord(SfGenerator.Instrument, (ushort)zone.InstrumentIndex));
                    }
                }

                layout.PresetHeaders.Add(("EOP", 0, 0, (ushort)layout.PresetBags.Count));
                layout.PresetBags.Add(((ushort)layout.PresetGenerators.Count, 0));
            }
        }

        /// <summary>
        /// Serializes a <see cref="SoundFontDocument"/> to the binary SoundFont 2 (.sf2) RIFF format
        /// (spec 2.01). Covers the subset needed for straightforward, one-sample-per-zone instruments.
        /// </summary>
        public static class Sf2Writer
        {
            private const int SamplePaddingFrames = 46; // spec: silence required after each sample's data

            public static void Write(SoundFontDocument doc, Stream output)
            {
                var offsets = ComputeSampleOffsets(doc);

                using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
                using var riff = new RiffChunk(writer, "RIFF");
                writer.Write(Encoding.ASCII.GetBytes("sfbk"));

                WriteInfoList(writer, doc);
                WriteSdtaList(writer, doc);
                WritePdtaList(writer, doc, offsets);
            }

            private static List<(int Start, int End)> ComputeSampleOffsets(SoundFontDocument doc)
            {
                var offsets = new List<(int Start, int End)>();
                var cursor = 0;

                foreach (var sample in doc.Samples)
                {
                    var start = cursor;
                    var end = start + sample.Pcm.Length;
                    offsets.Add((start, end));
                    cursor = end + SamplePaddingFrames;
                }

                return offsets;
            }

            private static void WriteInfoList(BinaryWriter w, SoundFontDocument doc)
            {
                using var list = new RiffChunk(w, "LIST");
                w.Write(Encoding.ASCII.GetBytes("INFO"));

                using (new RiffChunk(w, "ifil"))
                {
                    w.Write((ushort)2); // major
                    w.Write((ushort)1); // minor
                }

                WriteZstrChunk(w, "isng", "EMU8000");
                WriteZstrChunk(w, "INAM", doc.Name);
            }

            private static void WriteZstrChunk(BinaryWriter w, string id, string value)
            {
                using var chunk = new RiffChunk(w, id);
                w.Write(Encoding.ASCII.GetBytes(value));
                w.Write((byte)0);
                if (value.Length % 2 == 0)
                {
                    w.Write((byte)0);
                }
            }

            private static void WriteSdtaList(BinaryWriter w, SoundFontDocument doc)
            {
                using var list = new RiffChunk(w, "LIST");
                w.Write(Encoding.ASCII.GetBytes("sdta"));

                using var smpl = new RiffChunk(w, "smpl");
                foreach (var sample in doc.Samples)
                {
                    foreach (var frame in sample.Pcm)
                    {
                        w.Write(frame);
                    }

                    for (var i = 0; i < SamplePaddingFrames; i++)
                    {
                        w.Write((short)0);
                    }
                }
            }

            private static void WritePdtaList(BinaryWriter w, SoundFontDocument doc, List<(int Start, int End)> offsets)
            {
                using var list = new RiffChunk(w, "LIST");
                w.Write(Encoding.ASCII.GetBytes("pdta"));

                var layout = SfLayoutBuilder.Build(doc);

                WritePhdr(w, layout);
                WriteBag(w, "pbag", layout.PresetBags);
                WriteMod(w, "pmod");
                WriteGen(w, "pgen", layout.PresetGenerators);

                WriteInst(w, layout);
                WriteBag(w, "ibag", layout.InstrumentBags);
                WriteMod(w, "imod");
                WriteGen(w, "igen", layout.InstrumentGenerators);

                WriteShdr(w, doc, offsets);
            }

            private static void WritePhdr(BinaryWriter w, SfLayout layout)
            {
                using var chunk = new RiffChunk(w, "phdr");
                foreach (var preset in layout.PresetHeaders)
                {
                    WriteFixedString(w, preset.Name, 20);
                    w.Write(preset.Program);
                    w.Write(preset.Bank);
                    w.Write(preset.BagIndex);
                    w.Write(0u); // library
                    w.Write(0u); // genre
                    w.Write(0u); // morphology
                }
            }

            private static void WriteInst(BinaryWriter w, SfLayout layout)
            {
                using var chunk = new RiffChunk(w, "inst");
                foreach (var instrument in layout.InstrumentHeaders)
                {
                    WriteFixedString(w, instrument.Name, 20);
                    w.Write(instrument.BagIndex);
                }
            }

            private static void WriteBag(BinaryWriter w, string id, List<(ushort GenIndex, ushort ModIndex)> bags)
            {
                using var chunk = new RiffChunk(w, id);
                foreach (var bag in bags)
                {
                    w.Write(bag.GenIndex);
                    w.Write(bag.ModIndex);
                }
            }

            private static void WriteMod(BinaryWriter w, string id)
            {
                // We never emit real modulators, so every zone's ModIndex is 0 and this chunk
                // needs only the single terminal (all-zero) record the spec requires.
                using var chunk = new RiffChunk(w, id);
                for (var i = 0; i < 10; i++)
                {
                    w.Write((byte)0);
                }
            }

            private static void WriteGen(BinaryWriter w, string id, List<GeneratorRecord> generators)
            {
                using var chunk = new RiffChunk(w, id);
                foreach (var gen in generators)
                {
                    w.Write((ushort)gen.Operator);
                    w.Write(gen.Amount);
                }

                w.Write((ushort)0); // terminal generator record
                w.Write((ushort)0);
            }

            private static void WriteShdr(BinaryWriter w, SoundFontDocument doc, List<(int Start, int End)> offsets)
            {
                using var chunk = new RiffChunk(w, "shdr");

                for (var i = 0; i < doc.Samples.Count; i++)
                {
                    var sample = doc.Samples[i];
                    var (start, end) = offsets[i];

                    WriteFixedString(w, sample.Name, 20);
                    w.Write((uint)start);
                    w.Write((uint)end);
                    w.Write((uint)(start + (sample.Loops ? sample.LoopStartSample : 0)));
                    w.Write((uint)(start + (sample.Loops ? sample.LoopEndSample : 0)));
                    w.Write((uint)sample.SampleRate);
                    w.Write(sample.OriginalKey);
                    w.Write(sample.PitchCorrectionCents);
                    w.Write((ushort)0); // sampleLink: unused for mono samples
                    w.Write((ushort)1); // sfSampleType: monoSample
                }

                WriteFixedString(w, "EOS", 20);
                for (var i = 0; i < 5; i++)
                {
                    w.Write(0u); // start/end/startloop/endloop/sampleRate
                }

                w.Write((byte)0);
                w.Write((sbyte)0);
                w.Write((ushort)0);
                w.Write((ushort)0);
            }

            private static void WriteFixedString(BinaryWriter w, string value, int length)
            {
                var bytes = new byte[length];
                var src = Encoding.ASCII.GetBytes(value);
                Array.Copy(src, bytes, Math.Min(src.Length, length));
                w.Write(bytes);
            }
        }

        private static short TimeToTimecents(TimeSpan time)
        {
            var seconds = time.TotalSeconds;
            if (seconds <= 0.001)
            {
                return -12000;
            }

            return (short)Math.Round(1200.0 * Math.Log(seconds, 2.0));
        }
    }
}
