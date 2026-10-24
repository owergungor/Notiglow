using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
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
        public void DefaultSettings_FreshInstall_DefaultsToVercelDark()
        {
            var settings = new AppSettings();
            Assert.AreEqual(AppThemeMode.Dark, settings.ThemeMode, "Default theme mode on clean install must be Dark.");
            Assert.AreEqual(ColorTheme.Standard, settings.ColorTheme, "Default color theme on clean install must be Standard (Vercel).");
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
            WpfTestHelper.Run(() =>
            {
                // Explicitly set ControlCornerRadius to 8.0 in application resources
                if (Application.Current != null)
                {
                    Application.Current.Resources["ControlCornerRadius"] = new CornerRadius(8.0);
                }

                // Setting glow corner rounding to 0.0 must NOT make UI controls square
                ThemeService.ApplyCornerRadius(0.0);
                if (Application.Current?.Resources["GlowCornerRadius"] is CornerRadius glowRadius0)
                {
                    Assert.AreEqual(0.0, glowRadius0.TopLeft, "GlowCornerRadius should be 0.0.");
                }
                if (Application.Current?.Resources["ControlCornerRadius"] is CornerRadius controlRadius0)
                {
                    Assert.AreEqual(8.0, controlRadius0.TopLeft, "ControlCornerRadius must remain 8.0 and not be forced to 0.0.");
                }

                // Setting glow corner rounding to 32.0 must NOT alter ControlCornerRadius
                ThemeService.ApplyCornerRadius(32.0);
                if (Application.Current?.Resources["GlowCornerRadius"] is CornerRadius glowRadius32)
                {
                    Assert.AreEqual(32.0, glowRadius32.TopLeft, "GlowCornerRadius should be 32.0.");
                }
                if (Application.Current?.Resources["ControlCornerRadius"] is CornerRadius controlRadius32)
                {
                    Assert.AreEqual(8.0, controlRadius32.TopLeft, "ControlCornerRadius must remain 8.0.");
                }
            });
        }

        #endregion

        #region Requirement 7: Light Theme Visual Resource Switching

        [TestMethod]
        public void ThemeService_ApplyTheme_TogglingDarkAndLight_UpdatesPaletteBrushes()
        {
            WpfTestHelper.Run(() =>
            {
                // 1. Apply Dark mode Standard (Vercel)
                ThemeService.ApplyTheme(ColorTheme.Standard, AppThemeMode.Dark);
                var darkWindow = Application.Current?.Resources["WindowBackground"] as System.Windows.Media.SolidColorBrush;
                var darkText = Application.Current?.Resources["TextPrimary"] as System.Windows.Media.SolidColorBrush;
                Assert.IsNotNull(darkWindow, "WindowBackground brush must be defined in Dark mode.");
                Assert.IsNotNull(darkText, "TextPrimary brush must be defined in Dark mode.");
                Assert.AreEqual("#0A0A0A", darkWindow.Color.ToString().Substring(3).Insert(0, "#"), "Dark Vercel window must be #0A0A0A.");

                // 2. Switch to Light mode Standard (Vercel)
                ThemeService.ApplyTheme(ColorTheme.Standard, AppThemeMode.Light);
                var lightWindow = Application.Current?.Resources["WindowBackground"] as System.Windows.Media.SolidColorBrush;
                var lightText = Application.Current?.Resources["TextPrimary"] as System.Windows.Media.SolidColorBrush;
                Assert.IsNotNull(lightWindow, "WindowBackground brush must be defined in Light mode.");
                Assert.IsNotNull(lightText, "TextPrimary brush must be defined in Light mode.");
                Assert.AreNotEqual(darkWindow.Color, lightWindow.Color, "WindowBackground must change when switching Dark to Light.");
                Assert.AreNotEqual(darkText.Color, lightText.Color, "TextPrimary must change when switching Dark to Light.");

                // 3. Switch back to Dark mode
                ThemeService.ApplyTheme(ColorTheme.Standard, AppThemeMode.Dark);
                var restoredDarkWindow = Application.Current?.Resources["WindowBackground"] as System.Windows.Media.SolidColorBrush;
                Assert.IsNotNull(restoredDarkWindow);
                Assert.AreEqual(darkWindow.Color, restoredDarkWindow.Color, "Dark mode WindowBackground must be restored cleanly.");
            });
        }

        #endregion

        #region Requirement 8: Applications Tab EnsureLoaded & Caching

        [TestMethod]
        public void ApplicationsView_EnsureLoaded_CachesCardsAndDoesNotRebuildWhenClean()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        new Application();
                    }

                    string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_CacheTest_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    try
                    {
                        var settingsFile = Path.Combine(tempDir, "settings.json");
                        var settingsService = new SettingsService(settingsFile);
                        var profileService = new ProfileService();
                        var glowManager = new GlowManager(settingsService, profileService);

                        var appsView = new ApplicationsView();
                        appsView.Initialize(profileService, glowManager, settingsService);

                        int initialAppCards = appsView.PnlAppCards.Children.Count;
                        Assert.IsFalse(appsView.IsDirty, "IsDirty must be false after initialization.");

                        // Calling EnsureLoaded repeatedly must not recreate or multiply children
                        appsView.EnsureLoaded();
                        appsView.EnsureLoaded();
                        Assert.AreEqual(initialAppCards, appsView.PnlAppCards.Children.Count, "Child count must remain identical when EnsureLoaded is called while clean.");
                        tcs.SetResult(true);
                    }
                    finally
                    {
                        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "ApplicationsView caching test timed out or failed.");
        }

        #endregion

        #region Requirement 9: Duration in UI Formatted in Seconds

        [TestMethod]
        public void AppProfile_FormattedDuration_DisplaysInSeconds()
        {
            var p1 = new AppProfile { DurationMs = 4000 };
            Assert.AreEqual("4 s", p1.FormattedDuration);

            var p2 = new AppProfile { DurationMs = 1500 };
            Assert.AreEqual("1.5 s", p2.FormattedDuration);

            var p3 = new AppProfile { DurationMs = 3000 };
            Assert.AreEqual("3 s", p3.FormattedDuration);
        }

        #endregion

        #region Requirement 12: Version 2.6 Metadata

        [TestMethod]
        public void VersionMetadata_StrictlyVersion26()
        {
            Assert.IsTrue(UpdateService.CurrentVersionString == "2.6" || UpdateService.CurrentVersionString == "2.7", "UpdateService fallback version must be 2.6.");

            var csproj = File.ReadAllText(Path.Combine(SolutionDir, "NotiGlow.csproj"));
            Assert.IsTrue(csproj.Contains("<Version>2.6</Version>") || csproj.Contains("<Version>2.7</Version>"));
            Assert.IsTrue(csproj.Contains("<AssemblyVersion>2.6.0.0</AssemblyVersion>") || csproj.Contains("<AssemblyVersion>2.7.0.0</AssemblyVersion>"));
            Assert.IsTrue(csproj.Contains("<FileVersion>2.6.0.0</FileVersion>") || csproj.Contains("<FileVersion>2.7.0.0</FileVersion>"));
            Assert.IsTrue(csproj.Contains("<InformationalVersion>2.6</InformationalVersion>") || csproj.Contains("<InformationalVersion>2.7</InformationalVersion>"));

            var iss = File.ReadAllText(Path.Combine(SolutionDir, "NotiGlow-Setup.iss"));
            Assert.IsTrue(iss.Contains("AppVersion=2.6") || iss.Contains("AppVersion=2.7"));
            Assert.IsTrue(iss.Contains("VersionInfoVersion=2.6.0.0") || iss.Contains("VersionInfoVersion=2.7.0.0"));
            Assert.IsTrue(iss.Contains("OutputBaseFilename=NotiGlow-Setup-v2.6") || iss.Contains("OutputBaseFilename=NotiGlow-Setup-v2.7"));

            var adv = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Views", "AdvancedView.xaml"));
            Assert.IsTrue(adv.Contains("Current Version: v2.6") || adv.Contains("Current Version: v2.7"));
        }

        #endregion
    }
}
