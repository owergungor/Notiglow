using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V19FeatureTests
    {
        #region 1. Steam Detection Tests

        [TestMethod]
        public void SteamGameDetector_FindLibraryFolders_ParsesMultiLibraryVdfCorrectly()
        {
            string steamRoot = @"C:\Steam";
            string secondaryLib = @"D:\SteamLibrary";

            string vdfContent = @"
""libraryfolders""
{
	""0""
	{
		""path""		""C:\\Steam""
		""label""		""""
	}
	""1""
	{
		""path""		""D:\\SteamLibrary""
		""label""		""SSD""
	}
}";

            var detector = new SteamGameDetector(
                getSteamRoot: () => steamRoot,
                directoryExists: dir => true,
                fileExists: path => path.EndsWith("libraryfolders.vdf", StringComparison.OrdinalIgnoreCase),
                readAllText: path => vdfContent
            );

            var libraries = detector.FindLibraryFolders(steamRoot);

            Assert.AreEqual(2, libraries.Count);
            Assert.IsTrue(libraries.Any(l => l.Equals(steamRoot, StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(libraries.Any(l => l.Equals(secondaryLib, StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void SteamGameDetector_ParseAppManifest_ResolvesNameAndExecutable()
        {
            string acfContent = @"
""AppState""
{
	""appid""		""730""
	""Universe""		""1""
	""name""		""Counter-Strike 2""
	""installdir""		""Counter-Strike Global Offensive""
	""StateFlags""		""4""
}";

            string simulatedGameDir = @"C:\Steam\steamapps\common\Counter-Strike Global Offensive";
            string simulatedExe = @"C:\Steam\steamapps\common\Counter-Strike Global Offensive\cs2.exe";

            var detector = new SteamGameDetector(
                getSteamRoot: () => @"C:\Steam",
                directoryExists: dir => true,
                fileExists: path => true,
                readAllText: path => acfContent,
                getFiles: (dir, pattern) => dir.Equals(simulatedGameDir, StringComparison.OrdinalIgnoreCase)
                    ? new[] { simulatedExe }
                    : Array.Empty<string>(),
                getDirectories: dir => Array.Empty<string>()
            );

            var game = detector.ParseAppManifest(@"C:\Steam\steamapps\appmanifest_730.acf", @"C:\Steam\steamapps");

            Assert.IsNotNull(game);
            Assert.AreEqual("Counter-Strike 2", game.Name);
            Assert.AreEqual("Steam", game.Launcher);
            Assert.AreEqual("730", game.AppId);
            Assert.AreEqual(Path.GetFullPath(simulatedExe), game.ExecutablePath);
        }

        [TestMethod]
        public void SteamGameDetector_FiltersNonGameRuntimes_AndUtilityExes()
        {
            string redistAcf = @"
""AppState""
{
	""appid""		""228980""
	""name""		""Steamworks Common Redistributables""
	""installdir""		""Steamworks Shared""
}";

            var detector = new SteamGameDetector(
                directoryExists: dir => true,
                fileExists: path => true,
                readAllText: path => redistAcf
            );

            var game = detector.ParseAppManifest(@"C:\Steam\steamapps\appmanifest_228980.acf", @"C:\Steam\steamapps");

            Assert.IsNull(game, "Steamworks common redistributables must be excluded from tracked games.");
        }

        #endregion

        #region 2. Epic Detection Tests

        [TestMethod]
        public void EpicGameDetector_ParseManifestItem_ResolvesJsonItemCorrectly()
        {
            string manifestJson = @"{
                ""FormatVersion"": 0,
                ""AppVersionString"": ""1.0.0"",
                ""AppName"": ""HadesAppId"",
                ""DisplayName"": ""Hades"",
                ""InstallLocation"": ""D:\\EpicGames\\Hades"",
                ""LaunchExecutable"": ""Hades.exe""
            }";

            string expectedExe = @"D:\EpicGames\Hades\Hades.exe";

            var detector = new EpicGameDetector(
                manifestsDirectory: @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests",
                directoryExists: dir => true,
                fileExists: path => path.Equals(expectedExe, StringComparison.OrdinalIgnoreCase),
                readAllText: path => manifestJson,
                getFiles: (dir, pattern) => new[] { @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\hades.item" }
            );

            var games = detector.DetectGames();

            Assert.AreEqual(1, games.Count);
            var hades = games[0];
            Assert.AreEqual("Hades", hades.Name);
            Assert.AreEqual("Epic", hades.Launcher);
            Assert.AreEqual(Path.GetFullPath(expectedExe), hades.ExecutablePath);
        }

        [TestMethod]
        public void EpicGameDetector_MissingExecutable_ReturnsNull()
        {
            string manifestJson = @"{
                ""DisplayName"": ""Deleted Game"",
                ""InstallLocation"": ""D:\\Games\\Deleted"",
                ""LaunchExecutable"": ""NonExistent.exe""
            }";

            var detector = new EpicGameDetector(
                manifestsDirectory: @"C:\Manifests",
                directoryExists: dir => true,
                fileExists: path => false, // Executable missing on disk!
                readAllText: path => manifestJson,
                getFiles: (dir, pattern) => new[] { @"C:\Manifests\deleted.item" }
            );

            var games = detector.DetectGames();

            Assert.AreEqual(0, games.Count, "Non-existent game executables must not be added to tracked list.");
        }

        #endregion

        #region 3. GameDetectionService Integration Tests

        [TestMethod]
        public async Task GameDetectionService_Deduplication_SuppressesDuplicatesAcrossLaunchers()
        {
            // Steam detector finds GTA5
            var steamDetector = new SteamGameDetector(
                getSteamRoot: () => @"C:\Steam",
                directoryExists: dir => true,
                fileExists: path => true,
                readAllText: path => @"
""AppState""
{
	""appid""		""271590""
	""name""		""Grand Theft Auto V""
	""installdir""		""Grand Theft Auto V""
}",
                getFiles: (dir, pattern) => new[] { @"C:\Steam\steamapps\common\Grand Theft Auto V\GTA5.exe" },
                getDirectories: dir => Array.Empty<string>()
            );

            // Epic detector also finds GTA5
            var epicDetector = new EpicGameDetector(
                manifestsDirectory: @"C:\EpicManifests",
                directoryExists: dir => true,
                fileExists: path => true,
                readAllText: path => @"{
                    ""DisplayName"": ""Grand Theft Auto V"",
                    ""InstallLocation"": ""D:\\Epic\\GTAV"",
                    ""LaunchExecutable"": ""GTA5.exe""
                }",
                getFiles: (dir, pattern) => new[] { @"C:\EpicManifests\gtav.item" }
            );

            var service = new GameDetectionService(new SettingsService(), steamDetector, epicDetector);

            var detected = await service.DetectInstalledGamesAsync();

            // Should be deduplicated by executable filename (GTA5.exe)
            Assert.AreEqual(1, detected.Count, "Duplicate game discovered in both Steam and Epic must be deduplicated.");
        }

        [TestMethod]
        public async Task GameDetectionService_IgnoredGames_DoesNotReAddDeletedGame()
        {
            var steamDetector = new SteamGameDetector(
                getSteamRoot: () => @"C:\Steam",
                directoryExists: dir => true,
                fileExists: path => true,
                readAllText: path => @"
""AppState""
{
	""appid""		""1091500""
	""name""		""Cyberpunk 2077""
	""installdir""		""Cyberpunk 2077""
}",
                getFiles: (dir, pattern) => new[] { @"C:\Steam\steamapps\common\Cyberpunk 2077\Cyberpunk2077.exe" },
                getDirectories: dir => Array.Empty<string>()
            );

            var epicDetector = new EpicGameDetector(
                directoryExists: dir => false
            );

            var settingsService = new SettingsService();
            // User previously deleted Cyberpunk2077.exe -> placed in IgnoredGames
            settingsService.Current.TrackedGames.Clear();
            settingsService.Current.IgnoredGames.Add("Cyberpunk2077.exe");

            var service = new GameDetectionService(settingsService, steamDetector, epicDetector);

            int syncedCount = await service.ScanAndSyncTrackedGamesAsync();

            Assert.AreEqual(0, syncedCount, "User-ignored game must not be re-added automatically.");
            Assert.IsFalse(settingsService.Current.TrackedGames.Any(g => g.Contains("Cyberpunk2077.exe")));
        }

        [TestMethod]
        public async Task GameDetectionService_Resilient_DoesNotThrowOnCorruptedManifests()
        {
            var steamDetector = new SteamGameDetector(
                getSteamRoot: () => @"C:\Steam",
                directoryExists: dir => true,
                fileExists: path => true,
                readAllText: path => "CORRUPTED { INVALID [[[[ VDF !!!",
                getFiles: (dir, pattern) => new[] { @"C:\Steam\steamapps\corrupted.acf" },
                getDirectories: dir => Array.Empty<string>()
            );

            var epicDetector = new EpicGameDetector(
                manifestsDirectory: @"C:\EpicManifests",
                directoryExists: dir => true,
                fileExists: path => true,
                readAllText: path => "{ INVALID JSON !!!",
                getFiles: (dir, pattern) => new[] { @"C:\EpicManifests\invalid.item" }
            );

            var service = new GameDetectionService(new SettingsService(), steamDetector, epicDetector);

            // Must execute gracefully without crashing
            var games = await service.DetectInstalledGamesAsync();
            Assert.AreEqual(0, games.Count);
        }

        #endregion
    }
}
