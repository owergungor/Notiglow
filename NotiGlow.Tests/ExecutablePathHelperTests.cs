using System;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;

namespace NotiGlow.Tests
{
    [TestClass]
    public class ExecutablePathHelperTests
    {
        [TestMethod]
        [DataRow(@"C:\Program Files\Google\Chrome\Application\chrome.exe", "chrome.exe")]
        [DataRow(@"D:\Games\Steam\steamapps\common\Game\game.exe", "game.exe")]
        [DataRow(@"C:\Windows\System32\notepad.exe", "notepad.exe")]
        [DataRow(@"discord.exe", "discord.exe")]
        [DataRow(@"C:\Program Files\App\UPPERCASE.EXE", "UPPERCASE.EXE")]
        public void TryGetExecutableName_ValidExePath_ReturnsTrueAndFileName(string path, string expectedFileName)
        {
            bool success = ExecutablePathHelper.TryGetExecutableName(path, out string actualFileName);

            Assert.IsTrue(success, $"Should successfully extract filename for {path}");
            Assert.AreEqual(expectedFileName, actualFileName, true);
        }

        [TestMethod]
        [DataRow(@"C:\Documents\test.txt")]
        [DataRow(@"C:\Images\logo.png")]
        [DataRow(@"C:\Archives\package.zip")]
        [DataRow(@"C:\Scripts\run.bat")]
        [DataRow(@"C:\Scripts\run.sh")]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(null)]
        public void TryGetExecutableName_NonExePath_ReturnsFalse(string? path)
        {
            bool success = ExecutablePathHelper.TryGetExecutableName(path, out string actualFileName);

            Assert.IsFalse(success, $"Should return false for non-exe path '{path}'");
            Assert.AreEqual(string.Empty, actualFileName);
        }

        [TestMethod]
        public void TryApplySelectedExecutable_ValidExe_UpdatesAppId()
        {
            string currentAppId = "custom_app";
            string selectedPath = @"C:\Program Files\Discord\Discord.exe";

            bool applied = ExecutablePathHelper.TryApplySelectedExecutable(currentAppId, selectedPath, out string newAppId, out string? errorMessage);

            Assert.IsTrue(applied);
            Assert.AreEqual("Discord.exe", newAppId, true);
            Assert.IsNull(errorMessage);
        }

        [TestMethod]
        public void TryApplySelectedExecutable_NonExe_RejectsAndPreservesCurrentAppId()
        {
            string currentAppId = "custom_app";
            string selectedPath = @"C:\Documents\Notes.docx";

            bool applied = ExecutablePathHelper.TryApplySelectedExecutable(currentAppId, selectedPath, out string newAppId, out string? errorMessage);

            Assert.IsFalse(applied);
            Assert.AreEqual("custom_app", newAppId);
            Assert.IsNotNull(errorMessage);
            StringAssert.Contains(errorMessage, ".exe");
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public void TryApplySelectedExecutable_CancelledOrEmpty_PreservesCurrentAppIdWithoutError(string? selectedPath)
        {
            string currentAppId = "custom_app";

            bool applied = ExecutablePathHelper.TryApplySelectedExecutable(currentAppId, selectedPath, out string newAppId, out string? errorMessage);

            Assert.IsFalse(applied);
            Assert.AreEqual("custom_app", newAppId);
            Assert.IsNull(errorMessage, "Cancelled selection should not generate an error message");
        }

        [TestMethod]
        public void ResolveAppIdentifier_FullPathToExe_ReturnsFileName()
        {
            string input = @"C:\Program Files\Notepad++\notepad++.exe";
            string resolved = ExecutablePathHelper.ResolveAppIdentifier(input);

            Assert.AreEqual("notepad++.exe", resolved, true);
        }

        [TestMethod]
        public void ResolveAppIdentifier_PlainTextIdentifier_ReturnsTrimmedString()
        {
            string input = "  discord  ";
            string resolved = ExecutablePathHelper.ResolveAppIdentifier(input);

            Assert.AreEqual("discord", resolved);
        }

        [TestMethod]
        public void AppProfile_ExecutablePath_CloningAndSerialization()
        {
            var profile = new AppProfile
            {
                AppId = "chrome.exe",
                Name = "Google Chrome",
                ExecutablePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                ColorHex = "#4285F4"
            };

            // Test Clone()
            var clone = profile.Clone();
            Assert.AreEqual(profile.ExecutablePath, clone.ExecutablePath);

            // Test JSON Serialization / Deserialization
            string json = JsonSerializer.Serialize(profile);
            var deserialized = JsonSerializer.Deserialize<AppProfile>(json);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(profile.ExecutablePath, deserialized.ExecutablePath);
            Assert.AreEqual(profile.AppId, deserialized.AppId);
        }
    }
}
