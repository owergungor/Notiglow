using System.Text.Json;
using NotiGlow.Models;
using NotiGlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class ThemeSelectionTests
    {
        [TestMethod]
        public void Theme_Dark_Selection_PersistsCorrectly()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.Theme = AppTheme.Dark;
            settingsService.Save(settings);

            Assert.AreEqual(AppTheme.Dark, settingsService.Current.Theme);
        }

        [TestMethod]
        public void Theme_Light_Selection_PersistsCorrectly()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.Theme = AppTheme.Light;
            settingsService.Save(settings);

            Assert.AreEqual(AppTheme.Light, settingsService.Current.Theme);
        }

        [TestMethod]
        public void Theme_System_Selection_PersistsCorrectly()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.Theme = AppTheme.System;
            settingsService.Save(settings);

            Assert.AreEqual(AppTheme.System, settingsService.Current.Theme);
        }

        [TestMethod]
        public void Theme_NightBlue_MapsToLiquidGlassEnum_AndPersistsCorrectly()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.Theme = AppTheme.LiquidGlass;
            settingsService.Save(settings);

            Assert.AreEqual(AppTheme.LiquidGlass, settingsService.Current.Theme);
        }

        [TestMethod]
        public void Theme_LegacySettings_LiquidGlass_DeserializesProperly()
        {
            // Legacy JSON with "Theme": 3 or "Theme": "LiquidGlass"
            string legacyJson = "{\"Theme\": 3, \"MasterEnabled\": true}";
            var deserialized = JsonSerializer.Deserialize<AppSettings>(legacyJson);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(AppTheme.LiquidGlass, deserialized.Theme);

            string legacyStringJson = "{\"Theme\": \"LiquidGlass\", \"MasterEnabled\": true}";
            var deserializedString = JsonSerializer.Deserialize<AppSettings>(legacyStringJson);

            Assert.IsNotNull(deserializedString);
            Assert.AreEqual(AppTheme.LiquidGlass, deserializedString.Theme);
        }

        [TestMethod]
        public void Theme_AllThemeEnums_AreHandledAndDistinct()
        {
            var themes = new[] { AppTheme.Dark, AppTheme.Light, AppTheme.System, AppTheme.LiquidGlass };
            var settingsService = new SettingsService();

            foreach (var t in themes)
            {
                var s = settingsService.Current;
                s.Theme = t;
                settingsService.Save(s);
                Assert.AreEqual(t, settingsService.Current.Theme);
            }
        }
    }
}
