using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using Windows.UI.Notifications.Management;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V42MemoryOptimizationTests
    {
        private static string RepoRoot => TestPathHelper.SolutionDir;

        [TestMethod]
        [Timeout(10000)]
        public void ApplicationsView_UsesVirtualizedItemsRepeater_AndDeferredEditor()
        {
            string xaml = File.ReadAllText(Path.Combine(RepoRoot, "UI", "Views", "ApplicationsView.xaml"));
            StringAssert.Contains(xaml, "<ItemsRepeater x:Name=\"PnlAppCards\"", "ApplicationsView must use virtualized ItemsRepeater for PnlAppCards.");
            StringAssert.Contains(xaml, "ElementPrepared=\"PnlAppCards_ElementPrepared\"", "ApplicationsView must wire ElementPrepared for recycled cards.");
            StringAssert.Contains(xaml, "ElementClearing=\"PnlAppCards_ElementClearing\"", "ApplicationsView must wire ElementClearing to detach handlers on recycled cards.");
            StringAssert.Contains(xaml, "x:Load=\"False\"", "ApplicationsView must defer-load PnlEditProfile via x:Load=\"False\".");
        }

        [TestMethod]
        [Timeout(10000)]
        public void GamingView_UsesDeferredProfileEditor_AndUnloadObject()
        {
            string xaml = File.ReadAllText(Path.Combine(RepoRoot, "UI", "Views", "GamingView.xaml"));
            string code = File.ReadAllText(Path.Combine(RepoRoot, "UI", "Views", "GamingView.xaml.cs"));

            StringAssert.Contains(xaml, "x:Load=\"False\"", "GamingView must defer-load PnlEditGameOverride via x:Load=\"False\".");
            StringAssert.Contains(code, "UnloadObject", "GamingView must unload PnlEditGameOverride when dismissed.");
            StringAssert.Contains(code, "public void DetachEvents()", "GamingView must expose DetachEvents() to prevent static event leaks.");
        }

        [TestMethod]
        [Timeout(10000)]
        public void MainWindow_ReclaimsSecondaryViewsAndBackdrop_OnHideToTray()
        {
            string code = File.ReadAllText(Path.Combine(RepoRoot, "UI", "MainWindow.xaml.cs"));

            StringAssert.Contains(code, "public void HideToTray()", "MainWindow must expose HideToTray() for tray memory reclamation.");
            StringAssert.Contains(code, "public void ShowFromTray()", "MainWindow must expose ShowFromTray() to restore backdrop and views.");
            StringAssert.Contains(code, "ReleaseSecondaryViews(keepTag: null)", "HideToTray must release secondary views.");
            StringAssert.Contains(code, "ClearInMemoryBitmaps()", "HideToTray must clear in-memory decoded icon bitmaps.");
            StringAssert.Contains(code, "SystemBackdrop = null", "HideToTray must detach Acrylic/Mica backdrop while hidden in tray.");
        }

        [TestMethod]
        [Timeout(10000)]
        public void GlowManager_IdleOverlayDisposal_LeavesZeroActiveOverlays()
        {
            var settings = new SettingsService();
            var profiles = new ProfileService();
            var manager = new GlowManager(settings, profiles)
            {
                SuppressOverlayWindowsForTesting = true
            };

            Assert.AreEqual(0, manager.ActiveOverlayWindowCount);
            manager.TriggerProfile(new AppProfile
            {
                AppId = "TestApp",
                Name = "TestApp",
                ColorHex = "#0078D4",
                DurationMs = 1000,
                Intensity = 0.8
            });
            manager.DisposeIdleOverlays();
            Assert.AreEqual(0, manager.ActiveOverlayWindowCount);
            manager.CloseAllOverlays();
            Assert.AreEqual(0, manager.ActiveOverlayWindowCount);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task NotificationService_IdlePolling_ProcessesRepeatedPollsWithZeroDuplicates()
        {
            var reader = new StaticMockNotificationReader(new[]
            {
                new RawNotificationData { NotificationId = 1001, AppId = "Discord", AppName = "Discord", Title = "Hi", CreationTime = DateTime.UtcNow }
            });
            var service = new NotificationService(reader) { AutoStartPolling = false };
            int received = 0;
            service.NotificationReceived += (s, n) => received++;

            // First poll sees 1 notification
            int first = await service.PollOnceAsync();
            Assert.AreEqual(1, first);
            Assert.AreEqual(1, received);

            // 100 subsequent idle polls should return 0 without re-triggering
            for (int i = 0; i < 100; i++)
            {
                int next = await service.PollOnceAsync();
                Assert.AreEqual(0, next);
            }
            Assert.AreEqual(1, received);
        }

        [TestMethod]
        [Timeout(10000)]
        public void TrayService_Dispose_CleansUpResourcesWithoutLeakingSyncContext()
        {
            var prev = SynchronizationContext.Current;
            try
            {
                var settings = new SettingsService();
                var tray = new TrayService(settings, () => { }, () => { }, () => { });
                Assert.IsTrue(tray.IsVisible);
                tray.Dispose();
                Assert.IsFalse(tray.IsVisible);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        [TestMethod]
        [Timeout(10000)]
        public void AppIconService_ClearInMemoryBitmaps_And_CsprojRetainVmFalse()
        {
            AppIconService.Instance.ClearInMemoryBitmaps();
            string csproj = File.ReadAllText(Path.Combine(RepoRoot, "NotiGlow.csproj"));
            StringAssert.Contains(csproj, "<RetainVMGarbageCollection>false</RetainVMGarbageCollection>",
                "NotiGlow.csproj should release freed managed heap segments back to OS.");
        }

        private sealed class StaticMockNotificationReader : INotificationReader
        {
            private readonly IReadOnlyList<RawNotificationData> _items;
            public StaticMockNotificationReader(IReadOnlyList<RawNotificationData> items) => _items = items;
            public Task<UserNotificationListenerAccessStatus> RequestAccessAsync() => Task.FromResult(UserNotificationListenerAccessStatus.Allowed);
            public Task<IReadOnlyList<RawNotificationData>> GetCurrentNotificationsAsync() => Task.FromResult(_items);
            public bool TrySubscribeNotificationChanged(Action onNotificationChanged) => false;
            public void UnsubscribeNotificationChanged() { }
        }
    }
}
