using System;
using System.IO;
using System.Text.Json;
using GlowBorder.Models;
using GlowBorder.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlowBorder.Tests
{
    [TestClass]
    public class AnimationStyleDataFlowTests
    {
        [TestMethod]
        public void Scenario1_Profile_AnimationStyle_Pulse_Preserved()
        {
            var profile = new AppProfile { AppId = "TestApp1", Name = "Test App 1", Style = GlowStyle.Pulse };
            Assert.AreEqual(GlowStyle.Pulse, profile.Style);
        }

        [TestMethod]
        public void Scenario2_Profile_AnimationStyle_Sweep_Preserved()
        {
            var profile = new AppProfile { AppId = "TestApp2", Name = "Test App 2", Style = GlowStyle.Sweep };
            Assert.AreEqual(GlowStyle.Sweep, profile.Style);
        }

        [TestMethod]
        public void Scenario3_Profile_AnimationStyle_Ambient_Preserved()
        {
            var profile = new AppProfile { AppId = "TestApp3", Name = "Test App 3", Style = GlowStyle.Ambient };
            Assert.AreEqual(GlowStyle.Ambient, profile.Style);
        }

        [TestMethod]
        public void Scenario4_Profile_AnimationStyle_Comet_Preserved()
        {
            var profile = new AppProfile { AppId = "TestApp4", Name = "Test App 4", Style = GlowStyle.Comet };
            Assert.AreEqual(GlowStyle.Comet, profile.Style);
        }

        [TestMethod]
        public void Scenario5_Profile_AnimationStyle_Ripple_Preserved()
        {
            var profile = new AppProfile { AppId = "TestApp5", Name = "Test App 5", Style = GlowStyle.Ripple };
            Assert.AreEqual(GlowStyle.Ripple, profile.Style);
        }

        [TestMethod]
        public void Scenario6_Profile_SaveAndLoad_PreservesAllAnimationStyles()
        {
            var styles = new[] { GlowStyle.Pulse, GlowStyle.Sweep, GlowStyle.Ambient, GlowStyle.Comet, GlowStyle.Ripple };

            foreach (var style in styles)
            {
                var profile = new AppProfile
                {
                    AppId = $"app_{style}",
                    Name = $"App {style}",
                    Enabled = true,
                    ColorHex = "#24A1DE",
                    DurationMs = 3500,
                    Intensity = 0.85,
                    Thickness = 5,
                    GlowSize = 35,
                    Style = style,
                    Priority = NotificationPriority.Normal
                };

                string json = JsonSerializer.Serialize(profile);
                var deserialized = JsonSerializer.Deserialize<AppProfile>(json);

                Assert.IsNotNull(deserialized);
                Assert.AreEqual(style, deserialized.Style, $"Style {style} was not preserved across serialization!");
                Assert.AreEqual(profile.AppId, deserialized.AppId);
                Assert.AreEqual(profile.ColorHex, deserialized.ColorHex);
                Assert.AreEqual(profile.DurationMs, deserialized.DurationMs);
            }
        }

        [TestMethod]
        public void Scenario7_GlowManager_UsesProfileAnimationStyle_WhenProcessingNotification()
        {
            var profileService = new ProfileService();
            var settingsService = new SettingsService();

            var customProfile = new AppProfile
            {
                AppId = "slack_app",
                Name = "Slack",
                Enabled = true,
                ColorHex = "#E01E5A",
                DurationMs = 4500,
                Intensity = 0.9,
                Style = GlowStyle.Comet
            };

            profileService.AddOrUpdateProfile(customProfile);

            var retrieved = profileService.GetProfile("slack_app", "Slack");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(GlowStyle.Comet, retrieved.Style);

            var notification = new NotificationItem
            {
                AppId = "slack_app",
                AppName = "Slack",
                Title = "New direct message",
                Timestamp = DateTime.Now
            };

            var matchedProfile = profileService.GetProfile(notification.AppId, notification.AppName);
            Assert.IsNotNull(matchedProfile);
            Assert.AreEqual(GlowStyle.Comet, matchedProfile.Style);
        }

        [TestMethod]
        public void Scenario8_GlobalDefaultStyleChange_DoesNotOverrideAppSpecificProfileStyle()
        {
            var settingsService = new SettingsService();
            var profileService = new ProfileService();

            // Set global default to Sweep
            var settings = settingsService.Current;
            settings.DefaultStyle = GlowStyle.Sweep;
            settingsService.Save(settings);

            // Add app profile with Ripple
            var appProfile = new AppProfile
            {
                AppId = "custom_chat",
                Name = "Custom Chat",
                Enabled = true,
                Style = GlowStyle.Ripple
            };
            profileService.AddOrUpdateProfile(appProfile);

            // Change global default to Ambient
            settings.DefaultStyle = GlowStyle.Ambient;
            settingsService.Save(settings);

            // Verify app profile still has Ripple and was not overridden by global default
            var retrieved = profileService.GetProfile("custom_chat");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(GlowStyle.Ripple, retrieved.Style);
            Assert.AreNotEqual(settings.DefaultStyle, retrieved.Style);
        }

        [TestMethod]
        public void Scenario9_UntrackedNotification_FilteringBehaviorPreserved()
        {
            var settingsService = new SettingsService();
            var profileService = new ProfileService();
            var manager = new GlowManager(settingsService, profileService);

            var notification = new NotificationItem
            {
                AppId = "unknown_system_service_xyz",
                AppName = "Unknown Service",
                Title = "Disk check completed",
                Timestamp = DateTime.Now
            };

            var profile = profileService.GetProfile(notification.AppId, notification.AppName);
            Assert.IsNull(profile, "Untracked app must return null from GetProfile.");

            // GlowManager must safely ignore without throwing or animating
            manager.TriggerNotification(notification);
            Assert.IsFalse(manager.IsAnimating);
        }

        [TestMethod]
        public void Scenario10_ProfileService_AddOrUpdate_CaseInsensitiveEnumParsing()
        {
            var profile = new AppProfile
            {
                AppId = "telegram_client",
                Name = "Telegram",
                Enabled = true
            };

            string[] styleStrings = { "pulse", "SWEEP", "Ambient", "comet", "RIPPLE" };
            GlowStyle[] expectedStyles = { GlowStyle.Pulse, GlowStyle.Sweep, GlowStyle.Ambient, GlowStyle.Comet, GlowStyle.Ripple };

            for (int i = 0; i < styleStrings.Length; i++)
            {
                bool success = Enum.TryParse<GlowStyle>(styleStrings[i], true, out var parsedStyle);
                Assert.IsTrue(success);
                Assert.AreEqual(expectedStyles[i], parsedStyle);

                profile.Style = parsedStyle;
                Assert.AreEqual(expectedStyles[i], profile.Style);
            }
        }

        [TestMethod]
        public void Scenario11_SequentialStyleTransitions_Pulse_Sweep_Ambient_Comet_Ripple()
        {
            var profileService = new ProfileService();
            var testAppId = "discord_sequential_test";

            var profile = new AppProfile
            {
                AppId = testAppId,
                Name = "Discord Test",
                Enabled = true,
                Style = GlowStyle.Pulse
            };

            var sequence = new[] { GlowStyle.Pulse, GlowStyle.Sweep, GlowStyle.Ambient, GlowStyle.Comet, GlowStyle.Ripple };

            foreach (var expectedStyle in sequence)
            {
                profile.Style = expectedStyle;
                profileService.AddOrUpdateProfile(profile);

                var retrieved = profileService.GetProfile(testAppId);
                Assert.IsNotNull(retrieved);
                Assert.AreEqual(expectedStyle, retrieved.Style, $"Failed at step {expectedStyle}: style mismatch in ProfileService.");
            }
        }
    }
}
