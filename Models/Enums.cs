using System.Text.Json;
using System.Text.Json.Serialization;

namespace NotiGlow.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum GlowStyle
    {
        Pulse,
        Sweep,
        Ambient,
        Comet,
        Ripple
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NotificationPriority
    {
        Low,
        Normal,
        High
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MonitorMode
    {
        ActiveMonitor,
        PrimaryMonitor,
        AllMonitors
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BurstMode
    {
        Restart,
        Extend,
        Queue,
        Ignore
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AppTheme
    {
        Dark,
        Light,
        System,
        LiquidGlass
    }

    [JsonConverter(typeof(ThemeModeJsonConverter))]
    public enum ThemeMode
    {
        System,
        Light,
        Dark
    }

    public class ThemeModeJsonConverter : JsonConverter<ThemeMode>
    {
        public override ThemeMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    string normalized = str.Trim();
                    if (string.Equals(normalized, "Liquid", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "LiquidGlass", StringComparison.OrdinalIgnoreCase))
                    {
                        return ThemeMode.Dark;
                    }
                    if (string.Equals(normalized, "Light", StringComparison.OrdinalIgnoreCase))
                    {
                        return ThemeMode.Light;
                    }
                    if (string.Equals(normalized, "Dark", StringComparison.OrdinalIgnoreCase))
                    {
                        return ThemeMode.Dark;
                    }
                    if (string.Equals(normalized, "System", StringComparison.OrdinalIgnoreCase))
                    {
                        return ThemeMode.System;
                    }
                    if (Enum.TryParse<ThemeMode>(normalized, true, out var parsed))
                    {
                        return parsed;
                    }
                }
                return ThemeMode.Dark;
            }
            else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intVal))
            {
                if (Enum.IsDefined(typeof(ThemeMode), intVal))
                    return (ThemeMode)intVal;
            }
            return ThemeMode.Dark;
        }

        public override void Write(Utf8JsonWriter writer, ThemeMode value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApplicationViewMode
    {
        List,
        Grid
    }

    [JsonConverter(typeof(ColorThemeJsonConverter))]
    public enum ColorTheme
    {
        Windows = 0,    // Windows / System Accent Color (Default)
        System = 0,     // Alias for Windows System Accent
        Blue = 1,       // Clean Blue (formerly Standard / Vercel)
        Purple = 2,     // Clean Purple (formerly Amethyst / Violet)
        Pink = 3,       // Clean Pink (formerly Bubblegum / Sakura)
        Red = 4,        // Clean Red (formerly Burgundy)
        Orange = 5,     // Clean Orange (formerly Amber / Mocha)
        Yellow = 6,     // Clean Yellow / Gold (formerly Zen)
        Green = 7,      // Clean Green (formerly Nature)
        Teal = 8,       // Clean Teal

        // Backward compatibility aliases for legacy configs & tests:
        Standard = 10,
        Vercel = 10,
        Zen = 12,
        Amber = 13,
        Mocha = 14,
        Burgundy = 15,
        Sakura = 16,
        Bubblegum = 17,
        Amethyst = 18,
        Violet = 19,
        Sapphire = 20,
        Nature = 21
    }

    [JsonConverter(typeof(WindowMaterialJsonConverter))]
    public enum WindowMaterial
    {
        Acrylic,
        Mica,
        MicaAlt
    }

    public class WindowMaterialJsonConverter : JsonConverter<WindowMaterial>
    {
        public override WindowMaterial Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    string normalized = str.Trim();
                    if (string.Equals(normalized, "Transparent", StringComparison.OrdinalIgnoreCase))
                    {
                        // Graceful migration of legacy Transparent setting to default Acrylic
                        return WindowMaterial.Acrylic;
                    }
                    if (string.Equals(normalized, "MicaAlt", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Mica Alt", StringComparison.OrdinalIgnoreCase))
                    {
                        return WindowMaterial.MicaAlt;
                    }
                    if (string.Equals(normalized, "Mica", StringComparison.OrdinalIgnoreCase))
                    {
                        return WindowMaterial.Mica;
                    }
                    if (string.Equals(normalized, "Acrylic", StringComparison.OrdinalIgnoreCase))
                    {
                        return WindowMaterial.Acrylic;
                    }
                    if (Enum.TryParse<WindowMaterial>(normalized, true, out var parsed))
                    {
                        return parsed;
                    }
                }
                return WindowMaterial.Acrylic;
            }
            else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intVal))
            {
                if (Enum.IsDefined(typeof(WindowMaterial), intVal))
                    return (WindowMaterial)intVal;
            }
            return WindowMaterial.Acrylic;
        }

        public override void Write(Utf8JsonWriter writer, WindowMaterial value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    [JsonConverter(typeof(UpdateCheckFrequencyJsonConverter))]
    public enum UpdateCheckFrequency
    {
        OnStartup = 0,
        Startup = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3
    }

    public class UpdateCheckFrequencyJsonConverter : JsonConverter<UpdateCheckFrequency>
    {
        public override UpdateCheckFrequency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string? str = reader.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    string normalized = str.Trim();
                    if (string.Equals(normalized, "Startup", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "OnStartup", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "On Startup", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Açılışta", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Acilista", StringComparison.OrdinalIgnoreCase))
                    {
                        return UpdateCheckFrequency.OnStartup;
                    }
                    if (string.Equals(normalized, "Daily", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Günlük", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Gunluk", StringComparison.OrdinalIgnoreCase))
                    {
                        return UpdateCheckFrequency.Daily;
                    }
                    if (string.Equals(normalized, "Weekly", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Haftalık", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Haftalik", StringComparison.OrdinalIgnoreCase))
                    {
                        return UpdateCheckFrequency.Weekly;
                    }
                    if (string.Equals(normalized, "Monthly", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Aylık", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "Aylik", StringComparison.OrdinalIgnoreCase))
                    {
                        return UpdateCheckFrequency.Monthly;
                    }
                    if (Enum.TryParse<UpdateCheckFrequency>(normalized, true, out var parsed))
                    {
                        return parsed;
                    }
                }
                return UpdateCheckFrequency.OnStartup;
            }
            else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int intVal))
            {
                if (Enum.IsDefined(typeof(UpdateCheckFrequency), intVal))
                    return (UpdateCheckFrequency)intVal;
            }
            return UpdateCheckFrequency.OnStartup;
        }

        public override void Write(Utf8JsonWriter writer, UpdateCheckFrequency value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
