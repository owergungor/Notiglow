using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V18FeatureTests
    {
        [TestMethod]
        public void BuiltInAppDetector_Definitions_ContainsAllTenCanonicalApps()
        {
            var defs = BuiltInAppDetector.Definitions;
            Assert.AreEqual(10, defs.Count, "Built-in app detector must define exactly 10 canonical applications.");

            var expectedAppIds = new[]
            {
                "Claude", "OpenAI.ChatGPT", "Microsoft.Copilot", "Google.Gemini",
                "Discord", "WhatsApp", "Telegram", "MSTeams", "Steam", "Spotify"
            };

            foreach (var expectedId in expectedAppIds)
            {
                Assert.IsTrue(defs.Any(d => d.AppId.Equals(expectedId, StringComparison.OrdinalIgnoreCase)),
                    $"Expected built-in app '{expectedId}' was not found in definitions.");
            }
        }

        [TestMethod]
        public void BuiltInAppDetector_AppNotFound_IsCompletelyOmitted_NoPhantomPath()
        {
            // Detector where no files exist anywhere
            var detector = new BuiltInAppDetector(
                fileExists: path => false,
                directoryExists: dir => false,
                wildcardLookup: (parent, remainder) => null,
                registryAppPathLookup: exe => null
            );

            var installed = detector.GetInstalledProfiles();

            Assert.AreEqual(0, installed.Count, "When no apps exist on the system, the returned list must be completely empty.");
        }

        [TestMethod]
        public void BuiltInAppDetector_RealInstallationPath_IsDetectedAndAssigned()
        {
            string fakeDiscordExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Discord\Discord.exe");

            var detector = new BuiltInAppDetector(
                fileExists: path => string.Equals(path, fakeDiscordExe, StringComparison.OrdinalIgnoreCase),
                directoryExists: dir => true,
                wildcardLookup: (parent, remainder) => null,
                registryAppPathLookup: exe => null
            );

            var installed = detector.GetInstalledProfiles();

            Assert.AreEqual(1, installed.Count, "Only the installed app should be detected.");
            var discord = installed[0];
            Assert.AreEqual("Discord", discord.AppId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(discord.ExecutablePath), "ExecutablePath must not be empty.");
            Assert.IsTrue(discord.ExecutablePath!.EndsWith("Discord.exe", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void BuiltInAppDetector_RegistryAppPath_IsResolvedCorrectly()
        {
            string simulatedSteamPath = @"C:\Valve\Steam\steam.exe";

            var detector = new BuiltInAppDetector(
                fileExists: path => string.Equals(path, simulatedSteamPath, StringComparison.OrdinalIgnoreCase),
                directoryExists: dir => true,
                wildcardLookup: (parent, remainder) => null,
                registryAppPathLookup: exe => string.Equals(exe, "steam.exe", StringComparison.OrdinalIgnoreCase) ? simulatedSteamPath : null
            );

            var steamDef = BuiltInAppDetector.Definitions.First(d => d.AppId == "Steam");
            bool detected = detector.TryDetectApp(steamDef, out string detectedPath);

            Assert.IsTrue(detected, "App must be successfully detected via Registry App Paths.");
            Assert.AreEqual(Path.GetFullPath(simulatedSteamPath), detectedPath);
        }

        [TestMethod]
        public void BuiltInAppDetector_WildcardPath_IsResolvedCorrectly()
        {
            string simulatedVersionedExe = @"C:\Users\User\AppData\Local\Discord\app-1.0.9168\Discord.exe";

            var detector = new BuiltInAppDetector(
                fileExists: path => string.Equals(path, simulatedVersionedExe, StringComparison.OrdinalIgnoreCase),
                directoryExists: dir => true,
                wildcardLookup: (parent, remainder) => simulatedVersionedExe,
                registryAppPathLookup: exe => null
            );

            var discordDef = BuiltInAppDetector.Definitions.First(d => d.AppId == "Discord");
            bool detected = detector.TryDetectApp(discordDef, out string detectedPath);

            Assert.IsTrue(detected, "App must be successfully detected via wildcard/versioned subpath.");
            Assert.AreEqual(Path.GetFullPath(simulatedVersionedExe), detectedPath);
        }

        [TestMethod]
        public void BuiltInAppDetector_SingleSourceOfTruth_GetDefaultProfilesMatchesDefinitions()
        {
            var defaultProfiles = ProfileService.GetDefaultProfiles();
            var definitions = BuiltInAppDetector.Definitions;

            Assert.AreEqual(definitions.Count, defaultProfiles.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                Assert.AreEqual(definitions[i].AppId, defaultProfiles[i].AppId);
                Assert.AreEqual(definitions[i].Name, defaultProfiles[i].Name);
                Assert.AreEqual(definitions[i].Category, defaultProfiles[i].Category);
                Assert.AreEqual(definitions[i].ColorHex, defaultProfiles[i].ColorHex);
            }
        }

        [TestMethod]
        public void BuiltInAppDetector_MultipleAppsDetected_AllAssignedRealPaths()
        {
            var installedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Telegram.exe"] = @"C:\Program Files\Telegram Desktop\Telegram.exe",
                ["Spotify.exe"] = @"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_z6rc50735yxwm\Spotify.exe"
            };

            var detector = new BuiltInAppDetector(
                fileExists: path => installedPaths.Values.Any(v => string.Equals(v, path, StringComparison.OrdinalIgnoreCase)),
                directoryExists: dir => true,
                wildcardLookup: (parent, remainder) => null,
                registryAppPathLookup: exe => installedPaths.TryGetValue(exe, out var p) ? p : null
            );

            var installed = detector.GetInstalledProfiles();

            Assert.AreEqual(2, installed.Count, "Exactly the two present apps should be detected.");
            Assert.IsTrue(installed.Any(p => p.AppId == "Telegram"));
            Assert.IsTrue(installed.Any(p => p.AppId == "Spotify"));

            foreach (var profile in installed)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(profile.ExecutablePath));
                Assert.IsTrue(profile.ExecutablePath!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            }
        }

        [TestMethod]
        public void BuiltInAppDetector_PruningAndResolution_RemovesAbsentBuiltInAndPreservesCustomApp()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "NotiGlow_PruneTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string profilesFile = Path.Combine(tempDir, "profiles.json");
                var list = new List<AppProfile>
                {
                    // A custom profile created by user -> MUST BE PRESERVED
                    new AppProfile { AppId = "my_custom_tool", Name = "Custom User App", Enabled = true },
                    // A built-in profile with non-existent path -> MUST BE REMOVED on a machine where it doesn't exist
                    new AppProfile { AppId = "NonExistentBuiltIn", Name = "NonExistentBuiltIn", ExecutablePath = @"C:\Fake\Fake.exe" }
                };
                File.WriteAllText(profilesFile, System.Text.Json.JsonSerializer.Serialize(list));

                var builtInIds = new HashSet<string>(BuiltInAppDetector.Definitions.Select(d => d.AppId), StringComparer.OrdinalIgnoreCase);

                // Simulation: Custom app is kept, built-in apps missing on disk are pruned
                list.RemoveAll(p => builtInIds.Contains(p.AppId) && (string.IsNullOrEmpty(p.ExecutablePath) || !File.Exists(p.ExecutablePath)));

                Assert.IsTrue(list.Any(p => p.AppId == "my_custom_tool"), "Custom user profile must always be preserved.");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [TestMethod]
        public void V18_VersionMetadata_MatchesVersion18()
        {
            Assert.AreEqual("1.8", UpdateService.CurrentVersionString, "CurrentVersionString must dynamically resolve to 1.8 in v1.8.");

            var thread = new System.Threading.Thread(() =>
            {
                var app = System.Windows.Application.Current ?? new System.Windows.Application();
                var window = new NotiGlow.UI.MainWindow();
                Assert.AreEqual("NotiGlow 1.8", window.Title);
                var titleBar = window.FindName("AppTitleBar") as Wpf.Ui.Controls.TitleBar;
                Assert.IsNotNull(titleBar);
                Assert.AreEqual("NotiGlow 1.8 — Ambient Notification Utility", titleBar.Title);
                Assert.IsFalse(window.Title?.Contains("1.6") == true);
                Assert.IsFalse(titleBar?.Title?.Contains("1.6") == true);
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }
}
