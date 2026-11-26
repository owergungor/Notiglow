using System;
using System.IO;
using System.Text.Json;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class SettingsService
    {
        private static readonly JsonSerializerOptions WriteIndentedOptions = new() { WriteIndented = true };
        private readonly string _settingsPath;
        public AppSettings Current { get; private set; }

        public event EventHandler? SettingsChanged;

        public static string GetDefaultSettingsPath()
        {
            string? customConfigDir = Environment.GetEnvironmentVariable("NOTIGLOW_CONFIG_DIR");
            if (!string.IsNullOrWhiteSpace(customConfigDir))
            {
                Directory.CreateDirectory(customConfigDir);
                return Path.Combine(customConfigDir, "settings.json");
            }

            if (ProfileService.IsTestEnvironment)
            {
                string testDir = Path.Combine(Path.GetTempPath(), "NotiGlow_TestEnv");
                Directory.CreateDirectory(testDir);
                return Path.Combine(testDir, $"settings_{Environment.ProcessId}.json");
            }

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "NotiGlow");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }

        public SettingsService(string? customSettingsPath = null)
        {
            if (!string.IsNullOrEmpty(customSettingsPath))
            {
                _settingsPath = customSettingsPath;
                string? dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                Current = Load();
                return;
            }

            _settingsPath = GetDefaultSettingsPath();

            // Migrate legacy GlowBorder settings if present and NotiGlow settings don't exist yet
            if (!ProfileService.IsTestEnvironment)
            {
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string legacySettingsPath = Path.Combine(appData, "GlowBorder", "settings.json");
                    if (!File.Exists(_settingsPath) && File.Exists(legacySettingsPath))
                    {
                        File.Copy(legacySettingsPath, _settingsPath, true);
                        LoggerService.LogInfo("Migrated legacy GlowBorder settings.json to NotiGlow.");
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"Legacy settings migration notice: {ex.Message}");
                }
            }

            Current = Load();
        }

        private static readonly System.Collections.Generic.HashSet<string> LegacyDemoGames = new(StringComparer.OrdinalIgnoreCase)
        {
            "cs2.exe",
            "cyberpunk2077.exe",
            "eldenring.exe",
            "forzahorizon5.exe",
            "assettocorsa.exe",
            "leagueoflegends.exe",
            "valorant.exe",
            "gta5.exe",
            "rdr2.exe",
            "appactions.exe"
        };

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    int persistedSchemaVersion = 0;
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("SettingsSchemaVersion", out var svProp) &&
                            svProp.ValueKind == JsonValueKind.Number &&
                            svProp.TryGetInt32(out int sv))
                        {
                            persistedSchemaVersion = sv;
                        }
                    }
                    catch { }

                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        bool migrated = false;
                        if (persistedSchemaVersion < 5)
                        {
                            // One-time v4.x -> v5.0 migration: adopt Rust Background Worker defaults
                            // while preserving every existing user preference and profile.
                            // Once SettingsSchemaVersion == 5 is persisted, explicit user opt-outs are respected.
                            settings.SettingsSchemaVersion = 5;
                            settings.UseRustBackgroundWorker = true;
                            settings.UseRustGameDetection = true;
                            settings.UseRustGlowEngine = true;
                            migrated = true;
                        }

                        if (settings.TrackedGames != null)
                        {
                            settings.TrackedGames.RemoveAll(g =>
                            {
                                string trimmed = g.Trim();
                                return LegacyDemoGames.Contains(trimmed) ||
                                       LegacyDemoGames.Contains(Path.GetFileName(trimmed)) ||
                                       trimmed.Contains(@"\Windows\SystemApps\", StringComparison.OrdinalIgnoreCase);
                            });
                        }

                        if (migrated)
                        {
                            try
                            {
                                string migratedJson = JsonSerializer.Serialize(settings, WriteIndentedOptions);
                                File.WriteAllText(_settingsPath, migratedJson);
                                LoggerService.LogInfo("Migrated v4.x settings.json to v5.0 schema (Rust architecture enabled by default).");
                            }
                            catch (Exception mex)
                            {
                                LoggerService.LogWarning($"v5.0 settings migration save notice: {mex.Message}");
                            }
                        }

                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to load settings.json, creating defaults.", ex);
            }

            var defaultSettings = new AppSettings();
            Save(defaultSettings);
            return defaultSettings;
        }

        public void Save(AppSettings settings)
        {
            try
            {
                Current = settings;
                string json = JsonSerializer.Serialize(settings, WriteIndentedOptions);
                File.WriteAllText(_settingsPath, json);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to save settings.json", ex);
            }
        }

        /// <summary>
        /// Enforces dependency rule: sub-settings cannot be modified when GlowDuringGames is false.
        /// </summary>
        public bool UpdateGameSubSetting(Action<AppSettings> updateAction)
        {
            if (!Current.GlowDuringGames)
            {
                LoggerService.LogWarning("Attempted to modify in-game sub-settings while GlowDuringGames is disabled.");
                return false;
            }

            updateAction(Current);
            Save(Current);
            return true;
        }
    }
}
