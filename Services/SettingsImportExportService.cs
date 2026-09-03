using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class SettingsContainer
    {
        public string Version { get; set; } = "2.0.0";
        public AppSettings Settings { get; set; } = new AppSettings();
        public List<AppProfile> Profiles { get; set; } = new List<AppProfile>();
    }

    public class SettingsImportExportService
    {
        private readonly SettingsService _settingsService;
        private readonly ProfileService _profileService;

        public SettingsImportExportService(SettingsService settingsService, ProfileService profileService)
        {
            _settingsService = settingsService;
            _profileService = profileService;
        }

        public bool ExportSettings(string filePath)
        {
            try
            {
                var container = new SettingsContainer
                {
                    Version = "2.0.0",
                    Settings = _settingsService.Current,
                    Profiles = new List<AppProfile>(_profileService.Profiles)
                };

                string json = JsonSerializer.Serialize(container, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to export settings", ex);
                return false;
            }
        }

        public bool ImportSettings(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                string json = File.ReadAllText(filePath);
                var container = JsonSerializer.Deserialize<SettingsContainer>(json);
                if (container?.Settings == null || container?.Profiles == null) return false;

                _settingsService.Save(container.Settings);

                foreach (var profile in container.Profiles)
                {
                    _profileService.AddOrUpdateProfile(profile);
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to import settings", ex);
                return false;
            }
        }

        public void ResetToDefaults()
        {
            _settingsService.Save(new AppSettings());

            var defaultProfiles = ProfileService.GetDefaultProfiles();
            var currentProfiles = new List<AppProfile>(_profileService.Profiles);

            foreach (var existing in currentProfiles)
            {
                _profileService.RemoveProfile(existing.AppId);
            }

            foreach (var def in defaultProfiles)
            {
                _profileService.AddOrUpdateProfile(def);
            }
        }
    }
}
