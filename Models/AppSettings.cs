using System.Collections.Generic;

namespace NotiGlow.Models
{
    public class AppSettings
    {
        public bool MasterEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool ReduceAnimations { get; set; } = false;
        public bool ReduceMotion { get; set; } = false;
        public bool ReduceGlow { get; set; } = false;
        public bool OledMode { get; set; } = false;
        public ThemeMode ThemeMode { get; set; } = ThemeMode.Dark;
        public ColorTheme ColorTheme { get; set; } = ColorTheme.Standard;
        public AppTheme Theme
        {
            get => ThemeMode switch
            {
                ThemeMode.Light => AppTheme.Light,
                ThemeMode.System => AppTheme.System,
                _ => (ColorTheme == ColorTheme.Indigo ? AppTheme.LiquidGlass : AppTheme.Dark)
            };
            set
            {
                if (value == AppTheme.Light)
                {
                    ThemeMode = ThemeMode.Light;
                }
                else if (value == AppTheme.System)
                {
                    ThemeMode = ThemeMode.System;
                }
                else if (value == AppTheme.LiquidGlass)
                {
                    ThemeMode = ThemeMode.Dark;
                    ColorTheme = ColorTheme.Indigo;
                }
                else
                {
                    ThemeMode = ThemeMode.Dark;
                    if (ColorTheme == ColorTheme.Indigo)
                    {
                        ColorTheme = ColorTheme.Standard;
                    }
                }
            }
        }
        public MonitorMode MonitorMode { get; set; } = MonitorMode.ActiveMonitor;
        public BurstMode BurstMode { get; set; } = BurstMode.Restart;
        public bool DebugLogging { get; set; } = true;
        public bool ShowIdentityDebugInfo { get; set; } = false;
        public bool FirstRunCompleted { get; set; } = false;

        // Gaming Mode Settings
        public bool GamingModeEnabled { get; set; } = false;
        public bool GlowDuringGames { get; set; } = true;
        public bool ReduceIntensityInGames { get; set; } = true;
        public double GamingIntensityMultiplier { get; set; } = 0.6; // 60% of original
        public bool ReduceDurationInGames { get; set; } = true;
        public double GamingDurationMultiplier { get; set; } = 0.5; // 50% of original
        public bool OnlyImportantInGames { get; set; } = false;
        public List<string> TrackedGames { get; set; } = new List<string>();
        public List<string> IgnoredGames { get; set; } = new List<string>();

        [System.Text.Json.Serialization.JsonIgnore]
        public bool CanModifyGameSubSettings => GlowDuringGames;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool EffectiveReduceIntensityInGames => GlowDuringGames && ReduceIntensityInGames;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool EffectiveReduceDurationInGames => GlowDuringGames && ReduceDurationInGames;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool EffectiveOnlyImportantInGames => GlowDuringGames && OnlyImportantInGames;

        // Global defaults for new app profiles
        public string DefaultColorHex { get; set; } = "#5865F2";
        public int DefaultDurationMs { get; set; } = 4000;
        public double DefaultIntensity { get; set; } = 0.8;
        public double DefaultThickness { get; set; } = 4.0;
        public double DefaultGlowSize { get; set; } = 30.0;
        public GlowStyle DefaultStyle { get; set; } = GlowStyle.Pulse;

        // View preferences
        public ApplicationViewMode ApplicationsViewMode { get; set; } = ApplicationViewMode.List;

        // Auto Update Settings
        public bool AutoCheckUpdates { get; set; } = true;
        public UpdateCheckFrequency UpdateFrequency { get; set; } = UpdateCheckFrequency.OnStartup;
        public DateTime? LastUpdateCheck { get; set; }
        public string? LastUpdateVersion { get; set; }
    }
}
