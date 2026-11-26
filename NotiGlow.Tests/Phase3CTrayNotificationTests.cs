using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.UI.Notifications.Management;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.Services.Ipc;

namespace NotiGlow.Tests
{
    [TestClass]
    public class Phase3CTrayNotificationTests
    {
        private static string ResolveTestWorkerExe()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = Path.Combine(dir, "native", "NotiGlow.Background", "target", "release", "NotiGlow_Background.exe");
                if (File.Exists(candidate)) return candidate;
                dir = Directory.GetParent(dir)?.FullName;
            }
            return string.Empty;
        }

        private static (string PipeName, string MutexName, string TempConfigDir) CreateIsolatedContext()
        {
            string tag = Guid.NewGuid().ToString("N")[..10];
            string pipe = $"NotiGlow_3C_IPC_{tag}";
            string mutex = $"Local\\NotiGlow_3C_Mutex_{tag}";
            string tempDir = Path.Combine(Path.GetTempPath(), $"NotiGlow_Phase3C_Test_{tag}");
            Directory.CreateDirectory(tempDir);
            return (pipe, mutex, tempDir);
        }

        private static void SafeDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task DualRunGuard_ExternalOwnershipBlocksLocalNotificationListener_AndAllowsForwarding()
        {
            int readerCalls = 0;
            var mockReader = new CountingNotificationReader(() => readerCalls++);
            var notifService = new NotificationService(mockReader);

            var received = new List<NotificationItem>();
            notifService.NotificationReceived += (_, item) => received.Add(item);

            notifService.EnableExternalWorkerOwnership(UserNotificationListenerAccessStatus.Allowed);
            Assert.IsTrue(notifService.IsExternallyOwnedByWorker);
            Assert.IsFalse(notifService.IsListening);
            Assert.AreEqual(UserNotificationListenerAccessStatus.Allowed, notifService.CurrentAccessStatus);

            // Attempting InitializeAsync or StartListeningAsync while owned by Rust worker MUST be a no-op!
            await notifService.InitializeAsync();
            await notifService.StartListeningAsync();
            Assert.IsFalse(notifService.IsListening);
            Assert.AreEqual(0, readerCalls, "C# WindowsNotificationReader must never be polled when owned by Rust worker.");

            // Forwarding from WorkerSyncCoordinator still reaches GlowManager subscribers
            notifService.ForwardExternalNotification(new NotificationItem
            {
                AppId = "discord.exe",
                AppName = "Discord",
                Timestamp = DateTime.Now
            });
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual("discord.exe", received[0].AppId);
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task RustTray_ExplorerRestart_AndPendingToggles_NeverWriteDiskUntilUiReopens()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            string settingsFile = Path.Combine(tempDir, "settings.json");

            var initialSettings = new AppSettings { MasterEnabled = true, GamingModeEnabled = false };
            await File.WriteAllTextAsync(settingsFile, JsonSerializer.Serialize(initialSettings));
            string hashBeforeTrayToggle = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(settingsFile)));

            // Start worker in full Phase 3C mode (ipcOnly: false -> Tray + Notification active)
            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: false);
            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));
                var st0 = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(st0);
                Assert.IsFalse(st0!.IpcOnly);

                // 1. Simulate Explorer restart (WM_TASKBAR_CREATED)
                var tbResp = await sup.IpcClient.ExecuteTrayActionAsync("SimulateTaskbarCreated");
                Assert.IsTrue(tbResp.Ok);
                await Task.Delay(120);
                var stAfterTb = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(stAfterTb);
                Assert.IsTrue(stAfterTb!.TaskbarRestores >= 1, "TaskbarCreated must restore tray icon.");

                // 2. While UI is closed, toggle MasterEnabled -> false and GamingMode -> true from Tray!
                var tMaster = await sup.IpcClient.ExecuteTrayActionAsync("ToggleMaster");
                var tGaming = await sup.IpcClient.ExecuteTrayActionAsync("ToggleGaming");
                Assert.IsTrue(tMaster.Ok);
                Assert.IsTrue(tGaming.Ok);

                // Verify worker holds pending changes in memory AND settings.json on disk is 100% UNCHANGED!
                string hashWhileUiClosed = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(settingsFile)));
                Assert.AreEqual(hashBeforeTrayToggle, hashWhileUiClosed, "Rust worker must NEVER write directly to settings.json!");

                var stPending = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(stPending);
                Assert.IsTrue(stPending!.PendingUiSync);
                Assert.AreEqual(false, stPending.PendingMasterEnabled);
                Assert.AreEqual(true, stPending.PendingGamingMode);
                Assert.IsFalse(stPending.MasterEnabled);
                Assert.IsTrue(stPending.GamingModeEnabled);

                // 3. Now UI opens -> SettingsService + WorkerSyncCoordinator atomically saves pending changes to disk!
                var settingsService = new SettingsService(settingsFile);
                var notifService = new NotificationService(new CountingNotificationReader(() => { }));
                notifService.EnableExternalWorkerOwnership();

                using var coordinator = new WorkerSyncCoordinator(sup, settingsService, notifService);
                bool synced = await coordinator.SyncPendingStateFromWorkerAsync();
                Assert.IsTrue(synced);
                Assert.IsFalse(settingsService.Current.MasterEnabled);
                Assert.IsTrue(settingsService.Current.GamingModeEnabled);

                // Verify disk was updated by C# SettingsService and worker's PendingUiSync is now cleared
                var reloadedDisk = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(settingsFile));
                Assert.IsNotNull(reloadedDisk);
                Assert.IsFalse(reloadedDisk!.MasterEnabled);
                Assert.IsTrue(reloadedDisk.GamingModeEnabled);

                var stCleared = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(stCleared);
                Assert.IsFalse(stCleared!.PendingUiSync);
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task NotificationEventBridge_AcksMonotonically_NoDuplicates_AndHandlesTrayActions()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            string settingsFile = Path.Combine(tempDir, "settings.json");
            await File.WriteAllTextAsync(settingsFile, JsonSerializer.Serialize(new AppSettings()));

            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: true);
            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));

                var settingsService = new SettingsService(settingsFile);
                var notifService = new NotificationService(new CountingNotificationReader(() => { }));
                notifService.EnableExternalWorkerOwnership();

                var receivedNotifs = new List<NotificationItem>();
                notifService.NotificationReceived += (_, n) => receivedNotifs.Add(n);

                int testAnimCalls = 0;
                using var coordinator = new WorkerSyncCoordinator(
                    sup,
                    settingsService,
                    notifService,
                    onTestAnimation: () => testAnimCalls++);

                // Push 2 notification events + 1 tray TestAnimation
                await sup.IpcClient.SendCommandAsync("NOTIFICATION_EVENT", new
                {
                    inject = new { kind = "Added", notificationId = 501, appId = "Slack", appName = "Slack", timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                });
                await sup.IpcClient.SendCommandAsync("NOTIFICATION_EVENT", new
                {
                    inject = new { kind = "Removed", notificationId = 501, appId = "Slack", appName = "Slack", timestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                });
                await sup.IpcClient.ExecuteTrayActionAsync("TestAnimation");

                int count1 = await coordinator.PumpEventsOnceAsync();
                Assert.IsTrue(count1 >= 2);
                Assert.AreEqual(1, receivedNotifs.Count);
                Assert.AreEqual("Slack", receivedNotifs[0].AppId);
                Assert.AreEqual(1, testAnimCalls);

                // Subsequent pump must receive 0 duplicates because ackSeq was advanced!
                int count2 = await coordinator.PumpEventsOnceAsync();
                Assert.AreEqual(0, count2);
                Assert.AreEqual(1, receivedNotifs.Count);
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(20000)]
        public async Task NotificationPermissionDenied_SimulatedMode_StaysStableAndReportsDeniedToUi()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            string settingsFile = Path.Combine(tempDir, "settings.json");
            await File.WriteAllTextAsync(settingsFile, JsonSerializer.Serialize(new AppSettings()));

            using var sup = new WorkerSupervisor(
                exe,
                pipe,
                mutex,
                tempDir,
                ipcOnly: false,
                simulateNotifDenied: true);
            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));
                await Task.Delay(350);

                var settingsService = new SettingsService(settingsFile);
                var notifService = new NotificationService(new CountingNotificationReader(() => { }));
                notifService.EnableExternalWorkerOwnership(UserNotificationListenerAccessStatus.Allowed);

                using var coordinator = new WorkerSyncCoordinator(sup, settingsService, notifService);
                await coordinator.SyncPendingStateFromWorkerAsync();

                Assert.AreEqual(UserNotificationListenerAccessStatus.Denied, notifService.CurrentAccessStatus);
                var status = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(status);
                Assert.AreEqual("Denied", status!.NotifAccess);
                Assert.AreEqual(0, status.Stats.ActiveListenerCount);
                Assert.IsTrue(status.Stats.DeniedChecks >= 1);
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(25000)]
        public async Task PermissionTransitions_AllowedToDeniedToAllowed_AndUnknown_ControlSingleListenerAndEmitIpcEvent()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            string settingsFile = Path.Combine(tempDir, "settings.json");
            await File.WriteAllTextAsync(settingsFile, JsonSerializer.Serialize(new AppSettings()));

            using var sup = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: false);
            try
            {
                Assert.IsTrue(await sup.EnsureStartedAsync(4000));
                await Task.Delay(350);

                var settingsService = new SettingsService(settingsFile);
                var notifService = new NotificationService(new CountingNotificationReader(() => { }));
                notifService.EnableExternalWorkerOwnership(UserNotificationListenerAccessStatus.Unspecified);

                var statusChanges = new List<UserNotificationListenerAccessStatus>();
                notifService.AccessStatusChanged += (_, s) => statusChanges.Add(s);

                using var coordinator = new WorkerSyncCoordinator(sup, settingsService, notifService);

                // 1. Force Allowed -> single listener active (count == 1)
                await sup.IpcClient.SendCommandAsync("NOTIFICATION_EVENT", new { setTestAccess = "Allowed" });
                await Task.Delay(300);
                var stAllowed = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(stAllowed);
                Assert.AreEqual("Allowed", stAllowed!.NotifAccess);
                Assert.AreEqual(1, stAllowed.Stats.ActiveListenerCount);

                // 2. Allowed -> Denied -> listener stopped cleanly (count == 0) + AccessChanged emitted!
                await sup.IpcClient.SendCommandAsync("NOTIFICATION_EVENT", new { setTestAccess = "Denied" });
                await Task.Delay(300);
                var stDenied = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(stDenied);
                Assert.AreEqual("Denied", stDenied!.NotifAccess);
                Assert.AreEqual(0, stDenied.Stats.ActiveListenerCount);
                Assert.IsTrue(stDenied.Stats.DeniedChecks >= 1);

                await coordinator.PumpEventsOnceAsync();
                Assert.AreEqual(UserNotificationListenerAccessStatus.Denied, notifService.CurrentAccessStatus);

                // 3. Denied -> Unknown -> reported as Unknown (never assumed Allowed!), listener remains 0
                await sup.IpcClient.SendCommandAsync("NOTIFICATION_EVENT", new { setTestAccess = "Unknown" });
                await Task.Delay(300);
                var stUnknown = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(stUnknown);
                Assert.AreEqual("Unknown", stUnknown!.NotifAccess);
                Assert.AreEqual(0, stUnknown.Stats.ActiveListenerCount);

                await coordinator.PumpEventsOnceAsync();
                Assert.AreEqual(UserNotificationListenerAccessStatus.Unspecified, notifService.CurrentAccessStatus);

                // 4. Unknown -> Allowed -> single listener restarted (count == 1, no duplicate!)
                await sup.IpcClient.SendCommandAsync("NOTIFICATION_EVENT", new { setTestAccess = "Allowed" });
                await Task.Delay(300);
                var stRecovered = await sup.IpcClient.GetStatusAsync();
                Assert.IsNotNull(stRecovered);
                Assert.AreEqual("Allowed", stRecovered!.NotifAccess);
                Assert.AreEqual(1, stRecovered.Stats.ActiveListenerCount);
                Assert.IsTrue(stRecovered.Stats.AccessTransitions >= 3);

                await coordinator.PumpEventsOnceAsync();
                Assert.AreEqual(UserNotificationListenerAccessStatus.Allowed, notifService.CurrentAccessStatus);
                Assert.IsTrue(statusChanges.Contains(UserNotificationListenerAccessStatus.Denied));
                Assert.IsTrue(statusChanges.Contains(UserNotificationListenerAccessStatus.Allowed));
            }
            finally
            {
                await sup.ShutdownAsync(2500);
                SafeDeleteDir(tempDir);
            }
        }

        [TestMethod]
        [Timeout(25000)]
        public async Task PermissionChangeWhileUiClosed_IsReflectedImmediatelyWhenUiReopens()
        {
            string exe = ResolveTestWorkerExe();
            Assert.IsTrue(File.Exists(exe), $"Worker binary missing: {exe}");
            var (pipe, mutex, tempDir) = CreateIsolatedContext();
            string settingsFile = Path.Combine(tempDir, "settings.json");
            await File.WriteAllTextAsync(settingsFile, JsonSerializer.Serialize(new AppSettings()));

            WorkerSupervisor? ui2 = null;
            try
            {
                // UI #1 starts worker, then closes (Detaches)
                using (var ui1 = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: false))
                {
                    Assert.IsTrue(await ui1.EnsureStartedAsync(4000));
                    await ui1.IpcClient.SendCommandAsync("NOTIFICATION_EVENT", new { setTestAccess = "Allowed" });
                    await Task.Delay(250);
                    ui1.Detach();
                }

                // While UI is closed, permission transitions to Denied
                var rawClient = new WorkerIpcClient(pipe);
                await rawClient.SendCommandAsync("NOTIFICATION_EVENT", new { setTestAccess = "Denied" });
                await Task.Delay(300);

                // UI #2 reopens -> immediately learns Denied status via SyncPendingStateFromWorkerAsync
                ui2 = new WorkerSupervisor(exe, pipe, mutex, tempDir, ipcOnly: false);
                Assert.IsTrue(await ui2.EnsureStartedAsync(4000));
                Assert.IsTrue(ui2.AttachedToExisting);

                var settingsService = new SettingsService(settingsFile);
                var notifService = new NotificationService(new CountingNotificationReader(() => { }));
                notifService.EnableExternalWorkerOwnership(UserNotificationListenerAccessStatus.Allowed);

                using var coord = new WorkerSyncCoordinator(ui2, settingsService, notifService);
                await coord.SyncPendingStateFromWorkerAsync();
                Assert.AreEqual(UserNotificationListenerAccessStatus.Denied, notifService.CurrentAccessStatus);
            }
            finally
            {
                if (ui2 != null)
                {
                    await ui2.ShutdownAsync(2500);
                    ui2.Dispose();
                }
                SafeDeleteDir(tempDir);
            }
        }

        private sealed class CountingNotificationReader : INotificationReader
        {
            private readonly Action _onCall;
            public CountingNotificationReader(Action onCall) => _onCall = onCall;

            public Task<UserNotificationListenerAccessStatus> RequestAccessAsync()
            {
                _onCall();
                return Task.FromResult(UserNotificationListenerAccessStatus.Allowed);
            }

            public Task<IReadOnlyList<RawNotificationData>> GetCurrentNotificationsAsync()
            {
                _onCall();
                return Task.FromResult<IReadOnlyList<RawNotificationData>>(Array.Empty<RawNotificationData>());
            }

            public bool TrySubscribeNotificationChanged(Action onNotificationChanged)
            {
                _onCall();
                return false;
            }

            public void UnsubscribeNotificationChanged() { }
        }
    }
}
