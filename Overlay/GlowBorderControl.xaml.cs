using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotiGlow.Models;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace NotiGlow.Overlay
{
    public partial class GlowBorderControl : UserControl
    {
        private Storyboard? _currentStoryboard;
        private Action? _onCompletedCallback;

        public GlowBorderControl()
        {
            InitializeComponent();
            Opacity = 0;
        }

        public void ApplyProfile(AppProfile profile, Action? onCompleted = null)
        {
            _onCompletedCallback = onCompleted;
            StopAnimation();

            bool isRgb = NotiGlow.Core.Helpers.ColorHelper.IsRgbSpectrum(profile.ColorHex);
            Color mainColor = isRgb ? Color.FromRgb(255, 0, 77) : NotiGlow.Core.Helpers.ColorHelper.ParseColor(profile.ColorHex);
            Color transparentColor = Color.FromArgb(0, mainColor.R, mainColor.G, mainColor.B);

            // Update edge sizes & bloom layers
            TopEdge.Height = profile.GlowSize;
            BottomEdge.Height = profile.GlowSize;
            LeftEdge.Width = profile.GlowSize;
            RightEdge.Width = profile.GlowSize;
            InnerBorder.BorderThickness = new Thickness(profile.Thickness);

            if (isRgb)
            {
                InnerBorder.BorderBrush = NotiGlow.Core.Helpers.ColorHelper.CreateRainbowLinearBrush(new Point(0, 0), new Point(1, 1));
                SpillBrush.Color = Color.FromRgb(124, 58, 237);
            }
            else
            {
                InnerBorder.BorderBrush = new SolidColorBrush(mainColor);
                SpillBrush.Color = mainColor;
            }

            // Update Gradient Colors
            TopStop0.Color = mainColor;
            TopStop1.Color = transparentColor;

            BottomStop0.Color = mainColor;
            BottomStop1.Color = transparentColor;

            LeftStop0.Color = mainColor;
            LeftStop1.Color = transparentColor;

            RightStop0.Color = mainColor;
            RightStop1.Color = transparentColor;

            double targetOpacity = Math.Clamp(profile.Intensity, 0.05, 1.0);
            int duration = Math.Max(500, (int)(profile.DurationMs / Math.Max(0.5, profile.Speed)));

            StartStyleAnimation(profile.Style, targetOpacity, duration, mainColor, transparentColor, isRgb);
        }

        private void StartStyleAnimation(GlowStyle style, double maxOpacity, int durationMs, Color mainColor, Color transparentColor, bool isRgb)
        {
            _currentStoryboard = new Storyboard();
            Duration duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

            SweepOverlay.Visibility = Visibility.Collapsed;
            CometOverlay.Visibility = Visibility.Collapsed;
            RippleOverlay.Visibility = Visibility.Collapsed;

            if (NotiGlow.UI.Animations.MotionPolicy.IsReduceMotion)
            {
                // Reduce Motion: Static clean glow without motion/traveling animations
                BaseGlowLayer.Opacity = 1.0;
                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.10)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.90)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                _currentStoryboard.Completed += OnStoryboardCompleted;
                _currentStoryboard.Begin();
                return;
            }

            if (style == GlowStyle.Pulse)
            {
                BaseGlowLayer.Opacity = 1.0;

                DoubleAnimationUsingKeyFrames keyFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.15), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.25, KeyTime.FromPercent(0.35), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.55), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.25, KeyTime.FromPercent(0.75), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.9, KeyTime.FromPercent(0.88), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseIn }));

                Storyboard.SetTarget(keyFrames, this);
                Storyboard.SetTargetProperty(keyFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Ambient)
            {
                BaseGlowLayer.Opacity = 1.0;

                DoubleAnimationUsingKeyFrames keyFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.85, KeyTime.FromPercent(0.30), new SineEase { EasingMode = EasingMode.EaseOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.70, KeyTime.FromPercent(0.70), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0), new SineEase { EasingMode = EasingMode.EaseIn }));

                Storyboard.SetTarget(keyFrames, this);
                Storyboard.SetTargetProperty(keyFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Sweep)
            {
                BaseGlowLayer.Opacity = 0.0; // Hide static base layer to remove artificial edge stripes
                SweepOverlay.Visibility = Visibility.Visible;
                SweepOverlay.BorderThickness = new Thickness(Math.Max(8, InnerBorder.BorderThickness.Left * 3));

                SweepGradientBrush.GradientStops.Clear();
                if (isRgb)
                {
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 0.0));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.15));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.35));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.50));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 0.65));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(124, 58, 237), 0.85));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 1.0));
                }
                else
                {
                    SweepGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, mainColor.R, mainColor.G, mainColor.B), 0.5));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.10)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.90)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Continuous fast perimeter travel loop
                PointAnimation startPointAnim = new PointAnimation
                {
                    From = new Point(-0.5, -0.5),
                    To = new Point(1.5, 1.5),
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Min(1500, durationMs / 2))),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(startPointAnim, SweepGradientBrush);
                Storyboard.SetTargetProperty(startPointAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(startPointAnim);

                PointAnimation endPointAnim = new PointAnimation
                {
                    From = new Point(0.0, 0.0),
                    To = new Point(2.0, 2.0),
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Min(1500, durationMs / 2))),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(endPointAnim, SweepGradientBrush);
                Storyboard.SetTargetProperty(endPointAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(endPointAnim);
            }
            else if (style == GlowStyle.Comet)
            {
                BaseGlowLayer.Opacity = 0.0; // Hide static base layer to remove artificial edge stripes
                CometOverlay.Visibility = Visibility.Visible;
                CometOverlay.BorderThickness = new Thickness(Math.Max(10, InnerBorder.BorderThickness.Left * 3.5));

                CometGradientBrush.GradientStops.Clear();
                if (isRgb)
                {
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.0));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.25));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(41, 121, 255), 0.50));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.65));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.80));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.90));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.96));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
                }
                else
                {
                    CometGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    CometGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.3));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(160, mainColor.R, mainColor.G, mainColor.B), 0.7));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.92));
                    CometGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.08)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.92)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Traveling Comet Head back & forth with speed
                PointAnimation cometStartAnim = new PointAnimation
                {
                    From = new Point(0, 0),
                    To = new Point(1, 1),
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Min(1200, durationMs * 0.4))),
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true
                };
                Storyboard.SetTarget(cometStartAnim, CometGradientBrush);
                Storyboard.SetTargetProperty(cometStartAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(cometStartAnim);

                PointAnimation cometEndAnim = new PointAnimation
                {
                    From = new Point(0.3, 0.1),
                    To = new Point(1.3, 1.1),
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Min(1200, durationMs * 0.4))),
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true
                };
                Storyboard.SetTarget(cometEndAnim, CometGradientBrush);
                Storyboard.SetTargetProperty(cometEndAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(cometEndAnim);
            }
            else if (style == GlowStyle.Ripple)
            {
                BaseGlowLayer.Opacity = 0.0; // Hide static base layer for clean ripple wave
                RippleOverlay.Visibility = Visibility.Visible;
                RippleOverlay.BorderThickness = new Thickness(Math.Max(12, TopEdge.Height * 1.5));

                RippleGradientBrush.GradientStops.Clear();
                if (isRgb)
                {
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.0));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(124, 58, 237), 0.25));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 0.45));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.60));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.75));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.90));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 1.0));
                }
                else
                {
                    RippleGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, mainColor.R, mainColor.G, mainColor.B), 0.5));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.12)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity * 0.8, KeyTime.FromPercent(0.70)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Aspect ratio compensation for perfectly round shockwave reaching all 4 corners simultaneously
                double screenW = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
                double screenH = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
                double aspect = screenW / Math.Max(1.0, screenH);

                DoubleAnimation rippleRadiusXAnim = new DoubleAnimation
                {
                    From = 0.02,
                    To = 1.3,
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Min(1400, durationMs * 0.5))),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleRadiusXAnim, RippleGradientBrush);
                Storyboard.SetTargetProperty(rippleRadiusXAnim, new PropertyPath(RadialGradientBrush.RadiusXProperty));
                _currentStoryboard.Children.Add(rippleRadiusXAnim);

                DoubleAnimation rippleRadiusYAnim = new DoubleAnimation
                {
                    From = 0.02 * aspect,
                    To = 1.3 * aspect,
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Min(1400, durationMs * 0.5))),
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleRadiusYAnim, RippleGradientBrush);
                Storyboard.SetTargetProperty(rippleRadiusYAnim, new PropertyPath(RadialGradientBrush.RadiusYProperty));
                _currentStoryboard.Children.Add(rippleRadiusYAnim);
            }

            _currentStoryboard.Completed += OnStoryboardCompleted;
            _currentStoryboard.Begin();
        }

        private void OnStoryboardCompleted(object? sender, EventArgs e)
        {
            StopAnimation();
            _onCompletedCallback?.Invoke();
        }

        public void StopAnimation()
        {
            if (_currentStoryboard != null)
            {
                _currentStoryboard.Completed -= OnStoryboardCompleted;
                _currentStoryboard.Stop();
                _currentStoryboard = null;
            }
            Opacity = 0;
            SweepOverlay.Visibility = Visibility.Collapsed;
            CometOverlay.Visibility = Visibility.Collapsed;
            RippleOverlay.Visibility = Visibility.Collapsed;
            BaseGlowLayer.Opacity = 1.0;
        }
    }
}
