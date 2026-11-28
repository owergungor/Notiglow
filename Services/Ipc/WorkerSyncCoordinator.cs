using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications.Management;
using NotiGlow.Models;

namespace NotiGlow.Services.Ipc
{
    /// <summary>
    /// Coordinates Phase 3C synchronization between NotiGlow.UI (single settings writer)
    /// and NotiGlow.Background (Rust tray & notification service owner).
    /// </summary>
    public sealed class WorkerSyncCoordinator : IDisposable
    {
        private readonly WorkerSupervisor _supervisor;
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationService;
        private readonly GameDetectionService? _gameDetectionService;
        private readonly Action? _onOpenUi;
        private readonly Action? _onOpenUpdates;
        private readonly Action? _onTestAnimation;
        private readonly Action? _onExitApp;

        private CancellationTokenSource? _pumpCts;
        private Task? _pumpTask;
        private long _clientRevision = 0;
        private long _lastNotifSeq = 0;
        private long _lastTraySeq = 0;
        private long _lastGameSeq = 0;
        private long _lastGlowSeq = 0;
        private int _lastSeenWorkerPid = 0;
        private int _suppressSettingsBroadcast = 0;
        private bool _pendingClientSync = false;
        private bool _disposed = false;

        public long LastAcknowledgedNotificationSeq => Interlocked.Read(ref _lastNotifSeq);
        public long LastAcknowledgedTraySeq => Interlocked.Read(ref _lastTraySeq);
        public long LastAcknowledgedGameSeq => Interlocked.Read(ref _lastGameSeq);
        public long LastAcknowledgedGlowSeq => Interlocked.Read(ref _lastGlowSeq);
        public bool HasPendingClientSync => _pendingClientSync;

        public event EventHandler<WorkerGameEventDto>? GameEventReceived;
        public event EventHandler<WorkerGlowEventDto>? GlowEventReceived;

        public WorkerSyncCoordinator(
            WorkerSupervisor supervisor,
            SettingsService settingsService,
            NotificationService notificationService,
            Action? onOpenUi = null,
            Action? onOpenUpdates = null,
            Action? onTestAnimation = null,
            Action? onExitApp = null,
            GameDetectionService? gameDetectionService = null)
        {
            _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _gameDetectionService = gameDetectionService;
            _onOpenUi = onOpenUi;
            _onOpenUpdates = onOpenUpdates;
            _onTestAnimation = onTestAnimation;
            _onExitApp = onExitApp;

            _supervisor.WorkerRestarted += OnWorkerRestarted;
        }

