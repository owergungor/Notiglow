using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class NotificationService
    {
        private UserNotificationListener? _listener;
        private bool _isListening = false;
        private readonly HashSet<uint> _processedNotificationIds = new();

        public event EventHandler<NotificationItem>? NotificationReceived;
        public event EventHandler<UserNotificationListenerAccessStatus>? AccessStatusChanged;

        public bool IsListening => _isListening;
        public UserNotificationListenerAccessStatus CurrentAccessStatus { get; private set; } = UserNotificationListenerAccessStatus.Unspecified;

        public async Task InitializeAsync()
        {
            if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
            {
                LoggerService.LogError("UserNotificationListener API is not supported on this system.");
                return;
            }

            try
            {
                _listener = UserNotificationListener.Current;
                CurrentAccessStatus = await _listener.RequestAccessAsync();
                AccessStatusChanged?.Invoke(this, CurrentAccessStatus);

                if (CurrentAccessStatus == UserNotificationListenerAccessStatus.Allowed)
                {
                    StartListening();
                }
                else
                {
                    LoggerService.LogWarning($"UserNotificationListener access status: {CurrentAccessStatus}");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to initialize UserNotificationListener", ex);
            }
        }

        public async Task<UserNotificationListenerAccessStatus> RequestAccessAsync()
        {
            if (_listener == null)
            {
                if (ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
                {
                    _listener = UserNotificationListener.Current;
                }
                else
                {
                    return UserNotificationListenerAccessStatus.Unspecified;
                }
            }

            try
            {
                CurrentAccessStatus = await _listener.RequestAccessAsync();
                AccessStatusChanged?.Invoke(this, CurrentAccessStatus);

                if (CurrentAccessStatus == UserNotificationListenerAccessStatus.Allowed && !_isListening)
                {
                    StartListening();
                }

                return CurrentAccessStatus;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed requesting notification access", ex);
                return UserNotificationListenerAccessStatus.Unspecified;
            }
        }

        public static void OpenWindowsNotificationSettings()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:notifications",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to open Windows Notification Settings", ex);
            }
        }

        private void StartListening()
        {
            if (_listener == null || _isListening) return;

            try
            {
                _listener.NotificationChanged += OnNotificationChanged;
                _isListening = true;
                LoggerService.LogInfo("Started listening to Windows notifications.");
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to subscribe to NotificationChanged", ex);
            }
        }

        public void Stop()
        {
            if (_listener != null && _isListening)
            {
                try
                {
                    _listener.NotificationChanged -= OnNotificationChanged;
                    _isListening = false;
                    LoggerService.LogInfo("Stopped listening to Windows notifications.");
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Error unsubscribing from NotificationChanged", ex);
                }
            }
        }

        private void OnNotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
        {
            try
            {
                var notification = sender.GetNotification(args.UserNotificationId);
                if (notification == null) return;

                lock (_processedNotificationIds)
                {
                    if (_processedNotificationIds.Contains(notification.Id)) return;
                    _processedNotificationIds.Add(notification.Id);

                    if (_processedNotificationIds.Count > 200)
                    {
                        _processedNotificationIds.Clear();
                    }
                }

                string appId = notification.AppInfo?.AppUserModelId ?? notification.AppInfo?.Id ?? "UnknownApp";
                string appName = notification.AppInfo?.DisplayInfo?.DisplayName ?? appId;

                string title = "";
                try
                {
                    var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
                    if (binding != null)
                    {
                        var textElements = binding.GetTextElements();
                        if (textElements != null && textElements.Count > 0)
                        {
                            title = textElements[0].Text;
                        }
                    }
                }
                catch
                {
                    // Ignore text extraction errors for privacy
                }

                var item = new NotificationItem
                {
                    AppId = appId,
                    AppName = appName,
                    Title = title,
                    Timestamp = DateTime.Now
                };

                LoggerService.LogInfo($"Notification Detected: AppId={appId}, AppName={appName}");

                NotificationReceived?.Invoke(this, item);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error handling NotificationChanged event", ex);
            }
        }
    }
}
