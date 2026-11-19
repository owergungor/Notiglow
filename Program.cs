using System;
using System.IO;
using System.Runtime.CompilerServices;
using NotiGlow.Services;
using WinRT;

namespace NotiGlow
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Immediate un-jitted file write as first instruction
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(localAppData, "NotiGlow");
                Directory.CreateDirectory(folder);
                string startupLog = Path.Combine(folder, "startup.log");
                File.AppendAllText(startupLog, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ENTRY] Program.Main entered.{Environment.NewLine}");
            }
            catch { }

            try
            {
                RunApp(args);
            }
            catch (Exception ex)
            {
                LoggerService.LogStartupError("Main Entry Point Exception", ex);

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logPath = Path.Combine(localAppData, "NotiGlow", "startup.log");

                string shortMsg = ex.Message;
                string message = $"NotiGlow could not start.\n\nStartup error: {shortMsg}\n\nDetailed information was written to:\n{logPath}";

                try
                {
                    NotiGlow.UI.Dialogs.ConfirmationDialogWindow.ShowError("NotiGlow Startup Error", message, "Close");
                }
                catch
                {
                    // Fallback ignored
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApp(string[] args)
        {
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch { }

            ComWrappersSupport.InitializeComWrappers();
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App(args);
            });
        }
    }
}
