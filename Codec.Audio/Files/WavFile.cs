// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Files
{
    using System.IO;
    using System.Text;

    internal class WavFile
    {
        public static void WritePcmHeader(BinaryWriter bw, int sampleRate, short channels, short bitsPerSample, int dataSize, short audioFormat = 1)
        {
            var byteRate = sampleRate * channels * (bitsPerSample / 8);
            var blockAlign = (short)(channels * (bitsPerSample / 8));

            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write(audioFormat);
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);

            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);
        }

        public static void FixStreamedWavHeader(MemoryStream wav)
        {
            const uint UnknownSize = 0xFFFFFFFF;

            if (wav.Length < 12)
            {
                return;
            }

            wav.Position = 0;

            var signature = new byte[4];
            wav.ReadExactly(signature);
            if (Encoding.ASCII.GetString(signature) != "RIFF")
            {
                return;
            }

            if (wav.ReadUInt32LittleEndian() != UnknownSize)
            {
                return;
            }

            wav.Position = 4;
            wav.WriteLittleEndian((uint)(wav.Length - 8));

            var offset = 12;
            while (offset + 8 <= wav.Length)
            {
                wav.Position = offset;
                wav.ReadExactly(signature);
                var chunkSize = wav.ReadUInt32LittleEndian();

                if (chunkSize == UnknownSize)
                {
                    if (Encoding.ASCII.GetString(signature) == "data")
                    {
                        var actualDataSize = (uint)(wav.Length - (offset + 8));
                        wav.Position = offset + 4;
                        wav.WriteLittleEndian(actualDataSize);
                    }

                    return;
                }

                offset = StreamExtensions.Align(offset + 8 + (int)chunkSize, 2);
            }
        }
    }
}
