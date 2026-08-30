// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;

    public static partial class WellKnownPaths
    {
        public static readonly string CDsPath = "system/roms";
        public static readonly string CD1Path = CDsPath + "/MGS_US_DISC1-washed.BIN";
        public static readonly string CD2Path = CDsPath + "/MGS_US_DISC2-washed.BIN";
        public static readonly string VRCDPath = CDsPath + "/mgs-vr-missions-ripped-washed.bin";
        public static readonly string StageDirPath = @"MGS\STAGE.DIR";
        public static readonly string FaceDatPath = @"MGS\FACE.DAT";
        public static readonly string AllDataBin = Path.Combine("MGS1", "windata", "alldata.bin");

        public static readonly string PCTextures = "PC_TXN_UP";
        public static readonly string PackedTextures = Path.Combine("paks", "TextureData.pak");

        private static readonly string HomePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string SteamPathMacOS = Path.Combine(HomePath, "Library", "Application Support", "Steam");
        public static readonly string SteamPathLinuxNative = Path.Combine(HomePath, ".steam", "steam");
        public static readonly string SteamPathLinuxLocalShare = Path.Combine(HomePath, ".local", "share", "Steam");
        public static readonly string SteamPathLinuxFlatpak = Path.Combine(HomePath, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        public static readonly string SteamPathWindows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");

        public static readonly string MGS1StartPath = Path.Combine(GetDefaultSteamAppsPath(2131630), AllDataBin, CD1Path, StageDirPath);
        public static readonly string MGS2StartPath = Path.Combine(GetDefaultSteamAppsPath(2131640), "MGS2");
        public static readonly string MGS3StartPath = Path.Combine(GetDefaultSteamAppsPath(2131650), "MGS3");
        public static readonly string MGS4StartPath = Path.Combine(GetDefaultSteamAppsPath(2492660), "METAL GEAR SOLID 4", "MGS4", "common");
        public static readonly string MGSPWStartPath = Path.Combine(GetDefaultSteamAppsPath(2492670), "MGS_PW", "mgspw", "MLG", "disc0_rel");

        public static readonly string[] StartPaths = [MGS4StartPath, MGS1StartPath, MGSPWStartPath, MGS2StartPath, MGS3StartPath];

        public static IList<string> SteamPaths =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? [SteamPathMacOS] :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? [SteamPathLinuxNative, SteamPathLinuxLocalShare, SteamPathLinuxFlatpak] :
            [SteamPathWindows];

        [GeneratedRegex(@"\\(.)")]
        private static partial Regex GetEscapeRegex();

        public static string GetDefaultSteamAppsPath(uint appId)
        {
            var steamPaths = SteamPaths;
            var steam = steamPaths.FirstOrDefault(Directory.Exists) ?? steamPaths[0];
            var defaultPath = Path.Combine(steam, "steamapps");

            try
            {
                var library = File.ReadAllText(Path.Combine(defaultPath, "libraryfolders.vdf"));
                var match = Regex.Match(library, @"""path""\s+""(?<escaped_path>([^\""]|\[\""])+)""[^{}]+""apps""[\r\n\s]+{[^}]+""(?<found_app_id>" + appId +  @")""\s+""\d+""");
                if (match.Success)
                {
                    return Path.Combine(GetEscapeRegex().Replace(match.Groups["escaped_path"].Value, "$1"), "steamapps", "common");
                }
            }
            catch
            {
            }

            return Path.Combine(defaultPath, "common");
        }
    }
}
