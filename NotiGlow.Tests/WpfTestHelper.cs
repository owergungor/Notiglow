using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace NotiGlow.Tests
{
    public static class WpfTestHelper
    {
        private static readonly object _lock = new();
        private static Thread? _thread;
        private static Dispatcher? _dispatcher;

        public static void Run(Action action)
        {
            lock (_lock)
            {
                if (_dispatcher == null || _thread == null || !_thread.IsAlive)
                {
                    var ready = new ManualResetEventSlim(false);
                    _thread = new Thread(() =>
                    {
                        if (Application.Current == null)
                        {
                            var app = new Application();
                            app.Resources.MergedDictionaries.Add(new Wpf.Ui.Markup.ControlsDictionary());
                        }
                        _dispatcher = Dispatcher.CurrentDispatcher;
                        ready.Set();
                        Dispatcher.Run();
                    });
                    _thread.SetApartmentState(ApartmentState.STA);
                    _thread.IsBackground = true;
                    _thread.Start();
                    ready.Wait();
                }

                Exception? caught = null;
                _dispatcher!.Invoke(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                });

                if (caught != null)
                {
                    throw caught;
                }
            }
        }
    }
}
