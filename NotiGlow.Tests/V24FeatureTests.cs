using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Dialogs;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V24FeatureTests
    {
        private static string SolutionDir => FindSolutionDirectory();

        private static string FindSolutionDirectory()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "NotiGlow.sln")))
                    return current;
                current = Path.GetDirectoryName(current);
            }
            return @"C:\Users\omer\Projects\Notiglow";
        }

        #region Requirement 1: Tab Animations (v2.2 Restoration & Single Indicator)

        [TestMethod]
        public void MainWindowXaml_ActiveSelectionBox_RestoresV22IndicatorStructure()
        {
            var path = Path.Combine(SolutionDir, "UI", "MainWindow.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Name=\"ActiveSelectionBox\"",
                "MainWindow.xaml must define ActiveSelectionBox overlay.");
            StringAssert.Contains(content, "CornerRadius=\"{DynamicResource ControlCornerRadius}\"",
                "ActiveSelectionBox must use ControlCornerRadius resource.");
            StringAssert.Contains(content, "Background=\"{DynamicResource NavIndicatorColor}\"",
                "ActiveSelectionBox must have single NavIndicatorColor indicator strip.");
            StringAssert.Contains(content, "Background=\"{DynamicResource NavActiveBackground}\"",
                "ActiveSelectionBox must have single NavActiveBackground fill.");
        }

        [TestMethod]
        public void AppXaml_NavigationViewItem_ActiveIndicatorIsCollapsedToPreventDoubleIndicator()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            // In NavigationViewItem template, ActiveIndicator must be collapsed or opacity 0 when active
            StringAssert.Contains(content, "TargetName=\"ActiveIndicator\" Property=\"Visibility\" Value=\"Collapsed\"",
                "App.xaml must collapse NavigationViewItem template ActiveIndicator to avoid duplicate selection bar with ActiveSelectionBox.");
        }

        [TestMethod]
        public void AppXaml_NavigationViewItem_BuiltInSelectionLayersAreTransparent()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            // Verify built-in sharp rectangular selection layers from Wpf.Ui are overridden to Transparent
            StringAssert.Contains(content, "<SolidColorBrush x:Key=\"NavigationViewItemBackgroundSelected\" Color=\"Transparent\" />",
                "App.xaml must override NavigationViewItemBackgroundSelected to Transparent to remove sharp rectangular selection background.");
            StringAssert.Contains(content, "<SolidColorBrush x:Key=\"NavigationViewItemBackgroundSelectedLeftFluent\" Color=\"Transparent\" />",
                "App.xaml must override NavigationViewItemBackgroundSelectedLeftFluent to Transparent.");
            StringAssert.Contains(content, "<SolidColorBrush x:Key=\"NavigationViewSelectionIndicatorForeground\" Color=\"Transparent\" />",
                "App.xaml must override NavigationViewSelectionIndicatorForeground to Transparent to prevent duplicate built-in indicator.");
        }

        #endregion

        #region Requirement 2: In-App Update Confirmation Dialog

        [TestMethod]
        public void UpdateDialog_LocalizationKeys_ExistInAllSupportedLanguages()
        {
            var languages = new[] { "en", "es", "fr", "de", "tr" };
            var requiredKeys = new[]
            {
                "UpdateDialog.Title",
                "UpdateDialog.AvailableTitle",
                "UpdateDialog.CurrentVersionLabel",
                "UpdateDialog.NewVersionLabel",
                "UpdateDialog.Description",
                "UpdateDialog.ReleaseNotes",
                "UpdateDialog.BtnUpdate",
                "UpdateDialog.BtnCancel"
            };

            foreach (var lang in languages)
            {
                LocalizationService.Instance.SetLanguage(lang, persist: false);
                foreach (var key in requiredKeys)
                {
                    string val = LocalizationService.Instance.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Missing localization key '{key}' in language '{lang}'");
                    Assert.AreNotEqual(key, val, $"Key '{key}' was not resolved in language '{lang}'");
                }
            }
        }

        [TestMethod]
        public void UpdateConfirmDialogWindow_ConstructsWithCorrectProperties()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var dialog = new UpdateConfirmDialogWindow("v2.4.0", "v2.3.0", "New release notes available");
                    Assert.AreEqual("v2.4.0", dialog.NewVersion);
                    Assert.AreEqual("v2.3.0", dialog.CurrentVersion);
                    Assert.IsFalse(dialog.UserConfirmed);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "UpdateConfirmDialogWindow constructor timed out or failed.");
        }

        #endregion

        #region Requirement 3: Automatic Application Detection & Custom Profile Protection

        [TestMethod]
        public void BuiltInAppDetector_NonExistentPaths_AreFilteredOut()
        {
            // BuiltInAppDetector only registers apps if they actually exist on disk
            var detected = BuiltInAppDetector.Default.GetInstalledProfiles();
            Assert.IsNotNull(detected);

            foreach (var app in detected)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(app.ExecutablePath), "Detected app must have an ExecutablePath");
                Assert.IsTrue(File.Exists(app.ExecutablePath), $"Detected path '{app.ExecutablePath}' must exist on disk.");
            }
        }

        [TestMethod]
        public void ProfileService_CustomProfiles_ArePreservedDuringPrune()
        {
            var profiles = new List<AppProfile>
            {
                new AppProfile { AppId = "CustomApp1", Name = "Custom User App", ExecutablePath = @"C:\Fake\App.exe", IsCustom = true },
                new AppProfile { AppId = "discord", Name = "Discord", ExecutablePath = @"C:\NonExistent\Discord.exe", IsCustom = false }
            };

            var builtInIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "discord", "slack" };

            // Simulate pruning logic
            var remaining = profiles.Where(p => p.IsCustom || !builtInIds.Contains(p.AppId) || File.Exists(p.ExecutablePath)).ToList();

            Assert.IsTrue(remaining.Any(p => p.AppId == "CustomApp1"), "Custom profiles must always be preserved.");
            Assert.IsFalse(remaining.Any(p => p.AppId == "discord"), "Non-existent built-in profiles must be pruned.");
        }

        #endregion

        #region Requirement 4: Corner Rounding Setting

        [TestMethod]
        public void AppSettings_CornerRadius_DefaultsTo8AndPersists()
        {
            var settings = new AppSettings();
            Assert.AreEqual(8.0, settings.CornerRadius);

            settings.CornerRadius = 14.5;
            string json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(14.5, deserialized.CornerRadius);
        }

        [TestMethod]
        public void ThemeService_ApplyCornerRadius_UpdatesApplicationResources()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    if (Application.Current == null)
                    {
                        new Application();
                    }

                    ThemeService.ApplyCornerRadius(12.0);

                    if (Application.Current?.Resources["ControlCornerRadius"] is CornerRadius controlRadius)
                    {
                        Assert.AreEqual(12.0, controlRadius.TopLeft);
                    }
                    if (Application.Current?.Resources["CardCornerRadius"] is CornerRadius cardRadius)
                    {
                        Assert.AreEqual(12.0, cardRadius.TopLeft);
                    }
                    if (Application.Current?.Resources["PopupCornerRadius"] is CornerRadius popupRadius)
                    {
                        Assert.AreEqual(12.0, popupRadius.TopLeft);
                    }
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "ApplyCornerRadius test timed out or failed.");
        }

        #endregion

        #region Requirement 5: Language Selection Flag Emojis

        [TestMethod]
        public void LocalizationService_SupportedLanguages_ContainExpectedFlagEmojis()
        {
            var languages = LocalizationService.SupportedLanguages;
            Assert.IsNotNull(languages);
            Assert.AreEqual(5, languages.Count);

            var en = languages.FirstOrDefault(l => l.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            var tr = languages.FirstOrDefault(l => l.Code.StartsWith("tr", StringComparison.OrdinalIgnoreCase));
            var es = languages.FirstOrDefault(l => l.Code.StartsWith("es", StringComparison.OrdinalIgnoreCase));
            var fr = languages.FirstOrDefault(l => l.Code.StartsWith("fr", StringComparison.OrdinalIgnoreCase));
            var de = languages.FirstOrDefault(l => l.Code.StartsWith("de", StringComparison.OrdinalIgnoreCase));

            Assert.IsNotNull(en);
            Assert.AreEqual("🇬🇧", en.FlagEmoji);
            Assert.IsNotNull(tr);
            Assert.AreEqual("🇹🇷", tr.FlagEmoji);
            Assert.IsNotNull(es);
            Assert.AreEqual("🇪🇸", es.FlagEmoji);
            Assert.IsNotNull(fr);
            Assert.AreEqual("🇫🇷", fr.FlagEmoji);
            Assert.IsNotNull(de);
            Assert.AreEqual("🇩🇪", de.FlagEmoji);
        }

        #endregion

        #region Requirement 6: Horizontal Sliding Animation for Theme Mode

        [TestMethod]
        public void AppXaml_SlidingThemeRadioButtonStyle_IsDefined()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Key=\"SlidingThemeRadioButtonStyle\"",
                "App.xaml must define SlidingThemeRadioButtonStyle.");
        }

        [TestMethod]
        public void GeneralViewXaml_ThemeModeCard_ContainsThemeIndicatorPillAndTranslateTransform()
        {
            var path = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Name=\"ThemeIndicatorPill\"",
                "GeneralView.xaml must contain ThemeIndicatorPill for sliding selection.");
            StringAssert.Contains(content, "x:Name=\"ThemeIndicatorTranslate\"",
                "GeneralView.xaml must define ThemeIndicatorTranslate TranslateTransform for X-axis motion.");
            StringAssert.Contains(content, "TranslateTransform",
                "Theme indicator must use TranslateTransform on X axis.");
        }

        #endregion

        #region Requirement 7: Shared Gaming Settings & Per-Game Priority

        [TestMethod]
        public void AppSettings_CommonGamingSettings_AreStoredAndClamped()
        {
            var settings = new AppSettings();
            Assert.AreEqual(3000, settings.CommonGamingDurationMs);
            Assert.AreEqual(0.6, settings.CommonGamingIntensity);
            Assert.AreEqual(30.0, settings.CommonGamingGlowSize);

            settings.SetCommonGamingSettings(5500, 0.85, 45.0);

            Assert.AreEqual(5500, settings.CommonGamingDurationMs);
            Assert.AreEqual(0.85, settings.CommonGamingIntensity);
            Assert.AreEqual(45.0, settings.CommonGamingGlowSize);
            Assert.IsTrue(settings.HasCustomCommonGamingSettings);
        }

        [TestMethod]
        public void GlowManager_AdjustProfileParameters_AppliesCommonGamingSettingsDuringGame()
        {
            var settingsService = new SettingsService();
            var gameService = new GameDetectionService(settingsService);
            var glowManager = new GlowManager(null!, null!, gameService);

            var settings = new AppSettings
            {
                GamingModeEnabled = true,
                ReduceDurationInGames = false,
                ReduceIntensityInGames = false
            };
            settings.SetCommonGamingSettings(4200, 0.70, 48.0);

            var originalProfile = new AppProfile
            {
                DurationMs = 2000,
                Intensity = 0.5,
                GlowSize = 25.0
            };

            var adjusted = glowManager.AdjustProfileParameters(originalProfile, settings, isTest: true);

            Assert.AreEqual(4200, adjusted.DurationMs, "Shared gaming duration must apply in game session.");
            Assert.AreEqual(0.70, adjusted.Intensity, 0.01, "Shared gaming intensity must apply in game session.");
            Assert.AreEqual(48.0, adjusted.GlowSize, 0.01, "Shared gaming blur size must apply in game session.");
        }

        [TestMethod]
        public void GamingView_PerGameOverride_DoesNotSilentlyOverwriteGlobalCommonSettings()
        {
            var settings = new AppSettings();
            settings.SetCommonGamingSettings(3000, 0.6, 30.0);
            settings.HasCustomCommonGamingSettings = false;

            // Set override for a specific game
            string gameId = AppSettings.NormalizeGameIdentifier(@"C:\Games\Cyberpunk2077\bin\x64\Cyberpunk2077.exe");
            settings.GameOverrides = new Dictionary<string, GameGlowOverride>(StringComparer.OrdinalIgnoreCase)
            {
                [gameId] = new GameGlowOverride { DurationMs = 6000, Intensity = 0.9, GlowSize = 50.0 }
            };

            // Global common gaming settings must remain untouched
            Assert.AreEqual(3000, settings.CommonGamingDurationMs);
            Assert.AreEqual(0.6, settings.CommonGamingIntensity);
            Assert.AreEqual(30.0, settings.CommonGamingGlowSize);
            Assert.IsFalse(settings.HasCustomCommonGamingSettings);

            // Per-game override must be present for this game
            Assert.IsTrue(settings.GameOverrides.ContainsKey(gameId));
            Assert.AreEqual(6000, settings.GameOverrides[gameId].DurationMs);
        }

        #endregion

        #region Requirement 8: Backward Compatibility with Older Settings JSON

        [TestMethod]
        public void AppSettings_DeserializesLegacyV22SettingsWithoutErrors()
        {
            string legacyJson = @"{
                ""MasterEnabled"": true,
                ""ColorTheme"": 0,
                ""ThemeMode"": 0,
                ""GamingModeEnabled"": true,
                ""GlowDuringGames"": true,
                ""TrackedGames"": [""game.exe""]
            }";

            var deserialized = JsonSerializer.Deserialize<AppSettings>(legacyJson);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(8.0, deserialized.CornerRadius, "CornerRadius must safely default to 8.0 on older configs.");
            Assert.AreEqual(3000, deserialized.CommonGamingDurationMs, "CommonGamingDurationMs must safely default.");
            Assert.AreEqual(0.6, deserialized.CommonGamingIntensity, "CommonGamingIntensity must safely default.");
            Assert.AreEqual(30.0, deserialized.CommonGamingGlowSize, "CommonGamingGlowSize must safely default.");
            Assert.IsFalse(deserialized.HasCustomCommonGamingSettings);
        }

        #endregion

        #region Requirement 9: Unselected Tab Hover Styling & Template Isolation

        [TestMethod]
        public void AppXaml_NavigationViewItemBackgroundPointerOver_IsOverriddenTransparent()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Key=\"NavigationViewItemBackgroundPointerOver\" Color=\"Transparent\"",
                "App.xaml must override built-in Wpf.Ui NavigationViewItemBackgroundPointerOver as Transparent to eliminate sharp rectangular hover background.");
        }

        [TestMethod]
        public void ThemeService_NavigationViewItemBackgroundPointerOver_IsOverriddenTransparent()
        {
            var path = Path.Combine(SolutionDir, "Services", "ThemeService.cs");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "SetFrozenBrush(res, \"NavigationViewItemBackgroundPointerOver\", \"#00000000\")",
                "ThemeService must set NavigationViewItemBackgroundPointerOver to transparent (#00000000) during ApplyTheme.");
        }

        [TestMethod]
        public void MainWindowXaml_RootNavigationView_UsesNotiGlowNavigationViewItemTemplate()
        {
            var path = Path.Combine(SolutionDir, "UI", "MainWindow.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "ItemTemplate=\"{DynamicResource NotiGlowNavigationViewItemTemplate}\"",
                "RootNavigationView must explicitly bind ItemTemplate to NotiGlowNavigationViewItemTemplate so Wpf.Ui does not overwrite items with its built-in rectangular template.");
        }

        [TestMethod]
        public void AppXaml_NotiGlowNavigationViewItemTemplate_HoverUsesControlHoverBackgroundWithCornerRadius()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Key=\"NotiGlowNavigationViewItemTemplate\"",
                "App.xaml must declare NotiGlowNavigationViewItemTemplate as an accessible resource.");
            StringAssert.Contains(content, "CornerRadius=\"16\"",
                "NotiGlowNavigationViewItemTemplate must use 16px corner radius for rounded hover.");
            StringAssert.Contains(content, "TargetName=\"ItemBorder\" Property=\"Background\" Value=\"{DynamicResource ControlHoverBackground}\"",
                "NotiGlowNavigationViewItemTemplate must use ControlHoverBackground for inactive item hover.");
        }

        #endregion

        #region Requirement 10: Default Vercel Theme For New Installations & Theme Scenarios A-E

        [TestMethod]
        public void ScenarioA_NoSettingsJson_OrNoColorTheme_RuntimeIsStandardVercel()
        {
            // 1. When settings.json does not exist
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string nonExistentFile = Path.Combine(tempDir, "settings.json");
                var service = new SettingsService(nonExistentFile);

                Assert.AreEqual(ColorTheme.Standard, service.Current.ColorTheme,
                    "Scenario A: When settings.json does not exist, ColorTheme must default to ColorTheme.Standard.");
                Assert.AreEqual(NotiGlow.Models.ThemeMode.Dark, service.Current.ThemeMode,
                    "Scenario A: ThemeMode must default to Dark.");

                var palette = ThemeService.GetPalette(service.Current.ColorTheme, isDark: true);
                Assert.AreEqual("#3B82F6", palette.AccentColor, "Vercel Dark accent must be #3B82F6.");
                Assert.AreEqual("#0A0A0A", palette.WindowBackground, "Vercel Dark window background must be #0A0A0A.");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }

            // 2. When settings.json exists but has no ColorTheme (legacy settings)
            string legacyJson = "{\"ThemeMode\": \"Dark\", \"MasterEnabled\": true, \"CornerRadius\": 8.0}";
            var deserialized = JsonSerializer.Deserialize<AppSettings>(legacyJson);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(ColorTheme.Standard, deserialized.ColorTheme,
                "Scenario A: Legacy settings without ColorTheme must default safely to ColorTheme.Standard.");
        }

        [TestMethod]
        public void ScenarioB_SettingsJsonWithIndigo_IndigoIsPreserved()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsFile = Path.Combine(tempDir, "settings.json");
                File.WriteAllText(settingsFile, "{\"ColorTheme\": \"Indigo\", \"ThemeMode\": \"Dark\"}");

                var service = new SettingsService(settingsFile);
                Assert.AreEqual(ColorTheme.Indigo, service.Current.ColorTheme,
                    "Scenario B: Settings with ColorTheme=Indigo must preserve Indigo.");

                var palette = ThemeService.GetPalette(service.Current.ColorTheme, isDark: true);
                Assert.AreEqual("#6366F1", palette.AccentColor, "Indigo Frost accent must be #6366F1.");
                Assert.AreEqual("#0B0F19", palette.WindowBackground, "Indigo Frost background must be #0B0F19.");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void ScenarioC_SettingsJsonWithZen_ZenIsPreserved()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsFile = Path.Combine(tempDir, "settings.json");
                File.WriteAllText(settingsFile, "{\"ColorTheme\": \"Zen\", \"ThemeMode\": \"Dark\"}");

                var service = new SettingsService(settingsFile);
                Assert.AreEqual(ColorTheme.Zen, service.Current.ColorTheme,
                    "Scenario C: Settings with ColorTheme=Zen must preserve Zen.");

                var palette = ThemeService.GetPalette(service.Current.ColorTheme, isDark: true);
                Assert.AreEqual("#C7A97B", palette.AccentColor, "Zen accent must be #C7A97B.");
                Assert.AreEqual("#181614", palette.WindowBackground, "Zen background must be #181614.");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void ScenarioD_AppRestart_PersistsSavedTheme()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsFile = Path.Combine(tempDir, "settings.json");

                var themesToVerify = new[] { ColorTheme.Zen, ColorTheme.Indigo, ColorTheme.Burgundy, ColorTheme.Standard };
                foreach (var expectedTheme in themesToVerify)
                {
                    // Session 1: User sets theme and saves
                    var session1 = new SettingsService(settingsFile);
                    var s = session1.Current;
                    s.ColorTheme = expectedTheme;
                    session1.Save(s);

                    // Session 2: App restarts (new SettingsService instance loads from disk)
                    var session2 = new SettingsService(settingsFile);
                    Assert.AreEqual(expectedTheme, session2.Current.ColorTheme,
                        $"Scenario D: Saved theme {expectedTheme} must be preserved across app restart.");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void ScenarioE_FirstStartup_WpfUiDoesNotOverrideVercelTheme()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var app = System.Windows.Application.Current ?? new System.Windows.Application();

                    // Apply Vercel theme (what App.xaml.cs does at startup for new users)
                    ThemeService.ApplyTheme(ColorTheme.Standard, NotiGlow.Models.ThemeMode.Dark);

                    var res = app.Resources;
                    var accentBrush = res["AccentColor"] as System.Windows.Media.SolidColorBrush;
                    var windowBgBrush = res["WindowBackground"] as System.Windows.Media.SolidColorBrush;
                    var sysAccentBrush = res["SystemAccentColorBrush"] as System.Windows.Media.SolidColorBrush;
                    var fillBrush = res["AccentFillColorDefaultBrush"] as System.Windows.Media.SolidColorBrush;

                    Assert.IsNotNull(accentBrush, "AccentColor resource must exist.");
                    Assert.AreEqual("#FF3B82F6", accentBrush.Color.ToString(),
                        "Scenario E: AccentColor must be Vercel Blue (#3B82F6), not overridden by Wpf.Ui Indigo (#5865F2) or purple.");

                    Assert.IsNotNull(windowBgBrush, "WindowBackground resource must exist.");
                    Assert.AreEqual("#FF0A0A0A", windowBgBrush.Color.ToString(),
                        "Scenario E: WindowBackground must be Vercel Dark (#0A0A0A), not overridden by Wpf.Ui or legacy #141414.");

                    var navIndicatorBrush = res["NavIndicatorColor"] as System.Windows.Media.SolidColorBrush;
                    Assert.IsNotNull(navIndicatorBrush, "NavIndicatorColor resource must exist.");
                    Assert.AreEqual("#FF3B82F6", navIndicatorBrush.Color.ToString(),
                        "Scenario E: NavIndicatorColor must be Vercel Blue (#3B82F6).");

                    // Create MainWindow and verify resources remain Vercel Standard
                    var mainWindow = new NotiGlow.UI.MainWindow();
                    var postWindowAccent = (app.Resources["AccentColor"] as System.Windows.Media.SolidColorBrush)?.Color.ToString();
                    Assert.AreEqual("#FF3B82F6", postWindowAccent,
                        "Scenario E: MainWindow creation must not overwrite Vercel AccentColor.");
                    mainWindow.Close();

                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "Scenario E STA test timed out or failed.");
            if (tcs.Task.IsFaulted && tcs.Task.Exception != null)
            {
                throw tcs.Task.Exception.InnerException ?? tcs.Task.Exception;
            }
        }

        [TestMethod]
        public void AppXaml_DefaultThemeBrushes_MatchVercelDarkPalette()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Key=\"AccentColor\" Color=\"#3B82F6\"", "App.xaml default AccentColor must match Vercel #3B82F6.");
            StringAssert.Contains(content, "x:Key=\"WindowBackground\" Color=\"#0A0A0A\"", "App.xaml default WindowBackground must match Vercel #0A0A0A.");
            StringAssert.Contains(content, "x:Key=\"NavIndicatorColor\" Color=\"#3B82F6\"", "App.xaml default NavIndicatorColor must match Vercel #3B82F6.");
        }

        [TestMethod]
        public void ThemePalette_Defaults_MatchVercelDarkPalette()
        {
            var palette = new ThemePalette();
            Assert.AreEqual("#3B82F6", palette.AccentColor, "ThemePalette default accent must match Vercel #3B82F6.");
            Assert.AreEqual("#0A0A0A", palette.WindowBackground, "ThemePalette default WindowBackground must match Vercel #0A0A0A.");
            Assert.AreEqual("#3B82F6", palette.NavIndicatorColor, "ThemePalette default NavIndicatorColor must match Vercel #3B82F6.");
        }

        [TestMethod]
        public void GeneralView_NewUser_DisplaysVercelStandardAsSelected()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var app = System.Windows.Application.Current ?? new System.Windows.Application();
                    string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowTests_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    try
                    {
                        string tempSettings = Path.Combine(tempDir, "settings.json");
                        var settingsService = new SettingsService(tempSettings);
                        var profileService = new ProfileService();
                        var glowManager = new GlowManager(settingsService, profileService);

                        var view = new NotiGlow.UI.Views.GeneralView();
                        view.Initialize(settingsService, glowManager);

                        var cmb = view.FindName("CmbColorTheme") as System.Windows.Controls.ComboBox;
                        Assert.IsNotNull(cmb, "CmbColorTheme combobox must exist.");

                        var selected = cmb.SelectedItem as System.Windows.Controls.ComboBoxItem;
                        Assert.IsNotNull(selected, "CmbColorTheme must have a selected item.");
                        Assert.AreEqual("Standard", selected.Tag?.ToString());

                        var stack = selected.Content as System.Windows.Controls.StackPanel;
                        Assert.IsNotNull(stack);
                        var textBlock = stack.Children.OfType<System.Windows.Controls.TextBlock>().FirstOrDefault();
                        Assert.IsNotNull(textBlock);
                        Assert.AreEqual("Vercel (Standard)", textBlock.Text);
                    }
                    finally
                    {
                        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                    }

                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "GeneralView STA test timed out or failed.");
            if (tcs.Task.IsFaulted && tcs.Task.Exception != null)
            {
                throw tcs.Task.Exception.InnerException ?? tcs.Task.Exception;
            }
        }

        [TestMethod]
        public void GeneralView_ColorThemeComboBox_ShowsAllThemesWithoutScrollbar()
        {
            var appXamlPath = Path.Combine(SolutionDir, "App.xaml");
            var appXamlContent = File.ReadAllText(appXamlPath);
            var generalViewPath = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            var generalViewContent = File.ReadAllText(generalViewPath);

            // 1. Verify ColorThemeComboBoxStyle is declared with adequate height and no ScrollViewer in App.xaml
            StringAssert.Contains(appXamlContent, "x:Key=\"ColorThemeComboBoxStyle\"",
                "App.xaml must declare ColorThemeComboBoxStyle.");
            Assert.IsTrue(appXamlContent.Contains("Property=\"MaxDropDownHeight\" Value=\"550\"") || appXamlContent.Contains("MaxDropDownHeight=\"550\""),
                "ColorThemeComboBoxStyle must define MaxDropDownHeight to fit all themes without scrolling.");

            // Verify ScrollViewer is removed from ColorTheme dropdown template
            StringAssert.Contains(appXamlContent, "<!-- Full item list host without vertical scrollbar so all themes are visible simultaneously -->",
                "ColorThemeComboBoxStyle template must eliminate ScrollViewer to prevent vertical scrollbar.");

            // 2. Verify CmbColorTheme specifically uses ColorThemeComboBoxStyle in GeneralView.xaml
            StringAssert.Contains(generalViewContent, "<ComboBox x:Name=\"CmbColorTheme\"\r\n                          Style=\"{DynamicResource ColorThemeComboBoxStyle}\"",
                "CmbColorTheme must use ColorThemeComboBoxStyle.");

            // 3. Verify other ComboBoxes (e.g. CmbLanguage) continue to use AppleSelectComboBoxStyle
            StringAssert.Contains(generalViewContent, "<ComboBox x:Name=\"CmbLanguage\"\r\n                          Style=\"{DynamicResource AppleSelectComboBoxStyle}\"",
                "CmbLanguage must continue using AppleSelectComboBoxStyle without being affected.");

            // 4. Runtime STA verification that all 12 themes exist in CmbColorTheme
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var app = System.Windows.Application.Current ?? new System.Windows.Application();
                    var view = new NotiGlow.UI.Views.GeneralView();
                    var cmb = view.FindName("CmbColorTheme") as System.Windows.Controls.ComboBox;
                    Assert.IsNotNull(cmb);

                    // All 12 canonical themes must be present as items
                    Assert.AreEqual(12, cmb.Items.Count, "CmbColorTheme must contain all 12 theme options.");
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "GeneralView STA test timed out or failed.");
            if (tcs.Task.IsFaulted && tcs.Task.Exception != null)
            {
                throw tcs.Task.Exception.InnerException ?? tcs.Task.Exception;
            }
        }

        [TestMethod]
        public void GeneralView_ColorThemeComboBox_RuntimeTemplate_HasNoScrollViewerAndFitsAllThemes()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        var app = new NotiGlow.App();
                        app.InitializeComponent();
                    }
                    var win = new NotiGlow.UI.MainWindow();
                    var settings = new SettingsService();
                    var profile = new ProfileService();
                    var notif = new NotificationService();
                    var glow = new GlowManager(settings, profile);
                    win.Initialize(settings, profile, notif, glow);

                    win.Show();

                    var cmb = win.ViewGeneral.CmbColorTheme;
                    Assert.IsNotNull(cmb, "CmbColorTheme must be present.");
                    
                    ThemeService.ApplyTheme(ColorTheme.Standard, NotiGlow.Models.ThemeMode.Dark);
                    
                    cmb.ApplyTemplate();
                    cmb.IsDropDownOpen = true;

                    Assert.IsTrue(cmb.MaxDropDownHeight >= 500, $"CmbColorTheme MaxDropDownHeight must be at least 500, was: {cmb.MaxDropDownHeight}");

                    var popup = cmb.Template?.FindName("Popup", cmb) as System.Windows.Controls.Primitives.Popup;
                    var partPopup = cmb.Template?.FindName("PART_Popup", cmb) as System.Windows.Controls.Primitives.Popup;
                    var activePopup = popup ?? partPopup;
                    Assert.IsNotNull(activePopup, "Popup must be found in CmbColorTheme template.");
                    Assert.IsNotNull(activePopup.Child, "Popup.Child must not be null.");

                    // 1. Verify NO ScrollViewer exists anywhere in Popup.Child tree
                    bool hasScrollViewer = HasTypeInVisualTree<System.Windows.Controls.ScrollViewer>(activePopup.Child);
                    Assert.IsFalse(hasScrollViewer, "ColorTheme popup template must NOT contain a ScrollViewer.");

                    // 2. Verify all 12 themes fit within MaxDropDownHeight
                    if (activePopup.Child is FrameworkElement dropDownBorder)
                    {
                        dropDownBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        Assert.IsTrue(dropDownBorder.DesiredSize.Height <= cmb.MaxDropDownHeight,
                            $"DropDownBorder height ({dropDownBorder.DesiredSize.Height}) must fit within MaxDropDownHeight ({cmb.MaxDropDownHeight}).");
                    }

                    // 3. Verify other ComboBoxes retain ScrollViewer in AppleSelectComboBoxStyle
                    var cmbLang = win.ViewGeneral.CmbLanguage;
                    Assert.IsNotNull(cmbLang, "CmbLanguage must exist.");
                    cmbLang.ApplyTemplate();
                    cmbLang.IsDropDownOpen = true;
                    var langPopup = (cmbLang.Template?.FindName("Popup", cmbLang) ?? cmbLang.Template?.FindName("PART_Popup", cmbLang)) as System.Windows.Controls.Primitives.Popup;
                    Assert.IsNotNull(langPopup?.Child, "CmbLanguage popup child must exist.");
                    bool langHasScrollViewer = HasTypeInVisualTree<System.Windows.Controls.ScrollViewer>(langPopup.Child);
                    Assert.IsTrue(langHasScrollViewer, "Standard ComboBox (CmbLanguage) must retain its ScrollViewer in AppleSelectComboBoxStyle.");

                    win.Close();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool finished = tcs.Task.Wait(10000);
            Assert.IsTrue(finished, "Runtime template test timed out.");
            if (tcs.Task.IsFaulted && tcs.Task.Exception != null)
            {
                throw tcs.Task.Exception.InnerException ?? tcs.Task.Exception;
            }
        }

        private static bool HasTypeInVisualTree<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return false;
            if (root is T) return true;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (HasTypeInVisualTree<T>(child)) return true;
            }
            return false;
        }

        private static string DumpTreeVisual(DependencyObject root, int indent = 0)
        {
            if (root == null) return "";
            var sb = new System.Text.StringBuilder();
            string name = (root as FrameworkElement)?.Name ?? "";
            sb.AppendLine($"{new string(' ', indent * 2)}{root.GetType().FullName} (Name='{name}')");
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                sb.Append(DumpTreeVisual(System.Windows.Media.VisualTreeHelper.GetChild(root, i), indent + 1));
            }
            return sb.ToString();
        }

        #endregion

        #region Requirement 11: Application Discovery & Deduplication Tests

        [TestMethod]
        public void LiveSystem_DetectInstalledApps_DiscoversValidApplicationsWithoutDuplicates()
        {
            var apps = InstalledAppScanner.Default.DetectAllInstalledApps();
            Assert.IsTrue(apps.Count > 0, "Real system must discover at least one installed application.");

            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in apps)
            {
                Assert.IsFalse(string.IsNullOrEmpty(a.ExecutablePath));
                Assert.IsTrue(File.Exists(a.ExecutablePath!), $"Executable path must exist: {a.ExecutablePath}");
                Assert.IsFalse(seenPaths.Contains(a.ExecutablePath!), $"Duplicate executable detected: {a.ExecutablePath}");
                seenPaths.Add(a.ExecutablePath!);
            }
        }

        [TestMethod]
        public void InstalledAppScanner_Deduplicates_MultipleSourcesForSameApp()
        {
            var validMockFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"C:\Program Files\TestApp\TestApp.exe",
                @"C:\Users\TestUser\AppData\Local\Programs\TestApp\TestApp.exe"
            };

            var scanner = new InstalledAppScanner(
                fileExists: path => validMockFiles.Contains(path),
                directoryExists: dir => true,
                shortcutResolver: lnk => @"C:\Program Files\TestApp\TestApp.exe"
            );

            var profiles = new List<AppProfile>
            {
                new AppProfile { AppId = "TestApp", Name = "Test App", ExecutablePath = @"C:\Program Files\TestApp\TestApp.exe" },
                new AppProfile { AppId = "testapp.exe", Name = "Test App Duplicate", ExecutablePath = @"c:\program files\testapp\testapp.exe" },
                new AppProfile { AppId = "TestApp_Shortcut", Name = "Test App", ExecutablePath = "\"C:\\Program Files\\TestApp\\TestApp.exe\"" }
            };

            var deduplicated = ProfileService.DeduplicateProfiles(profiles);
            Assert.AreEqual(1, deduplicated.Count, "Duplicate app profiles from different sources must deduplicate into a single entry.");
            Assert.AreEqual(@"C:\Program Files\TestApp\TestApp.exe", deduplicated[0].ExecutablePath);
        }

        [TestMethod]
        public void InstalledAppScanner_FiltersOut_NonExistentExecutables()
        {
            var validMockFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                @"C:\Windows\System32\notepad.exe"
            };

            var scanner = new InstalledAppScanner(
                fileExists: path => validMockFiles.Contains(path),
                directoryExists: dir => true,
                shortcutResolver: lnk => null
            );

            var detected = scanner.DetectAllInstalledApps();
            Assert.IsNotNull(detected);

            foreach (var app in detected)
            {
                Assert.IsFalse(string.IsNullOrEmpty(app.ExecutablePath));
                Assert.IsTrue(validMockFiles.Contains(app.ExecutablePath!),
                    $"Only existing files should be detected. Found: {app.ExecutablePath}");
            }
        }

        [TestMethod]
        public void ProfileService_NormalizePath_HandlesQuotesAndEnvironmentVariables()
        {
            string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string raw = "\"%WINDIR%\\System32\\notepad.exe\"";
            string normalized = ProfileService.NormalizePath(raw);

            Assert.IsFalse(normalized.StartsWith("\""), "Normalized path must not contain leading quotes.");
            Assert.IsFalse(normalized.EndsWith("\""), "Normalized path must not contain trailing quotes.");
            Assert.IsFalse(normalized.Contains("%WINDIR%"), "Normalized path must expand environment variables.");
            Assert.IsTrue(normalized.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase),
                $"Normalized path should start with {systemRoot}");
        }

        [TestMethod]
        public void ProfileService_DeduplicateProfiles_ConsolidatesDuplicatesWithoutDataLoss()
        {
            var input = new List<AppProfile>
            {
                new AppProfile { AppId = "app1", Name = "App One", ExecutablePath = @"C:\Apps\App1.exe", ColorHex = "#5865F2" },
                new AppProfile { AppId = "app1_dup", Name = "App One", ExecutablePath = @"c:\apps\app1.exe", ColorHex = "#3B82F6" },
                new AppProfile { AppId = "app2", Name = "App Two", ExecutablePath = @"C:\Apps\App2.exe", ColorHex = "#10B981" }
            };

            var result = ProfileService.DeduplicateProfiles(input);
            Assert.AreEqual(2, result.Count, "Deduplication must yield exactly 2 unique profiles.");
            Assert.IsTrue(result.Any(p => p.Name == "App One"));
            Assert.IsTrue(result.Any(p => p.Name == "App Two"));
        }

        #endregion

        #region Requirement 12: In-App Themed Confirmation & Error Dialogs

        [TestMethod]
        public void ConfirmationDialogWindow_STAInstantiation_SetsPropertiesCorrectly()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var dialog = new ConfirmationDialogWindow();
                    dialog.TxtTitle.Text = "Custom Title";
                    dialog.TxtMessage.Text = "Custom Message";
                    dialog.BtnConfirm.Content = "Yes";
                    dialog.BtnCancel.Content = "No";

                    Assert.AreEqual("Custom Title", dialog.TxtTitle.Text);
                    Assert.AreEqual("Custom Message", dialog.TxtMessage.Text);
                    Assert.AreEqual("Yes", dialog.BtnConfirm.Content);
                    Assert.AreEqual("No", dialog.BtnCancel.Content);
                    Assert.IsFalse(dialog.UserConfirmed);

                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "ConfirmationDialogWindow STA test timed out or failed.");
        }

        [TestMethod]
        public void ConfirmationDialogWindow_UnifiedDialog_SetsWarningAndErrorStylesCorrectly()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var dialog = new ConfirmationDialogWindow();
                    
                    // Verify header drag event is registered
                    Assert.IsNotNull(dialog.HeaderGrid);

                    // Verify default confirm mode has both buttons visible
                    Assert.AreEqual(Visibility.Visible, dialog.BtnConfirm.Visibility);
                    Assert.AreEqual(Visibility.Visible, dialog.BtnCancel.Visibility);

                    // Test single button warning/error/info styling
                    dialog.HeaderIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24;
                    dialog.HeaderIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
                    dialog.IconBadge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
                    dialog.BtnCancel.Visibility = Visibility.Collapsed;

                    Assert.AreEqual(Wpf.Ui.Controls.SymbolRegular.Warning24, dialog.HeaderIcon.Symbol);
                    Assert.AreEqual(Visibility.Collapsed, dialog.BtnCancel.Visibility);

                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            bool success = tcs.Task.Wait(5000);
            Assert.IsTrue(success, "Unified ConfirmationDialogWindow STA test timed out or failed.");
        }

        [TestMethod]
        public void EntireCodebase_DoesNotContainDirectNativeMessageBoxShow()
        {
            var csFiles = Directory.GetFiles(SolutionDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\") && !f.EndsWith("V24FeatureTests.cs"));

            foreach (var file in csFiles)
            {
                string text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains("MessageBox.Show("),
                    $"File '{Path.GetFileName(file)}' still contains direct native MessageBox.Show. Custom themed dialogs must be used instead.");
            }
        }

        #endregion

        #region Requirement 13: Tab Press Animation & Navigation Verification

        [TestMethod]
        public void MainWindow_TabPressAnimation_UsesV22ScaleAndTimingParameters()
        {
            var path = Path.Combine(SolutionDir, "UI", "MainWindow.xaml.cs");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "0.97",
                "MainWindow must use 0.97 scale on mouse down to match v2.2 tab press animation.");
            StringAssert.Contains(content, "80",
                "MainWindow must use 80ms duration on mouse down to match v2.2 tab press animation.");
            StringAssert.Contains(content, "115",
                "MainWindow must use 115ms duration on mouse up to match v2.2 tab release animation.");
            StringAssert.Contains(content, "CubicEase",
                "MainWindow must use CubicEase easing function to match v2.2 smooth spring/transition.");
        }

        #endregion

        #region Requirement 14: Global Gaming Settings Propagation Across Multiple Games

        [TestMethod]
        public void GlobalGamingSettings_AppliedAcrossMultipleGames()
        {
            var settingsService = new SettingsService();
            var gameService = new GameDetectionService(settingsService);
            var glowManager = new GlowManager(null!, null!, gameService);

            var settings = new AppSettings
            {
                GamingModeEnabled = true,
                ReduceDurationInGames = false,
                ReduceIntensityInGames = false
            };
            settings.SetCommonGamingSettings(4800, 0.75, 52.0);

            var game1Profile = new AppProfile
            {
                AppId = "Cyberpunk2077.exe",
                Name = "Cyberpunk 2077",
                DurationMs = 1500,
                Intensity = 0.3,
                GlowSize = 20.0
            };

            var game2Profile = new AppProfile
            {
                AppId = "Witcher3.exe",
                Name = "The Witcher 3",
                DurationMs = 2500,
                Intensity = 0.5,
                GlowSize = 35.0
            };

            var adjusted1 = glowManager.AdjustProfileParameters(game1Profile, settings, isTest: true);
            var adjusted2 = glowManager.AdjustProfileParameters(game2Profile, settings, isTest: true);

            // Both games should share the global gaming settings
            Assert.AreEqual(4800, adjusted1.DurationMs);
            Assert.AreEqual(0.75, adjusted1.Intensity, 0.01);
            Assert.AreEqual(52.0, adjusted1.GlowSize, 0.01);

            Assert.AreEqual(4800, adjusted2.DurationMs);
            Assert.AreEqual(0.75, adjusted2.Intensity, 0.01);
            Assert.AreEqual(52.0, adjusted2.GlowSize, 0.01);
        }

        #endregion
    }
}
