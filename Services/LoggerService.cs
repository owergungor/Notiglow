using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GlowBorder.Services
{
    public class LoggerService
    {
        private static readonly object _lock = new object();
        private static string? _logFilePath;
        private static string? _startupLogFilePath;

        public static string LogFilePath
        {
            get
            {
                if (_logFilePath == null)
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlowBorder", "logs");
                    Directory.CreateDirectory(folder);
                    _logFilePath = Path.Combine(folder, "app.log");
                }
                return _logFilePath;
            }
        }

        public static string StartupLogFilePath
        {
            get
            {
                if (_startupLogFilePath == null)
                {
                    string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlowBorder");
                    Directory.CreateDirectory(folder);
                    _startupLogFilePath = Path.Combine(folder, "startup.log");
                }
                return _startupLogFilePath;
            }
        }

        public static void LogInfo(string message) => Log("INFO", message);
        public static void LogWarning(string message) => Log("WARN", message);
        public static void LogError(string message, Exception? ex = null)
        {
            string fullMsg = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
            Log("ERROR", fullMsg);
        }

        public static void LogStartupPhase(string phase)
        {
            try
            {
                lock (_lock)
                {
                    string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [PHASE] {phase}{Environment.NewLine}";
                    if (phase == "START")
                    {
                        entry += $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ENV] OS: {Environment.OSVersion}, Runtime: {RuntimeInformation.FrameworkDescription}{Environment.NewLine}";
                    }
                    File.AppendAllText(StartupLogFilePath, entry);
                }
            }
            catch
            {
                // Silently ignore logging failures to keep app stable
            }
        }

        public static void LogStartupError(string phase, Exception ex)
        {
            try
            {
                lock (_lock)
                {
                    string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [STARTUP_ERROR] Phase: {phase}{Environment.NewLine}" +
                                   $"OS: {Environment.OSVersion}{Environment.NewLine}" +
                                   $".NET Runtime: {RuntimeInformation.FrameworkDescription}{Environment.NewLine}" +
                                   $"Exception Type: {ex.GetType().FullName}{Environment.NewLine}" +
                                   $"Message: {ex.Message}{Environment.NewLine}" +
                                   $"Stack Trace:{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";

                    var inner = ex.InnerException;
                    int depth = 1;
                    while (inner != null)
                    {
                        entry += $"--- INNER EXCEPTION #{depth} ({inner.GetType().FullName}) ---{Environment.NewLine}" +
                                 $"Message: {inner.Message}{Environment.NewLine}" +
                                 $"Stack Trace:{Environment.NewLine}{inner.StackTrace}{Environment.NewLine}";
                        inner = inner.InnerException;
                        depth++;
                    }

                    File.AppendAllText(StartupLogFilePath, entry);
                }
            }
            catch
            {
                // Silently ignore logging failures
            }
        }

        private static void Log(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, entry);
                }
            }
            catch
            {
                // Silently ignore logging failures to keep app stable
            }
        }
    }
}

