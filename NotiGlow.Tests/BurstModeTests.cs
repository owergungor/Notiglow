using NotiGlow.Models;
using NotiGlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class BurstModeTests
    {
        [TestMethod]
        public void BurstMode_IgnoreSetting_MaintainsState()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.BurstMode = BurstMode.Ignore;
            settingsService.Save(settings);

            Assert.AreEqual(BurstMode.Ignore, settingsService.Current.BurstMode);
        }

        [TestMethod]
        public void BurstMode_QueueSetting_MaintainsState()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.BurstMode = BurstMode.Queue;
            settingsService.Save(settings);

            Assert.AreEqual(BurstMode.Queue, settingsService.Current.BurstMode);
        }
    }
}
