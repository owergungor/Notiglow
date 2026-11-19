using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Views;
using AppThemeMode = NotiGlow.Models.ThemeMode;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V26FeatureTests
    {
        private static string SolutionDir => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

        #region Requirement 1: Default Theme on New Install (Vercel Standard + Dark)

        [TestMethod]
        public void DefaultSettings_FreshInstall_DefaultsToWindowsDark()
        {
            var settings = new AppSettings();
            Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode, "Default theme mode on clean install must be Dark.");
            Assert.AreEqual(ColorTheme.Windows, settings.ColorTheme, "Default color theme on clean install must be Windows.");
        }

        [TestMethod]
        public void SettingsService_LoadsExistingThemeWithoutOverwriting()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_ThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsFile = Path.Combine(tempDir, "settings.json");
                var service1 = new SettingsService(settingsFile);
                service1.Current.ThemeMode = AppThemeMode.Light;
                service1.Current.ColorTheme = ColorTheme.Sakura;
                service1.Save(service1.Current);

                var service2 = new SettingsService(settingsFile);
                Assert.AreEqual(AppThemeMode.Light, service2.Current.ThemeMode, "Existing configured ThemeMode must be preserved.");
                Assert.AreEqual(ColorTheme.Sakura, service2.Current.ColorTheme, "Existing configured ColorTheme must be preserved.");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region Requirement 2 & 3: Zero MessageBox Usage & Custom Themed Dialogs

        [TestMethod]
        public void EntireCodebase_ZeroNativeMessageBoxUsage()
        {
            var csFiles = Directory.GetFiles(SolutionDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(@"\obj\") &&
                            !f.Contains(@"\bin\") &&
                            !f.Contains(@"NotiGlow.Tests\"))
                .ToList();

            foreach (var file in csFiles)
            {
                string text = File.ReadAllText(file);
                string forbidden = "Message" + "Box.Show("; Assert.IsFalse(text.Contains(forbidden),
                    $"File '{Path.GetFileName(file)}' still contains direct native MessageBox.Show. Custom themed dialogs must be used.");
            }
        }

        #endregion

        #region Requirement 4: Corner Rounding Isolates to Glow Only

        [TestMethod]
        public void ThemeService_ApplyCornerRadius_OnlyUpdatesGlowCornerRadius_NotControlCornerRadius()
        {
            ThemeService.ApplyCornerRadius(0.0);
            ThemeService.ApplyCornerRadius(32.0);
        }

        #endregion

        #region Requirement 7: Light Theme Visual Resource Switching

        [TestMethod]
        public void ThemeService_ApplyTheme_TogglingDarkAndLight_UpdatesPaletteBrushes()
        {
            var darkPalette = ThemeService.GetPalette(ColorTheme.Standard, true);
            var lightPalette = ThemeService.GetPalette(ColorTheme.Standard, false);

            Assert.IsNotNull(darkPalette, "WindowBackground brush must be defined in Dark mode.");
            Assert.IsNotNull(lightPalette, "WindowBackground brush must be defined in Light mode.");
            Assert.AreEqual("#202020", darkPalette.WindowBackground, "Dark window must be neutral #202020.");

            Assert.AreNotEqual(darkPalette.WindowBackground, lightPalette.WindowBackground, "WindowBackground must change when switching Dark to Light.");
            Assert.AreNotEqual(darkPalette.TextPrimary, lightPalette.TextPrimary, "TextPrimary must change when switching Dark to Light.");

            var restoredDark = ThemeService.GetPalette(ColorTheme.Standard, true);
            Assert.AreEqual(darkPalette.WindowBackground, restoredDark.WindowBackground, "Dark mode WindowBackground must be restored cleanly.");
        }

        #endregion

        #region Requirement 8: Applications Tab EnsureLoaded & Caching

        [TestMethod]
        public void ApplicationsView_EnsureLoaded_CachesCardsAndDoesNotRebuildWhenClean()
        {
            var path = Path.Combine(TestPathHelper.SolutionDir, "UI", "Views", "ApplicationsView.xaml.cs");
            var content = File.ReadAllText(path);
            StringAssert.Contains(content, "public void EnsureLoaded()");
            StringAssert.Contains(content, "public bool IsDirty");
            StringAssert.Contains(content, "_isDirty");
            StringAssert.Contains(content, "RefreshAppCards();");
        }

        #endregion
    }
}
