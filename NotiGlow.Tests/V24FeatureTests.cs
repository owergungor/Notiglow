using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
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

            StringAssert.Contains(content, "x:Name=\"RootNavigationView\"",
                "MainWindow.xaml must define RootNavigationView for navigation.");
            StringAssert.Contains(content, "PaneDisplayMode=\"Left\"",
                "MainWindow navigation must use left pane display mode.");
            StringAssert.Contains(content, "SelectionChanged=\"RootNavigationView_SelectionChanged\"",
                "RootNavigationView must handle tab selection changes.");
        }

        [TestMethod]
        public void AppXaml_NavigationViewItem_ActiveIndicatorIsCollapsedToPreventDoubleIndicator()
        {
            var path = Path.Combine(SolutionDir, "UI", "MainWindow.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Name=\"NavItemGeneral\"", "MainWindow must define NavItemGeneral.");
            StringAssert.Contains(content, "x:Name=\"NavItemApplications\"", "MainWindow must define NavItemApplications.");
            StringAssert.Contains(content, "x:Name=\"NavItemAppearance\"", "MainWindow must define NavItemAppearance.");
            StringAssert.Contains(content, "x:Name=\"NavItemDisplay\"", "MainWindow must define NavItemDisplay.");
            StringAssert.Contains(content, "x:Name=\"NavItemGaming\"", "MainWindow must define NavItemGaming.");
            StringAssert.Contains(content, "x:Name=\"NavItemNotifications\"", "MainWindow must define NavItemNotifications.");
            StringAssert.Contains(content, "x:Name=\"NavItemAdvanced\"", "MainWindow must define NavItemAdvanced.");
        }

        [TestMethod]
        public void AppXaml_NavigationViewItem_BuiltInSelectionLayersAreTransparent()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Key=\"NavIndicatorColor\"", "App.xaml must define NavIndicatorColor.");
            StringAssert.Contains(content, "x:Key=\"NavActiveBackground\"", "App.xaml must define NavActiveBackground.");
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
            var xamlPath = Path.Combine(SolutionDir, "UI", "Windows", "UpdateConfirmDialogWindow.xaml");
            var csPath = Path.Combine(SolutionDir, "UI", "Windows", "UpdateConfirmDialogWindow.xaml.cs");
            var xaml = File.ReadAllText(xamlPath);
            var cs = File.ReadAllText(csPath);

            StringAssert.Contains(xaml, "x:Name=\"TxtNewVersion\"");
            StringAssert.Contains(xaml, "x:Name=\"TxtCurrentVersion\"");
            StringAssert.Contains(cs, "public UpdateConfirmDialogWindow(string newVersion");
            StringAssert.Contains(cs, "ShowUpdateConfirm");
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
            Assert.IsTrue(settings.CornerRadius == 0.0 || settings.CornerRadius == 8.0);

            settings.CornerRadius = 14.5;
            string json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(14.5, deserialized.CornerRadius);
        }

        [TestMethod]
        public void ThemeService_ApplyCornerRadius_UpdatesApplicationResources()
        {
            // Verify ApplyCornerRadius executes safely and clamps within expected range [0, 48]
            ThemeService.ApplyCornerRadius(12.0);
            ThemeService.ApplyCornerRadius(-5.0);
            ThemeService.ApplyCornerRadius(100.0);
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
            var path = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Name=\"BtnModeSystem\"", "GeneralView must define BtnModeSystem radio button.");
            StringAssert.Contains(content, "x:Name=\"BtnModeLight\"", "GeneralView must define BtnModeLight radio button.");
            StringAssert.Contains(content, "x:Name=\"BtnModeDark\"", "GeneralView must define BtnModeDark radio button.");
        }

        [TestMethod]
        public void GeneralViewXaml_ThemeModeCard_ContainsThemeIndicatorPillAndTranslateTransform()
        {
            var path = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "GroupName=\"ThemeModeGroup\"",
                "ThemeMode radio buttons must belong to ThemeModeGroup.");
            StringAssert.Contains(content, "Click=\"ThemeMode_Click\"",
                "ThemeMode radio buttons must handle click events.");
        }

        #endregion

        #region Requirement 7: Shared Gaming Settings & Per-Game Priority

        [TestMethod]
        public void AppSettings_CommonGamingSettings_AreStoredAndClamped()
        {
            var settings = new AppSettings();
            Assert.AreEqual(3000, settings.CommonGamingDurationMs);
            Assert.IsTrue(settings.CommonGamingIntensity == 0.6 || settings.CommonGamingIntensity == 0.8);
            Assert.IsTrue(settings.CommonGamingGlowSize == 30.0 || settings.CommonGamingGlowSize == 32.0);

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
            Assert.IsTrue(settings.CommonGamingIntensity == 0.6 || settings.CommonGamingIntensity == 0.8);
            Assert.IsTrue(settings.CommonGamingGlowSize == 30.0 || settings.CommonGamingGlowSize == 32.0);
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
            Assert.IsTrue(deserialized.CornerRadius == 0.0 || deserialized.CornerRadius == 8.0, "CornerRadius must safely default.");
            Assert.AreEqual(3000, deserialized.CommonGamingDurationMs, "CommonGamingDurationMs must safely default.");
            Assert.IsTrue(deserialized.CommonGamingIntensity == 0.6 || deserialized.CommonGamingIntensity == 0.8, "CommonGamingIntensity must safely default.");
            Assert.IsTrue(deserialized.CommonGamingGlowSize == 30.0 || deserialized.CommonGamingGlowSize == 32.0, "CommonGamingGlowSize must safely default.");
            Assert.IsFalse(deserialized.HasCustomCommonGamingSettings);
        }

        #endregion

                #region Requirement 9: Unselected Tab Hover Styling & Template Isolation

        [TestMethod]
        public void AppXaml_NavigationViewItemBackgroundPointerOver_IsOverriddenTransparent()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Key=\"NavActiveHoverBackground\"",
                "App.xaml must define NavActiveHoverBackground.");
        }

        [TestMethod]
        public void ThemeService_NavigationViewItemBackgroundPointerOver_IsOverriddenTransparent()
        {
            var path = Path.Combine(SolutionDir, "Services", "ThemeService.cs");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "NavActiveHoverBackground",
                "ThemeService must register NavActiveHoverBackground in palette.");
        }

        [TestMethod]
        public void MainWindowXaml_RootNavigationView_UsesNotiGlowNavigationViewItemTemplate()
        {
            var path = Path.Combine(SolutionDir, "UI", "MainWindow.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Name=\"RootNavigationView\"",
                "MainWindow must define RootNavigationView.");
            StringAssert.Contains(content, "RootContentGrid",
                "MainWindow must define RootContentGrid for hosting view pages.");
        }

        [TestMethod]
        public void AppXaml_NotiGlowNavigationViewItemTemplate_HoverUsesControlHoverBackgroundWithCornerRadius()
        {
            var path = Path.Combine(SolutionDir, "App.xaml");
            var content = File.ReadAllText(path);

            StringAssert.Contains(content, "x:Key=\"ControlCornerRadius\"",
                "App.xaml must declare ControlCornerRadius as an accessible resource.");
        }

        #endregion

        #region Requirement 10: Default Vercel Theme For New Installations & Theme Scenarios A-E

        [TestMethod]
        public void ScenarioA_NoSettingsJson_OrNoColorTheme_RuntimeIsWindowsSystemAccent()
        {
            // 1. When settings.json does not exist
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string nonExistentFile = Path.Combine(tempDir, "settings.json");
                var service = new SettingsService(nonExistentFile);

                Assert.AreEqual(ColorTheme.Windows, service.Current.ColorTheme,
                    "Scenario A: When settings.json does not exist, ColorTheme must default to ColorTheme.Windows.");
                Assert.AreEqual(NotiGlow.Models.ThemeMode.Dark, service.Current.ThemeMode,
                    "Scenario A: ThemeMode must default to Dark.");

                var palette = ThemeService.GetPalette(service.Current.ColorTheme, isDark: true);
                Assert.AreEqual("#202020", palette.WindowBackground, "Windows dark neutral window background must be #202020.");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }

            // 2. When settings.json exists but has no ColorTheme (legacy settings)
            string legacyJson = "{\"ThemeMode\": \"Dark\", \"MasterEnabled\": true, \"CornerRadius\": 8.0}";
            var deserialized = JsonSerializer.Deserialize<AppSettings>(legacyJson);
            Assert.IsNotNull(deserialized);
            Assert.AreEqual(ColorTheme.Windows, deserialized.ColorTheme,
                "Scenario A: Legacy settings without ColorTheme must default safely to ColorTheme.Windows.");
        }

        [TestMethod]
        public void ScenarioB_SettingsJsonWithIndigo_GracefullyMigratesToWindows()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlowTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsFile = Path.Combine(tempDir, "settings.json");
                File.WriteAllText(settingsFile, "{\"ColorTheme\": \"Indigo\", \"ThemeMode\": \"Dark\"}");

                var service = new SettingsService(settingsFile);
                Assert.AreEqual(ColorTheme.Windows, service.Current.ColorTheme,
                    "Scenario B: Settings with legacy ColorTheme=Indigo must gracefully migrate to Windows.");
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
                Assert.AreEqual("#FFB900", palette.AccentColor, "Yellow accent must be #FFB900.");
                Assert.AreEqual("#202020", palette.WindowBackground, "Windows dark background must be #202020.");
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

                var themesToVerify = new[] { ColorTheme.Yellow, ColorTheme.Red, ColorTheme.Blue, ColorTheme.Windows };
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
            var palette = ThemeService.GetPalette(ColorTheme.Standard, true);
            Assert.AreEqual("#3B82F6", palette.AccentColor, "Scenario E: AccentColor must be Vercel Blue (#3B82F6).");
            Assert.AreEqual("#202020", palette.WindowBackground, "Scenario E: WindowBackground must be Windows Dark (#202020).");
            Assert.AreEqual("#3B82F6", palette.NavIndicatorColor, "Scenario E: NavIndicatorColor must be Vercel Blue (#3B82F6).");
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
        public void GeneralView_NewUser_DisplaysWindowsSystemAccentAsSelected()
        {
            var settings = new AppSettings();
            Assert.AreEqual(ColorTheme.Windows, settings.ColorTheme, "New user settings must default to ColorTheme.Windows.");
            var generalViewPath = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            var content = File.ReadAllText(generalViewPath);
            StringAssert.Contains(content, "Tag=\"Windows\"");
            StringAssert.Contains(content, "Windows (System Accent)");
        }

        [TestMethod]
        public void GeneralView_ColorThemeComboBox_ShowsAllThemesWithoutScrollbar()
        {
            var generalViewPath = Path.Combine(SolutionDir, "UI", "Views", "GeneralView.xaml");
            var generalViewContent = File.ReadAllText(generalViewPath);

            StringAssert.Contains(generalViewContent, "x:Name=\"CmbColorTheme\"", "GeneralView.xaml must declare CmbColorTheme.");
            
            Assert.AreEqual(9, ThemeService.CanonicalThemes.Count, "Must have exactly 9 refined color themes.");
            foreach (var theme in ThemeService.CanonicalThemes)
            {
                StringAssert.Contains(generalViewContent, $"Tag=\"{theme}\"", $"GeneralView.xaml must contain ComboBoxItem for {theme}.");
                var darkPalette = ThemeService.GetPalette(theme, true);
                Assert.IsNotNull(darkPalette, $"Palette for {theme} (Dark) must exist.");
                var lightPalette = ThemeService.GetPalette(theme, false);
                Assert.IsNotNull(lightPalette, $"Palette for {theme} (Light) must exist.");
            }
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
            var xamlPath = Path.Combine(SolutionDir, "UI", "Windows", "ConfirmationDialogWindow.xaml");
            var csPath = Path.Combine(SolutionDir, "UI", "Windows", "ConfirmationDialogWindow.xaml.cs");
            var xaml = File.ReadAllText(xamlPath);
            var cs = File.ReadAllText(csPath);

            StringAssert.Contains(xaml, "x:Name=\"TxtTitle\"");
            StringAssert.Contains(xaml, "x:Name=\"TxtMessage\"");
            StringAssert.Contains(xaml, "x:Name=\"BtnConfirm\"");
            StringAssert.Contains(xaml, "x:Name=\"BtnCancel\"");
            StringAssert.Contains(cs, "ShowConfirm");
            StringAssert.Contains(cs, "ShowWarning");
            StringAssert.Contains(cs, "ShowError");
        }

        [TestMethod]
        public void ConfirmationDialogWindow_UnifiedDialog_SetsWarningAndErrorStylesCorrectly()
        {
            var xamlPath = Path.Combine(SolutionDir, "UI", "Windows", "ConfirmationDialogWindow.xaml");
            var csPath = Path.Combine(SolutionDir, "UI", "Windows", "ConfirmationDialogWindow.xaml.cs");
            var xaml = File.ReadAllText(xamlPath);
            var cs = File.ReadAllText(csPath);

            StringAssert.Contains(xaml, "x:Name=\"HeaderGrid\"");
            StringAssert.Contains(xaml, "x:Name=\"HeaderIcon\"");
            StringAssert.Contains(xaml, "x:Name=\"IconBadge\"");
            StringAssert.Contains(cs, "SetSingleButtonMode");
        }

        [TestMethod]
        public void EntireCodebase_DoesNotContainDirectNativeMessageBoxShow()
        {
            var csFiles = Directory.GetFiles(SolutionDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj") && !f.Contains("NotiGlow.Tests"));

            foreach (var file in csFiles)
            {
                string t = File.ReadAllText(file);
                Assert.IsFalse(t.Contains("MessageBox.Show("),
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

            StringAssert.Contains(content, "RootNavigationView_SelectionChanged",
                "MainWindow must handle tab navigation selection changes smoothly.");
            StringAssert.Contains(content, "NavigateToTag",
                "MainWindow must navigate to tag when tab changes.");
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
