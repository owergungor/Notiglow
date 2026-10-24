using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class ProfileService
    {
        private readonly string _profilesPath;
        private readonly List<AppProfile> _profiles = new();

        public IReadOnlyList<AppProfile> Profiles => _profiles.AsReadOnly();

        public event EventHandler? ProfilesChanged;

        public ProfileService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "NotiGlow");
            Directory.CreateDirectory(folder);
            _profilesPath = Path.Combine(folder, "profiles.json");

            // Migrate legacy GlowBorder profiles if present and NotiGlow profiles don't exist yet
            try
            {
                string legacyProfilesPath = Path.Combine(appData, "GlowBorder", "profiles.json");
                if (!File.Exists(_profilesPath) && File.Exists(legacyProfilesPath))
                {
                    File.Copy(legacyProfilesPath, _profilesPath, true);
                    LoggerService.LogInfo("Migrated legacy GlowBorder profiles.json to NotiGlow.");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Legacy profile migration notice: {ex.Message}");
            }

            Load();
        }

        public static bool IsTestEnvironment { get; set; } = DetectTestHostEnvironment();

        private static bool DetectTestHostEnvironment()
        {
            try
            {
                var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                if (processName.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    processName.IndexOf("vstest", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                var domainName = AppDomain.CurrentDomain.FriendlyName;
                if (domainName.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    domainName.IndexOf("vstest", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName().Name ?? string.Empty;
                    if (name.IndexOf("VisualStudio.TestPlatform", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MSTest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("NotiGlow.Tests", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public void Load()
        {
            _profiles.Clear();
            try
            {
                if (File.Exists(_profilesPath))
                {
                    string json = File.ReadAllText(_profilesPath);
                    var loaded = JsonSerializer.Deserialize<List<AppProfile>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        var deduplicated = DeduplicateProfiles(loaded);
                        _profiles.AddRange(deduplicated);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to load profiles.json, generating defaults", ex);
            }

            if (!IsTestEnvironment)
            {
                SyncWithInstalledApps();
            }
            else
            {
                if (_profiles.Count == 0)
                {
                    _profiles.AddRange(GetDefaultProfiles());
                }
                PruneOrResolveProfiles();
            }

            Save();
        }

        public static string NormalizePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;
            string clean = Environment.ExpandEnvironmentVariables(rawPath.Trim().Trim('\"'));
            try
            {
                return Path.GetFullPath(clean);
            }
            catch
            {
                return clean;
            }
        }

        public static List<AppProfile> DeduplicateProfiles(IEnumerable<AppProfile> profiles)
        {
            var result = new List<AppProfile>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenAppIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in profiles)
            {
                if (p == null) continue;

                string normPath = !string.IsNullOrEmpty(p.ExecutablePath)
                    ? NormalizePath(p.ExecutablePath)
                    : string.Empty;

                string exeName = !string.IsNullOrEmpty(normPath)
                    ? Path.GetFileName(normPath).ToLowerInvariant()
                    : (p.AppId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p.AppId.ToLowerInvariant() : string.Empty);

                string cleanId = CleanIdentityString(p.AppId);
                string cleanName = CleanIdentityString(p.Name);

                bool isDuplicate = false;
                if (!string.IsNullOrEmpty(normPath) && seenPaths.Contains(normPath)) isDuplicate = true;
                if (!string.IsNullOrEmpty(exeName) && seenExes.Contains(exeName)) isDuplicate = true;
                if (!string.IsNullOrEmpty(cleanId) && seenAppIds.Contains(cleanId)) isDuplicate = true;

                if (isDuplicate)
                {
                    // Merge/enrich existing entry if this one has valid executable path
                    var existing = result.FirstOrDefault(x =>
                        (!string.IsNullOrEmpty(normPath) && !string.IsNullOrEmpty(x.ExecutablePath) && NormalizePath(x.ExecutablePath).Equals(normPath, StringComparison.OrdinalIgnoreCase)) ||
                        CleanIdentityString(x.AppId).Equals(cleanId, StringComparison.OrdinalIgnoreCase));
                    if (existing != null && string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(normPath))
                    {
                        existing.ExecutablePath = normPath;
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(normPath))
                {
                    p.ExecutablePath = normPath;
                    seenPaths.Add(normPath);
                }
                if (!string.IsNullOrEmpty(exeName)) seenExes.Add(exeName);
                if (!string.IsNullOrEmpty(cleanId)) seenAppIds.Add(cleanId);
                if (!string.IsNullOrEmpty(cleanName)) seenAppIds.Add(cleanName);

                result.Add(p);
            }

            return result;
        }

        private void PruneOrResolveProfiles()
        {
            if (IsTestEnvironment) return;

            var builtInAppIds = new HashSet<string>(
                Core.Helpers.BuiltInAppDetector.Definitions.Select(d => d.AppId),
                StringComparer.OrdinalIgnoreCase);

            for (int i = _profiles.Count - 1; i >= 0; i--)
            {
                var profile = _profiles[i];

                // If executable path is missing or does not exist on disk
                if (!string.IsNullOrEmpty(profile.ExecutablePath) && !File.Exists(profile.ExecutablePath))
                {
                    // If it's a built-in app, attempt to detect real installed path
                    if (builtInAppIds.Contains(profile.AppId))
                    {
                        var def = Core.Helpers.BuiltInAppDetector.Definitions.FirstOrDefault(d =>
                            d.AppId.Equals(profile.AppId, StringComparison.OrdinalIgnoreCase));
                        if (def != null && Core.Helpers.BuiltInAppDetector.Default.TryDetectApp(def, out string realPath))
                        {
                            profile.ExecutablePath = realPath;
                            continue;
                        }
                    }

                    // Not found or custom app file no longer exists: prune to keep list clean!
                    _profiles.RemoveAt(i);
                    continue;
                }

                // If built-in app has no ExecutablePath yet, try to detect it
                if (builtInAppIds.Contains(profile.AppId) && string.IsNullOrEmpty(profile.ExecutablePath))
                {
                    var def = Core.Helpers.BuiltInAppDetector.Definitions.FirstOrDefault(d =>
                        d.AppId.Equals(profile.AppId, StringComparison.OrdinalIgnoreCase));
                    if (def != null && Core.Helpers.BuiltInAppDetector.Default.TryDetectApp(def, out string realPath))
                    {
                        profile.ExecutablePath = realPath;
                    }
                    else
                    {
                        _profiles.RemoveAt(i);
                    }
                }
            }
        }

        public int SyncWithInstalledApps()
        {
            if (IsTestEnvironment) return 0;

            int newlyAdded = 0;
            bool modified = false;

            try
            {
                var installedProfiles = Core.Helpers.InstalledAppScanner.Default.DetectAllInstalledApps();

                var existingPaths = new Dictionary<string, AppProfile>(StringComparer.OrdinalIgnoreCase);
                var existingExes = new Dictionary<string, AppProfile>(StringComparer.OrdinalIgnoreCase);
                var existingIds = new Dictionary<string, AppProfile>(StringComparer.OrdinalIgnoreCase);
                var existingNames = new Dictionary<string, AppProfile>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in _profiles)
                {
                    if (!string.IsNullOrEmpty(p.ExecutablePath))
                    {
                        string norm = NormalizePath(p.ExecutablePath);
                        existingPaths[norm] = p;
                        string exe = Path.GetFileName(norm).ToLowerInvariant();
                        if (!string.IsNullOrEmpty(exe)) existingExes[exe] = p;
                    }
                    string cleanId = CleanIdentityString(p.AppId);
                    if (!string.IsNullOrEmpty(cleanId)) existingIds[cleanId] = p;
                    string cleanName = CleanIdentityString(p.Name);
                    if (!string.IsNullOrEmpty(cleanName)) existingNames[cleanName] = p;
                }

                foreach (var installed in installedProfiles)
                {
                    if (string.IsNullOrEmpty(installed.ExecutablePath) || !File.Exists(installed.ExecutablePath))
                        continue;

                    string normPath = NormalizePath(installed.ExecutablePath);
                    string exeName = Path.GetFileName(normPath).ToLowerInvariant();
                    string cleanId = CleanIdentityString(installed.AppId);
                    string cleanName = CleanIdentityString(installed.Name);

                    AppProfile? existing = null;
                    if (existingPaths.TryGetValue(normPath, out var matchP)) existing = matchP;
                    else if (existingExes.TryGetValue(exeName, out var matchE)) existing = matchE;
                    else if (existingIds.TryGetValue(cleanId, out var matchI)) existing = matchI;
                    else if (existingNames.TryGetValue(cleanName, out var matchN)) existing = matchN;

                    if (existing != null)
                    {
                        if (string.IsNullOrEmpty(existing.ExecutablePath) || !File.Exists(existing.ExecutablePath))
                        {
                            existing.ExecutablePath = normPath;
                            modified = true;
                        }
                    }
                    else
                    {
                        _profiles.Add(installed);
                        existingPaths[normPath] = installed;
                        existingExes[exeName] = installed;
                        if (!string.IsNullOrEmpty(cleanId)) existingIds[cleanId] = installed;
                        if (!string.IsNullOrEmpty(cleanName)) existingNames[cleanName] = installed;
                        newlyAdded++;
                        modified = true;
                    }
                }

                // Prune stale non-existent apps
                // Rule E: "Kullanıcının daha önce takip ettiği ama artık cihazda olmayan built-in app: gösterme"
                // Rule F: "Kullanıcının elle eklediği custom app: mevcut davranışı bozma"
                // Rule G: "Uygulama executable path'i: gerçekten mevcut olmalı"
                for (int i = _profiles.Count - 1; i >= 0; i--)
                {
                    var p = _profiles[i];
                    if (!p.IsCustom)
                    {
                        if (string.IsNullOrEmpty(p.ExecutablePath) || !File.Exists(p.ExecutablePath))
                        {
                            _profiles.RemoveAt(i);
                            modified = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("SyncWithInstalledApps error", ex);
            }

            if (modified)
            {
                Save();
            }

            return newlyAdded;
        }

        public async System.Threading.Tasks.Task<int> ScanAndSyncInstalledAppsAsync()
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                int added = SyncWithInstalledApps();
                if (added > 0)
                {
                    ProfilesChanged?.Invoke(this, EventArgs.Empty);
                }
                return added;
            });
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_profilesPath, json);
                ProfilesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to save profiles.json", ex);
            }
        }

        public AppProfile? GetProfile(string appId, string appName = "")
        {
            if (string.IsNullOrWhiteSpace(appId) && string.IsNullOrWhiteSpace(appName))
                return null;

            string rawAppId = appId?.Trim() ?? string.Empty;
            string cleanAppId = CleanIdentityString(appId ?? string.Empty);
            string rawAppName = appName?.Trim() ?? string.Empty;
            string cleanAppName = CleanIdentityString(appName ?? string.Empty);

            // Tier 1: Exact or Clean AppId / Name / ExecutablePath match
            foreach (var p in _profiles)
            {
                string pAppId = p.AppId.Trim();
                string pCleanAppId = CleanIdentityString(p.AppId);
                string pName = p.Name.Trim();
                string pCleanName = CleanIdentityString(p.Name);
                string pExeName = !string.IsNullOrEmpty(p.ExecutablePath) ? Path.GetFileName(p.ExecutablePath) : string.Empty;
                string pCleanExeName = CleanIdentityString(pExeName);

                if (MatchesAny(rawAppId, pAppId, pCleanAppId, pName, pCleanName, pExeName, pCleanExeName) ||
                    MatchesAny(cleanAppId, pAppId, pCleanAppId, pName, pCleanName, pExeName, pCleanExeName) ||
                    MatchesAny(rawAppName, pAppId, pCleanAppId, pName, pCleanName, pExeName, pCleanExeName) ||
                    MatchesAny(cleanAppName, pAppId, pCleanAppId, pName, pCleanName, pExeName, pCleanExeName))
                {
                    return p;
                }
            }

            // Tier 2: Package Family Name / AUMID containing profile identifiers
            foreach (var profile in _profiles)
            {
                string targetAppId = CleanIdentityString(profile.AppId).ToLowerInvariant();
                string targetName = CleanIdentityString(profile.Name).ToLowerInvariant();
                string targetExe = !string.IsNullOrEmpty(profile.ExecutablePath)
                    ? CleanIdentityString(Path.GetFileName(profile.ExecutablePath)).ToLowerInvariant()
                    : string.Empty;

                if (!string.IsNullOrEmpty(cleanAppId))
                {
                    if (IsWordOrPackageMatch(cleanAppId, targetAppId) ||
                        IsWordOrPackageMatch(cleanAppId, targetName) ||
                        (!string.IsNullOrEmpty(targetExe) && IsWordOrPackageMatch(cleanAppId, targetExe)))
                    {
                        return profile;
                    }
                }

                if (!string.IsNullOrEmpty(cleanAppName))
                {
                    if (IsWordOrPackageMatch(cleanAppName, targetAppId) ||
                        IsWordOrPackageMatch(cleanAppName, targetName) ||
                        (!string.IsNullOrEmpty(targetExe) && IsWordOrPackageMatch(cleanAppName, targetExe)))
                    {
                        return profile;
                    }
                }

                // Also check if profile name/appId is contained within the rawAppId (e.g. AUMID contains "WhatsApp")
                if (!string.IsNullOrEmpty(rawAppId))
                {
                    if (IsWordOrPackageMatch(rawAppId, targetAppId) ||
                        IsWordOrPackageMatch(rawAppId, targetName) ||
                        (!string.IsNullOrEmpty(targetExe) && IsWordOrPackageMatch(rawAppId, targetExe)))
                    {
                        return profile;
                    }
                }
            }

            return null;
        }

        private static bool MatchesAny(string input, params string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && candidate.Equals(input, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string CleanIdentityString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string s = input.Trim();
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 4);
            }
            return s;
        }

        private static bool IsWordOrPackageMatch(string source, string target)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return false;
            if (target.Length < 3) return source.Equals(target, StringComparison.OrdinalIgnoreCase);

            // Check if source contains target as a distinct component/word
            int index = source.IndexOf(target, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;

            // Ensure not an accidental partial match of unrelated word
            bool leftBoundary = index == 0 || !char.IsLetterOrDigit(source[index - 1]);
            bool rightBoundary = (index + target.Length == source.Length) || !char.IsLetterOrDigit(source[index + target.Length]);

            return leftBoundary && rightBoundary;
        }

        public void AddOrUpdateProfile(AppProfile profile)
        {
            string normPath = !string.IsNullOrEmpty(profile.ExecutablePath) ? NormalizePath(profile.ExecutablePath) : string.Empty;
            string cleanId = CleanIdentityString(profile.AppId);
            string exeName = !string.IsNullOrEmpty(normPath) ? Path.GetFileName(normPath).ToLowerInvariant() : string.Empty;

            var existing = _profiles.FirstOrDefault(p =>
                p.AppId.Equals(profile.AppId, StringComparison.OrdinalIgnoreCase) ||
                CleanIdentityString(p.AppId).Equals(cleanId, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(normPath) && !string.IsNullOrEmpty(p.ExecutablePath) && NormalizePath(p.ExecutablePath).Equals(normPath, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(exeName) && !string.IsNullOrEmpty(p.ExecutablePath) && Path.GetFileName(p.ExecutablePath).Equals(exeName, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(profile.Name) && p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                _profiles.Remove(existing);
            }
            if (!string.IsNullOrEmpty(normPath))
            {
                profile.ExecutablePath = normPath;
            }
            _profiles.Add(profile);
            Save();
        }

        public bool RemoveProfile(string appId)
        {
            var profile = _profiles.FirstOrDefault(p => p.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));
            if (profile != null)
            {
                _profiles.Remove(profile);
                Save();
                return true;
            }
            return false;
        }

        public static List<AppProfile> GetDefaultProfiles()
        {
            return Core.Helpers.BuiltInAppDetector.Definitions.Select(def => new AppProfile
            {
                AppId = def.AppId,
                Name = def.Name,
                Category = def.Category,
                Enabled = true,
                ColorHex = def.ColorHex,
                DurationMs = def.DurationMs,
                Intensity = def.Intensity,
                Thickness = 4.0,
                GlowSize = 32.0,
                CornerRadius = 0.0,
                Style = def.Style
            }).ToList();
        }
    }
}
