
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
        private bool _isAnimationPlaying = false;

        public EdgePreviewControl()
        {
            InitializeComponent();
            Loaded += (s, e) => UpdateAnimationPlayback();
            Unloaded += (s, e) => StopAnimationPlayback();
            IsVisibleChanged += (s, e) => UpdateAnimationPlayback();
        }

        private void UpdateAnimationPlayback()
        {
            if (IsLoaded && IsVisible)
            {
                StartAnimationPlayback();
            }
            else
            {
                StopAnimationPlayback();
            }
        }

        private void StartAnimationPlayback()
        {
            if (_previewStoryboard != null && !_isAnimationPlaying)
            {
                _previewStoryboard.Begin();
                _isAnimationPlaying = true;
            }
        }

        private void StopAnimationPlayback()
        {
            if (_previewStoryboard != null && _isAnimationPlaying)
            {
                _previewStoryboard.Stop();
                _isAnimationPlaying = false;
            }
        }

        public void UpdatePreview(AppProfile profile)
        {
            UpdatePreview(profile.ColorHex, profile.Thickness, profile.GlowSize, profile.Intensity, profile.Style);
        }

        public void UpdatePreview(string colorHex, double thickness, double glowSize, double intensity, GlowStyle style = GlowStyle.Pulse)
        {
            bool isRgb = NotiGlow.Core.Helpers.ColorHelper.IsRgbSpectrum(colorHex);
            Color mainColor = isRgb ? Color.FromRgb(255, 0, 77) : NotiGlow.Core.Helpers.ColorHelper.ParseColor(colorHex);

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

            if (isRgb)
            {
                PrevInnerBorder.BorderBrush = NotiGlow.Core.Helpers.ColorHelper.CreateRainbowLinearBrush(new Point(0, 0), new Point(1, 1));
            }
            else
            {
                PrevInnerBorder.BorderBrush = new SolidColorBrush(adjustedColor);
            }

            PTop0.Color = adjustedColor;
            PTop1.Color = transparentColor;

            PBottom0.Color = adjustedColor;
            PBottom1.Color = transparentColor;

            PLeft0.Color = adjustedColor;
            PLeft1.Color = transparentColor;

            PRight0.Color = adjustedColor;
            PRight1.Color = transparentColor;

            // Stop previous preview animation
            if (_previewStoryboard != null)
            {
                _previewStoryboard.Stop();
                _previewStoryboard = null;
                _isAnimationPlaying = false;
            }

            PrevSweepOverlay.Visibility = Visibility.Collapsed;
            PrevCometOverlay.Visibility = Visibility.Collapsed;
            PrevRippleOverlay.Visibility = Visibility.Collapsed;
            PrevBaseGlowLayer.Opacity = 1.0;

            if (NotiGlow.UI.Animations.MotionPolicy.IsReduceMotion)
            {
                // Static preview under Reduce Motion
                PrevBaseGlowLayer.Opacity = opacityVal;
                return;
            }

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
                PrevBaseGlowLayer.Opacity = 0.0;
                PrevSweepOverlay.Visibility = Visibility.Visible;
                PrevSweepOverlay.BorderThickness = new Thickness(Math.Max(3, scaledThickness * 2));

                PrevSweepBrush.GradientStops.Clear();
                if (isRgb)
                {
                    PrevSweepBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 0.0));
                    PrevSweepBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.2));
                    PrevSweepBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.5));
                    PrevSweepBrush.GradientStops.Add(new GradientStop(Color.FromRgb(124, 58, 237), 0.8));
                    PrevSweepBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 1.0));
                }
                else
                {
                    PrevSweepBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    PrevSweepBrush.GradientStops.Add(new GradientStop(adjustedColor, 0.5));
                    PrevSweepBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

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
                PrevBaseGlowLayer.Opacity = 0.0;
                PrevCometOverlay.Visibility = Visibility.Visible;
                PrevCometOverlay.BorderThickness = new Thickness(Math.Max(3, scaledThickness * 2));

                PrevCometBrush.GradientStops.Clear();
                if (isRgb)
                {
                    PrevCometBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.0));
                    PrevCometBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.25));
                    PrevCometBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.60));
                    PrevCometBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.85));
                    PrevCometBrush.GradientStops.Add(new GradientStop(Colors.White, 0.94));
                    PrevCometBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
                }
                else
                {
                    PrevCometBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    PrevCometBrush.GradientStops.Add(new GradientStop(transparentColor, 0.3));
                    PrevCometBrush.GradientStops.Add(new GradientStop(Color.FromArgb(160, mainColor.R, mainColor.G, mainColor.B), 0.7));
                    PrevCometBrush.GradientStops.Add(new GradientStop(Colors.White, 0.92));
                    PrevCometBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

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
                PrevBaseGlowLayer.Opacity = 0.0;
                PrevRippleOverlay.Visibility = Visibility.Visible;

                PrevRippleBrush.GradientStops.Clear();
                if (isRgb)
                {
                    PrevRippleBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.0));
                    PrevRippleBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 0.35));
                    PrevRippleBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.55));
                    PrevRippleBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.80));
                    PrevRippleBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 1.0));
                }
                else
                {
                    PrevRippleBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    PrevRippleBrush.GradientStops.Add(new GradientStop(adjustedColor, 0.5));
                    PrevRippleBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

                double aspect = ActualWidth > 0 && ActualHeight > 0 ? ActualWidth / ActualHeight : 1.77;

                var rippleAnimX = new DoubleAnimation
                {
                    From = 0.05,
                    To = 1.3,
                    Duration = TimeSpan.FromSeconds(1.5),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleAnimX, PrevRippleBrush);
                Storyboard.SetTargetProperty(rippleAnimX, new PropertyPath(RadialGradientBrush.RadiusXProperty));
                _previewStoryboard.Children.Add(rippleAnimX);

                var rippleAnimY = new DoubleAnimation
                {
                    From = 0.05 * aspect,
                    To = 1.3 * aspect,
                    Duration = TimeSpan.FromSeconds(1.5),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleAnimY, PrevRippleBrush);
                Storyboard.SetTargetProperty(rippleAnimY, new PropertyPath(RadialGradientBrush.RadiusYProperty));
                _previewStoryboard.Children.Add(rippleAnimY);
            }

            UpdateAnimationPlayback();
        }
    }
}
