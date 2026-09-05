using NotiGlow.Models;
using NotiGlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class MonitorSelectionTests
    {
        [TestMethod]
        public void MonitorMode_ActiveMonitor_SavesCorrectly()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.MonitorMode = MonitorMode.ActiveMonitor;
            settingsService.Save(settings);

            Assert.AreEqual(MonitorMode.ActiveMonitor, settingsService.Current.MonitorMode);
        }

        [TestMethod]
        public void MonitorMode_AllMonitors_SavesCorrectly()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.MonitorMode = MonitorMode.AllMonitors;
            settingsService.Save(settings);

            Assert.AreEqual(MonitorMode.AllMonitors, settingsService.Current.MonitorMode);
        }
    }
}
