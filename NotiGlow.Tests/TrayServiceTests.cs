using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class TrayServiceTests
    {
        [TestMethod]
        public void UpdateRoute_FollowsDistributionChannel()
        {
            Assert.AreEqual(UpdateRoute.GitHubSelfUpdate, PackageService.GetUpdateRoute(false));
            Assert.AreEqual(UpdateRoute.MicrosoftStorePage, PackageService.GetUpdateRoute(true));
            Assert.AreEqual("https://apps.microsoft.com/detail/9P9L33HG5V64", PackageService.StoreUrl);
        }

        [TestMethod]
        public void TrayService_InitializesWithValidIcon_AndBecomesVisible()
        {
            Exception? caughtEx = null;
            bool isVisible = false;
            bool hasIcon = false;
            int iconWidth = 0;

            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        _ = new System.Windows.Application();
                    }

                    var settingsService = new SettingsService();
                    using var trayService = new TrayService(
                        settingsService,
                        () => { },
                        () => { },
                        () => { }
                    );

                    isVisible = trayService.IsVisible;
                    hasIcon = trayService.CurrentIcon != null;
                    Assert.IsTrue(trayService.ContextMenuForTesting.Items.Count >= 8, "Tray menu should expose the primary controls and separators.");
                    Assert.AreEqual(LocalizationService.Instance.GetString("Tray.Show"), trayService.ContextMenuForTesting.Items[0].Text);
                    Assert.AreEqual(LocalizationService.Instance.GetString(PackageService.IsPackaged ? "Advanced.UpdateInStore" : "Advanced.CheckForUpdates"), trayService.ContextMenuForTesting.Items[6].Text);
                    if (trayService.CurrentIcon != null)
                    {
                        iconWidth = trayService.CurrentIcon.Width;
                    }
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caughtEx, $"TrayService initialization threw exception: {caughtEx}");
            Assert.IsTrue(isVisible, "TrayService should be visible after initialization.");
            Assert.IsTrue(hasIcon, "TrayService should have a valid icon assigned.");
            Assert.IsTrue(iconWidth > 0, "Tray icon width should be greater than 0.");
        }

        [TestMethod]
        public void TrayMenu_RefreshesColorsWhenThemeChanges()
        {
            Exception? caughtEx = null;
            bool updatedToLightTheme = false;
            string settingsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"notiglow-test-{Guid.NewGuid():N}.json");
            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null) _ = new System.Windows.Application();
                    var settings = new SettingsService(settingsPath);
                    using var tray = new TrayService(settings, () => { }, () => { }, () => { });
                    settings.Current.ThemeMode = NotiGlow.Models.ThemeMode.Light;
                    settings.Save(settings.Current);
                    var expected = System.Drawing.ColorTranslator.FromHtml(ThemeService.GetPalette(settings.Current.ColorTheme, false).CardBackground);
                    updatedToLightTheme = tray.ContextMenuForTesting.BackColor == expected;
                }
                catch (Exception ex) { caughtEx = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            try { if (System.IO.File.Exists(settingsPath)) System.IO.File.Delete(settingsPath); } catch { }
            Assert.IsNull(caughtEx, $"Tray theme refresh threw exception: {caughtEx}");
            Assert.IsTrue(updatedToLightTheme, "Tray menu should refresh using the active theme palette.");
        }

        [TestMethod]
        public void TrayService_Dispose_HidesTrayIcon()
        {
            Exception? caughtEx = null;
            bool visibleAfterDispose = true;

            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        _ = new System.Windows.Application();
                    }

                    var settingsService = new SettingsService();
                    var trayService = new TrayService(
                        settingsService,
                        () => { },
                        () => { },
                        () => { }
                    );

                    trayService.Dispose();
                    visibleAfterDispose = trayService.IsVisible;
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caughtEx, $"TrayService dispose threw exception: {caughtEx}");
            Assert.IsFalse(visibleAfterDispose, "TrayService should not be visible after Dispose().");
        }
    }
}
