using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V25FeatureTests
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

        #region Requirement 16: Versioning v2.5

        [TestMethod]
        public void ProjectAndAssemblyInfo_ReflectVersion25()
        {
            ReleaseVersionAssertions.AssertApplicationVersionMetadata();
        }

        [TestMethod]
        public void SetupScript_ReflectVersion25()
        {
            var version = ReleaseVersionAssertions.AssertApplicationVersionMetadata();
            ReleaseVersionAssertions.AssertInstallerVersionMetadata(version);
        }

        #endregion

        #region Requirement 1 & 2: Default Glow Settings (#FFFFFF, 32px blur, 0px corner, 80% intensity)

        [TestMethod]
        public void AppSettings_DefaultGlowSettings_MatchV25Specifications()
        {
            var settings = new AppSettings();
            Assert.AreEqual("#FFFFFF", settings.DefaultColorHex);
            Assert.AreEqual(32.0, settings.DefaultGlowSize);
            Assert.AreEqual(0.8, settings.DefaultIntensity, 0.001);
            Assert.AreEqual(0.0, settings.CornerRadius);
        }

        [TestMethod]
        public void AppProfile_DefaultGlowSettings_MatchV25Specifications()
        {
            var profile = new AppProfile();
            Assert.AreEqual("#FFFFFF", profile.ColorHex);
            Assert.AreEqual(32.0, profile.GlowSize);
            Assert.AreEqual(0.8, profile.Intensity, 0.001);
            Assert.AreEqual(0.0, profile.CornerRadius);
        }

        #endregion

        #region Requirement 3 & 4: Blur Range 0-256px and Corner Rounding Range 0-48px

        [TestMethod]
        public void GlowSize_Clamping_EnforcesRange0To256()
        {
            Assert.AreEqual(0.0, GameGlowOverride.ClampGlowSize(-10.0));
            Assert.AreEqual(0.0, GameGlowOverride.ClampGlowSize(0.0));
            Assert.AreEqual(32.0, GameGlowOverride.ClampGlowSize(32.0));
            Assert.AreEqual(256.0, GameGlowOverride.ClampGlowSize(256.0));
            Assert.AreEqual(256.0, GameGlowOverride.ClampGlowSize(999.0));
        }

        [TestMethod]
        public void CornerRadius_Clamping_EnforcesRange0To48()
        {
            Assert.AreEqual(0.0, GameGlowOverride.ClampCornerRadius(-5.0));
            Assert.AreEqual(0.0, GameGlowOverride.ClampCornerRadius(0.0));
            Assert.AreEqual(16.0, GameGlowOverride.ClampCornerRadius(16.0));
            Assert.AreEqual(48.0, GameGlowOverride.ClampCornerRadius(48.0));
            Assert.AreEqual(48.0, GameGlowOverride.ClampCornerRadius(100.0));
        }

        #endregion

        #region Requirement 5: Tracked Apps List View Bug ("Discord" bug fix)

        [TestMethod]
        public void AppProfileCardXaml_DoesNotContainHardcodedDiscord()
        {
            var path = Path.Combine(SolutionDir, "UI", "Controls", "AppProfileCard.xaml");
            var xaml = File.ReadAllText(path);

            Assert.IsFalse(xaml.Contains("Text=\"Discord\""),
                "AppProfileCard.xaml must NOT contain hardcoded Text=\"Discord\".");
            StringAssert.Contains(xaml, "{Binding Name",
                "AppProfileCard.xaml must bind TxtAppName to Name property.");
        }

        [TestMethod]
        public void AppProfileCardCodeBehind_ExplicitlySetsAppName()
        {
            var path = Path.Combine(SolutionDir, "UI", "Controls", "AppProfileCard.xaml.cs");
            var code = File.ReadAllText(path);

            StringAssert.Contains(code, "TxtAppName.Text =",
                "AppProfileCard.xaml.cs must explicitly assign TxtAppName.Text.");
            StringAssert.Contains(code, "DataContext = profile;",
                "AppProfileCard.xaml.cs must set DataContext to profile.");
        }

        #endregion

        #region Requirement 7: Zero Direct MessageBox Calls

        [TestMethod]
        public void EntireCodebase_ZeroDirectMessageBoxUsage()
        {
            var csFiles = Directory.GetFiles(SolutionDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("obj") && !f.Contains("bin") && !f.Contains("Tests"))
                .ToList();

            string forbiddenCall = "Message" + "Box.Show(";
            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                Assert.IsFalse(content.Contains(forbiddenCall),
                    $"File '{Path.GetFileName(file)}' must not use native dialogs.");
            }
        }

        #endregion

        #region Requirement 8, 9, 10, 11: Gaming Tab Global Settings & Priority Hierarchy

        [TestMethod]
        public void AppSettings_CommonGamingSettings_IncludeCornerRadius()
        {
            var settings = new AppSettings();
            settings.SetCommonGamingSettings(3500, 0.75, 40.0, 12.0);

            Assert.AreEqual(3500, settings.CommonGamingDurationMs);
            Assert.AreEqual(0.75, settings.CommonGamingIntensity);
            Assert.AreEqual(40.0, settings.CommonGamingGlowSize);
            Assert.AreEqual(12.0, settings.CommonGamingCornerRadius);
            Assert.IsTrue(settings.HasCustomCommonGamingSettings);
        }

        [TestMethod]
        public void GlowManager_GamingPriority_CustomGlowOn_UsesGameSpecificOverrides()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.GamingModeEnabled = true;
            settings.GlowDuringGames = true;
            settings.ReduceDurationInGames = false;
            settings.ReduceIntensityInGames = false;
            settings.CommonGamingDurationMs = 3000;
            settings.CommonGamingIntensity = 0.8;
            settings.CommonGamingGlowSize = 32.0;
            settings.CommonGamingCornerRadius = 0.0;

            string gameExe = "apex_legends.exe";
            settings.SetGameOverride(gameExe, new GameGlowOverride
            {
                CustomGlowEnabled = true,
                DurationMs = 6000,
                Intensity = 0.95,
                GlowSize = 64.0,
                CornerRadius = 24.0,
                ColorHex = "#FF00FF"
            });

            var gameDetection = new GameDetectionService(settingsService);
            gameDetection.ActiveGameIdentifier = gameExe;
            gameDetection.ActiveGameName = gameExe;
            gameDetection.SetSimulatedGame(gameExe);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService, gameDetection);

            var profile = new AppProfile
            {
                AppId = "discord.exe",
                Name = "Discord",
                ColorHex = "#5865F2",
                DurationMs = 4000,
                Intensity = 0.8,
                GlowSize = 32.0,
                CornerRadius = 0.0
            };

            var adjusted = glowManager.AdjustProfileParameters(profile, settings, isTest: false);

            Assert.AreEqual(6000, adjusted.DurationMs);
            Assert.AreEqual(0.95, adjusted.Intensity, 0.001);
            Assert.AreEqual(64.0, adjusted.GlowSize, 0.001);
            Assert.AreEqual(24.0, adjusted.CornerRadius, 0.001);
            Assert.AreEqual("#FF00FF", adjusted.ColorHex);
        }

        [TestMethod]
        public void GlowManager_GamingPriority_CustomGlowOff_UsesGlobalGamingSettings()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Current;
            settings.GamingModeEnabled = true;
            settings.GlowDuringGames = true;
            settings.ReduceDurationInGames = false;
            settings.ReduceIntensityInGames = false;
            settings.CommonGamingDurationMs = 4500;
            settings.CommonGamingIntensity = 0.70;
            settings.CommonGamingGlowSize = 48.0;
            settings.CommonGamingCornerRadius = 16.0;

            string gameExe = "valorant.exe";
            settings.SetGameOverride(gameExe, new GameGlowOverride
            {
                CustomGlowEnabled = false,
                DurationMs = 9000,
                Intensity = 1.0,
                GlowSize = 120.0
            });

            var gameDetection = new GameDetectionService(settingsService);
            gameDetection.ActiveGameIdentifier = gameExe;
            gameDetection.ActiveGameName = gameExe;
            gameDetection.SetSimulatedGame(gameExe);

            var profileService = new ProfileService();
            var glowManager = new GlowManager(settingsService, profileService, gameDetection);

            var profile = new AppProfile
            {
                AppId = "slack.exe",
                Name = "Slack",
                ColorHex = "#4A154B",
                DurationMs = 2000,
                Intensity = 0.5,
                GlowSize = 20.0,
                CornerRadius = 4.0
            };

            var adjusted = glowManager.AdjustProfileParameters(profile, settings, isTest: false);

            Assert.AreEqual(4500, adjusted.DurationMs);
            Assert.AreEqual(0.70, adjusted.Intensity, 0.001);
            Assert.AreEqual(48.0, adjusted.GlowSize, 0.001);
            Assert.AreEqual(16.0, adjusted.CornerRadius, 0.001);
        }

        #endregion

        #region Requirement 12 & 13: Gaming Apps Section & De-duplication

        [TestMethod]
        public void ApplicationsView_HasDedicatedGamingSectionContainers()
        {
            var path = Path.Combine(SolutionDir, "UI", "Views", "ApplicationsView.xaml");
            var xaml = File.ReadAllText(path);

            StringAssert.Contains(xaml, "x:Name=\"SectionGamingHeader\"");
            StringAssert.Contains(xaml, "x:Name=\"PnlGamingCards\"");
            StringAssert.Contains(xaml, "x:Name=\"SectionGamingGridHeader\"");
            StringAssert.Contains(xaml, "x:Name=\"PnlGamingGridCards\"");
        }

        #endregion

        #region Dialog Rounded Windows and Button Styles

        [TestMethod]
        public void DialogWindows_HaveRoundedCornerContainer()
        {
            var confirmXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Windows", "ConfirmationDialogWindow.xaml"));
            StringAssert.Contains(confirmXaml, "CornerRadius=\"14\"");

            var updateXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Windows", "UpdateConfirmDialogWindow.xaml"));
            StringAssert.Contains(updateXaml, "CornerRadius=\"14\"");

            var errorXaml = File.ReadAllText(Path.Combine(SolutionDir, "UI", "Windows", "ErrorDialogWindow.xaml"));
            StringAssert.Contains(errorXaml, "CornerRadius=\"14\"");
        }

        [TestMethod]
        public void AppXaml_DefinesHeroDialogButtonStyles()
        {
            var appXaml = File.ReadAllText(Path.Combine(SolutionDir, "App.xaml"));

            StringAssert.Contains(appXaml, "x:Key=\"HeroPrimaryButtonStyle\"");
            StringAssert.Contains(appXaml, "x:Key=\"HeroSecondaryButtonStyle\"");
            StringAssert.Contains(appXaml, "x:Key=\"HeroCloseButtonStyle\"");
        }

        #endregion
    
        #region Item 4, 5, 6, 8 Runtime Audit Tests

        [TestMethod]
        public void TrackedApps_RuntimeVerification_FiveApps_NoDiscordBug_GridListToggle_NoDuplicates()
        {
            Exception? threadEx = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    var profiles = new List<AppProfile>
                    {
                        new AppProfile { AppId = "chrome.exe", Name = "Google Chrome", Enabled = true },
                        new AppProfile { AppId = "spotify.exe", Name = "Spotify", Enabled = true },
                        new AppProfile { AppId = "whatsapp.exe", Name = "WhatsApp", Enabled = true },
                        new AppProfile { AppId = "code.exe", Name = "Visual Studio Code", Enabled = true },
                        new AppProfile { AppId = "telegram.exe", Name = "Telegram", Enabled = true },
                        new AppProfile { AppId = "discord.exe", Name = "Discord", Enabled = true }
                    };

                    // 1. Verify AppProfileCard (List View) for all 5+ apps
                    var listCards = new List<NotiGlow.UI.Controls.AppProfileCard>();
                    foreach (var p in profiles)
                    {
                        var card = new NotiGlow.UI.Controls.AppProfileCard();
                        card.SetProfile(p);
                        listCards.Add(card);

                        Assert.AreEqual(p.Name, card.TxtAppName.Text, $"List card for {p.AppId} must display {p.Name}.");
                        if (p.AppId != "discord.exe")
                        {
                            Assert.AreNotEqual("Discord", card.TxtAppName.Text,
                                $"List card for non-Discord app '{p.Name}' must never say 'Discord'.");
                        }
                    }

                    // 2. Verify AppProfileGridCard (Grid View) for all 5+ apps
                    var gridCards = new List<NotiGlow.UI.Controls.AppProfileGridCard>();
                    foreach (var p in profiles)
                    {
                        var card = new NotiGlow.UI.Controls.AppProfileGridCard();
                        card.SetProfile(p);
                        gridCards.Add(card);

                        Assert.AreEqual(p.Name, card.TxtAppName.Text, $"Grid card for {p.AppId} must display {p.Name}.");
                        if (p.AppId != "discord.exe")
                        {
                            Assert.AreNotEqual("Discord", card.TxtAppName.Text,
                                $"Grid card for non-Discord app '{p.Name}' must never say 'Discord'.");
                        }
                    }

                    // 3. Verify ApplicationsView Grid -> List -> Grid toggling
                    string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_Audit_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempDir);
                    try
                    {
                        var settingsFile = Path.Combine(tempDir, "settings.json");
                        var settingsService = new SettingsService(settingsFile);
                        var settings = settingsService.Current;
                        settings.TrackedGames = new List<string> { "cyberpunk2077.exe" };
                        settingsService.Save(settings);

                        var profileService = new ProfileService();
                        foreach (var p in profiles)
                        {
                            profileService.AddOrUpdateProfile(p);
                        }

                        var glowManager = new GlowManager(settingsService, profileService);
                        var appsView = new NotiGlow.UI.Views.ApplicationsView();
                        appsView.Initialize(profileService, glowManager, settingsService);

                        // Grid View
                        appsView.ApplyViewMode(ApplicationViewMode.Grid);
                        Assert.IsTrue(appsView.PnlAppGridCards.Children.Count >= 6, "Grid mode must contain at least 6 normal apps.");
                        Assert.AreEqual(1, appsView.PnlGamingGridCards.Children.Count, "Gaming grid must contain 1 game.");

                        // List View
                        appsView.ApplyViewMode(ApplicationViewMode.List);
                        Assert.IsTrue(appsView.PnlAppCards.Children.Count >= 6, "List mode must contain at least 6 normal apps.");
                        Assert.AreEqual(1, appsView.PnlGamingCards.Children.Count, "Gaming list must contain 1 game.");

                        // Verify names in List view
                        foreach (var child in appsView.PnlAppCards.Children)
                        {
                            if (child is NotiGlow.UI.Controls.AppProfileCard apc)
                            {
                                Assert.IsNotNull(apc.Profile);
                                Assert.AreEqual(apc.Profile.Name, apc.TxtAppName.Text);
                                if (apc.Profile.AppId != "discord.exe")
                                {
                                    Assert.AreNotEqual("Discord", apc.TxtAppName.Text);
                                }
                            }
                        }

                        // Back to Grid View (Grid -> List -> Grid)
                        appsView.ApplyViewMode(ApplicationViewMode.Grid);
                        foreach (var child in appsView.PnlAppGridCards.Children)
                        {
                            if (child is NotiGlow.UI.Controls.AppProfileGridCard gc)
                            {
                                Assert.IsNotNull(gc.Profile);
                                Assert.AreEqual(gc.Profile.Name, gc.TxtAppName.Text);
                                if (gc.Profile.AppId != "discord.exe")
                                {
                                    Assert.AreNotEqual("Discord", gc.TxtAppName.Text);
                                }
                            }
                        }

                        // Verify No Duplicate: Cyberpunk 2077 must NOT appear in normal apps
                        foreach (var child in appsView.PnlAppCards.Children)
                        {
                            if (child is NotiGlow.UI.Controls.AppProfileCard apc)
                            {
                                Assert.AreNotEqual("cyberpunk2077.exe", apc.Profile?.AppId);
                            }
                        }
                    }
                    finally
                    {
                        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (threadEx != null)
            {
                Assert.Fail($"STA Thread Exception: {threadEx.Message}\n{threadEx.StackTrace}");
            }
        }

        [TestMethod]
        public void Defaults_RuntimeVerification_NewSettingsConfig_AndExistingPreserved()
        {
            // 1. New default settings
            var newSettings = new AppSettings();
            Assert.AreEqual("#FFFFFF", newSettings.DefaultColorHex, "Default color must be #FFFFFF.");
            Assert.AreEqual(32.0, newSettings.DefaultGlowSize, "Default blur size must be 32px.");
            Assert.AreEqual(0.0, newSettings.CornerRadius, "Default corner rounding must be 0px.");
            Assert.AreEqual(0.80, newSettings.DefaultIntensity, 0.001, "Default intensity must be 80%.");

            // 2. Existing user configs must NOT be overwritten
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_Defaults_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsPath = Path.Combine(tempDir, "settings.json");
                string existingJson = @"
                {
                    ""DefaultColorHex"": ""#00FF00"",
                    ""DefaultGlowSize"": 45.0,
                    ""CornerRadius"": 18.0,
                    ""DefaultIntensity"": 0.65,
                    ""ThemeMode"": 1
                }";
                File.WriteAllText(settingsPath, existingJson);

                var loaded = new SettingsService(settingsPath);
                Assert.AreEqual("#00FF00", loaded.Current.DefaultColorHex, "Existing user color must be preserved.");
                Assert.AreEqual(45.0, loaded.Current.DefaultGlowSize, "Existing user blur size must be preserved.");
                Assert.AreEqual(18.0, loaded.Current.CornerRadius, "Existing user corner radius must be preserved.");
                Assert.AreEqual(0.65, loaded.Current.DefaultIntensity, 0.001, "Existing user intensity must be preserved.");
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void GamingSettings_RuntimeVerification_CustomGlowToggle_Persistence()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_Gaming_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string settingsPath = Path.Combine(tempDir, "settings.json");
                var service = new SettingsService(settingsPath);
                var settings = service.Current;

                // 1. Set global gaming settings
                settings.SetCommonGamingSettings(3200, 0.85, 48.0, 14.0);
                settings.TrackedGames.Add("rdr2.exe");
                settings.TrackedGames.Add("witcher3.exe");

                // rdr2: Custom Glow ON
                settings.SetGameOverride("rdr2.exe", new GameGlowOverride
                {
                    CustomGlowEnabled = true,
                    DurationMs = 5500,
                    Intensity = 0.95,
                    GlowSize = 80.0,
                    CornerRadius = 20.0
                });

                // witcher3: Custom Glow OFF
                settings.SetGameOverride("witcher3.exe", new GameGlowOverride
                {
                    CustomGlowEnabled = false,
                    DurationMs = 7000
                });

                service.Save(settings);

                // Simulate app restart by creating new SettingsService
                var reloadedService = new SettingsService(settingsPath);
                var reloaded = reloadedService.Current;

                Assert.AreEqual(3200, reloaded.CommonGamingDurationMs);
                Assert.AreEqual(0.85, reloaded.CommonGamingIntensity);
                Assert.AreEqual(48.0, reloaded.CommonGamingGlowSize);
                Assert.AreEqual(14.0, reloaded.CommonGamingCornerRadius);

                var rdr2Ov = reloaded.GetGameOverride("rdr2.exe");
                Assert.IsNotNull(rdr2Ov);
                Assert.IsTrue(rdr2Ov.CustomGlowEnabled);
                Assert.AreEqual(5500, rdr2Ov.DurationMs);
                Assert.AreEqual(0.95, rdr2Ov.Intensity);

                var witcherOv = reloaded.GetGameOverride("witcher3.exe");
                Assert.IsNotNull(witcherOv);
                Assert.IsFalse(witcherOv.CustomGlowEnabled);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        #endregion

    }
}
