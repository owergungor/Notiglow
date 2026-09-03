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
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to load profiles.json, generating defaults", ex);
            }

            // Create default starter profiles
            _profiles.AddRange(GetDefaultProfiles());
            Save();
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

            string normalizedAppId = CleanIdentityString(appId);
            string normalizedAppName = CleanIdentityString(appName);

            // Tier 1: Exact AppId or Name match
            var exactMatch = _profiles.FirstOrDefault(p =>
                (!string.IsNullOrEmpty(normalizedAppId) && (p.AppId.Equals(normalizedAppId, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(normalizedAppId, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrEmpty(normalizedAppName) && (p.AppId.Equals(normalizedAppName, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(normalizedAppName, StringComparison.OrdinalIgnoreCase))));

            if (exactMatch != null) return exactMatch;

            // Tier 2: Package Family Name / AUMID containing profile AppId (e.g. "Microsoft.WhatsApp_8wekyb3d8bbwe!App" -> "WhatsApp")
            foreach (var profile in _profiles)
            {
                string targetAppId = profile.AppId.ToLowerInvariant();
                string targetName = profile.Name.ToLowerInvariant();

                if (!string.IsNullOrEmpty(normalizedAppId))
                {
                    if (IsWordOrPackageMatch(normalizedAppId, targetAppId) || IsWordOrPackageMatch(normalizedAppId, targetName))
                        return profile;
                }

                if (!string.IsNullOrEmpty(normalizedAppName))
                {
                    if (IsWordOrPackageMatch(normalizedAppName, targetAppId) || IsWordOrPackageMatch(normalizedAppName, targetName))
                        return profile;
                }
            }

            return null;
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

            // Ensure not a accidental partial match of unrelated word
            bool leftBoundary = index == 0 || !char.IsLetterOrDigit(source[index - 1]);
            bool rightBoundary = (index + target.Length == source.Length) || !char.IsLetterOrDigit(source[index + target.Length]);

            return leftBoundary && rightBoundary;
        }

        public void AddOrUpdateProfile(AppProfile profile)
        {
            var existing = _profiles.FirstOrDefault(p => p.AppId.Equals(profile.AppId, StringComparison.OrdinalIgnoreCase));
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
            return new List<AppProfile>
            {
                new AppProfile { AppId = "Discord", Name = "Discord", Enabled = true, ColorHex = "#5865F2", DurationMs = 4000, Intensity = 0.80, Thickness = 4, GlowSize = 30, Style = GlowStyle.Pulse },
                new AppProfile { AppId = "WhatsApp", Name = "WhatsApp", Enabled = true, ColorHex = "#25D366", DurationMs = 3000, Intensity = 0.75, Thickness = 4, GlowSize = 25, Style = GlowStyle.Ambient },
                new AppProfile { AppId = "Steam", Name = "Steam", Enabled = true, ColorHex = "#66C0F4", DurationMs = 5000, Intensity = 0.70, Thickness = 4, GlowSize = 30, Style = GlowStyle.Pulse },
                new AppProfile { AppId = "Spotify", Name = "Spotify", Enabled = true, ColorHex = "#1DB954", DurationMs = 3000, Intensity = 0.70, Thickness = 4, GlowSize = 25, Style = GlowStyle.Pulse },
                new AppProfile { AppId = "Telegram", Name = "Telegram", Enabled = true, ColorHex = "#24A1DE", DurationMs = 4000, Intensity = 0.75, Thickness = 4, GlowSize = 30, Style = GlowStyle.Sweep },
                new AppProfile { AppId = "MSTeams", Name = "Microsoft Teams", Enabled = true, ColorHex = "#6264A7", DurationMs = 4000, Intensity = 0.70, Thickness = 4, GlowSize = 30, Style = GlowStyle.Pulse }
            };
        }
    }
}
