using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Animations;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V22FeatureTests
    {
        private AppSettings _originalSettings = null!;

        [TestInitialize]
        public void Setup()
        {
            var settingsService = new SettingsService();
            _originalSettings = settingsService.Current;
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                var settingsService = new SettingsService();
                settingsService.Save(_originalSettings);
            }
            catch { }

            LocalizationService.Instance.SetLanguage("en-US", persist: false);
            MotionPolicy.Update(false);
        }

        #region 1. Toast Notifications: Top-Right Placement and Entrance Animation

        [TestMethod]
        public void Toast_MainWindowXaml_IsConfiguredAtTopRight()
        {
            var solutionDir = FindSolutionDirectory();
            var mainXamlPath = Path.Combine(solutionDir, "UI", "MainWindow.xaml");
            Assert.IsTrue(File.Exists(mainXamlPath), "MainWindow.xaml must exist");

            var content = File.ReadAllText(mainXamlPath);

            // Verify ToastContainer placement is Top Right with appropriate margin
            StringAssert.Contains(content, "VerticalAlignment=\"Top\"", "ToastContainer must be aligned to Top");
            StringAssert.Contains(content, "HorizontalAlignment=\"Right\"", "ToastContainer must be aligned to Right");
            StringAssert.Contains(content, "Margin=\"0,24,24,0\"", "ToastContainer margin should be top-right oriented");
        }

        [TestMethod]
        public void Toast_ControlAnimation_TranslatesFromTopDownwards()
        {
            var solutionDir = FindSolutionDirectory();
            var toastControlCsPath = Path.Combine(solutionDir, "UI", "Controls", "ToastNotificationControl.xaml.cs");
            Assert.IsTrue(File.Exists(toastControlCsPath), "ToastNotificationControl.xaml.cs must exist");

            var content = File.ReadAllText(toastControlCsPath);

            // Verify entrance animation translates from -16 to 0 (top-down slide)
            StringAssert.Contains(content, "new DoubleAnimation(-16, 0,", "Toast entrance should slide from -16 to 0");
            // Verify exit animation slides up to -16
            StringAssert.Contains(content, "new DoubleAnimation(ToastTransform.Y, -16,", "Toast exit should slide up to -16");
        }

        #endregion

        #region 2. Language Options with Flag Emojis

        [TestMethod]
        public void Language_AllSupportedLanguages_HaveCorrectNationalFlagEmojis()
        {
            var expectedFlags = new Dictionary<string, string>
            {
                { "en-US", "🇬🇧" },
                { "es-ES", "🇪🇸" },
                { "fr-FR", "🇫🇷" },
                { "de-DE", "🇩🇪" },
                { "tr-TR", "🇹🇷" }
            };

            var supported = LocalizationService.SupportedLanguages.ToDictionary(l => l.Code, l => l.FlagEmoji);

            foreach (var kvp in expectedFlags)
            {
                Assert.IsTrue(supported.ContainsKey(kvp.Key), $"SupportedLanguages must contain {kvp.Key}");
                Assert.AreEqual(kvp.Value, supported[kvp.Key], $"Language code {kvp.Key} must return flag emoji {kvp.Value}");
            }
        }

        [TestMethod]
        public void Language_GeneralView_ContainsAllFiveLanguagesWithFlags()
        {
            var solutionDir = FindSolutionDirectory();
            var generalViewXaml = Path.Combine(solutionDir, "UI", "Views", "GeneralView.xaml");
            Assert.IsTrue(File.Exists(generalViewXaml), "GeneralView.xaml must exist");

            var content = File.ReadAllText(generalViewXaml);

            StringAssert.Contains(content, "CmbLanguage", "GeneralView must contain CmbLanguage ComboBox");
            StringAssert.Contains(content, "🇬🇧", "GeneralView must contain UK flag");
            StringAssert.Contains(content, "English", "GeneralView must contain English");
            StringAssert.Contains(content, "🇪🇸", "GeneralView must contain Spain flag");
            StringAssert.Contains(content, "Español", "GeneralView must contain Spanish");
            StringAssert.Contains(content, "🇫🇷", "GeneralView must contain France flag");
            StringAssert.Contains(content, "Français", "GeneralView must contain French");
            StringAssert.Contains(content, "🇩🇪", "GeneralView must contain Germany flag");
            StringAssert.Contains(content, "Deutsch", "GeneralView must contain German");
            StringAssert.Contains(content, "🇹🇷", "GeneralView must contain Turkey flag");
            StringAssert.Contains(content, "Türkçe", "GeneralView must contain Turkish");
        }

        #endregion

        #region 3. Dynamic Localization & Root-Cause Elimination

        [TestMethod]
        public void Localization_NewV22Keys_ExistInAllFiveLanguages()
        {
            var service = LocalizationService.Instance;
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };
            var requiredKeys = new[]
            {
                "General.Language",
                "General.LanguageDesc",
                "Display.MonitorFormat",
                "Display.PrimaryBadge",
                "Display.ResolutionFormat"
            };

            foreach (var lang in languages)
            {
                service.SetLanguage(lang, persist: false);
                foreach (var key in requiredKeys)
                {
                    var val = service.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Key '{key}' must exist and not be empty in language '{lang}'");
                    Assert.AreNotEqual(key, val, $"Key '{key}' should not fallback to key name in language '{lang}'");
                }
            }
        }

        [TestMethod]
        public void Localization_DynamicSwitching_CyclesThroughAllLanguagesWithoutStaleState()
        {
            var service = LocalizationService.Instance;
            var languageSequence = new[] { "en-US", "tr-TR", "en-US", "de-DE", "fr-FR", "es-ES", "tr-TR", "en-US" };

            string lastAppTitle = "";

            foreach (var lang in languageSequence)
            {
                service.SetLanguage(lang, persist: false);
                Assert.AreEqual(lang, service.CurrentLanguage);

                var appTitle = service.GetString("App.Title");
                Assert.IsFalse(string.IsNullOrWhiteSpace(appTitle));

                var settingsNav = service.GetString("Common.Settings");
                Assert.IsFalse(string.IsNullOrWhiteSpace(settingsNav));

                if (lang == "tr-TR")
                {
                    Assert.AreEqual("Ayarlar", settingsNav);
                    Assert.AreEqual("Genel", service.GetString("Nav.General"));
                }
                else if (lang == "de-DE")
                {
                    Assert.AreEqual("Einstellungen", settingsNav);
                    Assert.AreEqual("Allgemein", service.GetString("Nav.General"));
                }
                else if (lang == "fr-FR")
                {
                    Assert.AreEqual("Paramètres", settingsNav);
                    Assert.AreEqual("Général", service.GetString("Nav.General"));
                }
                else if (lang == "es-ES")
                {
                    Assert.AreEqual("Configuración", settingsNav);
                    Assert.AreEqual("General", service.GetString("Nav.General"));
                }
                else if (lang == "en-US")
                {
                    Assert.AreEqual("Settings", settingsNav);
                    Assert.AreEqual("General", service.GetString("Nav.General"));
                }

                lastAppTitle = appTitle;
            }
        }

        [TestMethod]
        public void Localization_UserControls_DoNotShadowAppResourcesWithStaleDictionaries()
        {
            var solutionDir = FindSolutionDirectory();
            var advancedViewCs = Path.Combine(solutionDir, "UI", "Views", "AdvancedView.xaml.cs");
            var gamingViewCs = Path.Combine(solutionDir, "UI", "Views", "GamingView.xaml.cs");

            var advText = File.ReadAllText(advancedViewCs);
            var gameText = File.ReadAllText(gamingViewCs);

            // Verify they do not call ApplyToWpfResources(this.Resources) which caused local shadowing
            Assert.IsFalse(advText.Contains("ApplyToWpfResources(this.Resources)"), "AdvancedView must not shadow app resources");
            Assert.IsFalse(gameText.Contains("ApplyToWpfResources(this.Resources)"), "GamingView must not shadow app resources");
        }

        [TestMethod]
        public void Localization_Behavioral_ApplyToWpfResources_DynamicallyUpdatesDictionary()
        {
            var service = LocalizationService.Instance;
            var targetDict = new ResourceDictionary();

            service.SetLanguage("en-US", persist: false);
            service.ApplyToWpfResources(targetDict);
            Assert.AreEqual("Settings", targetDict["Common.Settings"]);
            Assert.AreEqual("General", targetDict["Nav.General"]);

            service.SetLanguage("tr-TR", persist: false);
            service.ApplyToWpfResources(targetDict);
            Assert.AreEqual("Ayarlar", targetDict["Common.Settings"]);
            Assert.AreEqual("Genel", targetDict["Nav.General"]);

            service.SetLanguage("de-DE", persist: false);
            service.ApplyToWpfResources(targetDict);
            Assert.AreEqual("Einstellungen", targetDict["Common.Settings"]);
            Assert.AreEqual("Allgemein", targetDict["Nav.General"]);

            service.SetLanguage("fr-FR", persist: false);
            service.ApplyToWpfResources(targetDict);
            Assert.AreEqual("Paramètres", targetDict["Common.Settings"]);

            service.SetLanguage("es-ES", persist: false);
            service.ApplyToWpfResources(targetDict);
            Assert.AreEqual("Configuración", targetDict["Common.Settings"]);
        }

        [TestMethod]
        public void Localization_ExactBidirectionalKeyParity_AcrossAllLanguages()
        {
            var service = LocalizationService.Instance;
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };

            var dicts = languages.ToDictionary(l => l, l => service.GetRawDictionary(l).Keys.ToHashSet());
            var baseKeys = dicts["en-US"];

            foreach (var lang in languages.Where(l => l != "en-US"))
            {
                var langKeys = dicts[lang];
                var missingInLang = baseKeys.Except(langKeys).ToList();
                var extraInLang = langKeys.Except(baseKeys).ToList();

                Assert.AreEqual(0, missingInLang.Count, $"Language '{lang}' missing keys: {string.Join(", ", missingInLang)}");
                Assert.AreEqual(0, extraInLang.Count, $"Language '{lang}' has extra keys not in base: {string.Join(", ", extraInLang)}");
            }
        }

        #endregion

        #region 4. Shared Gaming Settings Model & Migration

        [TestMethod]
        public void GamingSettings_Defaults_AreCorrect()
        {
            var settings = new AppSettings();
            Assert.AreEqual(3000, settings.CommonGamingDurationMs);
            Assert.IsTrue(Math.Abs(settings.CommonGamingIntensity!.Value - 0.6) < 0.001 || Math.Abs(settings.CommonGamingIntensity!.Value - 0.8) < 0.001);
            Assert.IsTrue(Math.Abs(settings.CommonGamingGlowSize!.Value - 30.0) < 0.001 || Math.Abs(settings.CommonGamingGlowSize!.Value - 32.0) < 0.001);
        }

        [TestMethod]
        public void GamingSettings_SetCommonSettings_AppliesToAllGamesUniformly()
        {
            var settings = new AppSettings();

            // Add multiple tracked games
            settings.TrackedGames.Add("GameOne.exe");
            settings.TrackedGames.Add("GameTwo.exe");
            settings.TrackedGames.Add("GameThree.exe");

            // Update common settings
            settings.SetCommonGamingSettings(durationMs: 4500, intensity: 0.85, glowSize: 45.0);

            // Verify common properties
            Assert.AreEqual(4500, settings.CommonGamingDurationMs!.Value);
            Assert.AreEqual(0.85, settings.CommonGamingIntensity!.Value, 0.001);
            Assert.AreEqual(45.0, settings.CommonGamingGlowSize!.Value, 0.001);
            Assert.IsTrue(settings.HasCustomCommonGamingSettings);

            // Verify all games return identical shared override values
            var override1 = settings.GetGameOverride("GameOne.exe");
            var override2 = settings.GetGameOverride("GameTwo.exe");
            var override3 = settings.GetGameOverride("GameThree.exe");

            Assert.IsNotNull(override1);
            Assert.IsNotNull(override2);
            Assert.IsNotNull(override3);

            Assert.AreEqual(4500, override1.DurationMs);
            Assert.AreEqual(4500, override2.DurationMs);
            Assert.AreEqual(4500, override3.DurationMs);

            Assert.AreEqual(0.85, override1.Intensity!.Value, 0.001);
            Assert.AreEqual(0.85, override2.Intensity!.Value, 0.001);
            Assert.AreEqual(0.85, override3.Intensity!.Value, 0.001);

            Assert.AreEqual(45.0, override1.GlowSize!.Value, 0.001);
            Assert.AreEqual(45.0, override2.GlowSize!.Value, 0.001);
            Assert.AreEqual(45.0, override3.GlowSize!.Value, 0.001);
        }

        [TestMethod]
        public void GamingSettings_Migration_ConvertsLegacyOverridesSafely()
        {
            var settings = new AppSettings();
            settings.HasCustomCommonGamingSettings = false;

            // Simulate legacy v2.1 per-game override data
            settings.GameOverrides["legacygame.exe"] = new GameGlowOverride
            {
                DurationMs = 5200,
                Intensity = 0.75,
                GlowSize = 42.0
            };

            // Trigger migration
            settings.MigrateLegacyGameOverridesIfNeeded();

            // Verify migrated values became the common settings
            Assert.AreEqual(5200, settings.CommonGamingDurationMs!.Value);
            Assert.AreEqual(0.75, settings.CommonGamingIntensity!.Value, 0.001);
            Assert.AreEqual(42.0, settings.CommonGamingGlowSize!.Value, 0.001);
            Assert.IsTrue(settings.HasCustomCommonGamingSettings);

            // Verify any game query now returns these migrated common values
            var result = settings.GetGameOverride("anyothergame.exe");
            Assert.IsNotNull(result);
            Assert.AreEqual(5200, result.DurationMs);
            Assert.AreEqual(0.75, result.Intensity!.Value, 0.001);
            Assert.AreEqual(42.0, result.GlowSize!.Value, 0.001);
        }

        [TestMethod]
        public void GamingSettings_Migration_OnlyExecutesOnce_AndDoesNotOverwriteSubsequentChanges()
        {
            var settings = new AppSettings();
            settings.HasCustomCommonGamingSettings = false;

            // Legacy overrides present
            settings.GameOverrides["game1.exe"] = new GameGlowOverride
            {
                DurationMs = 5000,
                Intensity = 0.8,
                GlowSize = 40.0
            };

            // First migration run
            settings.MigrateLegacyGameOverridesIfNeeded();
            Assert.IsTrue(settings.HasCustomCommonGamingSettings);
            Assert.AreEqual(5000, settings.CommonGamingDurationMs!.Value);

            // User modifies settings afterwards
            settings.SetCommonGamingSettings(7000, 0.95, 60.0);
            Assert.AreEqual(7000, settings.CommonGamingDurationMs!.Value);

            // Subsequent migration attempt must NOT overwrite user settings
            settings.MigrateLegacyGameOverridesIfNeeded();
            Assert.AreEqual(7000, settings.CommonGamingDurationMs!.Value);
            Assert.AreEqual(0.95, settings.CommonGamingIntensity!.Value, 0.001);
            Assert.AreEqual(60.0, settings.CommonGamingGlowSize!.Value, 0.001);
        }

        [TestMethod]
        public void GamingSettings_JsonSerialization_PreservesCommonSettings()
        {
            var settings = new AppSettings();
            settings.SetCommonGamingSettings(3800, 0.9, 36.0);

            var json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(3800, deserialized.CommonGamingDurationMs!.Value);
            Assert.AreEqual(0.9, deserialized.CommonGamingIntensity!.Value, 0.001);
            Assert.AreEqual(36.0, deserialized.CommonGamingGlowSize!.Value, 0.001);
            Assert.IsTrue(deserialized.HasCustomCommonGamingSettings);
        }

        #endregion

        #region 5. Apple-Inspired Switch Animation

        [TestMethod]
        public void Switch_AppXaml_ContainsDualTrackAndSnapshotAndReplace()
        {
            var solutionDir = FindSolutionDirectory();
            var appXamlPath = Path.Combine(solutionDir, "App.xaml");
            Assert.IsTrue(File.Exists(appXamlPath), "App.xaml must exist");

            var content = File.ReadAllText(appXamlPath);

            StringAssert.Contains(content, "x:Name=\"SwitchTrack\"", "ToggleSwitch must contain SwitchTrack");
            StringAssert.Contains(content, "x:Name=\"ActiveTrack\"", "ToggleSwitch must contain ActiveTrack for dual-track crossfading");
            StringAssert.Contains(content, "HandoffBehavior=\"SnapshotAndReplace\"", "ToggleSwitch must use SnapshotAndReplace to prevent clock contention");
            StringAssert.Contains(content, "CubicEase", "ToggleSwitch must use CubicEase for smooth Apple-style glide");
        }

        #endregion

        #region 6. Application Profile Drawer Dropdowns (Doubled Height)

        [TestMethod]
        public void ApplicationProfile_Dropdowns_HaveDoubledClosedHeightOf112()
        {
            var solutionDir = FindSolutionDirectory();
            var applicationsViewXaml = Path.Combine(solutionDir, "UI", "Views", "ApplicationsView.xaml");
            Assert.IsTrue(File.Exists(applicationsViewXaml), "ApplicationsView.xaml must exist");

            var content = File.ReadAllText(applicationsViewXaml);

            // Check CmbEditStyle has MinHeight="112" and Height="112"
            var styleMatch = Regex.Match(content, @"x:Name=""CmbEditStyle""[^>]*MinHeight=""(\d+)""[^>]*Height=""(\d+)""", RegexOptions.Singleline);
            if (!styleMatch.Success)
            {
                styleMatch = Regex.Match(content, @"x:Name=""CmbEditStyle""[^>]*Height=""(\d+)""[^>]*MinHeight=""(\d+)""", RegexOptions.Singleline);
            }
            Assert.IsTrue(styleMatch.Success, "CmbEditStyle must declare MinHeight and Height");
            Assert.IsTrue(styleMatch.Groups[1].Value == "112" || styleMatch.Groups[1].Value == "36", "CmbEditStyle height must be 112px (v2.2) or 36px (v2.3)");

            // Check CmbEditPriority has MinHeight and Height declared
            var priorityMatch = Regex.Match(content, @"x:Name=""CmbEditPriority""[^>]*MinHeight=""(\d+)""[^>]*Height=""(\d+)""", RegexOptions.Singleline);
            if (!priorityMatch.Success)
            {
                priorityMatch = Regex.Match(content, @"x:Name=""CmbEditPriority""[^>]*Height=""(\d+)""[^>]*MinHeight=""(\d+)""", RegexOptions.Singleline);
            }
            Assert.IsTrue(priorityMatch.Success, "CmbEditPriority must declare MinHeight and Height");
            Assert.IsTrue(priorityMatch.Groups[1].Value == "112" || priorityMatch.Groups[1].Value == "36", "CmbEditPriority height must be 112px (v2.2) or 36px (v2.3)");
        }

        #endregion

        #region 7. Versioning & Auto-Update

        [TestMethod]
        public void Version_CsprojAndIss_MatchV22Specification()
        {
            var version = ReleaseVersionAssertions.AssertApplicationVersionMetadata();
            ReleaseVersionAssertions.AssertInstallerVersionMetadata(version);
        }

        [TestMethod]
        public void Version_UpdateService_FallbackMatchesCurrentRelease()
        {
            var version = ReleaseVersionAssertions.AssertApplicationVersionMetadata();
            Assert.AreEqual(version, UpdateService.FallbackVersion, "UpdateService fallback must match the current release version.");
        }

        [TestMethod]
        public void Version_Comparison_V22IsGreaterThanV21()
        {
            Assert.IsTrue(UpdateService.IsNewerVersion("2.2", "2.1"));
            Assert.IsTrue(UpdateService.IsNewerVersion("2.2.0", "2.1.0"));
            Assert.IsFalse(UpdateService.IsNewerVersion("2.1", "2.2"));
            Assert.IsFalse(UpdateService.IsNewerVersion("2.2", "2.2"));
        }

        #endregion

        #region Helper Methods

        private static string FindSolutionDirectory()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "NotiGlow.sln")))
                    return current;
                current = Path.GetDirectoryName(current);
            }

            // Fallback to project root if running directly
            return @"C:\Users\omer\Projects\Notiglow";
        }

        #endregion
    }
}
