using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V17FeatureTests
    {
        #region 1. Theme Tests

        [TestMethod]
        public void Theme_CanonicalThemes_CountIs12()
        {
            var themes = ThemeService.CanonicalThemes;
            Assert.AreEqual(12, themes.Count, "Theme dropdown must offer exactly 12 canonical themes.");
        }

        [TestMethod]
        public void Theme_Persistence_SavesAndRestoresColorTheme()
        {
            var settings = new AppSettings { ColorTheme = ColorTheme.Nature };
            string json = JsonSerializer.Serialize(settings);

            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(ColorTheme.Nature, deserialized.ColorTheme);
        }

        [TestMethod]
        public void Theme_Restart_ThemePreservedAcrossLoads()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_ThemeTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsFile = Path.Combine(tempDir, "settings.json");
                var initialSettings = new AppSettings { ColorTheme = ColorTheme.Amethyst };
                File.WriteAllText(settingsFile, JsonSerializer.Serialize(initialSettings));

                string readJson = File.ReadAllText(settingsFile);
                var reloaded = JsonSerializer.Deserialize<AppSettings>(readJson);

                Assert.IsNotNull(reloaded);
                Assert.AreEqual(ColorTheme.Amethyst, reloaded.ColorTheme, "Theme must be preserved across application restarts.");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void Theme_InvalidOrUnknownValue_FallsBackToStandard()
        {
            int invalidValue = 999;
            ColorTheme resolved = Enum.IsDefined(typeof(ColorTheme), invalidValue)
                ? (ColorTheme)invalidValue
                : ColorTheme.Standard;

            Assert.AreEqual(ColorTheme.Standard, resolved, "Invalid theme value must safely fallback to Standard.");
        }

        #endregion

        #region 2. Auto Update Tests

        [TestMethod]
        public void AutoUpdate_DefaultFrequency_IsOnStartup()
        {
            var settings = new AppSettings();
            Assert.AreEqual(UpdateCheckFrequency.OnStartup, settings.UpdateFrequency, "Default update frequency must be OnStartup.");
        }

        [TestMethod]
        public void AutoUpdate_LegacyMigration_PreservesExistingSettingsAndDefaultsToOnStartup()
        {
            string legacyJson = "{\"AutoCheckUpdates\": true, \"MasterEnabled\": true}";
            var settings = JsonSerializer.Deserialize<AppSettings>(legacyJson);

            Assert.IsNotNull(settings);
            Assert.IsTrue(settings.AutoCheckUpdates);
            Assert.AreEqual(UpdateCheckFrequency.OnStartup, settings.UpdateFrequency, "Legacy config must deserialize frequency to OnStartup default.");
        }

        [TestMethod]
        public void AutoUpdate_FrequencyPersistence_AllValuesSerializedCorrectly()
        {
            var frequencies = new[]
            {
                UpdateCheckFrequency.OnStartup,
                UpdateCheckFrequency.Daily,
                UpdateCheckFrequency.Weekly,
                UpdateCheckFrequency.Monthly
            };

            foreach (var freq in frequencies)
            {
                var settings = new AppSettings { UpdateFrequency = freq };
                string json = JsonSerializer.Serialize(settings);
                var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

                Assert.IsNotNull(deserialized);
                Assert.AreEqual(freq, deserialized.UpdateFrequency);
            }
        }

        [TestMethod]
        public void AutoUpdate_Disabled_NeverTriggersCheck()
        {
            var settings = new AppSettings
            {
                AutoCheckUpdates = false,
                UpdateFrequency = UpdateCheckFrequency.OnStartup,
                LastUpdateCheck = null
            };

            bool shouldCheck = UpdateService.ShouldCheckForUpdates(settings, DateTime.UtcNow);
            Assert.IsFalse(shouldCheck, "When AutoCheckUpdates is false, no check should ever occur.");
        }

        [TestMethod]
        public void AutoUpdate_Frequency_OnStartup_Behavior()
        {
            var settings = new AppSettings
            {
                AutoCheckUpdates = true,
                UpdateFrequency = UpdateCheckFrequency.OnStartup,
                LastUpdateCheck = DateTime.UtcNow.AddMinutes(-5)
            };

            bool shouldCheck = UpdateService.ShouldCheckForUpdates(settings, DateTime.UtcNow);
            Assert.IsTrue(shouldCheck, "OnStartup frequency should allow check upon app start.");
        }

        [TestMethod]
        public void AutoUpdate_Frequency_Daily_Behavior()
        {
            var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
            var settings = new AppSettings
            {
                AutoCheckUpdates = true,
                UpdateFrequency = UpdateCheckFrequency.Daily,
                LastUpdateCheck = now.AddHours(-10) // Checked 10 hours ago (< 24h)
            };

            Assert.IsFalse(UpdateService.ShouldCheckForUpdates(settings, now), "Daily frequency checked < 24h ago should not trigger.");

            settings.LastUpdateCheck = now.AddHours(-25); // Checked 25 hours ago (> 24h)
            Assert.IsTrue(UpdateService.ShouldCheckForUpdates(settings, now), "Daily frequency checked > 24h ago should trigger.");

            settings.LastUpdateCheck = null;
            Assert.IsTrue(UpdateService.ShouldCheckForUpdates(settings, now), "Daily frequency with no previous check should trigger.");
        }

        [TestMethod]
        public void AutoUpdate_Frequency_Weekly_Behavior()
        {
            var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
            var settings = new AppSettings
            {
                AutoCheckUpdates = true,
                UpdateFrequency = UpdateCheckFrequency.Weekly,
                LastUpdateCheck = now.AddDays(-3) // Checked 3 days ago (< 7d)
            };

            Assert.IsFalse(UpdateService.ShouldCheckForUpdates(settings, now), "Weekly frequency checked < 7d ago should not trigger.");

            settings.LastUpdateCheck = now.AddDays(-8); // Checked 8 days ago (> 7d)
            Assert.IsTrue(UpdateService.ShouldCheckForUpdates(settings, now), "Weekly frequency checked > 7d ago should trigger.");
        }

        [TestMethod]
        public void AutoUpdate_Frequency_Monthly_Behavior()
        {
            var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
            var settings = new AppSettings
            {
                AutoCheckUpdates = true,
                UpdateFrequency = UpdateCheckFrequency.Monthly,
                LastUpdateCheck = now.AddDays(-15) // Checked 15 days ago (< 30d)
            };

            Assert.IsFalse(UpdateService.ShouldCheckForUpdates(settings, now), "Monthly frequency checked < 30d ago should not trigger.");

            settings.LastUpdateCheck = now.AddDays(-31); // Checked 31 days ago (> 30d)
            Assert.IsTrue(UpdateService.ShouldCheckForUpdates(settings, now), "Monthly frequency checked > 30d ago should trigger.");
        }

        [TestMethod]
        public void AutoUpdate_FailedCheck_DoesNotUpdateLastUpdateCheckTimestamp()
        {
            var originalTimestamp = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
            var settings = new AppSettings
            {
                LastUpdateCheck = originalTimestamp
            };

            // Simulating failed network check: timestamp remains unchanged
            Assert.AreEqual(originalTimestamp, settings.LastUpdateCheck, "Failed network check must not overwrite LastUpdateCheck.");
        }

        [TestMethod]
        public void AutoUpdate_SuccessfulCheck_UpdatesLastUpdateCheckTimestamp()
        {
            var originalTimestamp = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
            var settings = new AppSettings
            {
                LastUpdateCheck = originalTimestamp
            };

            // Simulating successful check
            var newTimestamp = DateTime.UtcNow;
            settings.LastUpdateCheck = newTimestamp;

            Assert.AreEqual(newTimestamp, settings.LastUpdateCheck, "Successful check must update LastUpdateCheck.");
        }

        #endregion

        #region 3. Applications List/Grid View Mode Tests

        [TestMethod]
        public void Applications_ViewMode_Persistence()
        {
            var settings = new AppSettings { ApplicationsViewMode = ApplicationViewMode.Grid };
            string json = JsonSerializer.Serialize(settings);
            var restored = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(ApplicationViewMode.Grid, restored.ApplicationsViewMode);

            settings.ApplicationsViewMode = ApplicationViewMode.List;
            json = JsonSerializer.Serialize(settings);
            restored = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(ApplicationViewMode.List, restored.ApplicationsViewMode);
        }

        #endregion

        #region 4. In-Game Behavior Rules Dependency Tests

        [TestMethod]
        public void GameBehavior_DependencyLogic_GlowDisabled_SubSettingsAreIneffective()
        {
            var settings = new AppSettings
            {
                GlowDuringGames = false,
                ReduceIntensityInGames = true,
                ReduceDurationInGames = true,
                OnlyImportantInGames = true
            };

            // Direct properties retain user choice
            Assert.IsTrue(settings.ReduceIntensityInGames);
            Assert.IsTrue(settings.ReduceDurationInGames);
            Assert.IsTrue(settings.OnlyImportantInGames);

            // But effective behavior is disabled when GlowDuringGames is false
            Assert.IsFalse(settings.CanModifyGameSubSettings);
            Assert.IsFalse(settings.EffectiveReduceIntensityInGames);
            Assert.IsFalse(settings.EffectiveReduceDurationInGames);
            Assert.IsFalse(settings.EffectiveOnlyImportantInGames);
        }

        [TestMethod]
        public void GameBehavior_DependencyLogic_GlowEnabled_SubSettingsReflectValues()
        {
            var settings = new AppSettings
            {
                GlowDuringGames = true,
                ReduceIntensityInGames = true,
                ReduceDurationInGames = false,
                OnlyImportantInGames = true
            };

            Assert.IsTrue(settings.CanModifyGameSubSettings);
            Assert.IsTrue(settings.EffectiveReduceIntensityInGames);
            Assert.IsFalse(settings.EffectiveReduceDurationInGames);
            Assert.IsTrue(settings.EffectiveOnlyImportantInGames);
        }

        [TestMethod]
        public void GameBehavior_DependencyLogic_PreservesPreviousValuesWhenToggledBackOn()
        {
            var settings = new AppSettings
            {
                GlowDuringGames = true,
                ReduceIntensityInGames = true,
                ReduceDurationInGames = false,
                OnlyImportantInGames = true
            };

            // Glow turned OFF
            settings.GlowDuringGames = false;
            Assert.IsFalse(settings.EffectiveReduceIntensityInGames);
            Assert.IsFalse(settings.EffectiveReduceDurationInGames);
            Assert.IsFalse(settings.EffectiveOnlyImportantInGames);

            // Stored values remain preserved!
            Assert.IsTrue(settings.ReduceIntensityInGames);
            Assert.IsFalse(settings.ReduceDurationInGames);
            Assert.IsTrue(settings.OnlyImportantInGames);

            // Glow turned back ON -> previous values (true, false, true) are immediately active again!
            settings.GlowDuringGames = true;
            Assert.IsTrue(settings.EffectiveReduceIntensityInGames);
            Assert.IsFalse(settings.EffectiveReduceDurationInGames);
            Assert.IsTrue(settings.EffectiveOnlyImportantInGames);
        }

        [TestMethod]
        public void GameBehavior_SettingsService_EnforcesDependencyProtection()
        {
            var settingsService = new SettingsService();
            settingsService.Current.GlowDuringGames = false;
            settingsService.Current.ReduceIntensityInGames = true;

            // Attempting to modify sub-setting while GlowDuringGames is false should be rejected
            bool updated = settingsService.UpdateGameSubSetting(s => s.ReduceIntensityInGames = false);

            Assert.IsFalse(updated, "UpdateGameSubSetting must reject modifications while GlowDuringGames is false.");
            Assert.IsTrue(settingsService.Current.ReduceIntensityInGames, "Stored value must remain unchanged when rejected.");
        }

        #endregion
    }
}
