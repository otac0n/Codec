// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec
{
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Codec.MGS;
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

        [GeneratedRegex(@"""path""\s+""(?<escaped_path>([^\""]|\[\""])+)""[^{}]+""apps""[\r\n\s]+{[^}]+""(?<found_app_id>21316[345]0)""\s+""\d+""")] // TODO: |24926[67]0
        private static partial Regex GetPathFinderRegex();

        [GeneratedRegex(@"\\(.)")]
        private static partial Regex GetEscapeRegex();

        private static string GetDefaultSteamAppsPath()
        {
            var steamPaths = WellKnownPaths.SteamPaths;
            var steam = steamPaths.FirstOrDefault(Directory.Exists) ?? steamPaths[0];
            var defaultPath = Path.Combine(steam, "steamapps");

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
