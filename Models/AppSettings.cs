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
        public double CornerRadius { get; set; } = 0.0;
        public string AppLanguage { get; set; } = "en-US";
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

        // Shared Gaming Glow Settings (v2.2: Duration, Intensity, Blur Size unified across all games)
        public int? CommonGamingDurationMs { get; set; } = 3000;
        public double? CommonGamingIntensity { get; set; } = 0.8;
        public double? CommonGamingGlowSize { get; set; } = 32.0;
        public double? CommonGamingCornerRadius { get; set; } = 0.0;
        public bool HasCustomCommonGamingSettings { get; set; } = false;

        public Dictionary<string, GameGlowOverride> GameOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [System.Text.Json.Serialization.JsonIgnore]
        public bool CanModifyGameSubSettings => GlowDuringGames;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool EffectiveReduceIntensityInGames => GlowDuringGames && ReduceIntensityInGames;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool EffectiveReduceDurationInGames => GlowDuringGames && ReduceDurationInGames;

        [System.Text.Json.Serialization.JsonIgnore]
        public bool EffectiveOnlyImportantInGames => GlowDuringGames && OnlyImportantInGames;

        public void MigrateLegacyGameOverridesIfNeeded()
        {
            if (!HasCustomCommonGamingSettings && GameOverrides != null && GameOverrides.Count > 0)
            {
                foreach (var kvp in GameOverrides)
                {
                    var ov = kvp.Value;
                    if (ov != null && ov.HasOverrides)
                    {
                        CommonGamingDurationMs = ov.DurationMs.HasValue ? GameGlowOverride.ClampDuration(ov.DurationMs.Value) : null;
                        CommonGamingIntensity = ov.Intensity.HasValue ? GameGlowOverride.ClampIntensity(ov.Intensity.Value) : null;
                        CommonGamingGlowSize = ov.GlowSize.HasValue ? GameGlowOverride.ClampGlowSize(ov.GlowSize.Value) : null;
                        HasCustomCommonGamingSettings = true;
                        break;
                    }
                }
            }
        }

        public void SetCommonGamingSettings(int? durationMs, double? intensity, double? glowSize, double? cornerRadius = null)
        {
            CommonGamingDurationMs = durationMs.HasValue ? GameGlowOverride.ClampDuration(durationMs.Value) : null;
            CommonGamingIntensity = intensity.HasValue ? GameGlowOverride.ClampIntensity(intensity.Value) : null;
            CommonGamingGlowSize = glowSize.HasValue ? GameGlowOverride.ClampGlowSize(glowSize.Value) : null;
            CommonGamingCornerRadius = cornerRadius.HasValue ? GameGlowOverride.ClampCornerRadius(cornerRadius.Value) : null;
            HasCustomCommonGamingSettings = durationMs.HasValue || intensity.HasValue || glowSize.HasValue || cornerRadius.HasValue;

            GameOverrides ??= new Dictionary<string, GameGlowOverride>(StringComparer.OrdinalIgnoreCase);
            var sharedOverride = new GameGlowOverride
            {
                DurationMs = CommonGamingDurationMs,
                Intensity = CommonGamingIntensity,
                GlowSize = CommonGamingGlowSize,
                CornerRadius = CommonGamingCornerRadius,
                CustomGlowEnabled = false
            };
            foreach (var game in TrackedGames)
            {
                string id = NormalizeGameIdentifier(game);
                if (!string.IsNullOrEmpty(id))
                {
                    if (!GameOverrides.TryGetValue(id, out var existing) || !existing.CustomGlowEnabled)
                    {
                        GameOverrides[id] = sharedOverride;
                    }
                }
            }
        }

        public static string NormalizeGameIdentifier(string? gamePathOrName)
        {
            if (string.IsNullOrWhiteSpace(gamePathOrName)) return string.Empty;
            string trimmed = gamePathOrName.Trim();
            string fileName = System.IO.Path.GetFileName(trimmed);
            return !string.IsNullOrEmpty(fileName) ? fileName.ToLowerInvariant() : trimmed.ToLowerInvariant();
        }

        public GameGlowOverride? GetGameOverride(string? gamePathOrName)
        {
            MigrateLegacyGameOverridesIfNeeded();

            string id = NormalizeGameIdentifier(gamePathOrName);
            if (!string.IsNullOrEmpty(id) && GameOverrides != null && GameOverrides.TryGetValue(id, out var ov))
            {
                return ov;
            }

            if (!HasCustomCommonGamingSettings && (GameOverrides == null || GameOverrides.Count == 0))
            {
                return null;
            }

            return new GameGlowOverride
            {
                GameIdentifier = id,
                DurationMs = CommonGamingDurationMs,
                Intensity = CommonGamingIntensity,
                GlowSize = CommonGamingGlowSize,
                CornerRadius = CommonGamingCornerRadius,
                CustomGlowEnabled = false
            };
        }

        public bool HasGameOverride(string? gamePathOrName)
        {
            string id = NormalizeGameIdentifier(gamePathOrName);
            if (string.IsNullOrEmpty(id) || GameOverrides == null) return false;
            return GameOverrides.TryGetValue(id, out var ov) && ov != null && ov.HasOverrides;
        }

        public void RemoveGameOverride(string? gamePathOrName) => SetGameOverride(gamePathOrName, null);

        public void SetGameOverride(string? gamePathOrName, GameGlowOverride? overrideSettings)
        {
            string id = NormalizeGameIdentifier(gamePathOrName);
            if (string.IsNullOrEmpty(id)) return;

            GameOverrides ??= new Dictionary<string, GameGlowOverride>(StringComparer.OrdinalIgnoreCase);
            if (overrideSettings == null)
            {
                GameOverrides.Remove(id);
                return;
            }

            GameOverrides[id] = overrideSettings;
        }

        // Global defaults for new app profiles
        public string DefaultColorHex { get; set; } = "#FFFFFF";
        public int DefaultDurationMs { get; set; } = 4000;
        public double DefaultIntensity { get; set; } = 0.8;
        public double DefaultThickness { get; set; } = 4.0;
        public double DefaultGlowSize { get; set; } = 32.0;
        public GlowStyle DefaultStyle { get; set; } = GlowStyle.Pulse;

        // View preferences
        public ApplicationViewMode ApplicationsViewMode { get; set; } = ApplicationViewMode.List;
        public ApplicationViewMode GamingViewMode { get; set; } = ApplicationViewMode.List;

        // Auto Update Settings
        public bool AutoCheckUpdates { get; set; } = true;
        public UpdateCheckFrequency UpdateFrequency { get; set; } = UpdateCheckFrequency.OnStartup;
        public DateTime? LastUpdateCheck { get; set; }
        public string? LastUpdateVersion { get; set; }
    }
}
