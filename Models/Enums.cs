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
                if (string.Equals(str, "Startup", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(str, "OnStartup", StringComparison.OrdinalIgnoreCase))
                {
                    return UpdateCheckFrequency.OnStartup;
                }
                if (Enum.TryParse<UpdateCheckFrequency>(str, true, out var parsed))
                {
                    return parsed;
                }
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
