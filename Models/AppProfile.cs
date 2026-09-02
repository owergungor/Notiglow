using System.Text.Json.Serialization;

namespace GlowBorder.Models
{
    public class AppProfile
    {
        public string AppId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public string ColorHex { get; set; } = "#5865F2";
        public int DurationMs { get; set; } = 4000;
        public double Intensity { get; set; } = 0.8; // 0.0 to 1.0
        public double Thickness { get; set; } = 4.0; // 1 to 10 px
        public double GlowSize { get; set; } = 30.0; // 5 to 100 px
        public GlowStyle Style { get; set; } = GlowStyle.Pulse;
        public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

        // Advanced tuning parameters
        public double Speed { get; set; } = 1.0; // 0.5 to 2.0 multiplier
        public double CoreBrightness { get; set; } = 0.9; // 0.1 to 1.0
        public double TrailLength { get; set; } = 40.0; // 10 to 100 px

        [JsonIgnore]
        public string FormattedDuration => $"{DurationMs / 1000.0:0.#}s";

        [JsonIgnore]
        public string FormattedIntensity => $"{(int)(Intensity * 100)}%";

        public AppProfile Clone(string newNameSuffix = " (Copy)")
        {
            return new AppProfile
            {
                AppId = $"{AppId}_copy_{System.Guid.NewGuid().ToString().Substring(0, 4)}",
                Name = $"{Name}{newNameSuffix}",
                Enabled = Enabled,
                ColorHex = ColorHex,
                DurationMs = DurationMs,
                Intensity = Intensity,
                Thickness = Thickness,
                GlowSize = GlowSize,
                Style = Style,
                Priority = Priority,
                Speed = Speed,
                CoreBrightness = CoreBrightness,
                TrailLength = TrailLength
            };
        }
    }
}
