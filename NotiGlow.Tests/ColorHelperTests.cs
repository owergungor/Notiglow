using System.Windows.Media;
using NotiGlow.Core.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Color = System.Windows.Media.Color;

namespace NotiGlow.Tests
{
    [TestClass]
    public class ColorHelperTests
    {
        [DataTestMethod]
        [DataRow("#5865F2", (byte)88, (byte)101, (byte)242)]
        [DataRow("5865F2", (byte)88, (byte)101, (byte)242)]
        [DataRow("#FF5865F2", (byte)88, (byte)101, (byte)242)]
        [DataRow("rgb(88, 101, 242)", (byte)88, (byte)101, (byte)242)]
        [DataRow("rgb(255,0,128)", (byte)255, (byte)0, (byte)128)]
        public void ParseColor_ValidFormats_ReturnsCorrectRgb(string input, byte expectedR, byte expectedG, byte expectedB)
        {
            Color result = ColorHelper.ParseColor(input);
            Assert.AreEqual(expectedR, result.R);
            Assert.AreEqual(expectedG, result.G);
            Assert.AreEqual(expectedB, result.B);
        }

        [TestMethod]
        public void ParseColor_InvalidInput_ReturnsFallbackColor()
        {
            Color result = ColorHelper.ParseColor("invalid_color_xyz");
            Assert.AreEqual((byte)88, result.R);
            Assert.AreEqual((byte)101, result.G);
            Assert.AreEqual((byte)242, result.B);
        }

        [TestMethod]
        public void ToCanonicalHex_ReturnsHexFormat()
        {
            Color color = Color.FromRgb(37, 211, 102);
            string hex = ColorHelper.ToCanonicalHex(color);
            Assert.AreEqual("#25D366", hex);
        }
    }
}
