namespace Codec.MGS
{
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    internal static partial class StringCode
    {
        public static ulong Hash(string s)
        {
            var h = 0UL;

            for (var c = 0; c < s.Length; c++)
            {
                h = ((h << 0x05) | (h >> 0x13)) + s[c];
                h &= 0xffffff;
            }

            return h;
        }

        public static ulong GetStrCode(string filename)
        {
            filename = Path.GetFileNameWithoutExtension(filename);
            if (HexPrefixRegex().Match(filename) is { Success: true } match)
            {
                return ulong.Parse(match.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return Hash(filename);
        }

        [GeneratedRegex(@"^[a-f0-9]{8}(?=_|\.|$)")]
        private static partial Regex HexPrefixRegex();
    }
}