        private void OnWorkerRestarted(object? sender, int newPid)
        {
            // Reset sequence cursors for the new worker instance and flush any pending client sync
            Interlocked.Exchange(ref _lastNotifSeq, 0);
            Interlocked.Exchange(ref _lastTraySeq, 0);
            Interlocked.Exchange(ref _lastGameSeq, 0);
            Interlocked.Exchange(ref _lastGlowSeq, 0);
            _lastSeenWorkerPid = newPid;
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_pendingClientSync)
                    {
                        await BroadcastLocalSettingsChangeAsync().ConfigureAwait(false);
                    }
                    await SyncPendingStateFromWorkerAsync().ConfigureAwait(false);
                }
                catch { }
            });
        }

        public static UserNotificationListenerAccessStatus ParseAccessStatus(string? access) =>
            access?.Trim().ToLowerInvariant() switch
            {
                "allowed" => UserNotificationListenerAccessStatus.Allowed,
                "denied" => UserNotificationListenerAccessStatus.Denied,
                "unknown" => UserNotificationListenerAccessStatus.Unspecified,
                _ => UserNotificationListenerAccessStatus.Unspecified
            };

        public bool IsSuppressingSettingsBroadcast => Volatile.Read(ref _suppressSettingsBroadcast) > 0;

        public async Task<bool> BroadcastLocalSettingsChangeAsync(CancellationToken ct = default)
        {
            if (IsSuppressingSettingsBroadcast) return true;

            long rev = Interlocked.Increment(ref _clientRevision);
            var res = await _supervisor.IpcClient
                .NotifySettingsChangedAsync(rev, clearPendingUiSync: false, timeoutMs: 1500, cancellationToken: ct)
                .ConfigureAwait(false);

            if (res is { Reloaded: true } or { StaleIgnored: true })
            {
                _pendingClientSync = false;
                return true;
            }

            // Remember to flush when IPC reconnects
            _pendingClientSync = true;
            return false;
        }

        /// <summary>
        /// Pulls worker status, updates UI notification permission indicator, and atomically persists
        /// any pending tray toggles (MasterEnabled / GamingModeEnabled) made while UI was closed.
        /// </summary>
        public async Task<bool> SyncPendingStateFromWorkerAsync(CancellationToken ct = default)
        {
            var status = await _supervisor.IpcClient.GetStatusAsync(1500, ct).ConfigureAwait(false);
            if (status == null)
            {
                return false;
            }

            if (status.Pid != _lastSeenWorkerPid && status.Pid > 0)
            {
                if (_lastSeenWorkerPid != 0)
                {
                    Interlocked.Exchange(ref _lastNotifSeq, 0);
                    Interlocked.Exchange(ref _lastTraySeq, 0);
                    Interlocked.Exchange(ref _lastGameSeq, 0);
                    Interlocked.Exchange(ref _lastGlowSeq, 0);
                }
                _lastSeenWorkerPid = status.Pid;
            }

            _notificationService.UpdateExternalAccessStatus(ParseAccessStatus(status.NotifAccess));
            if (_gameDetectionService != null && _gameDetectionService.IsExternalWorkerOwned)
            {
                _gameDetectionService.UpdateExternalActiveGame(
                    status.ActiveGameInfo?.Name ?? status.ActiveGame,
                    status.ActiveGameInfo?.Identifier);
            }

            if (!status.PendingUiSync)
            {
                return false;
            }

            bool modified = false;
            var current = _settingsService.Current;
            if (status.PendingMasterEnabled.HasValue && current.MasterEnabled != status.PendingMasterEnabled.Value)
            {
                current.MasterEnabled = status.PendingMasterEnabled.Value;
                modified = true;
            }
            if (status.PendingGamingMode.HasValue && current.GamingModeEnabled != status.PendingGamingMode.Value)
            {
                current.GamingModeEnabled = status.PendingGamingMode.Value;
                modified = true;
            }

            Interlocked.Increment(ref _suppressSettingsBroadcast);
            try
            {
                if (modified)
                {
                    _settingsService.Save(current);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _suppressSettingsBroadcast);
            }

            long rev = Interlocked.Increment(ref _clientRevision);
            var ack = await _supervisor.IpcClient
                .NotifySettingsChangedAsync(rev, clearPendingUiSync: true, timeoutMs: 2000, cancellationToken: ct)
                .ConfigureAwait(false);

            return ack is { Reloaded: true };
        }

        public async Task<int> PumpEventsOnceAsync(CancellationToken ct = default)
        {
            int processed = 0;

            if (_pendingClientSync)
            {
                await BroadcastLocalSettingsChangeAsync(ct).ConfigureAwait(false);
            }

            long curNotifAck = Interlocked.Read(ref _lastNotifSeq);
            long curTrayAck = Interlocked.Read(ref _lastTraySeq);
            long curGameAck = Interlocked.Read(ref _lastGameSeq);
            long curGlowAck = Interlocked.Read(ref _lastGlowSeq);
            bool includeGame = _settingsService.Current.UseRustGameDetection;
            bool includeGlow = _settingsService.Current.UseRustGlowEngine;

            // Try single batched POLL_EVENTS round-trip (reduces 4 pipe handshakes to 1);
            // gracefully falls back to individual commands for legacy workers or mock clients.
            WorkerNotificationBatchData? notifBatch;
            WorkerTrayActionBatchData? trayBatch;
            WorkerGameStatusData? gameStatus = null;
            WorkerGlowStatusData? glowStatus = null;

            var batched = await _supervisor.IpcClient
                .PollEventsAsync(curNotifAck, curTrayAck, curGameAck, curGlowAck, includeGame, includeGlow, maxCount: 32, timeoutMs: 1200, cancellationToken: ct)
                .ConfigureAwait(false);

            if (batched != null)
            {
                notifBatch = batched.Notif;
                trayBatch = batched.Tray;
                gameStatus = batched.Game;
                glowStatus = batched.Glow;
            }
            else
            {
                notifBatch = await _supervisor.IpcClient
                    .DrainNotificationEventsAsync(curNotifAck, maxCount: 32, timeoutMs: 1200, cancellationToken: ct)
                    .ConfigureAwait(false);
                trayBatch = await _supervisor.IpcClient
                    .DrainTrayActionsAsync(curTrayAck, timeoutMs: 1200, cancellationToken: ct)
                    .ConfigureAwait(false);
                if (includeGame)
                {
                    gameStatus = await _supervisor.IpcClient
                        .GetGameStatusAsync(curGameAck, maxCount: 32, timeoutMs: 1200, cancellationToken: ct)
                        .ConfigureAwait(false);
                }
                if (includeGlow)
                {
                    glowStatus = await _supervisor.IpcClient
                        .GetGlowStatusAsync(curGlowAck, maxCount: 32, timeoutMs: 1200, cancellationToken: ct)
                        .ConfigureAwait(false);
                }
            }

            // 1. Process notification events with monotonic sequence acknowledgment
            if (notifBatch != null && notifBatch.Events.Count > 0)
            {
                foreach (var ev in notifBatch.Events)
                {
                    if (ev.Seq <= curNotifAck) continue;
                    curNotifAck = ev.Seq;

                    if (string.Equals(ev.Kind, "AccessChanged", StringComparison.OrdinalIgnoreCase))
                    {
                        _notificationService.UpdateExternalAccessStatus(ParseAccessStatus(ev.AppId));
                        processed++;
                    }
                    else if (string.Equals(ev.Kind, "Added", StringComparison.OrdinalIgnoreCase))
                    {
                        var ts = ev.TimestampUnixMs > 0
                            ? DateTimeOffset.FromUnixTimeMilliseconds(ev.TimestampUnixMs).LocalDateTime
                            : DateTime.Now;
                        _notificationService.ForwardExternalNotification(new NotificationItem
                        {
                            AppId = ev.AppId,
                            AppName = ev.AppName,
                            Title = string.Empty,
                            Timestamp = ts
                        });
                        processed++;
                    }
                }
                Interlocked.Exchange(ref _lastNotifSeq, Math.Max(curNotifAck, notifBatch.LatestSeq));
            }

            // 2. Process tray actions
            if (trayBatch != null && trayBatch.Actions.Count > 0)
            {
                bool needsPendingSync = false;
                foreach (var act in trayBatch.Actions)
                {
                    if (act.Seq <= curTrayAck) continue;
                    curTrayAck = act.Seq;

                    switch (act.Action)
                    {
                        case "MasterToggled":
                        case "GamingToggled":
                            needsPendingSync = true;
                            break;
                        case "TestAnimation":
                            _onTestAnimation?.Invoke();
                            break;
                        case "OpenUpdates":
                            _onOpenUpdates?.Invoke();
                            break;
                        case "OpenUi":
                            _onOpenUi?.Invoke();
                            break;
                        case "Exit":
                            _onExitApp?.Invoke();
                            break;
                    }
                    processed++;
                }
                Interlocked.Exchange(ref _lastTraySeq, Math.Max(curTrayAck, trayBatch.LatestSeq));

                if (needsPendingSync)
                {
                    await SyncPendingStateFromWorkerAsync(ct).ConfigureAwait(false);
                }
            }

            // 3. Process Phase 3D game detection events when Rust Game Detection is enabled
            if (includeGame && gameStatus != null)
            {
                if (_gameDetectionService != null && _gameDetectionService.IsExternalWorkerOwned)
                {
                    _gameDetectionService.UpdateExternalActiveGame(
                        gameStatus.ActiveGame?.Name,
                        gameStatus.ActiveGame?.Identifier);
                }

                if (gameStatus.Events.Count > 0)
                {
                    foreach (var ev in gameStatus.Events)
                    {
                        if (ev.Seq <= curGameAck) continue;
                        curGameAck = ev.Seq;
                        GameEventReceived?.Invoke(this, ev);
                        processed++;
                    }
                    Interlocked.Exchange(ref _lastGameSeq, Math.Max(curGameAck, gameStatus.LatestSeq));
                }
            }

            // 4. Process Phase 3D glow & monitor events when Rust Glow Engine is enabled
            if (includeGlow && glowStatus != null && glowStatus.Events.Count > 0)
            {
                foreach (var ev in glowStatus.Events)
                {
                    if (ev.Seq <= curGlowAck) continue;
                    curGlowAck = ev.Seq;
                    GlowEventReceived?.Invoke(this, ev);
                    processed++;
                }
                Interlocked.Exchange(ref _lastGlowSeq, Math.Max(curGlowAck, glowStatus.LatestSeq));
            }

            return processed;
        }

        public void StartEventPump(int intervalMs = 350)
        {
            if (_pumpTask != null) return;
            _pumpCts = new CancellationTokenSource();
            var ct = _pumpCts.Token;
            int minInterval = Math.Max(150, intervalMs);
            int maxIdleInterval = Math.Max(minInterval, 1000);
            _pumpTask = Task.Run(async () =>
            {
                int emptyPolls = 0;
                while (!ct.IsCancellationRequested)
                {
                    int processed = 0;
                    try
                    {
                        if (_supervisor.State == WorkerHealthState.Healthy)
                        {
                            processed = await PumpEventsOnceAsync(ct).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch { }

                    emptyPolls = processed > 0 ? 0 : Math.Min(emptyPolls + 1, 10);
                    int delayMs = emptyPolls >= 5 ? maxIdleInterval : minInterval;

                    try
                    {
                        await Task.Delay(delayMs, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, ct);
        }

        public void StopEventPump()
        {
            try
            {
                _pumpCts?.Cancel();
                _pumpCts?.Dispose();
            }
            catch { }
            _pumpCts = null;
            _pumpTask = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _supervisor.WorkerRestarted -= OnWorkerRestarted;
            StopEventPump();
        }
    }
}
