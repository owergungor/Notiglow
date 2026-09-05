using System;
using NotiGlow.Models;
using NotiGlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class PipelineTests
    {
        [TestMethod]
        public void TriggerNotification_UntrackedNotification_DoesNotThrow()
        {
            var settingsService = new SettingsService();
            var profileService = new ProfileService();
            var manager = new GlowManager(settingsService, profileService);

            var item = new NotificationItem
            {
                AppId = "untracked_app_exe",
                AppName = "Untracked App",
                Title = "Random Title",
                Timestamp = DateTime.Now
            };

            // Untracked notification should be ignored safely without crashing
            manager.TriggerNotification(item);
            Assert.IsFalse(manager.IsAnimating);
        }

        [TestMethod]
        public void TriggerNotification_DisabledMasterSwitch_SuppressesGlow()
        {
            var settingsService = new SettingsService();
            var profileService = new ProfileService();
            var manager = new GlowManager(settingsService, profileService);

            var settings = settingsService.Current;
            settings.MasterEnabled = false;
            settingsService.Save(settings);

            var item = new NotificationItem
            {
                AppId = "Discord",
                AppName = "Discord",
                Title = "Message",
                Timestamp = DateTime.Now
            };

            manager.TriggerNotification(item);
            Assert.IsFalse(manager.IsAnimating);
        }
    }
}
