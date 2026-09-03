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
