using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Notifications.Management;

namespace NotiGlow.Services
{
    public class RawNotificationData
    {
        public uint NotificationId { get; set; }
        public string AppId { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreationTime { get; set; } = DateTime.Now;
    }

    public interface INotificationReader
    {
        Task<UserNotificationListenerAccessStatus> RequestAccessAsync();
        Task<IReadOnlyList<RawNotificationData>> GetCurrentNotificationsAsync();
        bool TrySubscribeNotificationChanged(Action onNotificationChanged);
        void UnsubscribeNotificationChanged();
    }
}
