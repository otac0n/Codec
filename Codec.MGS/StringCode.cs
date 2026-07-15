namespace Codec.MGS
{
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    internal static partial class StringCode
    {
        public static ushort Hash16(string s)
        {
            ushort h = 0;

            for (var c = 0; c < s.Length; c++)
            {
                h = (ushort)(((h << 0x05) | (h >> 0x0B)) + s[c]);
            }

            return h;
        }

        public static uint Hash24(string s)
        {
            var h = 0U;

            for (var c = 0; c < s.Length; c++)
            {
                h = ((h << 0x05) | (h >> 0x13)) + s[c];
                h &= 0xffffff;
            }

            return h;
        }

        public static ulong GetStrCode16(string filename)
        {
            filename = Path.GetFileNameWithoutExtension(filename);
            if (Hex16PrefixRegex().Match(filename) is { Success: true } match)
            {
                return ulong.Parse(match.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return Hash16(filename);
        }

        public static ulong GetStrCode24(string filename)
        {
            filename = Path.GetFileNameWithoutExtension(filename);
            if (Hex24PrefixRegex().Match(filename) is { Success: true } match)
            {
                return ulong.Parse(match.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return Hash24(filename);
        }

        [GeneratedRegex(@"^[a-f0-9]{4}(?=_|\.|$)")]
        private static partial Regex Hex16PrefixRegex();

        [GeneratedRegex(@"^[a-f0-9]{6,8}(?=_|\.|$)")]
        private static partial Regex Hex24PrefixRegex();
    }
}
