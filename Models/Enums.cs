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

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ThemeMode
    {
        System,
        Light,
        Dark
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
        Standard,
        Zen,
        Amber,
        Mocha,
        Burgundy,
        Sakura,
        Bubblegum,
        Amethyst,
        Violet,
        Indigo,
        Sapphire,
        Nature
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
