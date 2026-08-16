// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;

    public static class JoyDictService
    {
        private static readonly Assembly Assembly = typeof(JoyDictService).Assembly;

        private static readonly string[] ManifestResources = Assembly.GetManifestResourceNames();

        private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new();

        private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

        public static bool TryGetOriginalFileName(string game, string container, string? version, string folder, uint hashedFilename, byte extension, out string result)
        {
            game = game.ToLowerInvariant();
            return TryGetOriginalFileName(
                game,
                container,
                version,
                folder,
                game switch
                {
                    "mgs1" => $"{hashedFilename:x4}.{(char)extension}",
                    "mgs2" => $"{hashedFilename:x8}.{(char)('a' + extension)}",
                    "mgstts" => $"{hashedFilename:x8}.{(char)('a' + extension)}",
                    _ => $"{hashedFilename:x8}.{extension:x2}",
                },
                out result);
        }

        /// <summary>
        /// Attempts to retrieve the original filename for a given game, container, and <see cref="StringCode">hashed</see> filename.
        /// </summary>
        /// <param name="game">A value like "mgs1", "mgs2", "mgstts", etc.</param>
        /// <param name="container">A value like "stage.dir", "stage.dat", "brf.dat", etc.</param>
        /// <param name="version">
        /// A value like "mgs.us", "mgsvs.us+eu", "mgs.demo2.jp", etc.
        /// A value of <c>null</c> searches every version before falling back to common.
        /// </param>
        /// <param name="folder">A folder such as "brf" or "s01a".</param>
        /// <param name="filename">The <see cref="StringCode">hashed</see> filename to look up.</param>
        /// <param name="result">The discovered filename.</param>
        /// <returns><c>true</c> if the filename was found; <c>false</c>, otherwise.</returns>
        public static bool TryGetOriginalFileName(string game, string container, string? version, string folder, string filename, out string result)
        {
            game = game.ToLowerInvariant();
            container = container.ToLowerInvariant();
            version = version?.ToLowerInvariant();
            folder = folder.ToLowerInvariant();
            filename = filename.ToLowerInvariant();

            if (version is not null)
            {
                foreach (var resource in EnumerateFolderResources($"Codec.MGS.Resources.JoyDict.{game}.{container}.{version}", folder))
                {
                    if (TryLookup(resource, filename, out result))
                    {
                        return true;
                    }
                }
            }
            else
            {
                var prefix = $"Codec.MGS.Resources.JoyDict.{game}.{container}.";
                foreach (var resource in ManifestResources)
                {
                    if (!resource.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (resource.Contains(".common.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!MatchesFolder(resource, folder))
                    {
                        continue;
                    }

                    if (TryLookup(resource, filename, out result))
                    {
                        return true;
                    }
                }
            }

            foreach (var resource in EnumerateFolderResources($"Codec.MGS.Resources.JoyDict.{game}.{container}.common", folder))
            {
                if (TryLookup(resource, filename, out result))
                {
                    return true;
                }
            }

            if (version != null)
            {
                if (TryLookup($"Codec.MGS.Resources.JoyDict.{game}.{container}.{version}.tbl", filename, out result))
                {
                    return true;
                }
            }

            if (TryLookup($"Codec.MGS.Resources.JoyDict.{game}.{container}.common.tbl", filename, out result))
            {
                return true;
            }

            result = filename;
            return false;
        }

        private static IEnumerable<string> EnumerateFolderResources(string prefix, string folder)
        {
            yield return $"{prefix}.{folder}.tbl";
            if (folder.Contains('-'))
            {
                yield break;
            }

            var subfolderPrefix = $"{prefix}.{folder}-";

            foreach (var resource in ManifestResources)
            {
                if (!resource.StartsWith(subfolderPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!resource.EndsWith(".tbl", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return resource;
            }
        }

        private static bool MatchesFolder(string resource, string folder)
        {
            if (resource.EndsWith($".{folder}.tbl", StringComparison.Ordinal))
            {
                return true;
            }

            if (folder.Contains('-') || !resource.Contains('-'))
            {
                return false;
            }

            return Regex.IsMatch(resource, $".{folder}-[^.]*\\.tbl$");
        }

        private static bool TryLookup(string resourceName, string filename, out string result)
        {
            var dictionary = Cache.GetOrAdd(resourceName, LoadDictionary);

            if (dictionary.TryGetValue(filename, out result!))
            {
                return !string.IsNullOrEmpty(result);
            }

            result = filename;
            return false;
        }

        private static IReadOnlyDictionary<string, string> LoadDictionary(string resourceName)
        {
            using var stream = Assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return Empty;
            }

            using var reader = new StreamReader(stream);

            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (reader.ReadLine() is { } line)
            {
                line = line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var separator = line.IndexOf('|');
                if (separator < 0)
                {
                    continue;
                }

                var key = line[..separator].Trim().ToLowerInvariant();
                var value = line[(separator + 1)..].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    dictionary[key] = value;
                }
            }

            return dictionary;
        }
    }
}
