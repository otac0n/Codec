// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Codec.Files;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            MidiFile.Register(services);
            CdaFile.Register(services);
            services.AddSingleton(new EntryTypeMatcher(EntryType.Audio, "*.mp3;*.wav"));
            services.AddSingleton(new EntryTypeMatcher(EntryType.Video, "*.avi;*.mov;*.mp4;*.mkv;*.webm"));

            var vagstreamExtensions = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            vagstreamExtensions.UnionWith(VgmSharp.FileExtensions.SupportedExtensions.Select(e => $".{e}"));
            services.AddSingleton(new EntryTypeMatcher(EntryType.Audio, string.Join(";", vagstreamExtensions.Select(e => $"*{e}"))));
            services.AddSingleton<FileHandlerResolver<AudioStream>>((serviceProvider, fullPath, parentRelativePath, parent, parentPath) =>
            {
                var ext = parent.Path.GetExtension(parentRelativePath);
                if (vagstreamExtensions.Contains(ext))
                {
                    return new((fullPath, parentRelativePath, parent, parentPath) =>
                        new AudioStream(parent.File.OpenRead(parentRelativePath), fullPath));
                }

                return null;
            });
        }
    }
}
