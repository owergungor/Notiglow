using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotiGlow.Models;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace NotiGlow.UI.Controls
{
    public partial class EdgePreviewControl : UserControl
    {
        private Storyboard? _previewStoryboard;

        public EdgePreviewControl()
        {
            InitializeComponent();
        }

        public void UpdatePreview(AppProfile profile)
        {
            UpdatePreview(profile.ColorHex, profile.Thickness, profile.GlowSize, profile.Intensity, profile.Style);
        }

        public void UpdatePreview(string colorHex, double thickness, double glowSize, double intensity, GlowStyle style = GlowStyle.Pulse)
        {
            Color mainColor = NotiGlow.Core.Helpers.ColorHelper.ParseColor(colorHex);

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

            // Stop previous preview animation
            if (_previewStoryboard != null)
            {
                _previewStoryboard.Stop();
                _previewStoryboard = null;
            }

            PrevSweepOverlay.Visibility = Visibility.Collapsed;
            PrevCometOverlay.Visibility = Visibility.Collapsed;
            PrevRippleOverlay.Visibility = Visibility.Collapsed;
            PrevBaseGlowLayer.Opacity = 1.0;

            _previewStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

            if (style == GlowStyle.Pulse)
            {
                PrevBaseGlowLayer.Opacity = 1.0;
                var keyFrames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(2.0) };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0.0)));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.2, KeyTime.FromPercent(0.35), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.65), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(keyFrames, PrevBaseGlowLayer);
                Storyboard.SetTargetProperty(keyFrames, new PropertyPath(UIElement.OpacityProperty));
                _previewStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Ambient)
            {
                PrevBaseGlowLayer.Opacity = 1.0;
                var keyFrames = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(3.0) };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.0)));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.45, KeyTime.FromPercent(0.50), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromPercent(1.0), new SineEase { EasingMode = EasingMode.EaseInOut }));

                Storyboard.SetTarget(keyFrames, PrevBaseGlowLayer);
                Storyboard.SetTargetProperty(keyFrames, new PropertyPath(UIElement.OpacityProperty));
                _previewStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Sweep)
            {
                PrevBaseGlowLayer.Opacity = 0.25;
                PrevSweepOverlay.Visibility = Visibility.Visible;
                PrevSweepOverlay.BorderThickness = new Thickness(Math.Max(3, scaledThickness * 2));
                PSweep0.Color = transparentColor;
                PSweep1.Color = Color.FromArgb(255, mainColor.R, mainColor.G, mainColor.B);
                PSweep2.Color = transparentColor;

                var startAnim = new PointAnimation
                {
                    From = new Point(-0.5, -0.5),
                    To = new Point(1.5, 1.5),
                    Duration = TimeSpan.FromSeconds(1.5)
                };
                Storyboard.SetTarget(startAnim, PrevSweepBrush);
                Storyboard.SetTargetProperty(startAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _previewStoryboard.Children.Add(startAnim);

                var endAnim = new PointAnimation
                {
                    From = new Point(0.0, 0.0),
                    To = new Point(2.0, 2.0),
                    Duration = TimeSpan.FromSeconds(1.5)
                };
                Storyboard.SetTarget(endAnim, PrevSweepBrush);
                Storyboard.SetTargetProperty(endAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _previewStoryboard.Children.Add(endAnim);
            }
            else if (style == GlowStyle.Comet)
            {
                PrevBaseGlowLayer.Opacity = 0.15;
                PrevCometOverlay.Visibility = Visibility.Visible;
                PrevCometOverlay.BorderThickness = new Thickness(Math.Max(3, scaledThickness * 2));
                PComet0.Color = transparentColor;
                PComet1.Color = Color.FromArgb(180, mainColor.R, mainColor.G, mainColor.B);
                PComet2.Color = Color.FromArgb(255, 255, 255, 255);

                var cometAnim = new PointAnimation
                {
                    From = new Point(0, 0),
                    To = new Point(1, 1),
                    Duration = TimeSpan.FromSeconds(1.2),
                    AutoReverse = true
                };
                Storyboard.SetTarget(cometAnim, PrevCometBrush);
                Storyboard.SetTargetProperty(cometAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _previewStoryboard.Children.Add(cometAnim);
            }
            else if (style == GlowStyle.Ripple)
            {
                PrevBaseGlowLayer.Opacity = 0.20;
                PrevRippleOverlay.Visibility = Visibility.Visible;
                PRipple0.Color = Color.FromArgb(255, mainColor.R, mainColor.G, mainColor.B);
                PRipple1.Color = Color.FromArgb(120, mainColor.R, mainColor.G, mainColor.B);
                PRipple2.Color = transparentColor;

                var rippleAnim = new DoubleAnimation
                {
                    From = 0.05,
                    To = 1.3,
                    Duration = TimeSpan.FromSeconds(1.5),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleAnim, PrevRippleBrush);
                Storyboard.SetTargetProperty(rippleAnim, new PropertyPath(RadialGradientBrush.RadiusXProperty));
                _previewStoryboard.Children.Add(rippleAnim);
            }

            _previewStoryboard.Begin();
        }
    }
}
