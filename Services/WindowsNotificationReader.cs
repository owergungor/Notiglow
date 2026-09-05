using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace NotiGlow.Services
{
    public class WindowsNotificationReader : INotificationReader
    {
        private UserNotificationListener? _listener;
        private Action? _notificationChangedCallback;

        public async Task<UserNotificationListenerAccessStatus> RequestAccessAsync()
        {
            if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
            {
                LoggerService.LogError("UserNotificationListener API is not supported on this system.");
                return UserNotificationListenerAccessStatus.Unspecified;
            }

            try
            {
                _listener = UserNotificationListener.Current;
                return await _listener.RequestAccessAsync();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed requesting UserNotificationListener access", ex);
                return UserNotificationListenerAccessStatus.Unspecified;
            }
        }

        public async Task<IReadOnlyList<RawNotificationData>> GetCurrentNotificationsAsync()
        {
            if (_listener == null)
            {
                if (ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
                {
                    try
                    {
                        _listener = UserNotificationListener.Current;
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError("Failed to acquire UserNotificationListener.Current", ex);
                        return Array.Empty<RawNotificationData>();
                    }
                }
                else
                {
                    return Array.Empty<RawNotificationData>();
                }
            }

            try
            {
                var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                var list = new List<RawNotificationData>(notifications.Count);

                foreach (var n in notifications)
                {
                    string appId = "UnknownApp";
                    string appName = "UnknownApp";

                    try
                    {
                        var appInfo = n.AppInfo;
                        if (appInfo != null)
                        {
                            try { appId = appInfo.AppUserModelId ?? appInfo.Id ?? "UnknownApp"; } catch { }
                            try { appName = appInfo.DisplayInfo?.DisplayName ?? appId; } catch { }
                        }
                    }
                    catch
                    {
                        // n.AppInfo can throw NotImplementedException in unpackaged apps for unregistered notifications
                    }

                    string title = "";
                    try
                    {
                        var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                        if (binding != null)
                        {
                            var textElements = binding.GetTextElements();
                            if (textElements != null && textElements.Count > 0)
                            {
                                title = textElements[0].Text ?? "";
                            }
                        }
                    }
                    catch
                    {
                        // Ignore text extraction errors for privacy/stability
                    }

                    DateTime creationTime = DateTime.Now;
                    try
                    {
                        creationTime = n.CreationTime.DateTime;
                    }
                    catch { }

                    list.Add(new RawNotificationData
                    {
                        NotificationId = n.Id,
                        AppId = appId,
                        AppName = appName,
                        Title = title,
                        CreationTime = creationTime
                    });
                }

                return list;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to get notifications from UserNotificationListener", ex);
                return Array.Empty<RawNotificationData>();
            }
        }

        public bool TrySubscribeNotificationChanged(Action onNotificationChanged)
        {
            if (_listener == null) return false;

            try
            {
                _notificationChangedCallback = onNotificationChanged;
                _listener.NotificationChanged += OnInternalNotificationChanged;
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Native NotificationChanged event subscription not available in this process context ({ex.Message}). Using active notification monitoring.");
                return false;
            }
        }

        public void UnsubscribeNotificationChanged()
        {
            if (_listener != null && _notificationChangedCallback != null)
            {
                try
                {
                    _listener.NotificationChanged -= OnInternalNotificationChanged;
                }
                catch { }
                _notificationChangedCallback = null;
            }
        }

        private void OnInternalNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
        {
            try
            {
                _notificationChangedCallback?.Invoke();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error in NotificationChanged callback invocation", ex);
            }
        }
    }
}
