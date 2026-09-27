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
                    return ColorTheme.Standard;
                }

                // Legacy Nubank maps to new Amethyst
                if (string.Equals(str, "Nubank", StringComparison.OrdinalIgnoreCase))
                {
                    return ColorTheme.Amethyst;
                }

                // Amethyst (legacy or new) safely resolves to Amethyst (with Nubank palette)
                if (string.Equals(str, "Amethyst", StringComparison.OrdinalIgnoreCase))
                {
                    return ColorTheme.Amethyst;
                }

                if (Enum.TryParse<ColorTheme>(str, true, out var result))
                {
                    return result;
                }

                return ColorTheme.Standard;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                int val = reader.GetInt32();
                if (Enum.IsDefined(typeof(ColorTheme), val))
                {
                    return (ColorTheme)val;
                }
                return ColorTheme.Standard;
            }

            return ColorTheme.Standard;
        }

        public override void Write(Utf8JsonWriter writer, ColorTheme value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
