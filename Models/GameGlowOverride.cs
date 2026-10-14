using System;

namespace NotiGlow.Models
{
    /// <summary>
    /// Per-game notification glow settings override.
    /// Allows a tracked game executable profile to override Duration, Intensity, and Glow Blur Size.
    /// When a property is null, the game inherits the global settings.
    /// </summary>
    public class GameGlowOverride
    {
        public string GameIdentifier { get; set; } = string.Empty;
        public int? DurationMs { get; set; }
        public double? Intensity { get; set; }
        public double? GlowSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasOverrides => DurationMs.HasValue || Intensity.HasValue || GlowSize.HasValue;

        public static int ClampDuration(int durationMs) => Math.Clamp(durationMs, 500, 15000);
        public static double ClampIntensity(double intensity) => Math.Clamp(intensity, 0.1, 1.0);
        public static double ClampGlowSize(double glowSize) => Math.Clamp(glowSize, 5.0, 100.0);

        public void ValidateAndClamp()
        {
            if (DurationMs.HasValue) DurationMs = ClampDuration(DurationMs.Value);
            if (Intensity.HasValue) Intensity = ClampIntensity(Intensity.Value);
            if (GlowSize.HasValue) GlowSize = ClampGlowSize(GlowSize.Value);
        }
    }
}
