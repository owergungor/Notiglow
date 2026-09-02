using System;
using System.IO;
using System.Runtime.CompilerServices;
using GlowBorder.Services;

namespace GlowBorder
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
                string folder = Path.Combine(localAppData, "GlowBorder");
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
                string logPath = Path.Combine(localAppData, "GlowBorder", "startup.log");

                string shortMsg = ex.Message;
                string message = $"NotiGlow could not start.\n\nStartup error: {shortMsg}\n\nDetailed information was written to:\n{logPath}";

                try
                {
                    System.Windows.MessageBox.Show(message, "NotiGlow Startup Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                catch
                {
                    try
                    {
                        System.Windows.Forms.MessageBox.Show(message, "NotiGlow Startup Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                    catch
                    {
                        // Fallback ignored
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApp(string[] args)
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
