using System;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;

namespace NotiGlow.UI.Controls
{
    public sealed partial class EdgePreviewControl : UserControl
    {
        public EdgePreviewControl()
        {
            this.InitializeComponent();
        }

        public void UpdatePreview(AppProfile profile)
        {
            UpdatePreview(profile.ColorHex, profile.Thickness, profile.GlowSize, profile.Intensity, profile.Style, profile.CornerRadius);
        }

        public void UpdatePreview(string colorHex, double thickness, double glowSize, double intensity, GlowStyle style = GlowStyle.Pulse, double cornerRadius = 0.0)
        {
            bool isRgb = ColorHelper.IsRgbSpectrum(colorHex);
            Color mainColor = isRgb ? Color.FromArgb(255, 255, 0, 77) : ColorHelper.ParseColor(colorHex);
            Color transparentColor = Color.FromArgb(0, mainColor.R, mainColor.G, mainColor.B);

            double scaledGlow = Math.Clamp(glowSize / 4.0, 0, 30);
            double scaledThickness = Math.Clamp(thickness / 2.0, 1, 6);
            double opacityVal = Math.Clamp(intensity, 0.1, 1.0);

            PrevTopEdge.Height = scaledGlow;
            PrevBottomEdge.Height = scaledGlow;
            PrevLeftEdge.Width = scaledGlow;
            PrevRightEdge.Width = scaledGlow;

            PrevInnerBorder.BorderThickness = new Thickness(scaledThickness);

            double scaledRadius = Math.Clamp(cornerRadius / 3.0, 0, 16);
            PrevInnerBorder.CornerRadius = new CornerRadius(scaledRadius);

            PInnerBrush.Color = mainColor;
            PTop0.Color = mainColor;
            PTop1.Color = transparentColor;
            PBottom0.Color = mainColor;
            PBottom1.Color = transparentColor;
            PLeft0.Color = mainColor;
            PLeft1.Color = transparentColor;
            PRight0.Color = mainColor;
            PRight1.Color = transparentColor;

            PrevBaseGlowLayer.Opacity = opacityVal;
        }
    }
}
