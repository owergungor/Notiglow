using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NotiGlow.Tests
{
    [TestClass]
    public class ColorPickerDragTests
    {
        [TestMethod]
        public void HuePositionMapsAcrossAndClampsToHueStrip()
        {
            Assert.AreEqual(0, NotiGlow.UI.Controls.ColorPickerControl.HueFromPosition(0, 200));
            Assert.AreEqual(180, NotiGlow.UI.Controls.ColorPickerControl.HueFromPosition(100, 200));
            Assert.AreEqual(360, NotiGlow.UI.Controls.ColorPickerControl.HueFromPosition(200, 200));
            Assert.AreEqual(0, NotiGlow.UI.Controls.ColorPickerControl.HueFromPosition(-20, 200));
            Assert.AreEqual(360, NotiGlow.UI.Controls.ColorPickerControl.HueFromPosition(240, 200));
        }

        [TestMethod]
        public void SatValPositionMapsAndClampsToColorField()
        {
            var center = NotiGlow.UI.Controls.ColorPickerControl.SatValFromPosition(new Point(125, 65), 250, 130);
            Assert.AreEqual(0.5, center.Saturation, 0.001);
            Assert.AreEqual(0.5, center.Value, 0.001);

            var outside = NotiGlow.UI.Controls.ColorPickerControl.SatValFromPosition(new Point(-10, 150), 250, 130);
            Assert.AreEqual(0, outside.Saturation, 0.001);
            Assert.AreEqual(0, outside.Value, 0.001);
        }
    }
}
