using NotiGlow.Models;
using NotiGlow.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class ApplicationMatchingTests
    {
        private ProfileService _profileService = null!;

        [TestInitialize]
        public void Setup()
        {
            try
            {
                string path1 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotiGlow", "profiles.json");
                if (System.IO.File.Exists(path1)) System.IO.File.Delete(path1);

                string path2 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotiGlow", "profiles.json");
                if (System.IO.File.Exists(path2)) System.IO.File.Delete(path2);

                string legacyPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlowBorder", "profiles.json");
                if (System.IO.File.Exists(legacyPath)) System.IO.File.Delete(legacyPath);
            }
            catch { }
            _profileService = new ProfileService();
        }

        [TestMethod]
        public void GetProfile_ExactMatch_ReturnsProfile()
        {
            var profile = _profileService.GetProfile("Discord", "Discord");
            Assert.IsNotNull(profile);
            Assert.AreEqual("Discord", profile.Name);
            Assert.AreEqual("#5865F2", profile.ColorHex);
        }

        [TestMethod]
        public void GetProfile_PackageFamilyNameMatch_ReturnsWhatsAppProfile()
        {
            var profile = _profileService.GetProfile("Microsoft.WhatsApp_8wekyb3d8bbwe!App", "WhatsApp");
            Assert.IsNotNull(profile);
            Assert.AreEqual("WhatsApp", profile.Name);
            Assert.AreEqual("#25D366", profile.ColorHex);
        }

        [TestMethod]
        public void GetProfile_ExecutableNameMatch_ReturnsSteamProfile()
        {
            var profile = _profileService.GetProfile("steam.exe", "Steam App");
            Assert.IsNotNull(profile);
            Assert.AreEqual("Steam", profile.Name);
            Assert.AreEqual("#66C0F4", profile.ColorHex);
        }

        [TestMethod]
        public void GetProfile_UnknownApplication_ReturnsNull()
        {
            var profile = _profileService.GetProfile("Chrome", "Google Chrome");
            Assert.IsNull(profile);
        }

        [TestMethod]
        public void GetProfile_UntrackedRandomApp_ReturnsNull()
        {
            var profile = _profileService.GetProfile("some_unknown_game.exe", "Random Game");
            Assert.IsNull(profile);
        }

        [TestMethod]
        public void GetProfile_ProfileWithExeAppId_MatchesCleanAppId()
        {
            var customProfile = new AppProfile
            {
                AppId = "claude.exe",
                Name = "Claude",
                ExecutablePath = @"C:\Program Files\AnthropicClaude\claude.exe",
                Enabled = true,
                ColorHex = "#FF5409"
            };
            _profileService.AddOrUpdateProfile(customProfile);

            var profile = _profileService.GetProfile("claude", "Claude");
            Assert.IsNotNull(profile);
            Assert.AreEqual("Claude", profile.Name);
            Assert.AreEqual("claude.exe", profile.AppId);
        }

        [TestMethod]
        public void GetProfile_ProfileWithExeAppId_MatchesExactExeAppId()
        {
            var customProfile = new AppProfile
            {
                AppId = "claude.exe",
                Name = "Claude",
                ExecutablePath = @"C:\Program Files\AnthropicClaude\claude.exe",
                Enabled = true,
                ColorHex = "#FF5409"
            };
            _profileService.AddOrUpdateProfile(customProfile);

            var profile = _profileService.GetProfile("claude.exe", "");
            Assert.IsNotNull(profile);
            Assert.AreEqual("Claude", profile.Name);
        }

        [TestMethod]
        public void GetProfile_ProfileWithExecutablePath_MatchesAumidPackage()
        {
            var customProfile = new AppProfile
            {
                AppId = "WhatsApp.Root.exe",
                Name = "WhatsApp.Root",
                ExecutablePath = @"C:\Program Files\WindowsApps\5319275a.whatsappdesktop_2.2632.100.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe",
                Enabled = true,
                ColorHex = "#25D366"
            };
            _profileService.AddOrUpdateProfile(customProfile);

            // AUMID contains whatsapp
            var profile = _profileService.GetProfile("5319275A.WhatsAppDesktop_cv1g1gvanyjgm!App", "WhatsApp");
            Assert.IsNotNull(profile);
            Assert.IsTrue(profile.Name == "WhatsApp" || profile.Name == "WhatsApp.Root");
        }
    }
}
