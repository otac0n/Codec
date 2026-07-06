// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.MGS
{
    using System.IO;

    public static class WellKnownPaths
    {
        public static readonly string CDsPath = "system/roms";
        public static readonly string CD1Path = CDsPath + "/MGS_US_DISC1-washed.BIN";
        public static readonly string CD2Path = CDsPath + "/MGS_US_DISC2-washed.BIN";
        public static readonly string VRCDPath = CDsPath + "/mgs-vr-missions-ripped-washed.bin";
        public static readonly string StageDirPath = @"MGS\STAGE.DIR";
        public static readonly string FaceDatPath = @"MGS\FACE.DAT";
        public static readonly string AllDataBin = Path.Combine("common", "MGS1", "windata", "alldata.bin");
    }
}
