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
}
