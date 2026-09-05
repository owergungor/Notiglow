using System;
using NotiGlow.Models;
using NotiGlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class NotificationDeduplicationTests
    {
        [TestMethod]
        public void IsDuplicate_IdenticalNotificationWithinWindow_ReturnsTrue()
        {
            var dedup = new NotificationDeduplicator();
            DateTime now = DateTime.Now;

            var item1 = new NotificationItem
            {
                AppId = "Discord",
                AppName = "Discord",
                Title = "Hello",
                Timestamp = now
            };

            var item2 = new NotificationItem
            {
                AppId = "Discord",
                AppName = "Discord",
                Title = "Hello",
                Timestamp = now
            };

            bool firstResult = dedup.IsDuplicate(item1);
            bool secondResult = dedup.IsDuplicate(item2);

            Assert.IsFalse(firstResult);
            Assert.IsTrue(secondResult);
        }

        [TestMethod]
        public void IsDuplicate_DifferentAppNotifications_ReturnsFalse()
        {
            var dedup = new NotificationDeduplicator();
            DateTime now = DateTime.Now;

            var item1 = new NotificationItem
            {
                AppId = "Discord",
                AppName = "Discord",
                Title = "Hello",
                Timestamp = now
            };

            var item2 = new NotificationItem
            {
                AppId = "Steam",
                AppName = "Steam",
                Title = "Game Ready",
                Timestamp = now
            };

            Assert.IsFalse(dedup.IsDuplicate(item1));
            Assert.IsFalse(dedup.IsDuplicate(item2));
        }
    }
}
