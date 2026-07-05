// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;

    public static class WellKnownPaths
    {
        public static readonly string CDsPath = "system/roms";
        public static readonly string CD1Path = CDsPath + "/MGS_US_DISC1-washed.BIN";
        public static readonly string CD2Path = CDsPath + "/MGS_US_DISC2-washed.BIN";
        public static readonly string VRCDPath = CDsPath + "/mgs-vr-missions-ripped-washed.bin";
        public static readonly string StageDirPath = @"MGS\STAGE.DIR";
        public static readonly string FaceDatPath = @"MGS\FACE.DAT";
        public static readonly string AllDataBin = Path.Combine("common", "MGS1", "windata", "alldata.bin");

        private static readonly string HomePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string SteamPathMacOS = Path.Combine(HomePath, "Library", "Application Support", "Steam");
        public static readonly string SteamPathLinuxNative = Path.Combine(HomePath, ".steam", "steam");
        public static readonly string SteamPathLinuxLocalShare = Path.Combine(HomePath, ".local", "share", "Steam");
        public static readonly string SteamPathLinuxFlatpak = Path.Combine(HomePath, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        public static readonly string SteamPathWindows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");

        public static IList<string> SteamPaths =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? [SteamPathMacOS] :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? [SteamPathLinuxNative, SteamPathLinuxLocalShare, SteamPathLinuxFlatpak] :
            [SteamPathWindows];
    }
}
