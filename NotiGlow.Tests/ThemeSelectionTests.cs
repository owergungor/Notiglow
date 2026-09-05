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

        [TestMethod]
        public void TitleBar_Properties_Check()
        {
            var props = typeof(Wpf.Ui.Controls.TitleBar).GetProperties();
            var propNames = string.Join(", ", props.Select(p => p.Name));
            System.Diagnostics.Debug.WriteLine($"TitleBar Properties: {propNames}");
            var prop = typeof(Wpf.Ui.Controls.TitleBar).GetProperty("ButtonsForeground");
            Assert.IsNotNull(prop, "TitleBar should have ButtonsForeground property");
        }

        [TestMethod]
        public void TitleBar_Icon_Resource_Check()
        {
            var assembly = typeof(NotiGlow.UI.MainWindow).Assembly;
            using var resourceStream = assembly.GetManifestResourceStream("NotiGlow.g.resources");
            Assert.IsNotNull(resourceStream, "NotiGlow.g.resources should exist");
            using var reader = new System.Resources.ResourceReader(resourceStream);
            reader.GetResourceData("assets/notiglowlogo.png", out _, out byte[] data);
            Assert.IsNotNull(data, "assets/notiglowlogo.png should exist in assembly resources");
            Assert.IsTrue(data.Length > 4);
            int len = BitConverter.ToInt32(data, 0);
            using var ms = new System.IO.MemoryStream(data, 4, len);
            var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                ms,
                System.Windows.Media.Imaging.BitmapCreateOptions.None,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            Assert.AreEqual(1254, frame.PixelWidth);
            Assert.AreEqual(1254, frame.PixelHeight);
        }

        [TestMethod]
        public void NavigationViewItem_Template_Check()
        {
            var navProps = typeof(Wpf.Ui.Controls.NavigationViewItem).GetProperties();
            var propNames = string.Join(", ", navProps.Select(p => p.Name));
            System.Diagnostics.Debug.WriteLine($"NavigationViewItem Properties: {propNames}");
            Assert.IsNotNull(navProps);
        }

        [TestMethod]
        public void MainWindow_SidebarSelectionOverlay_Check()
        {
            var thread = new System.Threading.Thread(() =>
            {
                var app = System.Windows.Application.Current ?? new System.Windows.Application();
                var window = new NotiGlow.UI.MainWindow();
                Assert.IsNotNull(window.SelectionOverlayCanvas, "SidebarOverlayCanvas should exist in visual tree");
                Assert.IsNotNull(window.SelectionBox, "ActiveSelectionBox should exist in visual tree");
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }
}
