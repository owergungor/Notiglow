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
                        _profiles.AddRange(loaded);
                        PruneOrResolveBuiltInProfiles();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to load profiles.json, generating defaults", ex);
            }

            // Create default starter profiles with authentic installed paths
            _profiles.AddRange(GetInitialStarterProfiles());
            Save();
        }

        private List<AppProfile> GetInitialStarterProfiles()
        {
            if (IsTestEnvironment)
            {
                return GetDefaultProfiles();
            }

            return Core.Helpers.BuiltInAppDetector.Default.GetInstalledProfiles().ToList();
        }

        private void PruneOrResolveBuiltInProfiles()
        {
            if (IsTestEnvironment) return;

            var builtInAppIds = new HashSet<string>(
                Core.Helpers.BuiltInAppDetector.Definitions.Select(d => d.AppId),
                StringComparer.OrdinalIgnoreCase);

            for (int i = _profiles.Count - 1; i >= 0; i--)
            {
                var profile = _profiles[i];
                if (!builtInAppIds.Contains(profile.AppId))
                {
                    // User's custom profile - always keep!
                    continue;
                }

                var def = Core.Helpers.BuiltInAppDetector.Definitions.FirstOrDefault(d =>
                    d.AppId.Equals(profile.AppId, StringComparison.OrdinalIgnoreCase));
                if (def == null) continue;

                // Check if existing ExecutablePath is valid
                if (!string.IsNullOrEmpty(profile.ExecutablePath) && File.Exists(profile.ExecutablePath))
                {
                    continue;
                }

                // Attempt to detect authentic installation path
                if (Core.Helpers.BuiltInAppDetector.Default.TryDetectApp(def, out string realPath))
                {
                    profile.ExecutablePath = realPath;
                }
                else
                {
                    // App is not installed on this machine: remove from built-in tracked list!
                    _profiles.RemoveAt(i);
                }
            }
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
            var existing = _profiles.FirstOrDefault(p =>
                p.AppId.Equals(profile.AppId, StringComparison.OrdinalIgnoreCase) ||
                CleanIdentityString(p.AppId).Equals(CleanIdentityString(profile.AppId), StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(profile.Name) && p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)));
            if (existing != null)
            {
                _profiles.Remove(existing);
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
                GlowSize = 30.0,
                Style = def.Style
            }).ToList();
        }
    }
}
