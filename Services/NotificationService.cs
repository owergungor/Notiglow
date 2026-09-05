using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications.Management;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class NotificationService
    {
        private readonly INotificationReader _reader;
        private readonly HashSet<uint> _processedNotificationIds = new();
        private readonly object _lock = new();

        private CancellationTokenSource? _pollCts;
        private Task? _pollTask;
        private bool _isListening = false;

        public event EventHandler<NotificationItem>? NotificationReceived;
        public event EventHandler<UserNotificationListenerAccessStatus>? AccessStatusChanged;

        public bool IsListening => _isListening;
        public bool AutoStartPolling { get; set; } = true;
        public UserNotificationListenerAccessStatus CurrentAccessStatus { get; private set; } = UserNotificationListenerAccessStatus.Unspecified;

        public NotificationService() : this(new WindowsNotificationReader())
        {
        }

        public NotificationService(INotificationReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public async Task InitializeAsync()
        {
            try
            {
                CurrentAccessStatus = await _reader.RequestAccessAsync();
                AccessStatusChanged?.Invoke(this, CurrentAccessStatus);

                if (CurrentAccessStatus == UserNotificationListenerAccessStatus.Allowed)
                {
                    await StartListeningAsync();
                }
                else
                {
                    LoggerService.LogWarning($"UserNotificationListener access status: {CurrentAccessStatus}. Windows notifications cannot be captured without permission.");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to initialize NotificationService", ex);
            }
        }

        public async Task<UserNotificationListenerAccessStatus> RequestAccessAsync()
        {
            try
            {
                CurrentAccessStatus = await _reader.RequestAccessAsync();
                AccessStatusChanged?.Invoke(this, CurrentAccessStatus);

                if (CurrentAccessStatus == UserNotificationListenerAccessStatus.Allowed && !_isListening)
                {
                    await StartListeningAsync();
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

        public async Task StartListeningAsync()
        {
            if (_isListening) return;

            try
            {
                // Snapshot existing notifications in Action Center so historical notifications do not fire on startup
                var existing = await _reader.GetCurrentNotificationsAsync();
                lock (_lock)
                {
                    foreach (var notif in existing)
                    {
                        _processedNotificationIds.Add(notif.NotificationId);
                    }
                }

                // Try native WinRT event subscription first (works if packaged/identity present)
                bool eventSubscribed = _reader.TrySubscribeNotificationChanged(OnNativeNotificationChangedTrigger);

                _isListening = true;
                _pollCts = new CancellationTokenSource();

                if (AutoStartPolling)
                {
                    // Start background polling loop
                    // When native event is subscribed, polling runs at a relaxed pace (1000ms) as backup.
                    // When native event is not available (unpackaged app), polling runs at high responsiveness (200ms).
                    int pollIntervalMs = eventSubscribed ? 1000 : 200;
                    _pollTask = Task.Run(() => PollingLoopAsync(pollIntervalMs, _pollCts.Token));

                    LoggerService.LogInfo($"Started listening to Windows notifications (Mode: {(eventSubscribed ? "Event+BackupPoll" : "ActivePoll")}, Interval: {pollIntervalMs}ms).");
                }
                else
                {
                    LoggerService.LogInfo("Started listening to Windows notifications (Mode: ManualPolling).");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to start listening for notifications", ex);
            }
        }

        public void Stop()
        {
            if (!_isListening) return;

            try
            {
                _isListening = false;
                _pollCts?.Cancel();
                _pollCts?.Dispose();
                _pollCts = null;

                _reader.UnsubscribeNotificationChanged();
                LoggerService.LogInfo("Stopped listening to Windows notifications.");
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error stopping NotificationService", ex);
            }
        }

        private void OnNativeNotificationChangedTrigger()
        {
            _ = PollOnceAsync();
        }

        public async Task<int> PollOnceAsync()
        {
            try
            {
                var currentNotifications = await _reader.GetCurrentNotificationsAsync();
                var newNotifications = new List<RawNotificationData>();

                lock (_lock)
                {
                    foreach (var n in currentNotifications)
                    {
                        if (!_processedNotificationIds.Contains(n.NotificationId))
                        {
                            _processedNotificationIds.Add(n.NotificationId);
                            newNotifications.Add(n);
                        }
                    }

                    // Keep processed IDs collection bounded
                    if (_processedNotificationIds.Count > 500)
                    {
                        var activeIds = new HashSet<uint>();
                        foreach (var n in currentNotifications) activeIds.Add(n.NotificationId);

                        _processedNotificationIds.RemoveWhere(id => !activeIds.Contains(id));
                    }
                }

                foreach (var raw in newNotifications)
                {
                    ProcessNewNotification(raw);
                }

                return newNotifications.Count;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error executing PollOnceAsync", ex);
                return 0;
            }
        }

        private async Task PollingLoopAsync(int intervalMs, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Error in notification polling loop", ex);
                }

                try
                {
                    await Task.Delay(intervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void ProcessNewNotification(RawNotificationData raw)
        {
            try
            {
                LoggerService.LogInfo($"Notification detected: ID={raw.NotificationId}");

                string appId = !string.IsNullOrWhiteSpace(raw.AppId) ? raw.AppId : "UnknownApp";
                string appName = !string.IsNullOrWhiteSpace(raw.AppName) ? raw.AppName : appId;
                string title = raw.Title ?? "";

                LoggerService.LogInfo($"Notification app identified: AppId='{appId}', AppName='{appName}', Title='{title}'");

                var item = new NotificationItem
                {
                    AppId = appId,
                    AppName = appName,
                    Title = title,
                    Timestamp = raw.CreationTime
                };

                NotificationReceived?.Invoke(this, item);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Error processing notification ID {raw.NotificationId}", ex);
            }
        }
    }
}
