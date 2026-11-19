using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Windows.UI;
using Microsoft.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace NotiGlow.Core.Helpers
{
    public static class ColorHelper
    {
        private static readonly Regex RgbRegex = new Regex(@"rgb\s*\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static Color FromRgb(byte r, byte g, byte b)
        {
            return Color.FromArgb(255, r, g, b);
        }

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
                    return Color.FromArgb(255, r, g, b);
                }
            }

            // Handle HEX format
            string hexCandidate = trimmed;
            if (!hexCandidate.StartsWith("#"))
            {
                hexCandidate = "#" + hexCandidate;
            }

            if (TryParseHex(hexCandidate, out Color parsedColor))
            {
                return parsedColor;
            }

            return GetFallbackColor(fallbackHex);
        }

        public static bool TryParseHex(string hex, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            string clean = hex.Trim();
            if (clean.StartsWith("#")) clean = clean.Substring(1);

            try
            {
                if (clean.Length == 3) // RGB (e.g. F05 -> FF0055)
                {
                    byte r = byte.Parse($"{clean[0]}{clean[0]}", NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte g = byte.Parse($"{clean[1]}{clean[1]}", NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte b = byte.Parse($"{clean[2]}{clean[2]}", NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }
                else if (clean.Length == 6) // RRGGBB
                {
                    byte r = byte.Parse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte g = byte.Parse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte b = byte.Parse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }
                else if (clean.Length == 8) // AARRGGBB
                {
                    byte a = byte.Parse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte r = byte.Parse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte g = byte.Parse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    byte b = byte.Parse(clean.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
            }
            catch
            {
                // Parse failure
            }

            return false;
        }

        public static bool IsRgbSpectrum(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var trimmed = input.Trim();
            return string.Equals(trimmed, "RGB", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(trimmed, "#RGB", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(trimmed, "Rainbow", StringComparison.OrdinalIgnoreCase);
        }

        public static readonly Color[] RgbSpectrumColors = new[]
        {
            Color.FromArgb(255, 255, 0, 77),    // Pink/Red
            Color.FromArgb(255, 255, 122, 0),   // Orange
            Color.FromArgb(255, 255, 214, 0),   // Yellow
            Color.FromArgb(255, 0, 230, 118),   // Green
            Color.FromArgb(255, 0, 229, 255),   // Cyan
            Color.FromArgb(255, 41, 121, 255),  // Blue
            Color.FromArgb(255, 124, 58, 237),  // Purple
            Color.FromArgb(255, 255, 0, 128),   // Magenta
            Color.FromArgb(255, 255, 0, 77)     // Loop back to Pink/Red
        };

        public static LinearGradientBrush CreateRainbowLinearBrush(Point startPoint, Point endPoint)
        {
            var brush = new LinearGradientBrush { StartPoint = startPoint, EndPoint = endPoint };
            double step = 1.0 / (RgbSpectrumColors.Length - 1);
            for (int i = 0; i < RgbSpectrumColors.Length; i++)
            {
                brush.GradientStops.Add(new GradientStop { Color = RgbSpectrumColors[i], Offset = i * step });
            }
            return brush;
        }

        public static string ToCanonicalHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static Color GetFallbackColor(string fallbackHex)
        {
            if (TryParseHex(fallbackHex, out Color color))
            {
                return color;
            }
            return Color.FromArgb(255, 88, 101, 242);
        }
    }
}
