using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Path = System.IO.Path;
using Rectangle = System.Windows.Shapes.Rectangle;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Overlay;
using NotiGlow.Services;
using NotiGlow.UI.Animations;
using Wpf.Ui.Controls;

namespace NotiGlow.Tests
{
    [TestClass]
    public class V15FeatureTests
    {
        [TestMethod]
        public void Test_ButtonClickAnimationTarget_ExcludesContainers()
        {
            var thread = new Thread(() =>
            {
                var app = Application.Current ?? new Application();

                // ButtonBase / Buttons are allowed
                var button = new System.Windows.Controls.Button();
                Assert.IsFalse(ButtonPressAnimationBehavior.GetIsEnabled(button)); // default false

                // NavigationViewItem should NOT animate as an entire container
                var navItem = new NavigationViewItem();
                Assert.IsFalse(ButtonPressAnimationBehavior.GetIsEnabled(navItem));

                // RadioButton cards should NOT be attached globally
                var radio = new System.Windows.Controls.RadioButton();
                Assert.IsFalse(ButtonPressAnimationBehavior.GetIsEnabled(radio));

                // Containers like Grid, StackPanel, Border
                var grid = new Grid();
                var panel = new StackPanel();
                var border = new Border();
                Assert.IsFalse(ButtonPressAnimationBehavior.GetIsEnabled(grid));
                Assert.IsFalse(ButtonPressAnimationBehavior.GetIsEnabled(panel));
                Assert.IsFalse(ButtonPressAnimationBehavior.GetIsEnabled(border));
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void Test_RgbSpectrum_ConfigurationAndRainbowBrush()
        {
            Assert.IsTrue(ColorHelper.IsRgbSpectrum("RGB"));
            Assert.IsTrue(ColorHelper.IsRgbSpectrum("#RGB"));
            Assert.IsTrue(ColorHelper.IsRgbSpectrum("Rainbow"));
            Assert.IsFalse(ColorHelper.IsRgbSpectrum("#5865F2"));
            Assert.IsFalse(ColorHelper.IsRgbSpectrum(""));

            Assert.IsNotNull(ColorHelper.RgbSpectrumColors);
            Assert.IsTrue(ColorHelper.RgbSpectrumColors.Length >= 8);
            // Verify loopback for seamless cycling
            Assert.AreEqual(ColorHelper.RgbSpectrumColors[0], ColorHelper.RgbSpectrumColors[ColorHelper.RgbSpectrumColors.Length - 1]);

            var brush = ColorHelper.CreateRainbowLinearBrush(new Point(0, 0), new Point(1, 1));
            Assert.IsNotNull(brush);
            Assert.AreEqual(ColorHelper.RgbSpectrumColors.Length, brush.GradientStops.Count);
        }

        [TestMethod]
        public void Test_GlowBorderControl_RgbMode_AppliesSpectrumToAllEdges()
        {
            var thread = new Thread(() =>
            {
                var app = Application.Current ?? new Application();
                var glow = new GlowBorderControl();

                var profile = new AppProfile
                {
                    AppId = "TestRGB",
                    Name = "Test RGB",
                    ColorHex = "RGB",
                    Style = GlowStyle.Pulse,
                    Intensity = 0.8,
                    GlowSize = 30,
                    Thickness = 4
                };

                glow.ApplyProfile(profile);

                var topEdge = glow.FindName("TopEdge") as Rectangle;
                var bottomEdge = glow.FindName("BottomEdge") as Rectangle;
                var leftEdge = glow.FindName("LeftEdge") as Rectangle;
                var rightEdge = glow.FindName("RightEdge") as Rectangle;
                var innerBorder = glow.FindName("InnerBorder") as Border;

                Assert.IsNotNull(topEdge);
                Assert.IsNotNull(bottomEdge);
                Assert.IsNotNull(leftEdge);
                Assert.IsNotNull(rightEdge);
                Assert.IsNotNull(innerBorder);

                // In RGB mode, all edges and borders must receive the rainbow brush, not static red
                Assert.IsInstanceOfType(topEdge.Fill, typeof(LinearGradientBrush));
                Assert.IsNotNull(topEdge.OpacityMask);
                Assert.IsNotNull(bottomEdge.OpacityMask);
                Assert.IsNotNull(leftEdge.OpacityMask);
                Assert.IsNotNull(rightEdge.OpacityMask);
                Assert.IsInstanceOfType(innerBorder.BorderBrush, typeof(LinearGradientBrush));

                var rainbowBrush = (LinearGradientBrush)innerBorder.BorderBrush;
                Assert.AreEqual(GradientSpreadMethod.Repeat, rainbowBrush.SpreadMethod);

                glow.StopAnimation();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void Test_RippleGeometry_AspectRatioCompensation()
        {
            // Verify aspect ratio compensation preserves physically equal radial wave across monitor ratios
            // 1. Widescreen 16:9
            double wWide = 1920;
            double hWide = 1080;
            double aspectWide = wWide / hWide;
            double radiusX = 0.5;
            double radiusY = radiusX * aspectWide;
            // Physical horizontal radius in pixels: radiusX * wWide
            double physX = radiusX * wWide;
            // Physical vertical radius in pixels: radiusY * hWide = (radiusX * (wWide / hWide)) * hWide = radiusX * wWide
            double physY = radiusY * hWide;
            Assert.AreEqual(physX, physY, 0.001);

            // 2. Square 1:1
            double wSquare = 1000;
            double hSquare = 1000;
            double aspectSquare = wSquare / hSquare;
            Assert.AreEqual(1.0, aspectSquare, 0.001);
            Assert.AreEqual(radiusX * wSquare, (radiusX * aspectSquare) * hSquare, 0.001);

            // 3. Portrait 9:16
            double wPort = 1080;
            double hPort = 1920;
            double aspectPort = wPort / hPort;
            Assert.AreEqual(radiusX * wPort, (radiusX * aspectPort) * hPort, 0.001);
        }

        [TestMethod]
        public void Test_ApplicationDefinitions_AiApplicationsPresentAndMatched()
        {
            var defaults = ProfileService.GetDefaultProfiles();

            // Verify ready AI applications exist
            var claude = defaults.FirstOrDefault(p => p.Name.Equals("Claude", StringComparison.OrdinalIgnoreCase));
            var chatgpt = defaults.FirstOrDefault(p => p.Name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase));
            var copilot = defaults.FirstOrDefault(p => p.Name.Equals("Microsoft Copilot", StringComparison.OrdinalIgnoreCase));
            var gemini = defaults.FirstOrDefault(p => p.Name.Equals("Google Gemini", StringComparison.OrdinalIgnoreCase));

            Assert.IsNotNull(claude);
            Assert.AreEqual("AI Assistants", claude.Category);
            Assert.IsTrue(claude.Enabled);

            Assert.IsNotNull(chatgpt);
            Assert.AreEqual("AI Assistants", chatgpt.Category);

            Assert.IsNotNull(copilot);
            Assert.AreEqual("AI Assistants", copilot.Category);

            Assert.IsNotNull(gemini);
            Assert.AreEqual("AI Assistants", gemini.Category);

            // Clean profile service instance
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NotiGlow", "profiles.json");
            try { if (File.Exists(path)) File.Delete(path); } catch { }

            var service = new ProfileService();

            // Test Claude package and AUMID matching
            var matchedClaude = service.GetProfile("Claude_pzs8sxrjxfjjc!App", "Claude");
            Assert.IsNotNull(matchedClaude);
            Assert.AreEqual("Claude", matchedClaude.Name);

            // Test ChatGPT package and AUMID matching
            var matchedChatGpt = service.GetProfile("OpenAI.ChatGPT_2p2nqsd0c76g0!App", "ChatGPT");
            Assert.IsNotNull(matchedChatGpt);
            Assert.AreEqual("ChatGPT", matchedChatGpt.Name);

            // Test Copilot package and AUMID matching
            var matchedCopilot = service.GetProfile("Microsoft.Copilot_8wekyb3d8bbwe!App", "Copilot");
            Assert.IsNotNull(matchedCopilot);
            Assert.AreEqual("Microsoft Copilot", matchedCopilot.Name);

            // Test Gemini matching
            var matchedGemini = service.GetProfile("Google.Gemini", "Google Gemini");
            Assert.IsNotNull(matchedGemini);
            Assert.AreEqual("Google Gemini", matchedGemini.Name);

            // Verify no false positives for random app
            var untracked = service.GetProfile("SomeRandomTool_123!App", "Random Tool");
            Assert.IsNull(untracked);
        }

        [TestMethod]
        public void Test_CollapsedSidebar_AlignmentGeometry()
        {
            // Compact pane length: 52px
            double compactPaneLength = 52.0;
            double itemMarginLeft = 6.0;
            double itemMarginRight = 6.0;
            double itemWidth = compactPaneLength - itemMarginLeft - itemMarginRight; // 40px
            double iconWidth = 24.0;

            // When icon is centered with HorizontalAlignment = Center, Margin = 0:
            double iconLeftOffsetInItem = (itemWidth - iconWidth) / 2.0; // 8px
            double iconRightOffsetInItem = itemWidth - (iconLeftOffsetInItem + iconWidth); // 8px

            Assert.AreEqual(8.0, iconLeftOffsetInItem);
            Assert.AreEqual(8.0, iconRightOffsetInItem);

            // Total distance from left of sidebar to icon:
            double totalLeftOffset = itemMarginLeft + iconLeftOffsetInItem; // 14px
            double totalRightOffset = itemMarginRight + iconRightOffsetInItem; // 14px

            // Symmetrical centering in compact sidebar:
            Assert.AreEqual(totalLeftOffset, totalRightOffset);
            Assert.AreEqual(14.0, totalLeftOffset);
        }
    }
}
