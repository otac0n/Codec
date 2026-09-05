namespace Codec.Files
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;
    using VgmSharp;

    public static class VgmStreamAudioResolver
    {
        public static void Register(IServiceCollection services)
        {
            var vagstreamExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                ".ogg",
            };
            vagstreamExtensions.UnionWith(FileExtensions.SupportedExtensions.Select(e => $".{e}"));
            services.AddSingleton(new EntryTypeMatcher(EntryType.Audio, string.Join(";", vagstreamExtensions.Select(e => $"*{e}"))));
            services.AddSingleton<FileHandlerResolver<AudioStream>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var ext = parent.Path.GetExtension(parentRelativePath);
                if (vagstreamExtensions.Contains(ext))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                    {
                        using var source = parent.File.OpenRead(parentRelativePath);
                        using var vgm = VgmStreamReader.Open(source, fullPath, config: VgmStreamConfig.PlayOnceNoLoop());
                        var output = new MemoryStream();
                        vgm.DecodeTo(output);
                        output.Position = 0;
                        return new AudioStream(output, fullPath);
                    });
                }

                return null;
            });
        }
    }
}
