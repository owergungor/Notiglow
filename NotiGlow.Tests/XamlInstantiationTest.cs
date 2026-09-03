using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class XamlInstantiationTest
    {
        [TestMethod]
        public void TestMainWindowXamlLoading()
        {
            Exception? caughtEx = null;

            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        var app = new System.Windows.Application();
                        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                    }

                    var window = new NotiGlow.UI.MainWindow();
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (caughtEx != null)
            {
                string details = $"EX: {caughtEx.GetType().FullName}: {caughtEx.Message}\nStackTrace:\n{caughtEx.StackTrace}";
                var inner = caughtEx.InnerException;
                int depth = 1;
                while (inner != null)
                {
                    details += $"\n--- INNER #{depth} ({inner.GetType().FullName}) ---\nMessage: {inner.Message}\nStackTrace:\n{inner.StackTrace}";
                    inner = inner.InnerException;
                    depth++;
                }

                Assert.Fail($"XAML Load Failed:\n{details}");
            }
        }

        [TestMethod]
        public void TestAppearanceViewDefaultStyleComboBoxHeightAndAlignment()
        {
            Exception? caughtEx = null;
            double cmbHeight = 0;
            double cmbMinHeight = 0;
            System.Windows.VerticalAlignment verticalAlignment = System.Windows.VerticalAlignment.Stretch;
            double contentBorderHeight = 0;

            var thread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        var app = new System.Windows.Application();
                        app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                    }

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
                }
                catch (Exception ex)
                {
                    caughtEx = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.IsNull(caughtEx, $"Failed with exception: {caughtEx}");
            Assert.AreEqual(72.0, cmbHeight, 0.1, "CmbDefaultStyle Height should be 72px");
            Assert.AreEqual(72.0, cmbMinHeight, 0.1, "CmbDefaultStyle MinHeight should be 72px");
            Assert.AreEqual(System.Windows.VerticalAlignment.Center, verticalAlignment, "CmbDefaultStyle should have VerticalContentAlignment set to Center");
            Assert.AreEqual(72.0, contentBorderHeight, 0.1, "ContentBorder template part must have explicit Height 72px");
        }
    }
}
