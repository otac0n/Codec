namespace Codec.Files
{
    using System;
    using System.IO;
    using System.Text;
    using Codec.Services;
    using MeltySynth;
    using Microsoft.Extensions.DependencyInjection;
    using NAudio.Wave;

    public class MidiFile
    {
        private static readonly string SoundFontResource = "Codec.Resources.florestan-subset.sf2";
        private static readonly MeltySynth.SoundFont SoundFont = new(typeof(MidiFile).Assembly.GetManifestResourceStream(SoundFontResource)!);

        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Audio, "*.mid;*.midi"));

            services.AddSingleton<FileHandlerResolver<AudioStream>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var ext = parent.Path.GetExtension(parentRelativePath);
                if (string.Equals(ext, ".mid", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".midi", StringComparison.OrdinalIgnoreCase))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var input = parent.File.OpenRead(parentRelativePath);
                        var sampleFileName = parent.Path.Combine(parent.Path.GetDirectoryName(parentRelativePath), "samples.sf2");

                        MeltySynth.SoundFont? soundFont = null;
                        if (parent.File.Exists(sampleFileName))
                        {
                            try
                            {
                                using var sf = parent.File.OpenRead(sampleFileName);
                                soundFont = new(sf);
                            }
                            catch
                            {
                                // TODO: Log sound font load failure.
                            }
                        }

                        return new AudioStream(
                            ConvertToPCMStream(input, soundFont),
                            fullPath);
                    });
                }

                return null;
            });
        }

        public static MemoryStream ConvertToPCMStream(Stream midiStream, MeltySynth.SoundFont? soundFont = null)
        {
            var sampleRate = 44100;
            var settings = new SynthesizerSettings(sampleRate);
            var synthesizer = new Synthesizer(soundFont ?? SoundFont, settings);
            var midiFile = new MeltySynth.MidiFile(midiStream);
            var sequencer = new MidiFileSequencer(synthesizer);
            sequencer.Play(midiFile, loop: false);

            var output = new MemoryStream();
            var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels: 2);

            WavFile.WritePcmHeader(writer, waveFormat.SampleRate, (short)waveFormat.Channels, (short)waveFormat.BitsPerSample, 0, 3);
            var dataStart = output.Position;

            var blockSize = sampleRate / 10;
            var leftBuffer = new float[blockSize];
            var rightBuffer = new float[blockSize];
            var interleaved = new float[blockSize * 2];
            var sampleBytes = new byte[blockSize * 2 * sizeof(float)];
            while (!sequencer.EndOfSequence)
            {
                sequencer.Render(leftBuffer, rightBuffer);
                for (var i = 0; i < blockSize; i++)
                {
                    interleaved[i * 2] = leftBuffer[i];
                    interleaved[i * 2 + 1] = rightBuffer[i];
                }

                Buffer.BlockCopy(interleaved, 0, sampleBytes, 0, sampleBytes.Length);

                output.Write(sampleBytes, 0, sampleBytes.Length);
            }

            output.Position = dataStart - sizeof(int);
            writer.Write((int)(output.Length - dataStart));
            output.Position = 0;
            return output;
        }
    }
}
