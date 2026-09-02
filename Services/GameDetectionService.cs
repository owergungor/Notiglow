using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GlowBorder.Core.Win32;

namespace GlowBorder.Services
{
    public class GameDetectionService
    {
        private readonly SettingsService _settingsService;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private bool _cachedIsGaming = false;
        private string _activeGameName = string.Empty;

        public string ActiveGameName => _activeGameName;

        public GameDetectionService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public bool IsGameRunning()
        {
            if (!_settingsService.Current.GamingModeEnabled)
            {
                _activeGameName = string.Empty;
                return false;
            }

            // Cache check for 1.5 seconds to minimize CPU load
            if ((DateTime.Now - _lastCheckTime).TotalMilliseconds < 1500)
            {
                return _cachedIsGaming;
            }

            _lastCheckTime = DateTime.Now;
            _cachedIsGaming = CheckForegroundProcessIsGame(out _activeGameName);
            return _cachedIsGaming;
        }

        private bool CheckForegroundProcessIsGame(out string gameName)
        {
            gameName = string.Empty;
            try
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;

                GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0) return false;

                using Process process = Process.GetProcessById((int)processId);
                string procName = process.ProcessName.ToLowerInvariant();
                string exeName = $"{procName}.exe";

                var trackedGames = _settingsService.Current.TrackedGames;

                foreach (var game in trackedGames)
                {
                    string cleanGame = game.Trim().ToLowerInvariant();
                    if (cleanGame.EndsWith(".exe")) cleanGame = cleanGame.Substring(0, cleanGame.Length - 4);

                    if (procName.Equals(cleanGame, StringComparison.OrdinalIgnoreCase))
                    {
                        gameName = process.MainWindowTitle.Length > 0 ? process.MainWindowTitle : process.ProcessName;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error during GameDetection process check", ex);
            }

            return false;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
