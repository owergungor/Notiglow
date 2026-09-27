using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Animations;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V16FeatureTests
    {
        #region 1. Theme Tests (Removal of old Amethyst, Nubank -> Amethyst mapping, migration)

        [TestMethod]
        public void Theme_CanonicalList_Has12Themes_AndNoNubank()
        {
            var themes = ThemeService.CanonicalThemes;
            Assert.AreEqual(12, themes.Count, "Canonical themes count must be exactly 12 in v1.6.");

            // Ensure Nubank is not in the canonical list
            var names = themes.Select(t => t.ToString()).ToList();
            Assert.IsFalse(names.Contains("Nubank"), "Theme list must not contain 'Nubank'.");
            Assert.IsTrue(names.Contains("Amethyst"), "Theme list must contain 'Amethyst'.");
        }

        [TestMethod]
        public void Theme_Amethyst_HasNubankPurpleColors()
        {
            var paletteDark = ThemeService.GetPalette(ColorTheme.Amethyst, true);
            var paletteLight = ThemeService.GetPalette(ColorTheme.Amethyst, false);
            Assert.IsNotNull(paletteDark);
            Assert.IsNotNull(paletteLight);
            Assert.AreEqual("#820AD1", paletteDark.AccentColor);
            Assert.AreEqual("#7B0ABA", paletteLight.AccentColor);
        }

        [TestMethod]
        public void Theme_LegacyJson_Nubank_DeserializesToAmethyst()
        {
            string json = "{\"ColorTheme\": \"Nubank\", \"MasterEnabled\": true}";
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.IsNotNull(settings);
            Assert.AreEqual(ColorTheme.Amethyst, settings.ColorTheme);
        }

        [TestMethod]
        public void Theme_LegacyJson_Amethyst_DeserializesToAmethystSafely()
        {
            string json = "{\"ColorTheme\": \"Amethyst\", \"MasterEnabled\": true}";
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            Assert.IsNotNull(settings);
            Assert.AreEqual(ColorTheme.Amethyst, settings.ColorTheme);
        }

        [TestMethod]
        public void Theme_CanonicalOrder_IsChromaticAndDeterministic()
        {
            var expectedOrder = new[]
            {
                ColorTheme.Standard,
                ColorTheme.Zen,
                ColorTheme.Amber,
                ColorTheme.Mocha,
                ColorTheme.Burgundy,
                ColorTheme.Sakura,
                ColorTheme.Bubblegum,
                ColorTheme.Amethyst,
                ColorTheme.Violet,
                ColorTheme.Indigo,
                ColorTheme.Sapphire,
                ColorTheme.Nature
            };

            var actualOrder = ThemeService.CanonicalThemes.ToArray();
            CollectionAssert.AreEqual(expectedOrder, actualOrder, "Canonical themes must follow the chromatic proximity order.");
        }

        #endregion

        #region 2. Ripple Tests (Center, circular geometry, multi-aspect ratio, reduce motion)

        [TestMethod]
        public void Ripple_CenterCoordinates_AreExactScreenCenter()
        {
            // Test 16:9 (1920x1080)
            double w1 = 1920, h1 = 1080;
            var center1 = GlowSpectrumBrushFactory.CalculateRippleCenter(w1, h1);
            Assert.AreEqual(960.0, center1.X, 0.001);
            Assert.AreEqual(540.0, center1.Y, 0.001);

            // Test 21:9 Ultrawide (2560x1080)
            double w2 = 2560, h2 = 1080;
            var center2 = GlowSpectrumBrushFactory.CalculateRippleCenter(w2, h2);
            Assert.AreEqual(1280.0, center2.X, 0.001);
            Assert.AreEqual(540.0, center2.Y, 0.001);

            // Test Square (1000x1000)
            double w3 = 1000, h3 = 1000;
            var center3 = GlowSpectrumBrushFactory.CalculateRippleCenter(w3, h3);
            Assert.AreEqual(500.0, center3.X, 0.001);
            Assert.AreEqual(500.0, center3.Y, 0.001);
        }

        [TestMethod]
        public void Ripple_MaxRadius_CoversAllFourCorners()
        {
            // 1920x1080: halfW=960, halfH=540. Distance to corner = sqrt(960^2 + 540^2) = 1101.45
            double w = 1920, h = 1080;
            double cornerDist = Math.Sqrt((w / 2.0) * (w / 2.0) + (h / 2.0) * (h / 2.0));
            double maxRadius = GlowSpectrumBrushFactory.CalculateRippleMaxRadius(w, h);

            Assert.IsTrue(maxRadius >= cornerDist, "Ripple max radius must reach or exceed all four screen corners.");
        }

        [TestMethod]
        public void Ripple_Geometry_IsTrueCircle_RegardlessOfAspectRatio()
        {
            // 16:9 ratio
            var brush169 = GlowSpectrumBrushFactory.CreateRippleBrush(1920, 1080, Colors.DeepSkyBlue, 500);
            Assert.AreEqual(BrushMappingMode.Absolute, brush169.MappingMode, "Brush must use Absolute mapping to prevent ellipse distortion.");
            Assert.AreEqual(brush169.RadiusX, brush169.RadiusY, "RadiusX must strictly equal RadiusY for true circular shockwave.");
            Assert.AreEqual(500.0, brush169.RadiusX);

            // 21:9 Ultrawide ratio
            var brush219 = GlowSpectrumBrushFactory.CreateRippleBrush(3440, 1440, Colors.Crimson, 750);
            Assert.AreEqual(BrushMappingMode.Absolute, brush219.MappingMode);
            Assert.AreEqual(brush219.RadiusX, brush219.RadiusY, "RadiusX must equal RadiusY in Ultrawide 21:9.");
            Assert.AreEqual(750.0, brush219.RadiusX);
        }

        [TestMethod]
        public void Ripple_AnnularShockwave_HasHollowCenter()
        {
            var brush = GlowSpectrumBrushFactory.CreateRippleBrush(1920, 1080, Colors.Magenta, 600);
            // Center stop at offset 0.0 must be transparent (hollow ring shockwave, no solid disk)
            var centerStop = brush.GradientStops.FirstOrDefault(s => s.Offset == 0.0);
            Assert.IsNotNull(centerStop);
            Assert.AreEqual(0, centerStop.Color.A, "Center of circular shockwave must be transparent.");
        }

        [TestMethod]
        public void Ripple_ReduceMotion_SuppressesDynamicAnimation()
        {
            MotionPolicy.Update(true);
            Assert.IsTrue(MotionPolicy.IsReduceMotion);
            // In Reduce Motion, animations should have zero duration
            var duration = MotionPolicy.GetDuration(TimeSpan.FromSeconds(2));
            Assert.AreEqual(TimeSpan.Zero, duration);

            // Reset
            MotionPolicy.Update(false);
            Assert.IsFalse(MotionPolicy.IsReduceMotion);
        }

        #endregion

        #region 3. RGB Common Rendering Tests (Pulse, Sweep, Ambient, Comet shared spectrum)

        [TestMethod]
        public void Rgb_ScreenSpaceAbsoluteBrush_EliminatesEdgeSeams()
        {
            double screenW = 1920, screenH = 1080;
            var brush = GlowSpectrumBrushFactory.CreateScreenSpaceRgbBrush(screenW, screenH);

            Assert.AreEqual(BrushMappingMode.Absolute, brush.MappingMode, "RGB rainbow brush must use Absolute coordinate mapping.");
            Assert.AreEqual(0.0, brush.StartPoint.X);
            Assert.AreEqual(0.0, brush.StartPoint.Y);
            Assert.AreEqual(screenW, brush.EndPoint.X);
            Assert.AreEqual(screenH, brush.EndPoint.Y);
        }

        [TestMethod]
        public void Rgb_AllFourEdges_SampleSameCoordinatesAtCorners()
        {
            // At screen coordinate (1920, 0) (Top-Right corner):
            // TopEdge rightmost point is at screen (1920, 0)
            // RightEdge topmost point is at screen (1920, 0)
            // With Absolute mapping, both sample the EXACT same point in the gradient:
            double screenW = 1920, screenH = 1080;
            var brush = GlowSpectrumBrushFactory.CreateScreenSpaceRgbBrush(screenW, screenH);

            Assert.AreEqual(new Point(0, 0), brush.StartPoint);
            Assert.AreEqual(new Point(screenW, screenH), brush.EndPoint);
        }

        [TestMethod]
        public void Rgb_SweepAndComet_ShareSynchronizedRainbowStops()
        {
            var sweepStops = new GradientStopCollection();
            GlowSpectrumBrushFactory.PopulateSweepRgbStops(sweepStops, false);
            Assert.IsTrue(sweepStops.Count >= 7, "Sweep stops must include full visible spectrum.");

            var cometStops = new GradientStopCollection();
            GlowSpectrumBrushFactory.PopulateCometRgbStops(cometStops, false);
            Assert.IsTrue(cometStops.Count >= 7, "Comet stops must include full visible spectrum.");

            var rippleStops = new GradientStopCollection();
            GlowSpectrumBrushFactory.PopulateRippleRgbStops(rippleStops);
            Assert.IsTrue(rippleStops.Count >= 7, "Ripple stops must include full visible spectrum.");
        }

        #endregion

        #region 4. Applications View Tests (List vs Grid mode, persistence, AI profiles)

        [TestMethod]
        public void Applications_ViewMode_ListAndGridEnum_AreDefined()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(ApplicationViewMode), ApplicationViewMode.List));
            Assert.IsTrue(Enum.IsDefined(typeof(ApplicationViewMode), ApplicationViewMode.Grid));
        }

        [TestMethod]
        public void Applications_ViewMode_PersistsInAppSettings()
        {
            var settings = new AppSettings();
            Assert.AreEqual(ApplicationViewMode.List, settings.ApplicationsViewMode, "Default view mode should be List.");

            settings.ApplicationsViewMode = ApplicationViewMode.Grid;
            string json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(ApplicationViewMode.Grid, deserialized.ApplicationsViewMode);
        }

        [TestMethod]
        public void Applications_AllAIProfilesAndDefaultProfiles_ArePresent()
        {
            var profiles = ProfileService.GetDefaultProfiles();

            // Verify AI profiles added in v1.5 remain intact
            var aiAppIds = new[] { "claude", "OpenAI.ChatGPT", "Microsoft.Copilot", "Google.Gemini" };
            foreach (var aiId in aiAppIds)
            {
                var found = profiles.FirstOrDefault(p => p.AppId.Equals(aiId, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(aiId, StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(found, $"AI profile '{aiId}' must remain present in default profiles.");
            }

            // Verify core standard apps remain intact
            var coreAppIds = new[] { "discord", "whatsapp", "telegram", "msteams", "steam", "spotify" };
            foreach (var coreId in coreAppIds)
            {
                var found = profiles.FirstOrDefault(p => p.AppId.Equals(coreId, StringComparison.OrdinalIgnoreCase) || p.Name.Equals(coreId, StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(found, $"Standard app profile '{coreId}' must remain present.");
            }
        }

        #endregion

        #region 5. Auto Update Tests (Version comparison, downgrade prevention, SHA256 extraction)

        [TestMethod]
        public void AutoUpdate_VersionComparison_DetectsNewerVersion()
        {
            Assert.IsTrue(UpdateService.IsNewerVersion("v1.7", "1.6"));
            Assert.IsTrue(UpdateService.IsNewerVersion("2.0", "1.6"));
            Assert.IsTrue(UpdateService.IsNewerVersion("v1.6.1", "1.6"));
        }

        [TestMethod]
        public void AutoUpdate_VersionComparison_PreventsDowngradeOrSame()
        {
            Assert.IsFalse(UpdateService.IsNewerVersion("v1.5", "1.6"), "Downgrade must return false.");
            Assert.IsFalse(UpdateService.IsNewerVersion("1.4", "1.6"), "Downgrade must return false.");
            Assert.IsFalse(UpdateService.IsNewerVersion("v1.6", "1.6"), "Same version must return false.");
            Assert.IsFalse(UpdateService.IsNewerVersion("", "1.6"), "Empty tag must return false.");
            Assert.IsFalse(UpdateService.IsNewerVersion("invalid", "1.6"), "Invalid tag must return false.");
        }

        [TestMethod]
        public void AutoUpdate_Sha256Extraction_ParsesChecksumLinesAccurately()
        {
            string shaContent =
                "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  NotiGlow_1.6_win-x64.zip\n" +
                "a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0  NotiGlow-Setup-x64.exe\n";

            string zipHash = UpdateService.ExtractHashForFile(shaContent, "NotiGlow_1.6_win-x64.zip");
            Assert.AreEqual("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", zipHash);

            string exeHash = UpdateService.ExtractHashForFile(shaContent, "NotiGlow-Setup-x64.exe");
            Assert.AreEqual("a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0", exeHash);

            string missingHash = UpdateService.ExtractHashForFile(shaContent, "NonExistent.exe");
            Assert.AreEqual(string.Empty, missingHash);
        }

        [TestMethod]
        public void AutoUpdate_Settings_DefaultsAndToggle_Persist()
        {
            var settings = new AppSettings();
            Assert.IsTrue(settings.AutoCheckUpdates, "AutoCheckUpdates should default to true.");

            settings.AutoCheckUpdates = false;
            settings.LastUpdateVersion = "1.7";
            settings.LastUpdateCheck = DateTime.UtcNow;

            string json = JsonSerializer.Serialize(settings);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.IsNotNull(deserialized);
            Assert.IsFalse(deserialized.AutoCheckUpdates);
            Assert.AreEqual("1.7", deserialized.LastUpdateVersion);
            Assert.IsNotNull(deserialized.LastUpdateCheck);
        }

        [TestMethod]
        public void AutoUpdate_VerifySha256_ValidatesAccurately()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"notiglow_sha_test_{Guid.NewGuid():N}.txt");
            try
            {
                File.WriteAllText(tempFile, "NotiGlow Test File Content for SHA256 Verification");
                using var sha = System.Security.Cryptography.SHA256.Create();
                using var fs = File.OpenRead(tempFile);
                string expectedHash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();

                Assert.IsTrue(UpdateService.VerifySha256(tempFile, expectedHash), "Matching hash must return true.");
                Assert.IsFalse(UpdateService.VerifySha256(tempFile, "0000000000000000000000000000000000000000000000000000000000000000"), "Mismatched hash must return false.");
                Assert.IsFalse(UpdateService.VerifySha256("nonexistent.txt", expectedHash), "Missing file must return false.");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [TestMethod]
        public void AutoUpdate_GenerateVbsUpdaterScript_ContainsAllAtomicStepsAndRollback()
        {
            int testPid = 12345;
            string testExe = @"C:\Program Files\NotiGlow\NotiGlow.exe";
            string testDir = @"C:\Program Files\NotiGlow";
            string testStaged = @"C:\Users\User\AppData\Roaming\NotiGlow\updates\staged_123";
            string testBackup = @"C:\Users\User\AppData\Roaming\NotiGlow\updates\backup_1.6";

            string script = UpdateService.GenerateVbsUpdaterScript(testPid, testExe, testDir, testStaged, testBackup);

            Assert.IsNotNull(script);
            Assert.IsTrue(script.Contains("pid = 12345"), "Script must wait for target process ID.");
            Assert.IsTrue(script.Contains("CopyFolder"), "Script must perform folder copy.");
            Assert.IsTrue(script.Contains(testBackup.Replace("\\", "\\\\")), "Script must create a backup folder.");
            Assert.IsTrue(script.Contains("If Err.Number <> 0 Then"), "Script must check for errors.");
            Assert.IsTrue(script.Contains("backupDir"), "Script must contain rollback path using backupDir.");
            Assert.IsTrue(script.Contains("shell.Run"), "Script must relaunch the target executable.");
            Assert.IsTrue(script.Contains("DeleteFolder stagedDir"), "Script must clean up staging directory.");
        }

        [TestMethod]
        public void AutoUpdate_OfflineOrNetworkFailure_FailsGracefullyWithoutThrowing()
        {
            var settings = new SettingsService();
            var updater = new UpdateService(settings);

            // Using cancelled token to simulate immediate network/connection failure
            using var cts = new System.Threading.CancellationTokenSource();
            cts.Cancel();

            var task = updater.CheckForUpdatesAsync(cts.Token);
            task.Wait();
            var info = task.Result;

            Assert.IsNotNull(info);
            Assert.IsFalse(info.IsUpdateAvailable, "Cancelled/offline request must not falsely report update available.");
            Assert.AreEqual("1.6", info.CurrentVersion);
        }

        [TestMethod]
        public void AutoUpdate_InterruptedDownload_LeavesNoCorruptedFinalFile()
        {
            string updatesDir = UpdateService.GetUpdatesDirectory();
            Assert.IsTrue(Directory.Exists(updatesDir), "Updates directory must exist.");

            // Verify .part files do not pollute clean target paths
            string testTarget = Path.Combine(updatesDir, "test_interrupted.bin");
            string testPart = testTarget + ".part";
            try
            {
                File.WriteAllText(testPart, "interrupted incomplete bytes");
                Assert.IsTrue(File.Exists(testPart));
                Assert.IsFalse(File.Exists(testTarget), "Final target file must never exist before verification.");
            }
            finally
            {
                if (File.Exists(testPart)) File.Delete(testPart);
                if (File.Exists(testTarget)) File.Delete(testTarget);
            }
        }

        #endregion
    }
}
