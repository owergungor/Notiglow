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
        private readonly ProfileService? _profileService;
        private readonly SteamGameDetector _steamDetector;
        private readonly EpicGameDetector _epicDetector;
        private readonly XboxAndStoreGameDetector _xboxStoreDetector;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private bool _cachedIsGaming = false;
        private string _activeGameName = string.Empty;
        private string _activeGameIdentifier = string.Empty;
        private string? _simulatedGameName = null;
        private bool _externalWorkerOwnership = false;
        private bool _externalIsGameRunning = false;

        public bool IsExternalWorkerOwned => _externalWorkerOwnership;

        public void EnableExternalWorkerOwnership()
        {
            _externalWorkerOwnership = true;
        }

        public void DisableExternalWorkerOwnership()
        {
            _externalWorkerOwnership = false;
            _externalIsGameRunning = false;
            _lastCheckTime = DateTime.MinValue;
        }

        public void UpdateExternalActiveGame(string? gameName, string? identifier)
        {
            if (string.IsNullOrWhiteSpace(gameName) && string.IsNullOrWhiteSpace(identifier))
            {
                _externalIsGameRunning = false;
                _activeGameName = string.Empty;
                _activeGameIdentifier = string.Empty;
            }
            else
            {
                _externalIsGameRunning = true;
                _activeGameName = gameName ?? identifier ?? string.Empty;
                _activeGameIdentifier = AppSettings.NormalizeGameIdentifier(identifier ?? gameName);
            }
        }

        public string ActiveGameName
        {
            get => _activeGameName;
            set => _activeGameName = value ?? string.Empty;
        }

        public string ActiveGameIdentifier
        {
            get => _activeGameIdentifier;
            set => _activeGameIdentifier = value ?? string.Empty;
        }

        public bool IsSimulatingGame => !string.IsNullOrEmpty(_simulatedGameName);

        public void SetSimulatedGame(string? gameName)
        {
            _simulatedGameName = gameName;
            if (!string.IsNullOrEmpty(gameName))
            {
                _activeGameName = gameName;
                _activeGameIdentifier = AppSettings.NormalizeGameIdentifier(gameName);
            }
            else
            {
                _activeGameName = string.Empty;
                _activeGameIdentifier = string.Empty;
            }
        }

        public GameDetectionService(
            SettingsService settingsService,
            SteamGameDetector? steamDetector = null,
            EpicGameDetector? epicDetector = null,
            XboxAndStoreGameDetector? xboxStoreDetector = null,
            ProfileService? profileService = null)
        {
            _settingsService = settingsService;
            _profileService = profileService;
            _steamDetector = steamDetector ?? new SteamGameDetector();
            _epicDetector = epicDetector ?? new EpicGameDetector();
            bool isMockContext = steamDetector != null || epicDetector != null;
            _xboxStoreDetector = xboxStoreDetector ?? (isMockContext ? XboxAndStoreGameDetector.CreateEmpty() : new XboxAndStoreGameDetector());
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

                try
                {
                    var xboxStoreGames = _xboxStoreDetector.DetectGames();
                    combined.AddRange(xboxStoreGames);
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Xbox and Store game detection failed", ex);
                }

                var uniqueGames = new List<DetectedGameInfo>();
                var seenExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var game in combined)
                {
                    if (string.IsNullOrWhiteSpace(game.ExecutablePath)) continue;

                    string exeName = Path.GetFileName(game.ExecutablePath).ToLowerInvariant();
                    if (exeName.Equals("appactions.exe", StringComparison.OrdinalIgnoreCase) ||
                        game.ExecutablePath.Contains(@"\Windows\SystemApps\", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string fullPath = game.ExecutablePath.Trim().ToLowerInvariant();
                    try
                    {
                        if (Path.IsPathRooted(fullPath)) fullPath = Path.GetFullPath(fullPath).ToLowerInvariant();
                    }
                    catch { }

                    string resolvedName = !string.IsNullOrWhiteSpace(game.Name)
                        ? Core.Helpers.AppCategoryHelper.ResolveFriendlyGameName(game.ExecutablePath, new AppProfile { Name = game.Name })
                        : Core.Helpers.AppCategoryHelper.ResolveFriendlyGameName(game.ExecutablePath);
                    game.Name = resolvedName;

                    string nameKey = resolvedName.Trim().ToLowerInvariant();
                    bool isSharedExe = Core.Helpers.AppCategoryHelper.IsSharedEngineExecutable(exeName);

                    bool exeDuplicate = !isSharedExe && seenExes.Contains(exeName);
                    bool pathDuplicate = seenPaths.Contains(fullPath);
                    bool nameDuplicate = !string.IsNullOrEmpty(nameKey) && seenNames.Contains(nameKey);

                    if (!exeDuplicate && !pathDuplicate && !nameDuplicate)
                    {
                        if (!isSharedExe) seenExes.Add(exeName);
                        seenPaths.Add(fullPath);
                        if (!string.IsNullOrEmpty(nameKey)) seenNames.Add(nameKey);
                        uniqueGames.Add(game);
                    }
                }

                return (IReadOnlyList<DetectedGameInfo>)uniqueGames;
            });
        }

        public async System.Threading.Tasks.Task<int> ScanAndSyncTrackedGamesAsync(ProfileService? profileService = null)
        {
            try
            {
                var targetProfileService = profileService ?? _profileService;
                var detected = await DetectInstalledGamesAsync();
                if (detected.Count == 0) return 0;

                var settings = _settingsService.Current;
                settings.TrackedGames ??= new List<string>();
                settings.IgnoredGames ??= new List<string>();

                // Clean up any stale Windows SystemApps false positive
                bool removedStale = settings.TrackedGames.RemoveAll(g =>
                    g.Contains(@"\Windows\SystemApps\", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(g.Trim()).Equals("AppActions.exe", StringComparison.OrdinalIgnoreCase)) > 0;

                var existingExes = new HashSet<string>(
                    settings.TrackedGames
                        .Select(g => Path.GetFileName(g.Trim()))
                        .Where(fn => !Core.Helpers.AppCategoryHelper.IsSharedEngineExecutable(fn)),
                    StringComparer.OrdinalIgnoreCase);

                var existingPaths = new HashSet<string>(
                    settings.TrackedGames.Select(g => ProfileService.NormalizePath(g)),
                    StringComparer.OrdinalIgnoreCase);

                var existingNames = new HashSet<string>(
                    settings.TrackedGames.Select(g => Core.Helpers.AppCategoryHelper.ResolveFriendlyGameName(g)),
                    StringComparer.OrdinalIgnoreCase);

                var ignoredExes = new HashSet<string>(
                    settings.IgnoredGames
                        .Select(g => Path.GetFileName(g.Trim()))
                        .Where(fn => !Core.Helpers.AppCategoryHelper.IsSharedEngineExecutable(fn)),
                    StringComparer.OrdinalIgnoreCase);

                int addedCount = 0;
                foreach (var game in detected)
                {
                    string exeName = Path.GetFileName(game.ExecutablePath);
                    string normPath = ProfileService.NormalizePath(game.ExecutablePath);
                    string friendlyName = Core.Helpers.AppCategoryHelper.ResolveFriendlyGameName(
                        game.ExecutablePath,
                        !string.IsNullOrWhiteSpace(game.Name) ? new AppProfile { Name = game.Name } : null);
                    bool isSharedExe = Core.Helpers.AppCategoryHelper.IsSharedEngineExecutable(exeName);

                    // Skip if user previously deleted this game (ignored)
                    if ((!isSharedExe && ignoredExes.Contains(exeName)) ||
                        settings.IgnoredGames.Contains(game.ExecutablePath, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool alreadyTracked = existingPaths.Contains(normPath) ||
                                          (!isSharedExe && existingExes.Contains(exeName)) ||
                                          (!string.IsNullOrEmpty(friendlyName) && existingNames.Contains(friendlyName));

                    if (!alreadyTracked)
                    {
                        settings.TrackedGames.Add(game.ExecutablePath);
                        existingPaths.Add(normPath);
                        if (!isSharedExe) existingExes.Add(exeName);
                        if (!string.IsNullOrEmpty(friendlyName)) existingNames.Add(friendlyName);
                        addedCount++;
                    }

                    // Ensure ProfileService has the friendly display name and Gaming category while preserving user settings
                    EnsureDetectedGameProfile(game, friendlyName, targetProfileService, settings);
                }

                if (addedCount > 0 || removedStale)
                {
                    _settingsService.Save(settings);
                    LoggerService.LogInfo($"Automatically synced {addedCount} games from launchers (Steam, Epic, Xbox, Microsoft Store).");
                }

                return addedCount;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error syncing detected games with settings", ex);
                return 0;
            }
        }

        public static void EnsureDetectedGameProfile(
            DetectedGameInfo game,
            string friendlyName,
            ProfileService? profileService,
            AppSettings settings)
        {
            if (profileService == null || game == null || string.IsNullOrWhiteSpace(game.ExecutablePath))
                return;

            var existing = profileService.Profiles.FirstOrDefault(p =>
                Core.Helpers.AppCategoryHelper.MatchesIdentifier(p, game.ExecutablePath) ||
                (!string.IsNullOrEmpty(friendlyName) && string.Equals(p.Name, friendlyName, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                // If user explicitly moved this profile to General, respect their choice; otherwise ensure clean name & path
                bool changed = false;
                if (Core.Helpers.AppCategoryHelper.IsGamingCategory(existing.Category))
                {
                    string resolvedExistingName = Core.Helpers.AppCategoryHelper.ResolveFriendlyGameName(game.ExecutablePath, existing);
                    if (!string.Equals(existing.Name, resolvedExistingName, StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(friendlyName))
                    {
                        existing.Name = friendlyName;
                        changed = true;
                    }
                    if (string.IsNullOrWhiteSpace(existing.ExecutablePath))
                    {
                        existing.ExecutablePath = game.ExecutablePath;
                        changed = true;
                    }
                    if (changed)
                    {
                        profileService.AddOrUpdateProfile(existing);
                    }
                }
                return;
            }

            string normId = AppSettings.NormalizeGameIdentifier(game.ExecutablePath);
            GameGlowOverride? ov = null;
            if (!string.IsNullOrEmpty(normId))
            {
                settings.GameOverrides?.TryGetValue(normId, out ov);
            }

            string appId = Core.Helpers.AppCategoryHelper.IsSharedEngineExecutable(game.ExecutablePath)
                ? (!string.IsNullOrWhiteSpace(game.AppId) ? $"{friendlyName}_{game.AppId}" : friendlyName)
                : Path.GetFileName(game.ExecutablePath);

            var newProfile = new AppProfile
            {
                AppId = appId,
                Name = !string.IsNullOrWhiteSpace(friendlyName) ? friendlyName : game.Name,
                ExecutablePath = game.ExecutablePath,
                Category = Core.Helpers.AppCategoryHelper.CategoryGaming,
                Enabled = true,
                Priority = NotificationPriority.Normal,
                ColorHex = ov?.ColorHex ?? settings.DefaultColorHex,
                DurationMs = ov?.DurationMs ?? (settings.CommonGamingDurationMs ?? 3000),
                Intensity = ov?.Intensity ?? (settings.CommonGamingIntensity ?? 0.8),
                GlowSize = ov?.GlowSize ?? (settings.CommonGamingGlowSize ?? 32.0),
                CornerRadius = ov?.CornerRadius ?? (settings.CommonGamingCornerRadius ?? 0.0),
                Style = ov?.Style ?? settings.DefaultStyle,
                Thickness = settings.DefaultThickness,
                IsCustom = true
            };

            profileService.AddOrUpdateProfile(newProfile);
        }

        public bool IsGameRunning()
        {
            if (!string.IsNullOrEmpty(_simulatedGameName))
            {
                _activeGameName = _simulatedGameName;
                return true;
            }

            if (!_settingsService.Current.GamingModeEnabled)
            {
                _activeGameName = string.Empty;
                return false;
            }

            if (_externalWorkerOwnership)
            {
                return _externalIsGameRunning;
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
                string procName = process.ProcessName;
                string exeName = $"{procName}.exe";

                var trackedGames = _settingsService.Current.TrackedGames;
                if (trackedGames == null || trackedGames.Count == 0)
                {
                    _activeGameIdentifier = string.Empty;
                    return false;
                }

                string? fullProcPath = null;
                bool fullPathQueried = false;

                foreach (var game in trackedGames)
                {
                    if (string.IsNullOrWhiteSpace(game)) continue;
                    string trimmed = game.Trim();

                    // Fast in-memory filename check first (avoids expensive Win32 MainModule lookup when process name doesn't match)
                    string fileName = Path.GetFileName(trimmed);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(trimmed);

                    bool nameMatches = procName.Equals(fileNameWithoutExt, StringComparison.OrdinalIgnoreCase) ||
                                       exeName.Equals(fileName, StringComparison.OrdinalIgnoreCase);

                    if (!nameMatches)
                    {
                        continue;
                    }

                    // If the tracked entry specifies a directory path, optionally verify MainModule path if accessible
                    if (trimmed.Contains('\\') || trimmed.Contains('/'))
                    {
                        if (!fullPathQueried)
                        {
                            fullPathQueried = true;
                            try
                            {
                                fullProcPath = process.MainModule?.FileName;
                            }
                            catch { }
                        }

                        if (!string.IsNullOrEmpty(fullProcPath) &&
                            string.Equals(fullProcPath, trimmed, StringComparison.OrdinalIgnoreCase))
                        {
                            gameName = process.MainWindowTitle.Length > 0 ? process.MainWindowTitle : process.ProcessName;
                            _activeGameIdentifier = AppSettings.NormalizeGameIdentifier(trimmed);
                            return true;
                        }
                    }

                    gameName = process.MainWindowTitle.Length > 0 ? process.MainWindowTitle : process.ProcessName;
                    _activeGameIdentifier = AppSettings.NormalizeGameIdentifier(trimmed);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error during GameDetection process check", ex);
            }

            _activeGameIdentifier = string.Empty;
            return false;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
