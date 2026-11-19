using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V41GameDetectionRegressionTests
    {
        [TestMethod]
        [Timeout(10000)]
        public void HaloInfinite_SteamManifest_IsDetectedWithCorrectNameAndExecutable()
        {
            string steamRoot = @"C:\Steam";
            string steamappsDir = @"C:\Steam\steamapps";
            string manifestPath = @"C:\Steam\steamapps\appmanifest_1240440.acf";
            string gameDir = @"C:\Steam\steamapps\common\Halo Infinite";
            string exePath = @"C:\Steam\steamapps\common\Halo Infinite\HaloInfinite.exe";

            string acfContent = @"
""AppState""
{
    ""appid""      ""1240440""
    ""Universe""   ""1""
    ""name""       ""Halo Infinite""
    ""StateFlags"" ""4""
    ""installdir"" ""Halo Infinite""
}";

            var detector = new SteamGameDetector(
                getSteamRoot: () => steamRoot,
                directoryExists: d => d.Equals(steamRoot, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(steamappsDir, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(gameDir, StringComparison.OrdinalIgnoreCase),
                fileExists: f => f.Equals(manifestPath, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(exePath, StringComparison.OrdinalIgnoreCase),
                readAllText: f => f.Equals(manifestPath, StringComparison.OrdinalIgnoreCase) ? acfContent : string.Empty,
                getFiles: (dir, pattern) =>
                {
                    if (dir.Equals(steamappsDir, StringComparison.OrdinalIgnoreCase) && pattern == "appmanifest_*.acf")
                        return new[] { manifestPath };
                    if (dir.Equals(gameDir, StringComparison.OrdinalIgnoreCase) && pattern == "*.exe")
                        return new[] { exePath };
                    return Array.Empty<string>();
                },
                getDirectories: _ => Array.Empty<string>()
            );

            var detected = detector.DetectGames();
            Assert.AreEqual(1, detected.Count);
            Assert.AreEqual("Halo Infinite", detected[0].Name);
            Assert.AreEqual("1240440", detected[0].AppId);
            Assert.AreEqual("Steam", detected[0].Launcher);
            Assert.IsTrue(detected[0].ExecutablePath.EndsWith("HaloInfinite.exe", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        [Timeout(10000)]
        public void HaloInfinite_XboxMicrosoftGameConfig_DefaultDisplayNameAndBinaryGamingRoot_AreDetected()
        {
            string driveRoot = @"D:\";
            string gamingRootPath = @"D:\.GamingRoot";
            string customXboxRoot = @"D:\XboxGames";
            string haloFolder = @"D:\XboxGames\Halo Infinite";
            string contentDir = @"D:\XboxGames\Halo Infinite\Content";
            string configPath = @"D:\XboxGames\Halo Infinite\Content\MicrosoftGame.config";
            string subExeDir = @"D:\XboxGames\Halo Infinite\Content\subfolders";
            string haloExe = @"D:\XboxGames\Halo Infinite\Content\subfolders\HaloInfinite.exe";
            string logoPath = @"D:\XboxGames\Halo Infinite\Content\Assets\Square150x150Logo.png";

            // Simulate binary RGBX UTF-16LE .GamingRoot content ("RGBX\x01\0\0\0X\0b\0o\0x\0G\0a\0m\0e\0s\0\0\0")
            string binaryGamingRoot = "RGBX\u0001\0\0\0X\0b\0o\0x\0G\0a\0m\0e\0s\0\0\0";

            string mgcXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Game configVersion=""1"">
  <Identity Name=""Microsoft.254428597CFE2"" Publisher=""CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"" Version=""1.3292.11703.0"" />
  <ExecutableList>
    <Executable Name=""gamelaunchhelper.exe"" Id=""GameHelper"" TargetDeviceFamily=""PC"" />
    <Executable Name=""subfolders\HaloInfinite.exe"" Id=""Game"" TargetDeviceFamily=""PC"" />
  </ExecutableList>
  <ShellVisuals DefaultDisplayName=""Halo Infinite""
                PublisherDisplayName=""Xbox Game Studios""
                Square150x150Logo=""Assets\Square150x150Logo.png""
                Square44x44Logo=""Assets\Square44x44Logo.png"" />
</Game>";

            var detector = new XboxAndStoreGameDetector(
                getXboxLibraryDrives: () => new[] { driveRoot },
                directoryExists: d => d.Equals(customXboxRoot, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(haloFolder, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(contentDir, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(subExeDir, StringComparison.OrdinalIgnoreCase),
                fileExists: f => f.Equals(gamingRootPath, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(configPath, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(haloExe, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(logoPath, StringComparison.OrdinalIgnoreCase),
                readAllText: f =>
                {
                    if (f.Equals(gamingRootPath, StringComparison.OrdinalIgnoreCase)) return binaryGamingRoot;
                    if (f.Equals(configPath, StringComparison.OrdinalIgnoreCase)) return mgcXml;
                    return string.Empty;
                },
                getFiles: (dir, pattern) =>
                {
                    if (dir.Equals(subExeDir, StringComparison.OrdinalIgnoreCase) && pattern == "*.exe")
                        return new[] { haloExe };
                    return Array.Empty<string>();
                },
                getDirectories: dir =>
                {
                    if (dir.Equals(customXboxRoot, StringComparison.OrdinalIgnoreCase))
                        return new[] { haloFolder };
                    if (dir.Equals(contentDir, StringComparison.OrdinalIgnoreCase))
                        return new[] { subExeDir };
                    return Array.Empty<string>();
                },
                winRtPackageScanner: () => Array.Empty<DetectedGameInfo>(),
                registryScanner: () => Array.Empty<DetectedGameInfo>()
            );

            var games = detector.DetectGames();
            Assert.AreEqual(1, games.Count);
            Assert.AreEqual("Halo Infinite", games[0].Name);
            Assert.AreEqual("Xbox", games[0].Launcher);
            Assert.AreEqual("Microsoft.254428597CFE2", games[0].AppId);
            Assert.AreEqual(logoPath, games[0].IconPath);
            Assert.AreEqual(haloExe, games[0].ExecutablePath);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task Portal_And_Portal2_SteamManifests_AreBothDetectedWithoutCollision()
        {
            string steamRoot = @"C:\Steam";
            string steamappsDir = @"C:\Steam\steamapps";
            string portal1Manifest = @"C:\Steam\steamapps\appmanifest_400.acf";
            string portal2Manifest = @"C:\Steam\steamapps\appmanifest_620.acf";
            string hl2Manifest = @"C:\Steam\steamapps\appmanifest_220.acf";

            string portal1Dir = @"C:\Steam\steamapps\common\Portal";
            string portal1Exe = @"C:\Steam\steamapps\common\Portal\hl2.exe";
            string portal1ToolExe = @"C:\Steam\steamapps\common\Portal\bin\bspzip.exe";

            string portal2Dir = @"C:\Steam\steamapps\common\Portal 2";
            string portal2Exe = @"C:\Steam\steamapps\common\Portal 2\portal2.exe";

            string hl2Dir = @"C:\Steam\steamapps\common\Half-Life 2";
            string hl2Exe = @"C:\Steam\steamapps\common\Half-Life 2\hl2.exe";

            string portal1Acf = @"
""AppState""
{
    ""appid""      ""400""
    ""name""       ""Portal""
    ""installdir"" ""Portal""
}";
            string portal2Acf = @"
""AppState""
{
    ""appid""      ""620""
    ""name""       ""Portal 2""
    ""installdir"" ""Portal 2""
}";
            string hl2Acf = @"
""AppState""
{
    ""appid""      ""220""
    ""name""       ""Half-Life 2""
    ""installdir"" ""Half-Life 2""
}";

            var steamDetector = new SteamGameDetector(
                getSteamRoot: () => steamRoot,
                directoryExists: d => d.Equals(steamRoot, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(steamappsDir, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(portal1Dir, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(portal2Dir, StringComparison.OrdinalIgnoreCase) ||
                                      d.Equals(hl2Dir, StringComparison.OrdinalIgnoreCase),
                fileExists: f => f.Equals(portal1Manifest, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(portal2Manifest, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(hl2Manifest, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(portal1Exe, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(portal1ToolExe, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(portal2Exe, StringComparison.OrdinalIgnoreCase) ||
                                 f.Equals(hl2Exe, StringComparison.OrdinalIgnoreCase),
                readAllText: f =>
                {
                    if (f.Equals(portal1Manifest, StringComparison.OrdinalIgnoreCase)) return portal1Acf;
                    if (f.Equals(portal2Manifest, StringComparison.OrdinalIgnoreCase)) return portal2Acf;
                    if (f.Equals(hl2Manifest, StringComparison.OrdinalIgnoreCase)) return hl2Acf;
                    return string.Empty;
                },
                getFiles: (dir, pattern) =>
                {
                    if (dir.Equals(steamappsDir, StringComparison.OrdinalIgnoreCase) && pattern == "appmanifest_*.acf")
                        return new[] { portal1Manifest, portal2Manifest, hl2Manifest };
                    if (dir.Equals(portal1Dir, StringComparison.OrdinalIgnoreCase) && pattern == "*.exe")
                        return new[] { portal1Exe };
                    if (dir.Equals(Path.Combine(portal1Dir, "bin"), StringComparison.OrdinalIgnoreCase) && pattern == "*.exe")
                        return new[] { portal1ToolExe };
                    if (dir.Equals(portal2Dir, StringComparison.OrdinalIgnoreCase) && pattern == "*.exe")
                        return new[] { portal2Exe };
                    if (dir.Equals(hl2Dir, StringComparison.OrdinalIgnoreCase) && pattern == "*.exe")
                        return new[] { hl2Exe };
                    return Array.Empty<string>();
                },
                getDirectories: dir =>
                {
                    if (dir.Equals(portal1Dir, StringComparison.OrdinalIgnoreCase))
                        return new[] { Path.Combine(portal1Dir, "bin") };
                    return Array.Empty<string>();
                }
            );

            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_PortalTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var settingsService = new SettingsService(Path.Combine(tempDir, "settings.json"));
                var profileService = new ProfileService(Path.Combine(tempDir, "profiles.json"));
                var emptyEpicDetector = new EpicGameDetector(directoryExists: _ => false, fileExists: _ => false, getFiles: (_, _) => Array.Empty<string>());
                var gds = new GameDetectionService(settingsService, steamDetector, emptyEpicDetector, XboxAndStoreGameDetector.CreateEmpty(), profileService);

                var detected = await gds.DetectInstalledGamesAsync();

                Assert.AreEqual(3, detected.Count, "Portal (hl2.exe), Portal 2 (portal2.exe), and Half-Life 2 (hl2.exe) must all be detected without dropping shared hl2.exe games.");
                Assert.IsTrue(detected.Any(g => g.Name == "Portal" && g.AppId == "400" && g.ExecutablePath.EndsWith(@"Portal\hl2.exe", StringComparison.OrdinalIgnoreCase)));
                Assert.IsTrue(detected.Any(g => g.Name == "Portal 2" && g.AppId == "620" && g.ExecutablePath.EndsWith(@"Portal 2\portal2.exe", StringComparison.OrdinalIgnoreCase)));
                Assert.IsTrue(detected.Any(g => g.Name == "Half-Life 2" && g.AppId == "220" && g.ExecutablePath.EndsWith(@"Half-Life 2\hl2.exe", StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        [TestMethod]
        [Timeout(10000)]
        public void GameNameNormalization_ResolvesCleanNamesForHaloPortalAndGdkGames()
        {
            Assert.AreEqual("Portal", AppCategoryHelper.ResolveFriendlyGameName(@"C:\Program Files (x86)\Steam\steamapps\common\Portal\hl2.exe"));
            Assert.AreEqual("Portal", AppCategoryHelper.ResolveFriendlyGameName(@"C:\Program Files (x86)\Steam\steamapps\common\Portal\portal.exe"));
            Assert.AreEqual("Portal 2", AppCategoryHelper.ResolveFriendlyGameName(@"C:\Program Files (x86)\Steam\steamapps\common\Portal 2\portal2.exe"));
            Assert.AreEqual("Halo Infinite", AppCategoryHelper.ResolveFriendlyGameName(@"C:\XboxGames\Halo Infinite\Content\HaloInfinite.exe"));
            Assert.AreEqual("Halo Infinite", AppCategoryHelper.ResolveFriendlyGameName(@"C:\Games\HaloInfinite.exe"));
            Assert.AreEqual("Halo Infinite", XboxAndStoreGameDetector.CleanGameDisplayName("Microsoft.254428597CFE2_8wekyb3d8bbwe!Game"));
            Assert.AreEqual("Fortnite", AppCategoryHelper.ResolveFriendlyGameName(@"C:\XboxGames\Fortnite\Content\FortniteGame\Binaries\Win64\GDKLauncher.exe"));
            Assert.AreEqual("F1 24", AppCategoryHelper.ResolveFriendlyGameName(@"C:\Program Files (x86)\Steam\steamapps\common\F1 24\F1_24.exe"));
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CrossLauncherDuplicatePrevention_And_ProfilePreservation_WorkCorrectly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_DedupPreservation_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var settingsService = new SettingsService(Path.Combine(tempDir, "settings.json"));
                var profileService = new ProfileService(Path.Combine(tempDir, "profiles.json"));

                // Pre-configure custom user profile settings for Halo Infinite
                var customHaloProfile = new AppProfile
                {
                    AppId = "HaloInfinite.exe",
                    Name = "Halo Infinite",
                    ExecutablePath = @"C:\Steam\steamapps\common\Halo Infinite\HaloInfinite.exe",
                    Category = AppCategoryHelper.CategoryGaming,
                    ColorHex = "#00E5FF",
                    DurationMs = 5200,
                    Intensity = 0.95,
                    GlowSize = 48.0,
                    Style = GlowStyle.Ripple,
                    IsCustom = true
                };
                profileService.AddOrUpdateProfile(customHaloProfile);

                string steamRoot = @"C:\Steam";
                string steamappsDir = @"C:\Steam\steamapps";
                string haloManifest = @"C:\Steam\steamapps\appmanifest_1240440.acf";
                string haloDir = @"C:\Steam\steamapps\common\Halo Infinite";
                string haloSteamExe = @"C:\Steam\steamapps\common\Halo Infinite\HaloInfinite.exe";
                string haloAcf = @"
""AppState""
{
    ""appid""      ""1240440""
    ""name""       ""Halo Infinite""
    ""installdir"" ""Halo Infinite""
}";

                var steamDetector = new SteamGameDetector(
                    getSteamRoot: () => steamRoot,
                    directoryExists: d => d.Equals(steamRoot, StringComparison.OrdinalIgnoreCase) ||
                                          d.Equals(steamappsDir, StringComparison.OrdinalIgnoreCase) ||
                                          d.Equals(haloDir, StringComparison.OrdinalIgnoreCase),
                    fileExists: f => f.Equals(haloManifest, StringComparison.OrdinalIgnoreCase) ||
                                     f.Equals(haloSteamExe, StringComparison.OrdinalIgnoreCase),
                    readAllText: _ => haloAcf,
                    getFiles: (dir, pattern) =>
                    {
                        if (dir.Equals(steamappsDir, StringComparison.OrdinalIgnoreCase) && pattern == "appmanifest_*.acf")
                            return new[] { haloManifest };
                        if (dir.Equals(haloDir, StringComparison.OrdinalIgnoreCase) && pattern == "*.exe")
                            return new[] { haloSteamExe };
                        return Array.Empty<string>();
                    },
                    getDirectories: _ => Array.Empty<string>()
                );

                // Xbox detector also reports Halo Infinite
                var xboxDetector = new XboxAndStoreGameDetector(
                    getXboxLibraryDrives: () => Array.Empty<string>(),
                    directoryExists: _ => false,
                    fileExists: _ => true,
                    winRtPackageScanner: () => new List<DetectedGameInfo>
                    {
                        new DetectedGameInfo
                        {
                            Name = "Halo Infinite",
                            ExecutablePath = @"C:\XboxGames\Halo Infinite\Content\HaloInfinite.exe",
                            Launcher = "Xbox",
                            AppId = "Microsoft.254428597CFE2"
                        }
                    },
                    registryScanner: () => Array.Empty<DetectedGameInfo>()
                );

                var emptyEpicDetector = new EpicGameDetector(directoryExists: _ => false, fileExists: _ => false, getFiles: (_, _) => Array.Empty<string>());
                var gds = new GameDetectionService(settingsService, steamDetector, emptyEpicDetector, xboxDetector, profileService);

                var detected = await gds.DetectInstalledGamesAsync();
                Assert.AreEqual(1, detected.Count(g => g.Name == "Halo Infinite"), "Same game reported by both Steam and Xbox must be deduplicated into a single card.");

                int added = await gds.ScanAndSyncTrackedGamesAsync(profileService);
                Assert.AreEqual(1, added);

                // Verify user's existing custom profile settings were preserved!
                var preserved = profileService.GetProfile("HaloInfinite.exe", "Halo Infinite");
                Assert.IsNotNull(preserved);
                Assert.AreEqual("Halo Infinite", preserved.Name);
                Assert.AreEqual("#00E5FF", preserved.ColorHex);
                Assert.AreEqual(5200, preserved.DurationMs);
                Assert.AreEqual(0.95, preserved.Intensity, 0.001);
                Assert.AreEqual(48.0, preserved.GlowSize, 0.001);
                Assert.AreEqual(GlowStyle.Ripple, preserved.Style);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
