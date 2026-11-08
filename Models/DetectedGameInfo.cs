namespace NotiGlow.Models
{
    public class DetectedGameInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string Launcher { get; set; } = string.Empty;
        public string? AppId { get; set; }
        public string? IconPath { get; set; }
        public string? AumId { get; set; }
    }
}
