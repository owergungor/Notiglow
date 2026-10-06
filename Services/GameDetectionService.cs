using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NotiGlow.Core.Win32;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class GameDetectionService
    {
        private readonly SettingsService _settingsService;
        private readonly SteamGameDetector _steamDetector;
        private readonly EpicGameDetector _epicDetector;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private bool _cachedIsGaming = false;
        private string _activeGameName = string.Empty;

        public string ActiveGameName => _activeGameName;

        public GameDetectionService(
            SettingsService settingsService,
            SteamGameDetector? steamDetector = null,
            EpicGameDetector? epicDetector = null)
        {
            _settingsService = settingsService;
            _steamDetector = steamDetector ?? new SteamGameDetector();
            _epicDetector = epicDetector ?? new EpicGameDetector();
        }

        public async System.Threading.Tasks.Task<IReadOnlyList<DetectedGameInfo>> DetectInstalledGamesAsync()
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var combined = new List<DetectedGameInfo>();

                try
                {
                    var steamGames = _steamDetector.DetectGames();
                    combined.AddRange(steamGames);
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Steam game detection failed", ex);
                }

                try
                {
                    var epicGames = _epicDetector.DetectGames();
                    combined.AddRange(epicGames);
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Epic game detection failed", ex);
                }

                var uniqueGames = new List<DetectedGameInfo>();
                var seenExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var game in combined)
                {
                    if (string.IsNullOrWhiteSpace(game.ExecutablePath)) continue;

                    string exeName = Path.GetFileName(game.ExecutablePath).ToLowerInvariant();
                    string fullPath = Path.GetFullPath(game.ExecutablePath).ToLowerInvariant();

                    if (!seenExes.Contains(exeName) && !seenPaths.Contains(fullPath))
                    {
                        seenExes.Add(exeName);
                        seenPaths.Add(fullPath);
                        uniqueGames.Add(game);
                    }
                }

                return (IReadOnlyList<DetectedGameInfo>)uniqueGames;
            });
        }

        public async System.Threading.Tasks.Task<int> ScanAndSyncTrackedGamesAsync()
        {
            try
            {
                var detected = await DetectInstalledGamesAsync();
                if (detected.Count == 0) return 0;

                var settings = _settingsService.Current;
                var existingExes = new HashSet<string>(
                    settings.TrackedGames.Select(g => Path.GetFileName(g.Trim())),
                    StringComparer.OrdinalIgnoreCase);

                var ignoredExes = new HashSet<string>(
                    settings.IgnoredGames.Select(g => Path.GetFileName(g.Trim())),
                    StringComparer.OrdinalIgnoreCase);

                int addedCount = 0;
                foreach (var game in detected)
                {
                    string exeName = Path.GetFileName(game.ExecutablePath);

                    // Skip if user previously deleted this game (ignored)
                    if (ignoredExes.Contains(exeName) || settings.IgnoredGames.Contains(game.ExecutablePath, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Skip if already in tracked games
                    if (existingExes.Contains(exeName) || settings.TrackedGames.Contains(game.ExecutablePath, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    settings.TrackedGames.Add(game.ExecutablePath);
                    existingExes.Add(exeName);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    _settingsService.Save(settings);
                    LoggerService.LogInfo($"Automatically synced {addedCount} games from Steam and Epic Games.");
                }

                return addedCount;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error syncing detected games with settings", ex);
                return 0;
            }
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

                string? fullProcPath = null;
                try
                {
                    fullProcPath = process.MainModule?.FileName;
                }
                catch { }

                var trackedGames = _settingsService.Current.TrackedGames;

                foreach (var game in trackedGames)
                {
                    if (string.IsNullOrWhiteSpace(game)) continue;
                    string trimmed = game.Trim();

                    // Compare full executable paths if available
                    if (!string.IsNullOrEmpty(fullProcPath) &&
                        string.Equals(fullProcPath, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        gameName = process.MainWindowTitle.Length > 0 ? process.MainWindowTitle : process.ProcessName;
                        return true;
                    }

                    // Compare executable filename without path
                    string fileName = Path.GetFileName(trimmed);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(trimmed);

                    if (procName.Equals(fileNameWithoutExt, StringComparison.OrdinalIgnoreCase) ||
                        exeName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
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
