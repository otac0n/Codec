namespace Codec.Files
{
    using System;
    using System.IO;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    internal class CdaFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton<FileHandlerResolver<AudioStream>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var ext = parent.Path.GetExtension(parentRelativePath);
                if (string.Equals(ext, ".cda", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".cdda", StringComparison.OrdinalIgnoreCase))
                {
                    return (fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        var input = parent.File.OpenRead(parentRelativePath);
                        var headerStream = MakeHeader((int)input.Length);
                        return (AudioStream)new ConcatStream(Ownership.Dispose, MappedStream.FromStream(headerStream, Ownership.Dispose), MappedStream.FromStream(input, Ownership.Dispose));
                    };
                }

                return null;
            });
        }

        public static MemoryStream MakeHeader(int dataSize)
        {
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms);

            var sampleRate = 44100;
            short channels = 2;
            short bitsPerSample = 16;

            var byteRate = sampleRate * channels * (bitsPerSample / 8);
            var blockAlign = (short)(channels * (bitsPerSample / 8));

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);

            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataSize);

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}
