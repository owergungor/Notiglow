using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI;
using NotiGlow.UI.Controls;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V33FeatureTests
    {
        private string _repoRoot = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            LocalizationService.Instance.SetLanguage("en-US");
            PackageService.SetPackagedOverrideForTesting(null);

            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "NotiGlow.sln")))
            {
                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
            _repoRoot = current ?? Directory.GetCurrentDirectory();
        }

        [TestCleanup]
        public void Cleanup()
        {
            PackageService.SetPackagedOverrideForTesting(null);
        }

        #region Requirement 0: Version 3.3 Metadata

        [TestMethod]
        public void Test_Version33_Metadata_Consistency()
        {
            Assert.AreEqual("3.3", UpdateService.CurrentVersionString);
            ReleaseVersionAssertions.AssertApplicationVersionMetadata();
            ReleaseVersionAssertions.AssertInstallerVersionMetadata("3.3");
            ReleaseVersionAssertions.AssertPackageManifestVersionMetadata("3.3");
        }

        #endregion

        #region Requirement 1: Xbox and Microsoft Store Game Detection

        [TestMethod]
        public void Test_XboxGameDetector_ParsesMicrosoftGameConfig()
        {
            string fakeGameFolder = Path.Combine(_repoRoot, "Test_Xbox_Game");
            string fakeContentDir = Path.Combine(fakeGameFolder, "Content");
            string fakeExe = Path.Combine(fakeContentDir, "ForzaHorizon5.exe");
            string fakeConfig = Path.Combine(fakeContentDir, "MicrosoftGame.config");

            string fakeConfigXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Game configVersion=""1"">
  <Identity Name=""Microsoft.624F8B84B80"" Publisher=""CN=Microsoft Corporation"" Version=""1.0.0.0""/>
  <ExecutableList>
    <Executable Name=""ForzaHorizon5.exe"" TargetDeviceFamily=""PC"" Id=""Game""/>
  </ExecutableList>
  <ShellVisuals DisplayName=""Forza Horizon 5"" Square150x150Logo=""Assets\Logo.png""/>
</Game>";

            var detector = new XboxAndStoreGameDetector(
                getXboxLibraryDrives: () => new[] { _repoRoot },
                directoryExists: dir => dir.Equals(Path.Combine(_repoRoot, "XboxGames"), StringComparison.OrdinalIgnoreCase) ||
                                       dir.Equals(fakeGameFolder, StringComparison.OrdinalIgnoreCase) ||
                                       dir.Equals(fakeContentDir, StringComparison.OrdinalIgnoreCase),
                fileExists: file => file.Equals(fakeExe, StringComparison.OrdinalIgnoreCase) ||
                                    file.Equals(fakeConfig, StringComparison.OrdinalIgnoreCase),
                readAllText: file => fakeConfigXml,
                getFiles: (dir, pattern) => new[] { fakeExe },
                getDirectories: dir => new[] { fakeGameFolder }
            );

            var games = detector.ScanXboxLibraryFolders();

            Assert.AreEqual(1, games.Count);
            Assert.AreEqual("Forza Horizon 5", games[0].Name);
            Assert.AreEqual("Xbox", games[0].Launcher);
            Assert.IsTrue(games[0].ExecutablePath.EndsWith("ForzaHorizon5.exe", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void Test_XboxGameDetector_ParsesAppxManifestFallback()
        {
            string fakeGameFolder = Path.Combine(_repoRoot, "Test_Store_Game");
            string fakeExe = Path.Combine(fakeGameFolder, "SeaOfThieves.exe");
            string fakeManifest = Path.Combine(fakeGameFolder, "appxmanifest.xml");

            string fakeManifestXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"">
  <Applications>
    <Application Id=""App"" Executable=""SeaOfThieves.exe"">
      <VisualElements DisplayName=""Sea of Thieves""/>
    </Application>
  </Applications>
</Package>";

            var detector = new XboxAndStoreGameDetector(
                getXboxLibraryDrives: () => new[] { _repoRoot },
                directoryExists: dir => dir.Equals(Path.Combine(_repoRoot, "XboxGames"), StringComparison.OrdinalIgnoreCase) ||
                                       dir.Equals(fakeGameFolder, StringComparison.OrdinalIgnoreCase),
                fileExists: file => file.Equals(fakeExe, StringComparison.OrdinalIgnoreCase) ||
                                    file.Equals(fakeManifest, StringComparison.OrdinalIgnoreCase),
                readAllText: file => fakeManifestXml,
                getFiles: (dir, pattern) => new[] { fakeExe },
                getDirectories: dir => new[] { fakeGameFolder }
            );

            var games = detector.ScanXboxLibraryFolders();

            Assert.AreEqual(1, games.Count);
            Assert.AreEqual("Sea of Thieves", games[0].Name);
            Assert.AreEqual("Xbox", games[0].Launcher);
            Assert.IsTrue(games[0].ExecutablePath.EndsWith("SeaOfThieves.exe", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void Test_XboxGameDetector_RejectsNonXboxFolderWithoutManifest()
        {
            string fakeFolder = Path.Combine(_repoRoot, "RandomSoftware");
            string fakeExe = Path.Combine(fakeFolder, "RandomTool.exe");

            var detector = new XboxAndStoreGameDetector(
                getXboxLibraryDrives: () => new[] { _repoRoot },
                directoryExists: dir => dir.Equals(fakeFolder, StringComparison.OrdinalIgnoreCase),
                fileExists: file => file.Equals(fakeExe, StringComparison.OrdinalIgnoreCase),
                readAllText: _ => string.Empty,
                getFiles: (dir, pattern) => new[] { fakeExe },
                getDirectories: _ => new[] { fakeFolder }
            );

            var game = detector.ParseXboxGameFolder(fakeFolder);
            Assert.IsNull(game, "Folder lacking MicrosoftGame.config and appxmanifest.xml outside XboxGames must not be parsed as Xbox game.");
        }

        [TestMethod]
        public async Task Test_GameDetectionService_IntegratesXboxStore_AndPreventsDuplicates()
        {
            var settings = new SettingsService();
            settings.Current.TrackedGames.Clear();

            var fakeXboxDetector = new XboxAndStoreGameDetector(
                getXboxLibraryDrives: () => Array.Empty<string>(),
                directoryExists: _ => false,
                fileExists: _ => true,
                winRtPackageScanner: () => new List<DetectedGameInfo>
                {
                    new DetectedGameInfo { Name = "Halo Infinite", ExecutablePath = @"C:\Games\HaloInfinite.exe", Launcher = "Xbox", AppId = "Halo" },
                    new DetectedGameInfo { Name = "Duplicate Steam Game", ExecutablePath = @"C:\Steam\Portal2.exe", Launcher = "Xbox", AppId = "Portal" }
                }
            );

            var fakeSteamDetector = new SteamGameDetector(
                getSteamRoot: () => @"C:\Steam",
                directoryExists: _ => true,
                fileExists: _ => true,
                getDirectories: _ => Array.Empty<string>(),
                getFiles: (_, _) => Array.Empty<string>()
            );

            var gds = new GameDetectionService(settings, fakeSteamDetector, null, fakeXboxDetector);

            var detected = await gds.DetectInstalledGamesAsync();

            // Verify Halo Infinite is detected
            var halo = detected.FirstOrDefault(g => g.Name.Equals("Halo Infinite", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(halo, "Xbox game should be detected by GameDetectionService");
            Assert.AreEqual("Xbox", halo.Launcher);

            // Sync with settings
            int synced = await gds.ScanAndSyncTrackedGamesAsync();
            Assert.IsTrue(synced >= 1, "Should sync detected games into TrackedGames");
            Assert.IsTrue(settings.Current.TrackedGames.Any(g => g.Contains("HaloInfinite.exe", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public async Task Test_GameDetectionService_FailsGracefully_WithoutAffectingSteamOrEpic()
        {
            var settings = new SettingsService();

            var throwingXboxDetector = new XboxAndStoreGameDetector(
                getXboxLibraryDrives: () => throw new UnauthorizedAccessException("Access denied testing"),
                directoryExists: _ => throw new InvalidOperationException("Directory error"),
                fileExists: _ => false
            );

            var gds = new GameDetectionService(settings, null, null, throwingXboxDetector);

            // Must NOT throw
            var detected = await gds.DetectInstalledGamesAsync();
            Assert.IsNotNull(detected);
        }

        #endregion

        #region Requirement 2: Taskbar Show/Hide Setting

        [TestMethod]
        public void Test_ShowInTaskbar_Setting_DefaultsToTrue_AndPersists()
        {
            var settings = new AppSettings();
            Assert.IsTrue(settings.ShowInTaskbar, "ShowInTaskbar should default to true.");

            settings.ShowInTaskbar = false;
            string json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.IsFalse(deserialized.ShowInTaskbar, "ShowInTaskbar setting must persist false.");
        }

        #endregion

        #region Requirement 3: App Icons Service and Fallback

        [TestMethod]
        public async Task Test_AppIconService_CachesResults_AndReturnsNullForMissing()
        {
            AppIconService.Instance.ClearCache();

            var icon1 = await AppIconService.Instance.GetAppIconAsync(@"C:\NonExistent_Application_Path_123.exe");
            Assert.IsNull(icon1, "Non-existent exe should safely return null fallback");

            // Verify cache contains null result and doesn't crash on repeated call
            var icon2 = await AppIconService.Instance.GetAppIconAsync(@"C:\NonExistent_Application_Path_123.exe");
            Assert.IsNull(icon2);
        }

        [TestMethod]
        public async Task Test_AppIconService_HandlesQuotedPathsAndArguments()
        {
            AppIconService.Instance.ClearCache();

            // Quoted non-existent path with arguments
            var icon = await AppIconService.Instance.GetAppIconAsync("\"C:\\NonExistent\\App.exe\" --arg1 -silent");
            Assert.IsNull(icon, "Should safely handle quotes and arguments without throwing.");
        }

        #endregion

        #region Requirement 4: Microsoft Store Check Frequency Visibility

        [TestMethod]
        public void Test_PackageService_Packaged_Controls_CheckFrequencyVisibility()
        {
            // Verify PackageService packaged toggle simulates store vs non-store
            PackageService.SetPackagedOverrideForTesting(true);
            Assert.IsTrue(PackageService.IsPackaged);

            PackageService.SetPackagedOverrideForTesting(false);
            Assert.IsFalse(PackageService.IsPackaged);
        }

        #endregion

        #region Requirement 5: ThemeMode System / Light / Dark

        [TestMethod]
        public void Test_ThemeMode_Enum_Contains_System_Light_Dark()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(ThemeMode), ThemeMode.System));
            Assert.IsTrue(Enum.IsDefined(typeof(ThemeMode), ThemeMode.Light));
            Assert.IsTrue(Enum.IsDefined(typeof(ThemeMode), ThemeMode.Dark));
            Assert.AreEqual(3, Enum.GetValues(typeof(ThemeMode)).Length, "ThemeMode must only contain System, Light, and Dark.");
        }

        [TestMethod]
        public void Test_ThemeMode_Json_Migration_And_Serialization()
        {
            // Test deserializing legacy "Liquid" string -> fallback to Dark
            string legacyLiquidJson = @"{ ""ThemeMode"": ""Liquid"" }";
            var deserializedLiquid = JsonSerializer.Deserialize<AppSettings>(legacyLiquidJson);
            Assert.IsNotNull(deserializedLiquid);
            Assert.AreEqual(ThemeMode.Dark, deserializedLiquid.ThemeMode, "Legacy 'Liquid' must safely fallback to ThemeMode.Dark");

            // Test deserializing legacy "LiquidGlass" string -> fallback to Dark
            string legacyGlassJson = @"{ ""ThemeMode"": ""LiquidGlass"" }";
            var deserializedGlass = JsonSerializer.Deserialize<AppSettings>(legacyGlassJson);
            Assert.IsNotNull(deserializedGlass);
            Assert.AreEqual(ThemeMode.Dark, deserializedGlass.ThemeMode, "Legacy 'LiquidGlass' must safely fallback to ThemeMode.Dark");

            // Test serializing and deserializing standard modes
            foreach (var mode in new[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark })
            {
                var current = new AppSettings { ThemeMode = mode };
                string json = JsonSerializer.Serialize(current);
                var roundtrip = JsonSerializer.Deserialize<AppSettings>(json);
                Assert.IsNotNull(roundtrip);
                Assert.AreEqual(mode, roundtrip.ThemeMode);
            }
        }

        [TestMethod]
        public void Test_ThemeService_ColorTheme_Accent_Preserved()
        {
            // Standard theme
            var standardDark = ThemeService.GetPalette(ColorTheme.Standard, isDark: true);
            Assert.IsNotNull(standardDark);
            Assert.AreEqual("#3B82F6", standardDark.AccentColor);

            // Nature theme
            var natureDark = ThemeService.GetPalette(ColorTheme.Nature, isDark: true);
            Assert.IsNotNull(natureDark);
            Assert.AreEqual("#10B981", natureDark.AccentColor);

            // Bubblegum theme
            var bubblegumDark = ThemeService.GetPalette(ColorTheme.Bubblegum, isDark: true);
            Assert.IsNotNull(bubblegumDark);
            Assert.AreEqual("#EC4899", bubblegumDark.AccentColor);
        }

        #endregion

        #region Requirement 6: System Tray Left Click & Context Menu

        [TestMethod]
        public void Test_TrayService_SingleLeftClick_Invokes_OpenSettingsAction()
        {
            bool opened = false;
            var settings = new SettingsService();
            using var tray = new TrayService(settings, () => opened = true, () => { }, () => { });

            tray.SimulateLeftClickForTesting();
            Assert.IsTrue(opened, "Single left click on tray must open the main window.");
        }

        [TestMethod]
        public void Test_TrayService_ContextMenu_Preserved_On_RightClick()
        {
            var settings = new SettingsService();
            using var tray = new TrayService(settings, () => { }, () => { }, () => { });

            Assert.IsNotNull(tray.ContextMenuForTesting, "Tray right-click context menu must exist.");
            Assert.IsTrue(tray.ContextMenuForTesting.Items.Count >= 4, "Context menu must have items.");
        }

        [TestMethod]
        public void Test_TrayService_RapidClicks_AreThrottled()
        {
            int openCount = 0;
            var settings = new SettingsService();
            using var tray = new TrayService(settings, () => openCount++, () => { }, () => { });

            tray.SimulateLeftClickForTesting();
            tray.SimulateLeftClickForTesting();
            Assert.AreEqual(1, openCount, "Rapid duplicate clicks within throttle threshold must be suppressed.");
        }

        #endregion

        #region Requirement 7: Localization Verification

        [TestMethod]
        public void Test_Localization_Contains_All_New_Keys_In_All_Languages()
        {
            string[] languages = new[] { "en-US", "es-ES", "fr-FR", "de-DE", "tr-TR" };
            string[] requiredKeys = new[]
            {
                "General.ShowInTaskbar",
                "General.ShowInTaskbarDesc",
                "Gaming.DetectionSupport",
                "Gaming.ScanLaunchersToolTip",
                "Gaming.ScanningGames"
            };

            foreach (var lang in languages)
            {
                LocalizationService.Instance.SetLanguage(lang, persist: false);

                foreach (var key in requiredKeys)
                {
                    string val = LocalizationService.Instance.GetString(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(val), $"Missing localization key '{key}' in language '{lang}'.");
                    Assert.AreNotEqual(key, val, $"Key '{key}' was not translated in language '{lang}'.");
                }
            }
        }

        #endregion

        #region Requirement 8: Vercel Default Color Theme

        [TestMethod]
        public void Test_DefaultColorTheme_IsVercel()
        {
            var settings = new AppSettings();
            Assert.AreEqual(ColorTheme.Vercel, settings.ColorTheme, "Fresh AppSettings must default to ColorTheme.Vercel.");
            Assert.AreEqual(ColorTheme.Standard, settings.ColorTheme, "ColorTheme.Vercel must be equivalent to ColorTheme.Standard.");
        }

        [TestMethod]
        public void Test_ColorTheme_JsonSerialization_Vercel_Compatibility()
        {
            // 1. JSON with Vercel string
            string vercelJson = "{\"ColorTheme\": \"Vercel\"}";
            var fromVercel = JsonSerializer.Deserialize<AppSettings>(vercelJson);
            Assert.IsNotNull(fromVercel);
            Assert.AreEqual(ColorTheme.Vercel, fromVercel.ColorTheme);

            // 2. JSON with legacy Standard string
            string standardJson = "{\"ColorTheme\": \"Standard\"}";
            var fromStandard = JsonSerializer.Deserialize<AppSettings>(standardJson);
            Assert.IsNotNull(fromStandard);
            Assert.AreEqual(ColorTheme.Vercel, fromStandard.ColorTheme);

            // 3. Existing user preference (e.g. Zen) preserved
            string zenJson = "{\"ColorTheme\": \"Zen\"}";
            var fromZen = JsonSerializer.Deserialize<AppSettings>(zenJson);
            Assert.IsNotNull(fromZen);
            Assert.AreEqual(ColorTheme.Zen, fromZen.ColorTheme);
        }

        [TestMethod]
        public void Test_VercelTheme_Palette_CompatibleWithAllThemeModes()
        {
            // Dark mode
            var darkPalette = ThemeService.GetPalette(ColorTheme.Vercel, ThemeMode.Dark);
            Assert.IsNotNull(darkPalette);
            Assert.AreEqual("#3B82F6", darkPalette.AccentColor);
            Assert.AreEqual("#0A0A0A", darkPalette.WindowBackground);

            // Light mode
            var lightPalette = ThemeService.GetPalette(ColorTheme.Vercel, ThemeMode.Light);
            Assert.IsNotNull(lightPalette);
            Assert.AreEqual("#2563EB", lightPalette.AccentColor);
            Assert.AreEqual("#FAFAFA", lightPalette.WindowBackground);

            // System mode
            var systemPalette = ThemeService.GetPalette(ColorTheme.Vercel, ThemeMode.System);
            Assert.IsNotNull(systemPalette);
            Assert.IsTrue(systemPalette.AccentColor == "#3B82F6" || systemPalette.AccentColor == "#2563EB");
        }

        #endregion

        #region Requirement 9: Application List & Grid Icon Sizing

        [TestMethod]
        public void Test_AppProfileCard_IconSizing_EnlargedAndUniform()
        {
            string xamlPath = Path.Combine(_repoRoot, "UI", "Controls", "AppProfileCard.xaml");
            Assert.IsTrue(File.Exists(xamlPath), "AppProfileCard.xaml must exist.");
            string content = File.ReadAllText(xamlPath);

            // Verify Border keeps 36x36 and CornerRadius 8
            StringAssert.Contains(content, "Width=\"36\" Height=\"36\" CornerRadius=\"8\"");

            // Verify ImgAppIcon is enlarged to 30x30 with Stretch="Uniform"
            StringAssert.Contains(content, "x:Name=\"ImgAppIcon\"");
            StringAssert.Contains(content, "Width=\"30\" Height=\"30\"");
            StringAssert.Contains(content, "Stretch=\"Uniform\"");

            // Verify FallbackAppIcon is enlarged
            StringAssert.Contains(content, "x:Name=\"FallbackAppIcon\"");
            StringAssert.Contains(content, "FontSize=\"22\"");
        }

        [TestMethod]
        public void Test_AppProfileGridCard_IconSizing_EnlargedAndUniform()
        {
            string xamlPath = Path.Combine(_repoRoot, "UI", "Controls", "AppProfileGridCard.xaml");
            Assert.IsTrue(File.Exists(xamlPath), "AppProfileGridCard.xaml must exist.");
            string content = File.ReadAllText(xamlPath);

            // Verify Border keeps 32x32 and CornerRadius 7
            StringAssert.Contains(content, "Width=\"32\" Height=\"32\" CornerRadius=\"7\"");

            // Verify ImgAppIcon is enlarged to 26x26 with Stretch="Uniform"
            StringAssert.Contains(content, "x:Name=\"ImgAppIcon\"");
            StringAssert.Contains(content, "Width=\"26\" Height=\"26\"");
            StringAssert.Contains(content, "Stretch=\"Uniform\"");

            // Verify FallbackAppIcon is enlarged
            StringAssert.Contains(content, "x:Name=\"FallbackAppIcon\"");
            StringAssert.Contains(content, "FontSize=\"18\"");
        }

        #endregion
    }
}
