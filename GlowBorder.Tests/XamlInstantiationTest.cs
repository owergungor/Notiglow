using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlowBorder.Tests
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

                    var window = new GlowBorder.UI.MainWindow();
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
    }
}
