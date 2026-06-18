// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.DependencyInjection;

    public partial class EnvironmentOptions
    {
        public static readonly Option<string> SteamAppsOption = new(
            name: "--steamApps",
            description: "The path to the steamapps folder that contains the games.",
            getDefaultValue: GetDefaultSteamAppsPath)
        {
            IsRequired = true,
        };

        public required string SteamApps { get; set; }

        public static void Attach(Command command)
        {
            command.AddGlobalOption(SteamAppsOption);
        }

        public static void Bind(InvocationContext context, IServiceCollection services)
        {
            var options = new EnvironmentOptions()
            {
                SteamApps = context.ParseResult.GetValueForOption(SteamAppsOption)!,
            };

            services.AddSingleton(options);
        }

        [GeneratedRegex(@"""path""\s+""(?<escaped_path>([^\""]|\[\""])+)""[^{}]+""apps""[\r\n\s]+{[^}]+""(?<found_app_id>21316[345]0)""\s+""\d+""")]
        private static partial Regex GetPathFinderRegex();

        [GeneratedRegex(@"\\(.)")]
        private static partial Regex GetEscapeRegex();

        private static string GetDefaultSteamAppsPath()
        {
            var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Steam\steamapps\");
            try
            {
                var library = File.ReadAllText(Path.Combine(defaultPath, "libraryfolders.vdf"));
                var match = GetPathFinderRegex().Match(library);
                if (match.Success)
                {
                    return Path.Combine(GetEscapeRegex().Replace(match.Groups["escaped_path"].Value, "$1"), "steamapps");
                }
            }
            catch
            {
            }

            return defaultPath;
        }
    }
}
