namespace GlowBorder.Models
{
    public enum GlowStyle
    {
        Pulse,
        Sweep,
        Ambient,
        Comet,
        Ripple
    }

    public enum NotificationPriority
    {
        Low,
        Normal,
        High
    }

    public enum MonitorMode
    {
        ActiveMonitor,
        PrimaryMonitor,
        AllMonitors
    }

    public enum BurstMode
    {
        Restart,
        Extend,
        Queue,
        Ignore
    }

    public enum AppTheme
    {
        Dark,
        Light,
        System,
        LiquidGlass
    }
}
