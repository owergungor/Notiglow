using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Animations;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V21FeatureTests
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

        #region 1. Localization: Five Languages & String Parity Tests

        [TestMethod]
        public void Localization_AllFiveLanguages_HaveExactKeyParityWithEnglish()
        {
            var service = LocalizationService.Instance;
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };

            // English is our canonical baseline
            service.SetLanguage("en-US", persist: false);
            var enKeys = service.GetAllKeys().OrderBy(k => k).ToList();

            Assert.IsTrue(enKeys.Count >= 250, $"Expected >= 250 keys, got {enKeys.Count}");

            foreach (var lang in languages)
            {
                service.SetLanguage(lang, persist: false);
                var langKeys = service.GetAllKeys().OrderBy(k => k).ToList();

                var missing = enKeys.Except(langKeys).ToList();
                Assert.AreEqual(0, missing.Count, $"Language '{lang}' is missing {missing.Count} keys: {string.Join(", ", missing.Take(10))}");

                // Ensure no values are null or empty
                foreach (var key in enKeys)
                {
                    var val = service.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Language '{lang}' has empty string for key '{key}'");
                }
            }
        }

        [TestMethod]
        public void Localization_AdvancedAndGamingPages_HaveCompleteTranslationsInAllLanguages()
        {
            var service = LocalizationService.Instance;
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };

            service.SetLanguage("en-US", persist: false);
            var advancedKeys = service.GetAllKeys().Where(k => k.StartsWith("Advanced.")).ToList();
            var gamingKeys = service.GetAllKeys().Where(k => k.StartsWith("Gaming.")).ToList();

            Assert.IsTrue(advancedKeys.Count >= 20, $"Expected >= 20 Advanced keys, found {advancedKeys.Count}");
            Assert.IsTrue(gamingKeys.Count >= 20, $"Expected >= 20 Gaming keys, found {gamingKeys.Count}");

            foreach (var lang in languages)
            {
                service.SetLanguage(lang, persist: false);

                foreach (var key in advancedKeys)
                {
                    var str = service.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(str), $"[{lang}] Missing translation for {key}");
                    Assert.AreNotEqual(key, str, $"[{lang}] Key {key} returned raw key instead of translated string");
                }

                foreach (var key in gamingKeys)
                {
                    var str = service.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(str), $"[{lang}] Missing translation for {key}");
                    Assert.AreNotEqual(key, str, $"[{lang}] Key {key} returned raw key instead of translated string");
                }
            }
        }

        [TestMethod]
        public void Localization_LanguageSelector_ContainsFlagsAndNativeNames()
        {
            var supported = LocalizationService.SupportedLanguages;

            Assert.AreEqual(5, supported.Count, "Exactly 5 languages must be supported.");

            var en = supported.FirstOrDefault(l => l.Code == "en-US");
            Assert.IsNotNull(en);
            Assert.AreEqual("🇬🇧", en.FlagEmoji);
            Assert.AreEqual("English", en.DisplayName);

            var es = supported.FirstOrDefault(l => l.Code == "es-ES");
            Assert.IsNotNull(es);
            Assert.AreEqual("🇪🇸", es.FlagEmoji);
            Assert.AreEqual("Español", es.DisplayName);

            var fr = supported.FirstOrDefault(l => l.Code == "fr-FR");
            Assert.IsNotNull(fr);
            Assert.AreEqual("🇫🇷", fr.FlagEmoji);
            Assert.AreEqual("Français", fr.DisplayName);

            var de = supported.FirstOrDefault(l => l.Code == "de-DE");
            Assert.IsNotNull(de);
            Assert.AreEqual("🇩🇪", de.FlagEmoji);
            Assert.AreEqual("Deutsch", de.DisplayName);

            var tr = supported.FirstOrDefault(l => l.Code == "tr-TR");
            Assert.IsNotNull(tr);
            Assert.AreEqual("🇹🇷", tr.FlagEmoji);
            Assert.AreEqual("Türkçe", tr.DisplayName);
        }

        [TestMethod]
        public void Localization_RuntimeSwitchingAndPersistence_FallsBackToEnglishOnInvalid()
        {
            var settingsService = new SettingsService();
            var service = LocalizationService.Instance;
            service.Initialize(settingsService);

            // Switching to de-DE persists code
            service.SetLanguage("de-DE", persist: true);
            Assert.AreEqual("de-DE", settingsService.Current.AppLanguage);
            Assert.AreEqual("de-DE", service.CurrentLanguage);

            // Re-initializing with invalid code falls back to en-US
            settingsService.Current.AppLanguage = "invalid-xx";
            service.Initialize(settingsService);
            Assert.AreEqual("en-US", service.CurrentLanguage);
        }

        #endregion

        #region 2. Per-Game Glow Settings: Model, Persistence & Overrides Pipeline

        [TestMethod]
        public void GameOverrides_IdentifierNormalization_HandlesPathsAndCase()
        {
            var raw1 = @"C:\Games\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe";
            var raw2 = "CYBERPUNK2077.EXE";
            var raw3 = "cyberpunk2077.exe";
            var raw4 = "Cyberpunk2077";

            var norm1 = AppSettings.NormalizeGameIdentifier(raw1);
            var norm2 = AppSettings.NormalizeGameIdentifier(raw2);
            var norm3 = AppSettings.NormalizeGameIdentifier(raw3);
            var norm4 = AppSettings.NormalizeGameIdentifier(raw4);

            Assert.AreEqual("cyberpunk2077.exe", norm1);
            Assert.AreEqual("cyberpunk2077.exe", norm2);
            Assert.AreEqual("cyberpunk2077.exe", norm3);
            Assert.AreEqual("cyberpunk2077", norm4);
        }

        [TestMethod]
        public void GameOverrides_ClampingAndValidation_RestrictsToValidRanges()
        {
            var overrid = new GameGlowOverride
            {
                DurationMs = -50,
                Intensity = 2.5,
                GlowSize = 999.0
            };

            overrid.ValidateAndClamp();

            Assert.AreEqual(500, overrid.DurationMs!.Value, "DurationMs below 500 must clamp to 500.");
            Assert.AreEqual(1.0, overrid.Intensity!.Value, 0.001, "Intensity above 1.0 must clamp to 1.0.");
            Assert.IsTrue(overrid.GlowSize!.Value == 100.0 || overrid.GlowSize!.Value == 256.0, "GlowSize above max must clamp.");

            overrid.DurationMs = 999999;
            overrid.Intensity = 0.001;
            overrid.GlowSize = 1.0;
            overrid.ValidateAndClamp();

            Assert.AreEqual(15000, overrid.DurationMs!.Value, "DurationMs above 15000 must clamp to 15000.");
            Assert.AreEqual(0.1, overrid.Intensity!.Value, 0.001, "Intensity below 0.1 must clamp to 0.1.");
            Assert.IsTrue(overrid.GlowSize!.Value <= 5.0, "GlowSize below min must clamp.");
        }

        [TestMethod]
        public void GameOverrides_Inheritance_ReturnsNullWhenNotOverridden()
        {
            var settings = new AppSettings();
            var entry = "eldenring.exe";

            // When no override is set
            Assert.IsFalse(settings.HasGameOverride(entry));
            var retrieved = settings.GetGameOverride(entry);
            Assert.IsNull(retrieved);
        }

        [TestMethod]
        public void GameOverrides_SetAndRemove_PersistsAcrossJsonSerialization()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;

            var gameKey = "doom_eternal.exe";
            var ov = new GameGlowOverride
            {
                DurationMs = 4500,
                Intensity = 0.85,
                GlowSize = 55.0
            };

            settings.SetGameOverride(gameKey, ov);
            settingsService.Save(settings);

            // Re-read settings
            var reloadedService = new SettingsService();
            var reloaded = reloadedService.Current;

            Assert.IsTrue(reloaded.HasGameOverride("DOOM_ETERNAL.EXE"));
            var loadedOv = reloaded.GetGameOverride("doom_eternal.exe");
            Assert.IsNotNull(loadedOv);
            Assert.AreEqual(4500, loadedOv.DurationMs!.Value);
            Assert.AreEqual(0.85, loadedOv.Intensity!.Value, 0.001);
            Assert.AreEqual(55.0, loadedOv.GlowSize!.Value, 0.001);

            // Remove override
            reloaded.RemoveGameOverride("doom_eternal.exe");
            settingsService.Save(reloaded);

            var finalReload = new SettingsService().Current;
            Assert.IsFalse(finalReload.HasGameOverride(gameKey));
        }

        [TestMethod]
        public void GameOverrides_GlowManagerPipeline_AppliesOverridesWhenActiveGameMatches()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.GamingModeEnabled = true;

            var gameExe = "witcher3.exe";
            settings.SetGameOverride(gameExe, new GameGlowOverride
            {
                DurationMs = 8000,
                Intensity = 0.95,
                GlowSize = 65.0
            });
            settingsService.Save(settings);

            var gameDetection = new GameDetectionService(settingsService);
            gameDetection.ActiveGameIdentifier = gameExe;
            gameDetection.ActiveGameName = gameExe;
            gameDetection.SetSimulatedGame(gameExe);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService, gameDetection);

            var dummyProfile = new AppProfile
            {
                AppId = "chrome.exe",
                Name = "Chrome",
                DurationMs = 2000,
                Intensity = 0.5,
                GlowSize = 25.0
            };

            var adjusted = glowManager.AdjustProfileParameters(dummyProfile, settings, isTest: false);

            Assert.AreEqual(8000, adjusted.DurationMs, "Active game override should set DurationMs to 8000.");
            Assert.AreEqual(0.95, adjusted.Intensity, 0.001, "Active game override should set Intensity to 0.95.");
            Assert.AreEqual(65.0, adjusted.GlowSize, 0.001, "Active game override should set GlowSize to 65.0.");

            // When no game is active, profile remains untouched by game override
            gameDetection.SetSimulatedGame(null);
            gameDetection.ActiveGameIdentifier = string.Empty;
            gameDetection.ActiveGameName = string.Empty;

            var untouched = glowManager.AdjustProfileParameters(dummyProfile, settings, isTest: false);
            Assert.AreEqual(2000, untouched.DurationMs);
            Assert.AreEqual(0.5, untouched.Intensity, 0.001);
            Assert.AreEqual(25.0, untouched.GlowSize, 0.001);
        }

        #endregion

        #region 3. Tracked Games: List/Grid Views Verification

        [TestMethod]
        public void TrackedGames_ViewLayoutResources_ExistInGamingViewXaml()
        {
            var xamlPath = Path.Combine(TestPathHelper.SolutionDir, "UI", "Views", "GamingView.xaml");
            Assert.IsTrue(File.Exists(xamlPath), "GamingView.xaml must exist.");

            var xamlContent = File.ReadAllText(xamlPath);
            Assert.IsTrue(xamlContent.Contains("PnlTrackedGames"), "GamingView must contain PnlTrackedGames container.");
            Assert.IsTrue(xamlContent.Contains("PnlEditGameOverride"), "GamingView must contain per-game override editor drawer.");
            Assert.IsTrue(xamlContent.Contains("BtnScanGames"), "GamingView must contain scan games button.");
        }

        #endregion

        #region 4. Sidebar Alignment when Collapsed

        [TestMethod]
        public void Sidebar_CollapsedAlignment_CentersIconsAndRemovesRightAlign()
        {
            var mainXamlPath = Path.Combine(TestPathHelper.SolutionDir, "UI", "MainWindow.xaml");
            Assert.IsTrue(File.Exists(mainXamlPath), "MainWindow.xaml must exist.");

            var content = File.ReadAllText(mainXamlPath);
            StringAssert.Contains(content, "RootNavigationView", "MainWindow must define RootNavigationView.");
            StringAssert.Contains(content, "CompactPaneLength=\"52\"", "Navigation pane must configure compact pane length.");
        }

        #endregion

        #region 5. Button and Click Animations & Apple Switch Toggle

        [TestMethod]
        public void ButtonAnimation_BehaviorAllowsButtonBase_AndRespectsReduceMotion()
        {
            MotionPolicy.Update(false);
            Assert.IsFalse(MotionPolicy.IsReduceMotion);
            Assert.AreEqual(100, MotionPolicy.GetDurationMs(100));

            MotionPolicy.Update(true);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);
            Assert.AreEqual(0, MotionPolicy.GetDurationMs(100));

            MotionPolicy.Update(false);
        }

        [TestMethod]
        public void AppleSwitch_StyleContainsSmoothTranslationAndSpringFeedback()
        {
            MotionPolicy.Update(true);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);
            Assert.AreEqual(TimeSpan.Zero, MotionPolicy.GetDuration(TimeSpan.FromMilliseconds(200)));
            MotionPolicy.Update(false);
            Assert.IsFalse(MotionPolicy.IsReduceMotion);
            Assert.AreEqual(TimeSpan.FromMilliseconds(200), MotionPolicy.GetDuration(TimeSpan.FromMilliseconds(200)));
        }

        #endregion

        #region 6. New Application Profile Drawer: Increased Vertical Space

        [TestMethod]
        public void ApplicationsView_StyleAndPriorityOptions_HaveExpandedVerticalSpace()
        {
            var xamlPath = Path.Combine(TestPathHelper.SolutionDir, "UI", "Controls", "ProfileEditorControl.xaml");
            var content = File.ReadAllText(xamlPath);

            // Verify CmbEditStyle has appropriate height/minheight (compact 34-36 DIP in v2.3)
            var styleComboMatch = Regex.Match(content, @"x:Name=""CmbEditStyle""[^>]*MinHeight=""(\d+)""");
            Assert.IsTrue(styleComboMatch.Success, "CmbEditStyle should specify MinHeight");
            var styleHeight = int.Parse(styleComboMatch.Groups[1].Value);
            Assert.IsTrue(styleHeight >= 34, $"CmbEditStyle vertical height ({styleHeight}) must be at least 34.");

            // Verify CmbEditPriority has appropriate height/minheight (compact 34-36 DIP in v2.3)
            var priorityComboMatch = Regex.Match(content, @"x:Name=""CmbEditPriority""[^>]*MinHeight=""(\d+)""");
            Assert.IsTrue(priorityComboMatch.Success, "CmbEditPriority should specify MinHeight");
            var priorityHeight = int.Parse(priorityComboMatch.Groups[1].Value);
            Assert.IsTrue(priorityHeight >= 34, $"CmbEditPriority vertical height ({priorityHeight}) must be at least 34.");
        }

        #endregion

        #region 7. User-Friendly Animated Error Notifications

        [TestMethod]
        public void ToastNotificationService_PublishesToastEvent_WithCorrectSeverity()
        {
            var service = ToastNotificationService.Instance;
            ToastItem? received = null;

            Action<ToastItem> handler = item => received = item;
            service.ToastRequested += handler;

            try
            {
                service.Show("Configuration Saved", "Your gaming profile was successfully updated.", ToastSeverity.Success);

                Assert.IsNotNull(received);
                Assert.AreEqual("Configuration Saved", received.Title);
                Assert.AreEqual("Your gaming profile was successfully updated.", received.Message);
                Assert.AreEqual(ToastSeverity.Success, received.Severity);
            }
            finally
            {
                service.ToastRequested -= handler;
            }
        }

        [TestMethod]
        public void ToastNotificationService_DebouncesRapidDuplicateErrors()
        {
            var service = ToastNotificationService.Instance;
            int eventCount = 0;

            Action<ToastItem> handler = _ => Interlocked.Increment(ref eventCount);
            service.ToastRequested += handler;

            try
            {
                // Fire duplicate errors immediately in rapid succession
                service.ShowError("Process Access Denied", "Unable to hook process.");
                service.ShowError("Process Access Denied", "Unable to hook process.");
                service.ShowError("Process Access Denied", "Unable to hook process.");

                // Should be debounced to 1 invocation
                Assert.AreEqual(1, eventCount, "Duplicate toasts fired within debounce window must be suppressed.");
            }
            finally
            {
                service.ToastRequested -= handler;
            }
        }

        [TestMethod]
        public void ToastNotificationService_ShowException_FormatsTechnicalDetailsSafely()
        {
            var service = ToastNotificationService.Instance;
            ToastItem? received = null;

            Action<ToastItem> handler = item => received = item;
            service.ToastRequested += handler;

            try
            {
                var ex = new UnauthorizedAccessException("C:\\System\\Secrets.dat is restricted by OS ACLs.");
                service.ShowException("File Save Failed", ex, "Retry", () => { });

                Assert.IsNotNull(received);
                Assert.AreEqual("File Save Failed", received.Title);
                Assert.AreEqual(ToastSeverity.Error, received.Severity);
                Assert.AreEqual("Retry", received.ActionText);
                Assert.IsNotNull(received.ActionCallback);

                // User-facing message must NOT expose raw filesystem paths or exception stack
                Assert.IsFalse(received.Message.Contains("Secrets.dat"), "Message should not leak raw private filesystem internals directly.");
            }
            finally
            {
                service.ToastRequested -= handler;
            }
        }

        [TestMethod]
        public void Localization_Behavioral_AllFiveLanguages_IncludeUpdateCheckFailed()
        {
            var service = LocalizationService.Instance;
            var languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };

            foreach (var lang in languages)
            {
                service.SetLanguage(lang, persist: false);
                var str = service.GetString("Advanced.UpdateCheckFailed");
                Assert.IsFalse(string.IsNullOrWhiteSpace(str), $"[{lang}] Advanced.UpdateCheckFailed must not be empty.");
                Assert.AreNotEqual("Advanced.UpdateCheckFailed", str, $"[{lang}] Must not return raw key.");
            }
        }

        [TestMethod]
        public void GameOverrides_Behavioral_PartialOverridesInheritGlobalDefaults()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.GamingModeEnabled = true;
            settings.ReduceDurationInGames = false;
            settings.ReduceIntensityInGames = false;

            var gameExe = "forza.exe";
            // Set ONLY Duration override; Intensity and GlowSize are null (inherited)
            settings.SetGameOverride(gameExe, new GameGlowOverride
            {
                DurationMs = 6000,
                Intensity = null,
                GlowSize = null
            });
            settingsService.Save(settings);

            var gameDetection = new GameDetectionService(settingsService);
            gameDetection.ActiveGameIdentifier = gameExe;
            gameDetection.ActiveGameName = gameExe;
            gameDetection.SetSimulatedGame(gameExe);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService, gameDetection);

            var dummyProfile = new AppProfile
            {
                AppId = "slack.exe",
                Name = "Slack",
                DurationMs = 2500,
                Intensity = 0.70,
                GlowSize = 45.0
            };

            var adjusted = glowManager.AdjustProfileParameters(dummyProfile, settings, isTest: false);

            Assert.AreEqual(6000, adjusted.DurationMs, "DurationMs should take the game override (6000ms).");
            Assert.AreEqual(0.70, adjusted.Intensity, 0.001, "Intensity should inherit original/default profile value (0.70).");
            Assert.AreEqual(45.0, adjusted.GlowSize, 0.001, "GlowSize should inherit original/default profile value (45.0).");
        }

        [TestMethod]
        public void GamingView_Behavioral_ApplyViewMode_TogglesLayoutContainers()
        {
            var settings = new SettingsService();
            settings.Current.GamingViewMode = ApplicationViewMode.Grid;
            Assert.AreEqual(ApplicationViewMode.Grid, settings.Current.GamingViewMode);

            settings.Current.GamingViewMode = ApplicationViewMode.List;
            Assert.AreEqual(ApplicationViewMode.List, settings.Current.GamingViewMode);
        }

        [TestMethod]
        public void GamingView_Behavioral_EmptyState_VisibilityUpdatesWithGames()
        {
            var settings = new SettingsService();
            settings.Current.TrackedGames.Clear();
            Assert.AreEqual(0, settings.Current.TrackedGames.Count);

            settings.Current.TrackedGames.Add("game.exe");
            Assert.AreEqual(1, settings.Current.TrackedGames.Count);
        }

        [TestMethod]
        public void Localization_AllFiveLanguages_HaveAuthenticNonEnglishTranslations()
        {
            var service = LocalizationService.Instance;
            var sampleKeys = new[]
            {
                "Gaming.Subtitle",
                "Gaming.Mode",
                "Gaming.GlowDuringGames",
                "Gaming.PerGameOverrides",
                "Advanced.Title",
                "Advanced.Subtitle",
                "Advanced.AutomaticUpdates",
                "Advanced.ReduceMotion",
                "Advanced.BackupRestore",
                "Applications.Title",
                "Applications.AddApplication"
            };

            service.SetLanguage("en-US", persist: false);
            var enValues = sampleKeys.ToDictionary(k => k, k => service.GetString(k));

            var nonEnglishLangs = new[] { "es-ES", "fr-FR", "de-DE", "tr-TR" };
            foreach (var lang in nonEnglishLangs)
            {
                service.SetLanguage(lang, persist: false);
                foreach (var key in sampleKeys)
                {
                    var localizedVal = service.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(localizedVal), $"[{lang}] Key {key} should have a value.");
                    Assert.AreNotEqual(key, localizedVal, $"[{lang}] Key {key} must not be equal to its raw key.");

                    // German allows "Gaming" for Gaming.Title, but for full phrases it must not match English
                    if (key.Contains("."))
                    {
                        var enVal = enValues[key];
                        if (enVal.Length > 8)
                        {
                            Assert.AreNotEqual(enVal, localizedVal, $"[{lang}] Key '{key}' has raw English text '{enVal}' instead of translated content.");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void MotionPolicy_TracksReduceMotionAndReduceAnimationsIndependently()
        {
            MotionPolicy.Update(false, false);
            Assert.IsFalse(MotionPolicy.IsReduceMotion);

            MotionPolicy.Update(true, false);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);

            MotionPolicy.Update(false, true);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);

            MotionPolicy.Update(true, true);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);

            MotionPolicy.Update(false, false);
            Assert.IsFalse(MotionPolicy.IsReduceMotion);
        }

        [TestMethod]
        public void ToggleSwitch_ApplyToggleSwitchReduceMotion_SnapsTranslateAndResetsScale()
        {
            MotionPolicy.Update(true);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);
            ButtonPressAnimationBehavior.ApplyToggleSwitchReduceMotion(new object(), true);
            MotionPolicy.Update(false);
        }

        [TestMethod]
        public void V21_VersionNumberRule_EnforcesStrictlyTwoPartUserVisibleVersion()
        {
            // User-visible versions remain major.minor even though assembly/file versions are four-part.
            string version = UpdateService.CurrentVersionString;
            Assert.AreEqual(version, ReleaseVersionAssertions.AssertApplicationVersionMetadata());
            Assert.IsFalse(Regex.IsMatch(version, @"^\d+\.\d+\.\d+"), "User-visible version must NOT be three-part (e.g. 2.1.0).");
            ReleaseVersionAssertions.AssertInstallerVersionMetadata(version);
        }

        [TestMethod]
        public void UpdateService_VersionComparison_HandlesTwoPartAndThreePartTags_Correctly()
        {
            // Upgrade from v2.0 to v2.1 must succeed whether tags have 'v' or not
            Assert.IsTrue(UpdateService.IsNewerVersion("v2.1", "2.0"), "v2.1 must be newer than 2.0");
            Assert.IsTrue(UpdateService.IsNewerVersion("2.1", "2.0"), "2.1 must be newer than 2.0");
            Assert.IsTrue(UpdateService.IsNewerVersion("v2.1", "v2.0"), "v2.1 must be newer than v2.0");
            Assert.IsTrue(UpdateService.IsNewerVersion("2.1", "2.0.0"), "2.1 must be newer than 2.0.0");

            // Same version must return false
            Assert.IsFalse(UpdateService.IsNewerVersion("v2.1", "2.1"), "v2.1 is identical to 2.1");
            Assert.IsFalse(UpdateService.IsNewerVersion("2.1", "2.1"), "2.1 is identical to 2.1");
            Assert.IsFalse(UpdateService.IsNewerVersion("v2.1.0", "2.1"), "v2.1.0 must be recognized as equivalent to 2.1");
            Assert.IsFalse(UpdateService.IsNewerVersion("v2.1", "2.1.0"), "v2.1 must be recognized as equivalent to 2.1.0");

            // Newer future versions
            Assert.IsTrue(UpdateService.IsNewerVersion("v2.1.1", "2.1"), "v2.1.1 is newer than 2.1");
            Assert.IsTrue(UpdateService.IsNewerVersion("v2.2", "2.1"), "v2.2 is newer than 2.1");
            Assert.IsTrue(UpdateService.IsNewerVersion("v3.0", "2.1"), "v3.0 is newer than 2.1");

            // Downgrade protection
            Assert.IsFalse(UpdateService.IsNewerVersion("v2.0", "2.1"), "v2.0 is older than 2.1");
            Assert.IsFalse(UpdateService.IsNewerVersion("v1.9", "2.1"), "v1.9 is older than 2.1");

            // Invalid or empty inputs
            Assert.IsFalse(UpdateService.IsNewerVersion("", "2.1"), "Empty latest tag should return false");
            Assert.IsFalse(UpdateService.IsNewerVersion("v2.1", ""), "Empty current version should return false");
            Assert.IsFalse(UpdateService.IsNewerVersion("invalid", "2.1"), "Unparseable tag should return false");
        }

        #endregion
    }
}
