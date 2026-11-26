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
        public bool IsExternallyOwnedByWorker { get; private set; } = false;
        public bool AutoStartPolling { get; set; } = true;
        public UserNotificationListenerAccessStatus CurrentAccessStatus { get; private set; } = UserNotificationListenerAccessStatus.Unspecified;

        public NotificationService() : this(new WindowsNotificationReader())
        {
        }

        public NotificationService(INotificationReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public void EnableExternalWorkerOwnership(UserNotificationListenerAccessStatus initialStatus = UserNotificationListenerAccessStatus.Allowed)
        {
            Stop();
            IsExternallyOwnedByWorker = true;
            CurrentAccessStatus = initialStatus;
            AccessStatusChanged?.Invoke(this, CurrentAccessStatus);
            LoggerService.LogInfo("NotificationService ownership delegated to Rust Background Worker (local C# listener disabled).");
        }

        public void DisableExternalWorkerOwnership()
        {
            IsExternallyOwnedByWorker = false;
        }

        public void UpdateExternalAccessStatus(UserNotificationListenerAccessStatus status)
        {
            if (CurrentAccessStatus != status)
            {
                CurrentAccessStatus = status;
                AccessStatusChanged?.Invoke(this, CurrentAccessStatus);
            }
        }

        public void ForwardExternalNotification(NotificationItem item)
        {
            if (item == null) return;
            NotificationReceived?.Invoke(this, item);
        }

        public async Task InitializeAsync()
        {
            if (IsExternallyOwnedByWorker)
            {
                return;
            }

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
            if (_isListening || IsExternallyOwnedByWorker) return;

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
                List<RawNotificationData>? newNotifications = null;

                lock (_lock)
                {
                    int count = currentNotifications.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var n = currentNotifications[i];
                        if (_processedNotificationIds.Add(n.NotificationId))
                        {
                            newNotifications ??= new List<RawNotificationData>(4);
                            newNotifications.Add(n);
                        }
                    }

                    // Keep processed IDs collection bounded
                    if (_processedNotificationIds.Count > 500)
                    {
                        var activeIds = new HashSet<uint>(count);
                        for (int i = 0; i < count; i++) activeIds.Add(currentNotifications[i].NotificationId);

                        _processedNotificationIds.RemoveWhere(id => !activeIds.Contains(id));
                    }
                }

                if (newNotifications == null)
                {
                    return 0;
                }

                for (int i = 0; i < newNotifications.Count; i++)
                {
                    ProcessNewNotification(newNotifications[i]);
                }

                return newNotifications.Count;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error executing PollOnceAsync", ex);
                return 0;
            }
        }

        private async Task PollingLoopAsync(int baseIntervalMs, CancellationToken ct)
        {
            int consecutiveEmptyPolls = 0;

            while (!ct.IsCancellationRequested)
            {
                int newCount = 0;
                try
                {
                    newCount = await PollOnceAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Error in notification polling loop", ex);
                }

                int currentIntervalMs = baseIntervalMs;
                if (baseIntervalMs < 1000)
                {
                    if (newCount > 0)
                    {
                        consecutiveEmptyPolls = 0;
                        currentIntervalMs = baseIntervalMs;
                    }
                    else
                    {
                        if (consecutiveEmptyPolls < 50)
                        {
                            consecutiveEmptyPolls++;
                        }

                        if (consecutiveEmptyPolls > 25)
                        {
                            currentIntervalMs = Math.Min(400, baseIntervalMs * 2);
                        }
                        else if (consecutiveEmptyPolls > 10)
                        {
                            currentIntervalMs = (int)(baseIntervalMs * 1.5);
                        }
                    }
                }

                try
                {
                    await Task.Delay(currentIntervalMs, ct);
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
