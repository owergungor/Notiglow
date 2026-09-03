using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Notifications.Management;
using NotiGlow.Models;
using NotiGlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    public class MockNotificationReader : INotificationReader
    {
        public UserNotificationListenerAccessStatus AccessStatus { get; set; } = UserNotificationListenerAccessStatus.Allowed;
        public List<RawNotificationData> Notifications { get; } = new();
        public bool SubscriptionSupported { get; set; } = true;
        public Action? Callback { get; private set; }
        public bool Unsubscribed { get; private set; } = false;

        public Task<UserNotificationListenerAccessStatus> RequestAccessAsync()
        {
            return Task.FromResult(AccessStatus);
        }

        public Task<IReadOnlyList<RawNotificationData>> GetCurrentNotificationsAsync()
        {
            return Task.FromResult<IReadOnlyList<RawNotificationData>>(new List<RawNotificationData>(Notifications));
        }

        public bool TrySubscribeNotificationChanged(Action onNotificationChanged)
        {
            if (!SubscriptionSupported) return false;
            Callback = onNotificationChanged;
            return true;
        }

        public void UnsubscribeNotificationChanged()
        {
            Unsubscribed = true;
            Callback = null;
        }

        public void SimulateNativeEvent()
        {
            Callback?.Invoke();
        }
    }

    [TestClass]
    public class NotificationServiceTests
    {
        [TestMethod]
        public async Task InitializeAsync_HistoricalNotifications_AreSnapshottedAndNotFired()
        {
            var mockReader = new MockNotificationReader();
            mockReader.Notifications.Add(new RawNotificationData
            {
                NotificationId = 1001,
                AppId = "Discord",
                AppName = "Discord",
                Title = "Old historical notification",
                CreationTime = DateTime.Now.AddMinutes(-5)
            });

            var service = new NotificationService(mockReader) { AutoStartPolling = false };
            int eventCount = 0;
            service.NotificationReceived += (s, item) => eventCount++;

            await service.InitializeAsync();

            Assert.IsTrue(service.IsListening);
            Assert.AreEqual(0, eventCount, "Historical notifications already present on startup should not trigger events.");
            service.Stop();
        }

        [TestMethod]
        public async Task PollOnceAsync_NewNotification_FiresNotificationReceivedEvent()
        {
            var mockReader = new MockNotificationReader();
            var service = new NotificationService(mockReader) { AutoStartPolling = false };
            await service.InitializeAsync();

            NotificationItem? receivedItem = null;
            service.NotificationReceived += (s, item) => receivedItem = item;

            mockReader.Notifications.Add(new RawNotificationData
            {
                NotificationId = 2002,
                AppId = "WhatsApp",
                AppName = "WhatsApp",
                Title = "New message from Alice",
                CreationTime = DateTime.Now
            });

            int newCount = await service.PollOnceAsync();

            Assert.AreEqual(1, newCount);
            Assert.IsNotNull(receivedItem);
            Assert.AreEqual("WhatsApp", receivedItem.AppId);
            Assert.AreEqual("WhatsApp", receivedItem.AppName);
            Assert.AreEqual("New message from Alice", receivedItem.Title);
            service.Stop();
        }

        [TestMethod]
        public async Task PollOnceAsync_SameNotificationIdTwice_SuppressedByProcessedIds()
        {
            var mockReader = new MockNotificationReader();
            var service = new NotificationService(mockReader) { AutoStartPolling = false };
            await service.InitializeAsync();

            int eventCount = 0;
            service.NotificationReceived += (s, item) => eventCount++;

            mockReader.Notifications.Add(new RawNotificationData
            {
                NotificationId = 3003,
                AppId = "Steam",
                AppName = "Steam",
                Title = "Friend is now online",
                CreationTime = DateTime.Now
            });

            int firstPoll = await service.PollOnceAsync();
            int secondPoll = await service.PollOnceAsync();

            Assert.AreEqual(1, firstPoll);
            Assert.AreEqual(0, secondPoll, "Same notification ID should not be reported as new on subsequent polls.");
            Assert.AreEqual(1, eventCount);
            service.Stop();
        }

        [TestMethod]
        public async Task PollOnceAsync_MissingAppInfo_FallsBackGracefully()
        {
            var mockReader = new MockNotificationReader();
            var service = new NotificationService(mockReader) { AutoStartPolling = false };
            await service.InitializeAsync();

            NotificationItem? receivedItem = null;
            service.NotificationReceived += (s, item) => receivedItem = item;

            mockReader.Notifications.Add(new RawNotificationData
            {
                NotificationId = 4004,
                AppId = "",
                AppName = "",
                Title = "System Toast",
                CreationTime = DateTime.Now
            });

            int count = await service.PollOnceAsync();

            Assert.AreEqual(1, count);
            Assert.IsNotNull(receivedItem);
            Assert.AreEqual("UnknownApp", receivedItem.AppId);
            Assert.AreEqual("UnknownApp", receivedItem.AppName);
            Assert.AreEqual("System Toast", receivedItem.Title);
            service.Stop();
        }

        [TestMethod]
        public async Task AutomaticBackgroundPolling_DetectsNewNotificationAsynchronously()
        {
            var mockReader = new MockNotificationReader();
            var service = new NotificationService(mockReader) { AutoStartPolling = true };

            var tcs = new TaskCompletionSource<NotificationItem>();
            service.NotificationReceived += (s, item) => tcs.TrySetResult(item);

            await service.InitializeAsync();

            mockReader.Notifications.Add(new RawNotificationData
            {
                NotificationId = 7777,
                AppId = "Discord",
                AppName = "Discord",
                Title = "Background async notification",
                CreationTime = DateTime.Now
            });

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2000));
            Assert.AreEqual(tcs.Task, completedTask, "Background polling loop failed to detect notification within timeout.");

            var result = await tcs.Task;
            Assert.AreEqual("Discord", result.AppId);
            Assert.AreEqual("Background async notification", result.Title);
            service.Stop();
        }

        [TestMethod]
        public async Task Stop_CancelsListening_AndUnsubscribes()
        {
            var mockReader = new MockNotificationReader();
            var service = new NotificationService(mockReader);
            await service.InitializeAsync();
            Assert.IsTrue(service.IsListening);

            service.Stop();

            Assert.IsFalse(service.IsListening);
            Assert.IsTrue(mockReader.Unsubscribed);
        }

        [TestMethod]
        public async Task RequestAccess_Denied_DoesNotStartListening()
        {
            var mockReader = new MockNotificationReader
            {
                AccessStatus = UserNotificationListenerAccessStatus.Denied
            };

            var service = new NotificationService(mockReader);
            UserNotificationListenerAccessStatus? reportedStatus = null;
            service.AccessStatusChanged += (s, status) => reportedStatus = status;

            await service.InitializeAsync();

            Assert.IsFalse(service.IsListening);
            Assert.AreEqual(UserNotificationListenerAccessStatus.Denied, reportedStatus);
            Assert.AreEqual(UserNotificationListenerAccessStatus.Denied, service.CurrentAccessStatus);
        }

        [TestMethod]
        public async Task EndToEndPipeline_SimulatedNotification_MatchesProfileAndTriggersGlow()
        {
            var settingsService = new SettingsService();
            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService);

            var mockReader = new MockNotificationReader();
            var service = new NotificationService(mockReader) { AutoStartPolling = false };

            NotificationItem? dispatchedItem = null;
            service.NotificationReceived += (s, item) =>
            {
                dispatchedItem = item;
                glowManager.TriggerNotification(item);
            };

            await service.InitializeAsync();

            // Simulate incoming notification for Discord (default profile)
            mockReader.Notifications.Add(new RawNotificationData
            {
                NotificationId = 5005,
                AppId = "Discord",
                AppName = "Discord",
                Title = "Direct Message Received",
                CreationTime = DateTime.Now
            });

            await service.PollOnceAsync();

            Assert.IsNotNull(dispatchedItem);
            Assert.AreEqual("Discord", dispatchedItem.AppId);
            service.Stop();
        }

        [TestMethod]
        public async Task RealWindowsNotificationListener_DetectsNewToastAndMatchesProfile()
        {
            var profileService = new ProfileService();
            var settingsService = new SettingsService();
            var glowManager = new GlowManager(settingsService, profileService);

            var realReader = new WindowsNotificationReader();
            var access = await realReader.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                Assert.Inconclusive("Notification access not allowed on this environment.");
                return;
            }

            var service = new NotificationService(realReader) { AutoStartPolling = false };
            await service.InitializeAsync();

            NotificationItem? capturedItem = null;
            service.NotificationReceived += (s, item) =>
            {
                capturedItem = item;
                glowManager.TriggerNotification(item);
            };

            // Send a new toast with unique tag
            var template = Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(Windows.UI.Notifications.ToastTemplateType.ToastText02);
            var textNodes = template.GetElementsByTagName("text");
            textNodes[0].AppendChild(template.CreateTextNode("WhatsApp Notification"));
            textNodes[1].AppendChild(template.CreateTextNode("Testing end to end real toast detection!"));

            var toast = new Windows.UI.Notifications.ToastNotification(template);
            toast.Tag = Guid.NewGuid().ToString();
            var notifier = Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App");
            notifier.Show(toast);

            await Task.Delay(600);

            int detectedCount = await service.PollOnceAsync();
            Assert.IsTrue(detectedCount >= 1, "Expected at least 1 new notification detected by active poller.");
            Assert.IsNotNull(capturedItem);
            Assert.AreEqual("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", capturedItem.AppId);
            Assert.AreEqual("WhatsApp", capturedItem.AppName);

            var matchedProfile = profileService.GetProfile(capturedItem.AppId, capturedItem.AppName);
            Assert.IsNotNull(matchedProfile, "Matched profile must not be null for WhatsApp notification!");
            Assert.AreEqual("WhatsApp", matchedProfile.Name);
            service.Stop();
        }
    }
}
