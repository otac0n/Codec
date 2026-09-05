// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Audio
{
    using Codec.Files;
    using Codec.Services;
    using Microsoft.Extensions.DependencyInjection;

    public static class ServiceRegistration
    {
        public static void Register(IServiceCollection services)
        {
            MidiFile.Register(services);
            CdaFile.Register(services);
            services.AddSingleton(new EntryTypeMatcher(EntryType.Video, "*.avi;*.mov;*.mp4;*.mkv;*.webm"));
            MediaFoundationAudioResolver.Register(services);
            VgmStreamAudioResolver.Register(services);
        }
    }
}
