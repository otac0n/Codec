namespace Codec.Files
{
    using System;
    using System.IO;
    using Codec.Services;
    using DiscUtils.Streams;
    using Microsoft.Extensions.DependencyInjection;

    internal class CdaFile
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Audio, "*.cda;*.cdda"));

            services.AddSingleton<FileHandlerResolver<AudioStream>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var ext = parent.Path.GetExtension(parentRelativePath);
                if (string.Equals(ext, ".cda", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".cdda", StringComparison.OrdinalIgnoreCase))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        var input = parent.File.OpenRead(parentRelativePath);
                        var headerStream = MakeHeader((int)input.Length);
                        return new AudioStream(
                            new ConcatStream(Ownership.Dispose, MappedStream.FromStream(headerStream, Ownership.Dispose), MappedStream.FromStream(input, Ownership.Dispose)),
                            fullPath);
                    });
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

            WavFile.WritePcmHeader(bw, sampleRate, channels, bitsPerSample, dataSize);

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}
