using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        public void Localization_InvalidLanguageFallback_DefaultsToEnglish()
        {
            var service = new LocalizationService();
            service.SetLanguage("de-DE", persist: false);
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

            // Confirm none of the display names were persisted
            Assert.AreNotEqual("English", settingsService.Current.AppLanguage);
            Assert.AreNotEqual("Español", settingsService.Current.AppLanguage);
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
        public void Localization_AllFourResourceSets_LoadSuccessfully()
        {
            var service = new LocalizationService();
            var requiredCodes = new[] { "en-US", "es-ES", "fr-FR", "tr-TR" };

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

            Assert.AreEqual(missingKey, result, "Missing key should safely return the key itself without throwing.");
        }

        #endregion

        #region 2. UI Elements & Language Dropdown Tests

        [TestMethod]
        public void UI_LanguageButton_ExistsAndRendersInMainWindow()
        {
            Exception? threadEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = Application.Current ?? new Application();
                    var window = new MainWindow();

                    var btnLanguage = window.FindName("BtnLanguage") as Button;
                    Assert.IsNotNull(btnLanguage, "Language button 'BtnLanguage' must exist in sidebar.");

                    var popupLanguage = window.FindName("PopupLanguage") as Popup;
                    Assert.IsNotNull(popupLanguage, "Language popup 'PopupLanguage' must exist.");

                    var txtCode = window.FindName("TxtCurrentLangCode") as TextBlock;
                    Assert.IsNotNull(txtCode, "TxtCurrentLangCode must exist for badge display.");

                    var txtLabel = window.FindName("TxtLanguageLabel") as TextBlock;
                    Assert.IsNotNull(txtLabel, "TxtLanguageLabel must exist for full sidebar display.");
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadEx != null) throw threadEx;
        }

        [TestMethod]
        public void UI_LanguageDropdown_HasExactFourSupportedLanguages()
        {
            Exception? threadEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = Application.Current ?? new Application();
                    var window = new MainWindow();

                    var btnEn = window.FindName("BtnLangEn") as Button;
                    var btnEs = window.FindName("BtnLangEs") as Button;
                    var btnFr = window.FindName("BtnLangFr") as Button;
                    var btnTr = window.FindName("BtnLangTr") as Button;

                    Assert.IsNotNull(btnEn, "English selection button must exist.");
                    Assert.IsNotNull(btnEs, "Español selection button must exist.");
                    Assert.IsNotNull(btnFr, "Français selection button must exist.");
                    Assert.IsNotNull(btnTr, "Türkçe selection button must exist.");

                    Assert.AreEqual("en-US", btnEn.Tag);
                    Assert.AreEqual("es-ES", btnEs.Tag);
                    Assert.AreEqual("fr-FR", btnFr.Tag);
                    Assert.AreEqual("tr-TR", btnTr.Tag);
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadEx != null) throw threadEx;
        }

        [TestMethod]
        public void UI_SelectedLanguage_IsIdentifiable()
        {
            Exception? threadEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = Application.Current ?? new Application();
                    var window = new MainWindow();

                    LocalizationService.Instance.SetLanguage("fr-FR", persist: false);
                    window.UpdateLanguageButtonVisuals();

                    var txtCode = window.FindName("TxtCurrentLangCode") as TextBlock;
                    Assert.IsNotNull(txtCode);
                    Assert.AreEqual("FR", txtCode.Text, "Language badge code must display 'FR' when French is selected.");

                    var checkFr = window.FindName("CheckLangFr") as FrameworkElement;
                    Assert.IsNotNull(checkFr);
                    Assert.AreEqual(Visibility.Visible, checkFr.Visibility, "French checkmark indicator must be visible.");

                    var checkEn = window.FindName("CheckLangEn") as FrameworkElement;
                    Assert.IsNotNull(checkEn);
                    Assert.AreEqual(Visibility.Collapsed, checkEn.Visibility, "English checkmark indicator must be collapsed.");
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadEx != null) throw threadEx;
        }

        [TestMethod]
        public void UI_GamingButton_TextAndResourceKey_IsTestGameAnimation()
        {
            Assert.AreEqual("Test Game Animation", LocalizationService.Instance.GetString("Gaming.TestGameAnimation"));

            Exception? threadEx = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var app = Application.Current ?? new Application();
                    var settingsService = new SettingsService();
                    var view = new GamingView();
                    view.Initialize(settingsService);

                    var btn = view.FindName("BtnTestAnimation") as Wpf.Ui.Controls.Button;
                    Assert.IsNotNull(btn, "BtnTestAnimation must exist in GamingView.");

                    var textBlock = (btn.Content as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault();
                    Assert.IsNotNull(textBlock, "BtnTestAnimation must contain TextBlock.");
                    Assert.AreEqual("Test Game Animation", textBlock.Text, "Button text must read 'Test Game Animation'.");
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (threadEx != null) throw threadEx;
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
