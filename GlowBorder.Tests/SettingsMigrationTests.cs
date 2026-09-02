using System.IO;
using GlowBorder.Models;
using GlowBorder.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlowBorder.Tests
{
    [TestClass]
    public class SettingsMigrationTests
    {
        [TestMethod]
        public void ExportAndImportSettings_PreservesProfilesAndSettings()
        {
            var settingsService = new SettingsService();
            var profileService = new ProfileService();
            var importExport = new SettingsImportExportService(settingsService, profileService);

            string tempFile = Path.Combine(Path.GetTempPath(), $"glowborder_test_{System.Guid.NewGuid()}.json");

            try
            {
                var settings = settingsService.Current;
                settings.OledMode = true;
                settings.GamingModeEnabled = true;
                settingsService.Save(settings);

                bool exportSuccess = importExport.ExportSettings(tempFile);
                Assert.IsTrue(exportSuccess);
                Assert.IsTrue(File.Exists(tempFile));

                // Modify current settings
                settings.OledMode = false;
                settingsService.Save(settings);

                // Import back
                bool importSuccess = importExport.ImportSettings(tempFile);
                Assert.IsTrue(importSuccess);
                Assert.IsTrue(settingsService.Current.OledMode);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void ResetToDefaults_RestoresStarterProfilesAndSettings()
        {
            var settingsService = new SettingsService();
            var profileService = new ProfileService();
            var importExport = new SettingsImportExportService(settingsService, profileService);

            importExport.ResetToDefaults();

            Assert.IsTrue(settingsService.Current.MasterEnabled);
            Assert.IsFalse(settingsService.Current.OledMode);
            Assert.IsTrue(profileService.Profiles.Count >= 6);
        }
    }
}
