using System.Text.Json;
using GlowBorder.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlowBorder.Tests
{
    [TestClass]
    public class SerializationTests
    {
        [TestMethod]
        public void AppProfile_SerializeAndDeserialize_PreservesAllFields()
        {
            var original = new AppProfile
            {
                AppId = "Discord",
                Name = "Discord",
                Enabled = true,
                ColorHex = "#5865F2",
                DurationMs = 4000,
                Intensity = 0.8,
                Thickness = 4,
                GlowSize = 30,
                Style = GlowStyle.Pulse
            };

            string json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AppProfile>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(original.AppId, deserialized.AppId);
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Enabled, deserialized.Enabled);
            Assert.AreEqual(original.ColorHex, deserialized.ColorHex);
            Assert.AreEqual(original.DurationMs, deserialized.DurationMs);
            Assert.AreEqual(original.Intensity, deserialized.Intensity);
            Assert.AreEqual(original.Style, deserialized.Style);
        }

        [TestMethod]
        public void AppSettings_Defaults_AreValid()
        {
            var settings = new AppSettings();
            Assert.IsTrue(settings.MasterEnabled);
            Assert.AreEqual(AppTheme.Dark, settings.Theme);
            Assert.AreEqual(MonitorMode.ActiveMonitor, settings.MonitorMode);
            Assert.AreEqual(BurstMode.Restart, settings.BurstMode);
        }
    }
}
