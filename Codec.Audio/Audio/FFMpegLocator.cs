// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Audio
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using FFMpegCore;
    using Microsoft.Extensions.Logging;

    public static class FFMpegLocator
    {
        public const string FFmpegPathEnvironmentVariable = "FFMPEG_PATH";

        public static readonly ImmutableHashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3",
            ".ogg",
            ".flac",
            ".wma",
        }.ToImmutableHashSet();

        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static bool located;

        public static async Task EnsureLocatedAsync(ILogger? logger = null, CancellationToken cancel = default)
        {
            if (located)
            {
                return;
            }

            await Gate.WaitAsync(cancel).ConfigureAwait(false);
            try
            {
                if (located)
                {
                    return;
                }

                if (await ProbeAsync(GlobalFFOptions.Current.BinaryFolder, logger).ConfigureAwait(false))
                {
                    located = true;
                    return;
                }

                if (Environment.GetEnvironmentVariable(FFmpegPathEnvironmentVariable) is string envPath)
                {
                    logger?.LogInformation("Found ffmpeg via %{EnvironmentVariable}% at '{BinaryFolder}'.", FFmpegPathEnvironmentVariable, envPath);
                    GlobalFFOptions.Configure(o => o.BinaryFolder = envPath);
                    located = true;
                    return;
                }

                if (await ProbeAsync(binaryFolder: null, logger).ConfigureAwait(false))
                {
                    located = true;
                    return;
                }

                foreach (var candidate in WellKnownInstallLocations())
                {
                    if (await ProbeAsync(candidate, logger).ConfigureAwait(false))
                    {
                        logger?.LogInformation("Found ffmpeg at '{BinaryFolder}'.", candidate);
                        GlobalFFOptions.Configure(o => o.BinaryFolder = candidate);
                        located = true;
                        return;
                    }
                }
            }
            finally
            {
                Gate.Release();
            }
        }

        private static async Task<bool> ProbeAsync(string? binaryFolder, ILogger? logger)
        {
            if (binaryFolder is not null && !Directory.Exists(binaryFolder))
            {
                return false;
            }

            var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
            var fileName = string.IsNullOrEmpty(binaryFolder) ? exeName : Path.Combine(binaryFolder, exeName);

            try
            {
                var startInfo = new ProcessStartInfo(fileName, "-version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return false;
                }

                await process.WaitForExitAsync().ConfigureAwait(false);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "No usable ffmpeg at '{BinaryFolder}'.", string.IsNullOrEmpty(binaryFolder) ? "<PATH>" : binaryFolder);
                return false;
            }
        }

        private static IEnumerable<string> WellKnownInstallLocations()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var programData = @"C:\ProgramData";

                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin");
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin");
                yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links");
                yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
                yield return Path.Combine(programData, "chocolatey", "bin");
                yield return Path.Combine(programData, "chocolatey", "lib", "ffmpeg", "tools");
                yield return Path.Combine(userProfile, "scoop", "apps", "ffmpeg", "current", "bin");
                yield return Path.Combine(programData, "scoop", "apps", "ffmpeg", "current", "bin");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                yield return "/opt/homebrew/bin";
                yield return "/usr/local/bin";
                yield return "/opt/local/bin";
            }
            else
            {
                yield return "/usr/bin";
                yield return "/usr/local/bin";
                yield return "/snap/bin";
                yield return "/home/linuxbrew/.linuxbrew/bin";
                yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nix-profile", "bin");
                yield return "/run/current-system/sw/bin";
            }
        }
    }
}
