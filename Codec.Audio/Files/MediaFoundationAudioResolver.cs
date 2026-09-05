namespace Codec.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;
    using NAudio.Wave;

    public static class MediaFoundationAudioResolver
    {
        public static void Register(IServiceCollection services)
        {
            var foundationExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foundationExtensions.UnionWith([".wav", ".mp3", ".m4a", ".aac", ".wma", ".asf"]);
            services.AddSingleton(new EntryTypeMatcher(EntryType.Audio, string.Join(";", foundationExtensions.Select(e => $"*{e}"))));
            services.AddSingleton<FileHandlerResolver<AudioStream>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var ext = parent.Path.GetExtension(parentRelativePath);
                if (foundationExtensions.Contains(ext))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var source = parent.File.OpenRead(parentRelativePath);
                        using var reader = new StreamMediaFoundationReader(source);
                        var output = new MemoryStream();
                        WaveFileWriter.WriteWavFileToStream(output, reader);
                        output.Position = 0;
                        return new AudioStream(output, fullPath);
                    });
                }

                return null;
            });
        }
    }
}
