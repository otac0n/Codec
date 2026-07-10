// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Imaging
{
    using ImageMagick;

    public static class ColorUtils
    {
        public static byte Expand5To8(int x) =>
            (byte)((x << 3) | (x >> 2)); // x * 255 / 31

        public static ushort Expand5To16(int x) =>
            (ushort)(x * Quantum.Max / 31);

        public static byte FindClosestPaletteIndex(MagickColor[] palette, IMagickColor<ushort> color)
        {
            var bestIndex = 0;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < palette.Length; i++)
            {
                var p = palette[i];
                var dr = (long)p.R - color.R;
                var dg = (long)p.G - color.G;
                var db = (long)p.B - color.B;
                var da = (long)p.A - color.A;
                var distance = (dr * dr) + (dg * dg) + (db * db) + (da * da);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return (byte)bestIndex;
        }
    }
}
