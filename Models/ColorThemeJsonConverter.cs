using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotiGlow.Models
{
    public class ColorThemeJsonConverter : JsonConverter<ColorTheme>
    {
        public override ColorTheme Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    return ColorTheme.Windows;
                }

                string trimmed = str.Trim();

                // Windows / System
                if (string.Equals(trimmed, "Windows", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, "System", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, "System Accent", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, "SystemAccent", StringComparison.OrdinalIgnoreCase))
                {
                    return ColorTheme.Windows;
                }

                // Standard / Vercel legacy compatibility
                if (string.Equals(trimmed, "Standard", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, "Vercel", StringComparison.OrdinalIgnoreCase))
                {
                    return ColorTheme.Vercel;
                }

                // Legacy Nubank
                if (string.Equals(trimmed, "Nubank", StringComparison.OrdinalIgnoreCase))
                {
                    return ColorTheme.Amethyst;
                }

                // Legacy Indigo completely removed -> migrate to Windows
                if (string.Equals(trimmed, "Indigo", StringComparison.OrdinalIgnoreCase))
                {
                    return ColorTheme.Windows;
                }

                if (Enum.TryParse<ColorTheme>(trimmed, true, out var result))
                {
                    return result;
                }

                return ColorTheme.Windows;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                int val = reader.GetInt32();
                if (val == 0)
                {
                    // Legacy v1.x/v2.x numeric 0 was Standard
                    return ColorTheme.Standard;
                }
                if (Enum.IsDefined(typeof(ColorTheme), val))
                {
                    return (ColorTheme)val;
                }
                return ColorTheme.Windows;
            }

            return ColorTheme.Windows;
        }

        public override void Write(Utf8JsonWriter writer, ColorTheme value, JsonSerializerOptions options)
        {
            if (value == ColorTheme.Windows)
            {
                writer.WriteStringValue("Windows");
            }
            else if (value == ColorTheme.Standard)
            {
                writer.WriteStringValue("Vercel");
            }
            else
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
