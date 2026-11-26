using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V5MigrationAndDefaultTests
    {
        private static string ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        [TestMethod]
        public void FreshInstall_V5Defaults_EnableRustArchitecture()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"NotiGlow_V5Fresh_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try
            {
                var svc = new SettingsService(Path.Combine(dir, "settings.json"));
                Assert.AreEqual(5, svc.Current.SettingsSchemaVersion);
                Assert.IsTrue(svc.Current.UseRustBackgroundWorker);
                Assert.IsTrue(svc.Current.UseRustGameDetection);
                Assert.IsTrue(svc.Current.UseRustGlowEngine);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [TestMethod]
        public void V42ToV5Migration_EnablesRustDefaults_PreservesAllUserPreferencesAndProfiles()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"NotiGlow_V5Mig_{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string settingsPath = Path.Combine(dir, "settings.json");
            string profilesPath = Path.Combine(dir, "profiles.json");

            try
            {
                // Simulate a v4.2 settings.json (explicit false flags, no SettingsSchemaVersion)
                string v42Json = """
                {
                  "MasterEnabled": true,
                  "OledMode": true,
                  "ReduceMotion": true,
                  "CornerRadius": 22.0,
                  "AppLanguage": "tr-TR",
                  "DefaultColorHex": "#FF3366",
                  "UseRustBackgroundWorker": false,
                  "UseRustGameDetection": false,
                  "UseRustGlowEngine": false,
                  "GamingModeEnabled": true,
                  "TrackedGames": ["C:\\Games\\MyCustomGame\\mygame.exe"],
                  "GameOverrides": {
                    "mygame.exe": {
                      "GameIdentifier": "mygame.exe",
                      "CustomGlowEnabled": true,
                      "DurationMs": 2200,
                      "Intensity": 0.75,
                      "ColorHex": "#00E5FF"
                    }
                  }
                }
                """;
                File.WriteAllText(settingsPath, v42Json);
                File.WriteAllText(profilesPath, JsonSerializer.Serialize(new List<AppProfile>
                {
                    new() { AppId = "Discord", Name = "Discord", ColorHex = "#5865F2", DurationMs = 3500 }
                }));
                string profilesHashBefore = ComputeSha256(profilesPath);

                // First load under v5.0 migrates Rust flags -> true while keeping every user preference
                var svc = new SettingsService(settingsPath);
                Assert.AreEqual(5, svc.Current.SettingsSchemaVersion);
                Assert.IsTrue(svc.Current.UseRustBackgroundWorker);
                Assert.IsTrue(svc.Current.UseRustGameDetection);
                Assert.IsTrue(svc.Current.UseRustGlowEngine);
                Assert.IsTrue(svc.Current.OledMode);
                Assert.IsTrue(svc.Current.ReduceMotion);
                Assert.AreEqual(22.0, svc.Current.CornerRadius);
                Assert.AreEqual("tr-TR", svc.Current.AppLanguage);
                Assert.AreEqual("#FF3366", svc.Current.DefaultColorHex);
                Assert.IsTrue(svc.Current.GamingModeEnabled);
                Assert.AreEqual(1, svc.Current.TrackedGames.Count);
                Assert.IsTrue(svc.Current.GameOverrides.ContainsKey("mygame.exe"));
                Assert.AreEqual(profilesHashBefore, ComputeSha256(profilesPath), "profiles.json must be untouched");

                // Subsequent explicit opt-out in v5.0 MUST be respected (never overwritten back to true)
                svc.Current.UseRustBackgroundWorker = false;
                svc.Save(svc.Current);

                var svcReload = new SettingsService(settingsPath);
                Assert.AreEqual(5, svcReload.Current.SettingsSchemaVersion);
                Assert.IsFalse(svcReload.Current.UseRustBackgroundWorker, "Explicit v5.0 user opt-out must be preserved");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
