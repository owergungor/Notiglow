using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GlowBorder.Models;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace GlowBorder.UI.Controls
{
    public partial class EdgePreviewControl : UserControl
    {
        public EdgePreviewControl()
        {
            InitializeComponent();
        }

        public void UpdatePreview(AppProfile profile)
        {
            UpdatePreview(profile.ColorHex, profile.Thickness, profile.GlowSize, profile.Intensity);
        }

        public void UpdatePreview(string colorHex, double thickness, double glowSize, double intensity)
        {
            Color mainColor = GlowBorder.Core.Helpers.ColorHelper.ParseColor(colorHex);

            // Scale parameters for mini preview box
            double scaledGlow = Math.Clamp(glowSize / 4.0, 5, 30);
            double scaledThickness = Math.Clamp(thickness / 2.0, 1, 6);
            double opacityVal = Math.Clamp(intensity, 0.1, 1.0);

            Color transparentColor = Color.FromArgb(0, mainColor.R, mainColor.G, mainColor.B);
            Color adjustedColor = Color.FromArgb((byte)(255 * opacityVal), mainColor.R, mainColor.G, mainColor.B);

            PrevTopEdge.Height = scaledGlow;
            PrevBottomEdge.Height = scaledGlow;
            PrevLeftEdge.Width = scaledGlow;
            PrevRightEdge.Width = scaledGlow;

            PrevInnerBorder.BorderThickness = new Thickness(scaledThickness);

            PTop0.Color = adjustedColor;
            PTop1.Color = transparentColor;

            PBottom0.Color = adjustedColor;
            PBottom1.Color = transparentColor;

            PLeft0.Color = adjustedColor;
            PLeft1.Color = transparentColor;

            PRight0.Color = adjustedColor;
            PRight1.Color = transparentColor;

            PInnerBrush.Color = adjustedColor;
        }
    }
}
