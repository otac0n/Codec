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
    }
}
