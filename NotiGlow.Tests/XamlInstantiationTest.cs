using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class XamlInstantiationTest
    {
        private static void RunOnSta(Action action)
        {
            WpfTestHelper.Run(action);
        }

        [TestMethod]
        public void TestMainWindowXamlLoading()
        {
            RunOnSta(() =>
            {
                var window = new NotiGlow.UI.MainWindow();
                var settings = new NotiGlow.Services.SettingsService();
                var profile = new NotiGlow.Services.ProfileService();
                var notif = new NotiGlow.Services.NotificationService();
                var glow = new NotiGlow.Services.GlowManager(settings, profile);
                window.Initialize(settings, profile, notif, glow);
            });
        }

        [TestMethod]
        public void TestAppearanceViewDefaultStyleComboBoxHeightAndAlignment()
        {
            double cmbHeight = 0;
            double cmbMinHeight = 0;
            VerticalAlignment verticalAlignment = VerticalAlignment.Stretch;
            double contentBorderHeight = 0;

            RunOnSta(() =>
            {
                var view = new NotiGlow.UI.Views.AppearanceView();
                var cmb = view.FindName("CmbDefaultStyle") as System.Windows.Controls.ComboBox;
                if (cmb != null)
                {
                    cmbHeight = cmb.Height;
                    cmbMinHeight = cmb.MinHeight;
                    verticalAlignment = cmb.VerticalContentAlignment;

                    cmb.ApplyTemplate();
                    var contentBorder = cmb.Template?.FindName("ContentBorder", cmb) as System.Windows.Controls.Border;
                    if (contentBorder != null)
                    {
                        contentBorderHeight = contentBorder.Height;
                    }
                }
            });

            Assert.AreEqual(48.0, cmbHeight, 0.1, "CmbDefaultStyle Height should be 48px");
            Assert.AreEqual(48.0, cmbMinHeight, 0.1, "CmbDefaultStyle MinHeight should be 48px");
            Assert.AreEqual(VerticalAlignment.Center, verticalAlignment, "CmbDefaultStyle should have VerticalContentAlignment set to Center");
            Assert.AreEqual(48.0, contentBorderHeight, 0.1, "ContentBorder template part must have explicit Height 48px");
        }
    }
}
