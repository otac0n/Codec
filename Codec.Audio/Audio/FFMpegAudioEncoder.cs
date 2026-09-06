// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Audio
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using FFMpegCore;
    using FFMpegCore.Pipes;
    using Microsoft.Extensions.Logging;

    public static class FFMpegAudioEncoder
    {
        public static bool CanEncode(string extension) =>
            FFMpegLocator.SupportedExtensions.Contains(extension);

        public static async Task EncodeAsync(Stream wavStream, string destinationPath, ILogger? logger = null, CancellationToken cancel = default)
        {
            ArgumentNullException.ThrowIfNull(wavStream);

            if (wavStream.CanSeek)
            {
                wavStream.Position = 0;
            }

            await FFMpegLocator.EnsureLocatedAsync(logger, cancel).ConfigureAwait(false);

            var success = await FFMpegArguments
                .FromPipeInput(new StreamPipeSource(wavStream))
                .OutputToFile(destinationPath, overwrite: true)
                .CancellableThrough(cancel)
                .ProcessAsynchronously()
                .ConfigureAwait(false);

            if (!success)
            {
                throw new InvalidOperationException($"ffmpeg failed to encode '{destinationPath}'.");
            }
        }
    }
}
