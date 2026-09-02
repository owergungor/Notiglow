using System;
using System.IO;
using System.Text.Json;
using GlowBorder.Models;

namespace GlowBorder.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        public AppSettings Current { get; private set; }

        public event EventHandler? SettingsChanged;

        public SettingsService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "GlowBorder");
            Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "settings.json");

            Current = Load();
        }

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                        return settings;
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
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to save settings.json", ex);
            }
        }
    }
}
