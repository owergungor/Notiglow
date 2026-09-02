using GlowBorder.Models;
using GlowBorder.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlowBorder.Tests
{
    [TestClass]
    public class ApplicationMatchingTests
    {
        private ProfileService _profileService = null!;

        [TestInitialize]
        public void Setup()
        {
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
    }
}
