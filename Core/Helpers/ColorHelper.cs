using System;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace GlowBorder.Core.Helpers
{
    public static class ColorHelper
    {
        private static readonly Regex RgbRegex = new Regex(@"rgb\s*\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static Color ParseColor(string input, string fallbackHex = "#5865F2")
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return GetFallbackColor(fallbackHex);
            }

            string trimmed = input.Trim();

            // Handle rgb(r, g, b) format
            var rgbMatch = RgbRegex.Match(trimmed);
            if (rgbMatch.Success)
            {
                if (byte.TryParse(rgbMatch.Groups[1].Value, out byte r) &&
                    byte.TryParse(rgbMatch.Groups[2].Value, out byte g) &&
                    byte.TryParse(rgbMatch.Groups[3].Value, out byte b))
                {
                    return Color.FromRgb(r, g, b);
                }
            }

            // Handle HEX format
            string hexCandidate = trimmed;
            if (!hexCandidate.StartsWith("#"))
            {
                hexCandidate = "#" + hexCandidate;
            }

            // Valid hex lengths: #RGB (4), #RRGGBB (7), #AARRGGBB (9)
            if (hexCandidate.Length == 7 || hexCandidate.Length == 9 || hexCandidate.Length == 4)
            {
                try
                {
                    var converted = ColorConverter.ConvertFromString(hexCandidate);
                    if (converted is Color color)
                    {
                        return color;
                    }
                }
                catch
                {
                    // Fallthrough to fallback
                }
            }

            return GetFallbackColor(fallbackHex);
        }

        public static string ToCanonicalHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static Color GetFallbackColor(string fallbackHex)
        {
            try
            {
                var converted = ColorConverter.ConvertFromString(fallbackHex);
                if (converted is Color color) return color;
            }
            catch
            {
                // Fallback to default Discord purple
            }
            return Color.FromRgb(88, 101, 242);
        }
    }
}
