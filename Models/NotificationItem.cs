using System;

namespace GlowBorder.Models
{
    public class NotificationItem
    {
        public string AppId { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
