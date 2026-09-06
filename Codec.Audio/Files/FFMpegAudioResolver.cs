// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Audio
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Codec.Files;
    using Codec.Services;
    using FFMpegCore.Enums;
    using FFMpegCore.Pipes;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public static class FFMpegAudioResolver
    {
        public static void Register(IServiceCollection services)
        {
            services.AddSingleton(new EntryTypeMatcher(EntryType.Audio, string.Join(";", FFMpegLocator.SupportedExtensions.Select(e => $"*{e}"))));
            services.AddSingleton<FileHandlerResolver<AudioStream>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var ext = parent.Path.GetExtension(parentRelativePath);
                if (FFMpegLocator.SupportedExtensions.Contains(ext))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        var logger = serviceProvider.GetService<ILogger>();
                        using var source = parent.File.OpenRead(parentRelativePath);
                        return new AudioStream(DecodeToWav(source, logger), fullPath);
                    });
                }

                return null;
            });
        }

        public static bool CanDecode(string extension) =>
            FFMpegLocator.SupportedExtensions.Contains(extension);

        public static Stream DecodeToWav(Stream source, ILogger? logger = null)
        {
            var output = new MemoryStream();

            Task.Run(async () =>
            {
                await FFMpegLocator.EnsureLocatedAsync(logger).ConfigureAwait(false);

                var success = await FFMpegCore.FFMpegArguments
                    .FromPipeInput(new StreamPipeSource(source))
                    .OutputToPipe(
                        new StreamPipeSink(output),
                        options => options
                            .ForceFormat("wav")
                            .DisableChannel(Channel.Video))
                    .ProcessAsynchronously()
                    .ConfigureAwait(false);

                if (!success)
                {
                    throw new InvalidOperationException("ffmpeg failed to decode the input stream.");
                }
            }).GetAwaiter().GetResult();

            WavFile.FixStreamedWavHeader(output);

            output.Position = 0;
            return output;
        }
    }
}
