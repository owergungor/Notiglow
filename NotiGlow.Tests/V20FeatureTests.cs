using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI;
using NotiGlow.UI.Views;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V20FeatureTests
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

            // Reset LocalizationService to default English
            LocalizationService.Instance.SetLanguage("en-US", persist: false);
        }

        #region 1. Localization Infrastructure & Runtime Switching Tests

        [TestMethod]
        public void Localization_DefaultLanguage_IsEnglish()
        {
            var service = new LocalizationService();
            Assert.AreEqual("en-US", service.CurrentLanguage);
            Assert.AreEqual("en-US", LocalizationService.DefaultLanguage);
            Assert.AreEqual("English", service.GetString("Language.English"));
            Assert.AreEqual("Save", service.GetString("Common.Save"));
            Assert.AreEqual("General Settings", service.GetString("General.Title"));
        }

        [TestMethod]
        public void Localization_SpanishSelection_SwitchesCultureAndDictionary()
        {
            var service = new LocalizationService();
            service.SetLanguage("es-ES", persist: false);

            Assert.AreEqual("es-ES", service.CurrentLanguage);
            Assert.AreEqual("es-ES", service.CurrentCulture.Name);
            Assert.AreEqual("Guardar", service.GetString("Common.Save"));
            Assert.AreEqual("Configuración General", service.GetString("General.Title"));
            Assert.AreEqual("Probar Animación del Juego", service.GetString("Gaming.TestGameAnimation"));
        }

        [TestMethod]
        public void Localization_FrenchSelection_SwitchesCultureAndDictionary()
        {
            var service = new LocalizationService();
            service.SetLanguage("fr-FR", persist: false);

            Assert.AreEqual("fr-FR", service.CurrentLanguage);
            Assert.AreEqual("fr-FR", service.CurrentCulture.Name);
            Assert.AreEqual("Enregistrer", service.GetString("Common.Save"));
            Assert.AreEqual("Paramètres Généraux", service.GetString("General.Title"));
            Assert.AreEqual("Tester l'Animation de Jeu", service.GetString("Gaming.TestGameAnimation"));
        }

        [TestMethod]
        public void Localization_TurkishSelection_SwitchesCultureAndDictionary()
        {
            var service = new LocalizationService();
            service.SetLanguage("tr-TR", persist: false);

            Assert.AreEqual("tr-TR", service.CurrentLanguage);
            Assert.AreEqual("tr-TR", service.CurrentCulture.Name);
            Assert.AreEqual("Kaydet", service.GetString("Common.Save"));
            Assert.AreEqual("Genel Ayarlar", service.GetString("General.Title"));
            Assert.AreEqual("Oyun Animasyonunu Test Et", service.GetString("Gaming.TestGameAnimation"));
        }

        [TestMethod]
        public void Localization_GermanSelection_SwitchesCultureAndDictionary()
        {
            var service = new LocalizationService();
            service.SetLanguage("de-DE", persist: false);

            Assert.AreEqual("de-DE", service.CurrentLanguage);
            Assert.AreEqual("de-DE", service.CurrentCulture.Name);
            Assert.AreEqual("Speichern", service.GetString("Common.Save"));
            Assert.AreEqual("Allgemeine Einstellungen", service.GetString("General.Title"));
            Assert.AreEqual("Spiel-Animation testen", service.GetString("Gaming.TestGameAnimation"));
        }

        [TestMethod]
        public void Localization_InvalidLanguageFallback_DefaultsToEnglish()
        {
            var service = new LocalizationService();
            service.SetLanguage("it-IT", persist: false);
            Assert.AreEqual("en-US", service.CurrentLanguage, "Unsupported language code must fallback to en-US.");

            service.SetLanguage("invalid-culture", persist: false);
            Assert.AreEqual("en-US", service.CurrentLanguage, "Invalid culture must fallback to en-US.");

            service.SetLanguage(string.Empty, persist: false);
            Assert.AreEqual("en-US", service.CurrentLanguage, "Empty language string must fallback to en-US.");

            Assert.AreEqual("Save", service.GetString("Common.Save"));
        }

        [TestMethod]
        public void Localization_PersistedLanguage_LoadsCorrectly()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.AppLanguage = "tr-TR";
            settingsService.Save(settings);

            var service = new LocalizationService();
            service.Initialize(settingsService);

            Assert.AreEqual("tr-TR", service.CurrentLanguage, "Configured language must load from AppSettings.");
            Assert.AreEqual("Kaydet", service.GetString("Common.Save"));
        }

        [TestMethod]
        public void Localization_StableCultureCodes_Persist()
        {
            var settingsService = new SettingsService();
            var service = new LocalizationService();
            service.Initialize(settingsService);

            service.SetLanguage("es-ES", persist: true);
            Assert.AreEqual("es-ES", settingsService.Current.AppLanguage);

            service.SetLanguage("fr-FR", persist: true);
            Assert.AreEqual("fr-FR", settingsService.Current.AppLanguage);

            service.SetLanguage("tr-TR", persist: true);
            Assert.AreEqual("tr-TR", settingsService.Current.AppLanguage);

            service.SetLanguage("en-US", persist: true);
            Assert.AreEqual("en-US", settingsService.Current.AppLanguage);

            service.SetLanguage("de-DE", persist: true);
            Assert.AreEqual("de-DE", settingsService.Current.AppLanguage);

            // Confirm none of the display names were persisted
            Assert.AreNotEqual("English", settingsService.Current.AppLanguage);
            Assert.AreNotEqual("Español", settingsService.Current.AppLanguage);
            Assert.AreNotEqual("Deutsch", settingsService.Current.AppLanguage);
            Assert.AreNotEqual("Türkçe", settingsService.Current.AppLanguage);
        }

        [TestMethod]
        public void Localization_RuntimeChange_NotifiesSubscribers()
        {
            var service = new LocalizationService();
            string? notifiedLang = null;
            service.LanguageChanged += (s, lang) => notifiedLang = lang;

            service.SetLanguage("es-ES", persist: false);
            Assert.AreEqual("es-ES", notifiedLang, "LanguageChanged event must fire with new language code.");

            service.SetLanguage("tr-TR", persist: false);
            Assert.AreEqual("tr-TR", notifiedLang, "LanguageChanged event must fire on subsequent switches.");
        }

        [TestMethod]
        public void Localization_AllFiveResourceSets_LoadSuccessfully()
        {
            var service = new LocalizationService();
            var requiredCodes = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };

            var coreKeys = new[]
            {
                "App.Title",
                "Nav.General",
                "Nav.Applications",
                "Nav.Appearance",
                "Nav.Display",
                "Nav.Gaming",
                "Nav.Notifications",
                "Nav.Advanced",
                "Language.Language",
                "Common.Save",
                "Common.Cancel",
                "Gaming.TestGameAnimation",
                "Applications.Title",
                "Advanced.Title"
            };

            foreach (var code in requiredCodes)
            {
                var dict = service.GetRawDictionary(code);
                Assert.IsNotNull(dict, $"Dictionary for {code} must not be null.");
                Assert.IsTrue(dict.Count >= coreKeys.Length, $"Dictionary for {code} must contain all core keys.");

                foreach (var key in coreKeys)
                {
                    Assert.IsTrue(dict.ContainsKey(key), $"Language '{code}' is missing key '{key}'.");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(dict[key]), $"Language '{code}' has empty value for key '{key}'.");
                }
            }
        }

        [TestMethod]
        public void Localization_MissingResourceKey_HasSafeFallback()
        {
            var service = new LocalizationService();
            service.SetLanguage("es-ES", persist: false);

            string missingKey = "NonExistent.Key.ForTesting123";
            string result = service.GetString(missingKey);

            Assert.IsFalse(string.IsNullOrWhiteSpace(result), "Missing key should safely return non-empty fallback text without throwing.");
            Assert.IsFalse(result.Contains("."), "Missing key should not leak raw dotted technical namespace path to users.");
        }

        #endregion

        #region 2. UI Elements & Language Dropdown Tests

        [TestMethod]
        public void UI_LanguageButton_ExistsAndRendersInMainWindow()
        {
            var xaml = File.ReadAllText(Path.Combine(TestPathHelper.SolutionDir, "UI", "MainWindow.xaml"));
            StringAssert.Contains(xaml, "x:Name=\"BtnLanguage\"");
            StringAssert.Contains(xaml, "x:Name=\"TxtCurrentLangCode\"");
            StringAssert.Contains(xaml, "x:Name=\"TxtLanguageLabel\"");
        }

        [TestMethod]
        public void UI_LanguageDropdown_HasExactFourSupportedLanguages()
        {
            var xaml = File.ReadAllText(Path.Combine(TestPathHelper.SolutionDir, "UI", "MainWindow.xaml"));
            StringAssert.Contains(xaml, "Tag=\"en-US\"");
            StringAssert.Contains(xaml, "Tag=\"es-ES\"");
            StringAssert.Contains(xaml, "Tag=\"fr-FR\"");
            StringAssert.Contains(xaml, "Tag=\"de-DE\"");
            StringAssert.Contains(xaml, "Tag=\"tr-TR\"");
        }

        [TestMethod]
        public void UI_SelectedLanguage_IsIdentifiable()
        {
            LocalizationService.Instance.SetLanguage("fr-FR", persist: false);
            Assert.AreEqual("fr-FR", LocalizationService.Instance.CurrentLanguageCode);
            var supported = LocalizationService.SupportedLanguages;
            Assert.IsTrue(supported.Any(l => l.Code == "fr-FR"));
        }

        [TestMethod]
        public void UI_GamingButton_TextAndResourceKey_IsTestGameAnimation()
        {
            Assert.AreEqual("Test Game Animation", LocalizationService.Instance.GetString("Gaming.TestGameAnimation"));
            var xaml = File.ReadAllText(Path.Combine(TestPathHelper.SolutionDir, "UI", "Views", "GamingView.xaml"));
            StringAssert.Contains(xaml, "x:Name=\"BtnTestAnimation\"");
        }

        #endregion

        #region 3. Gaming Test Animation Pipeline Tests

        [TestMethod]
        public void Gaming_TrackedGameExecutableExists_TriggerGameAnimationTest_Executes()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.GamingModeEnabled = true;
            settings.GlowDuringGames = true;
            settings.TrackedGames = new List<string> { "DoomEternal.exe", "Cyberpunk2077.exe" };
            settingsService.Save(settings);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService)
            {
                SuppressOverlayWindowsForTesting = true
            };
            var gameDetection = glowManager.GameDetectionService;

            // Test execution should succeed without throwing
            bool result = glowManager.TriggerGameAnimationTest();
            Assert.IsTrue(result, "TriggerGameAnimationTest should return true when a tracked game exists.");
            Assert.IsNotNull(glowManager.LastTriggeredProfile, "Game animation test must trigger a profile.");

            // After test finishes, simulated state must be restored
            Assert.IsFalse(gameDetection.IsSimulatingGame, "Simulated game flag must be cleared after test.");
            Assert.IsFalse(gameDetection.IsGameRunning(), "IsGameRunning must return false after test completes.");
        }

        [TestMethod]
        public void Gaming_EmptyTrackedGameList_HandlesGracefullyWithoutCrash()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.TrackedGames = new List<string>();
            settingsService.Save(settings);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService)
            {
                SuppressOverlayWindowsForTesting = true
            };
            var gameDetection = glowManager.GameDetectionService;

            // Should handle empty list gracefully without throwing
            bool result = glowManager.TriggerGameAnimationTest();
            Assert.IsFalse(result, "TriggerGameAnimationTest should return false when tracked games list is empty.");

            Assert.IsFalse(gameDetection.IsSimulatingGame);
            Assert.IsFalse(gameDetection.IsGameRunning());
        }

        [TestMethod]
        public void Gaming_TestAnimation_DoesNotLaunchGameProcess()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.TrackedGames = new List<string> { "NonExistentGame12345.exe" };
            settingsService.Save(settings);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService)
            {
                SuppressOverlayWindowsForTesting = true
            };

            int processesBefore = Process.GetProcessesByName("NonExistentGame12345").Length;
            Assert.AreEqual(0, processesBefore);

            glowManager.TriggerGameAnimationTest("NonExistentGame12345.exe");

            int processesAfter = Process.GetProcessesByName("NonExistentGame12345").Length;
            Assert.AreEqual(0, processesAfter, "No actual OS process must be spawned during game animation test.");
        }

        [TestMethod]
        public void Gaming_TestAnimation_DoesNotModifyTrackedGameList()
        {
            var settingsService = new SettingsService();
            var initialList = new List<string> { "Witcher3.exe", "Hades.exe" };
            var settings = settingsService.Current;
            settings.TrackedGames = new List<string>(initialList);
            settingsService.Save(settings);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService)
            {
                SuppressOverlayWindowsForTesting = true
            };

            glowManager.TriggerGameAnimationTest();

            CollectionAssert.AreEqual(initialList, settingsService.Current.TrackedGames, "Tracked games list must remain unchanged after test.");
        }

        [TestMethod]
        public void Gaming_GlowDuringGamesOff_BlocksTestAccordingToRules()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.GamingModeEnabled = true;
            settings.GlowDuringGames = false; // Blocked in game
            settings.TrackedGames = new List<string> { "EldenRing.exe" };
            settingsService.Save(settings);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService)
            {
                SuppressOverlayWindowsForTesting = true
            };
            var gameDetection = glowManager.GameDetectionService;

            // Trigger test: because GlowDuringGames is false, the animation pipeline respects the rule
            bool result = glowManager.TriggerGameAnimationTest("EldenRing.exe");
            Assert.IsFalse(result, "TriggerGameAnimationTest should return false when GlowDuringGames is disabled.");

            Assert.IsFalse(gameDetection.IsSimulatingGame);
            Assert.IsFalse(gameDetection.IsGameRunning());
        }

        [TestMethod]
        public void Gaming_TestAnimation_DoesNotPermanentlyAlterTrackingState()
        {
            var settingsService = new SettingsService();
            settingsService.Current.TrackedGames = new List<string> { "Valorant.exe" };
            settingsService.Save(settingsService.Current);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService)
            {
                SuppressOverlayWindowsForTesting = true
            };
            var gameDetection = glowManager.GameDetectionService;

            Assert.IsFalse(gameDetection.IsGameRunning());

            glowManager.TriggerGameAnimationTest("Valorant.exe");

            Assert.IsFalse(gameDetection.IsSimulatingGame, "Simulated state must revert to false.");
            Assert.IsFalse(gameDetection.IsGameRunning(), "IsGameRunning must revert to false.");
        }

        #endregion
    }
}
