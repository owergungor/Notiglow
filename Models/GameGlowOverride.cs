using System;

namespace NotiGlow.Models
{
    /// <summary>
    /// Per-game notification glow settings override.
    /// Allows a tracked game executable profile to override Duration, Intensity, Glow Blur Size, Corner Rounding, and Color.
    /// When CustomGlowEnabled is false, the game inherits the global gaming settings.
    /// </summary>
    public class GameGlowOverride
    {
        private bool? _customGlowEnabled = null;

        public string GameIdentifier { get; set; } = string.Empty;

        public bool CustomGlowEnabled
        {
            get => _customGlowEnabled ?? (DurationMs.HasValue || Intensity.HasValue || GlowSize.HasValue || CornerRadius.HasValue || !string.IsNullOrWhiteSpace(ColorHex));
            set => _customGlowEnabled = value;
        }

        public int? DurationMs { get; set; }
        public double? Intensity { get; set; }
        public double? GlowSize { get; set; }
        public double? CornerRadius { get; set; }
        public string? ColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasOverrides => CustomGlowEnabled || DurationMs.HasValue || Intensity.HasValue || GlowSize.HasValue || CornerRadius.HasValue || !string.IsNullOrWhiteSpace(ColorHex);

        public static int ClampDuration(int durationMs) => Math.Clamp(durationMs, 500, 15000);
        public static double ClampIntensity(double intensity) => Math.Clamp(intensity, 0.1, 1.0);
        public static double ClampGlowSize(double glowSize) => Math.Clamp(glowSize, 0.0, 256.0);
        public static double ClampCornerRadius(double radius) => Math.Clamp(radius, 0.0, 48.0);

        public void ValidateAndClamp()
        {
            if (DurationMs.HasValue) DurationMs = ClampDuration(DurationMs.Value);
            if (Intensity.HasValue) Intensity = ClampIntensity(Intensity.Value);
            if (GlowSize.HasValue) GlowSize = ClampGlowSize(GlowSize.Value);
            if (CornerRadius.HasValue) CornerRadius = ClampCornerRadius(CornerRadius.Value);
        }
    }
}
