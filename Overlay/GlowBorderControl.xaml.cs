using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotiGlow.Models;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
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

            LinearGradientBrush? sharedRgbBrush = null;
            if (isRgb)
            {
                // Full continuous RGB spectrum applied to inner border, ambient spill, and all edge blooms
                sharedRgbBrush = NotiGlow.Core.Helpers.ColorHelper.CreateRainbowLinearBrush(new Point(0, 0), new Point(1, 1));
                sharedRgbBrush.SpreadMethod = GradientSpreadMethod.Repeat;

                InnerBorder.BorderBrush = sharedRgbBrush;
                AmbientSpillBorder.BorderBrush = sharedRgbBrush;

                // For edges, fill with the full RGB spectrum brush and use OpacityMask for edge blooming
                TopEdge.Fill = sharedRgbBrush;
                TopEdge.OpacityMask = new LinearGradientBrush(Color.FromArgb(255, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), new Point(0.5, 0), new Point(0.5, 1));

                BottomEdge.Fill = sharedRgbBrush;
                BottomEdge.OpacityMask = new LinearGradientBrush(Color.FromArgb(255, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), new Point(0.5, 1), new Point(0.5, 0));

                LeftEdge.Fill = sharedRgbBrush;
                LeftEdge.OpacityMask = new LinearGradientBrush(Color.FromArgb(255, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), new Point(0, 0.5), new Point(1, 0.5));

                RightEdge.Fill = sharedRgbBrush;
                RightEdge.OpacityMask = new LinearGradientBrush(Color.FromArgb(255, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), new Point(1, 0.5), new Point(0, 0.5));
            }
            else
            {
                InnerBorder.BorderBrush = new SolidColorBrush(mainColor);
                SpillBrush.Color = mainColor;
                AmbientSpillBorder.BorderBrush = SpillBrush;

                TopEdge.OpacityMask = null;
                BottomEdge.OpacityMask = null;
                LeftEdge.OpacityMask = null;
                RightEdge.OpacityMask = null;

                TopEdge.Fill = TopGradientBrush;
                BottomEdge.Fill = BottomGradientBrush;
                LeftEdge.Fill = LeftGradientBrush;
                RightEdge.Fill = RightGradientBrush;

                TopStop0.Color = mainColor;
                TopStop1.Color = transparentColor;

                BottomStop0.Color = mainColor;
                BottomStop1.Color = transparentColor;

                LeftStop0.Color = mainColor;
                LeftStop1.Color = transparentColor;

                RightStop0.Color = mainColor;
                RightStop1.Color = transparentColor;
            }

            double targetOpacity = Math.Clamp(profile.Intensity, 0.05, 1.0);
            int duration = Math.Max(500, (int)(profile.DurationMs / Math.Max(0.5, profile.Speed)));

            StartStyleAnimation(profile.Style, targetOpacity, duration, mainColor, transparentColor, isRgb, sharedRgbBrush, profile);
        }

        private void StartStyleAnimation(GlowStyle style, double maxOpacity, int durationMs, Color mainColor, Color transparentColor, bool isRgb, LinearGradientBrush? rgbBrush, AppProfile profile)
        {
            _currentStoryboard = new Storyboard();
            Duration duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

            SweepContainer.Visibility = Visibility.Collapsed;
            CometContainer.Visibility = Visibility.Collapsed;
            RippleOverlay.Visibility = Visibility.Collapsed;

            // Reduce Motion: Static clean glow without motion / traveling / cycle animations
            if (NotiGlow.UI.Animations.MotionPolicy.IsReduceMotion)
            {
                BaseGlowLayer.Opacity = 1.0;
                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.12)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.88)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                _currentStoryboard.Completed += OnStoryboardCompleted;
                _currentStoryboard.Begin();
                return;
            }

            // Animate RGB continuous spectrum transition if active
            if (isRgb && rgbBrush != null)
            {
                PointAnimation rgbStartAnim = new PointAnimation
                {
                    From = new Point(0, 0),
                    To = new Point(1, 1),
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Max(1200, durationMs * 0.75))),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(rgbStartAnim, rgbBrush);
                Storyboard.SetTargetProperty(rgbStartAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(rgbStartAnim);

                PointAnimation rgbEndAnim = new PointAnimation
                {
                    From = new Point(1, 1),
                    To = new Point(2, 2),
                    Duration = new Duration(TimeSpan.FromMilliseconds(Math.Max(1200, durationMs * 0.75))),
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(rgbEndAnim, rgbBrush);
                Storyboard.SetTargetProperty(rgbEndAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(rgbEndAnim);
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
                BaseGlowLayer.Opacity = 0.20; // Soft ambient baseline
                SweepContainer.Visibility = Visibility.Visible;
                SweepOverlay.BorderThickness = new Thickness(Math.Max(8, profile.Thickness * 2.2));
                SweepBloomBorder.BorderThickness = new Thickness(Math.Max(26, profile.GlowSize));

                SweepGradientBrush.GradientStops.Clear();
                SweepBloomBrush.GradientStops.Clear();

                if (isRgb)
                {
                    // Broad rich rainbow sweep beam (no static red fallback)
                    Color[] rainbow = NotiGlow.Core.Helpers.ColorHelper.RgbSpectrumColors;
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 0.0));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 255, 0, 77), 0.15));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.35));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.50));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 0.65));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(140, 124, 58, 237), 0.85));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 1.0));

                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 0.0));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, 255, 0, 77), 0.20));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(180, 0, 230, 118), 0.50));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, 124, 58, 237), 0.80));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 1.0));
                }
                else
                {
                    // Wide luminous light sweep beam: dark -> soft -> vibrant -> bright core -> vibrant -> soft -> dark
                    SweepGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(60, mainColor.R, mainColor.G, mainColor.B), 0.20));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, mainColor.R, mainColor.G, mainColor.B), 0.40));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.50));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, mainColor.R, mainColor.G, mainColor.B), 0.60));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(60, mainColor.R, mainColor.G, mainColor.B), 0.80));
                    SweepGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));

                    SweepBloomBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, mainColor.R, mainColor.G, mainColor.B), 0.30));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(180, mainColor.R, mainColor.G, mainColor.B), 0.50));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, mainColor.R, mainColor.G, mainColor.B), 0.70));
                    SweepBloomBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.10)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.90)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Continuous wide diagonal beam sweep loop
                int sweepCycleMs = Math.Max(1000, (int)(1500 / Math.Max(0.5, profile.Speed)));
                Duration sweepCycleDuration = new Duration(TimeSpan.FromMilliseconds(sweepCycleMs));

                PointAnimation sweepStartAnim = new PointAnimation
                {
                    From = new Point(-0.8, -0.8),
                    To = new Point(1.6, 1.6),
                    Duration = sweepCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(sweepStartAnim, SweepGradientBrush);
                Storyboard.SetTargetProperty(sweepStartAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(sweepStartAnim);

                PointAnimation sweepEndAnim = new PointAnimation
                {
                    From = new Point(-0.2, -0.2),
                    To = new Point(2.2, 2.2),
                    Duration = sweepCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(sweepEndAnim, SweepGradientBrush);
                Storyboard.SetTargetProperty(sweepEndAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(sweepEndAnim);

                PointAnimation bloomStartAnim = new PointAnimation
                {
                    From = new Point(-0.8, -0.8),
                    To = new Point(1.6, 1.6),
                    Duration = sweepCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(bloomStartAnim, SweepBloomBrush);
                Storyboard.SetTargetProperty(bloomStartAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(bloomStartAnim);

                PointAnimation bloomEndAnim = new PointAnimation
                {
                    From = new Point(-0.2, -0.2),
                    To = new Point(2.2, 2.2),
                    Duration = sweepCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(bloomEndAnim, SweepBloomBrush);
                Storyboard.SetTargetProperty(bloomEndAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(bloomEndAnim);
            }
            else if (style == GlowStyle.Comet)
            {
                BaseGlowLayer.Opacity = 0.20; // Soft ambient baseline
                CometContainer.Visibility = Visibility.Visible;
                CometOverlay.BorderThickness = new Thickness(Math.Max(8, profile.Thickness * 2.2));
                CometBloomBorder.BorderThickness = new Thickness(Math.Max(28, profile.GlowSize * 1.1));

                CometGradientBrush.GradientStops.Clear();
                CometBloomBrush.GradientStops.Clear();

                if (isRgb)
                {
                    // Luminous RGB comet: visible bright head + trailing shifting rainbow tail
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 0.0));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(60, 124, 58, 237), 0.25));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(160, 41, 121, 255), 0.50));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.72));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.86));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.94));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.98));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));

                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 0.0));
                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, 41, 121, 255), 0.50));
                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(180, 255, 214, 0), 0.85));
                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(240, 255, 0, 77), 0.96));
                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 1.0));
                }
                else
                {
                    // Prominent bright comet head + rich glowing tail in theme color
                    CometGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(30, mainColor.R, mainColor.G, mainColor.B), 0.25));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(120, mainColor.R, mainColor.G, mainColor.B), 0.55));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(220, mainColor.R, mainColor.G, mainColor.B), 0.82));
                    CometGradientBrush.GradientStops.Add(new GradientStop(mainColor, 0.93));
                    CometGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.98));
                    CometGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));

                    CometBloomBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(60, mainColor.R, mainColor.G, mainColor.B), 0.40));
                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(180, mainColor.R, mainColor.G, mainColor.B), 0.80));
                    CometBloomBrush.GradientStops.Add(new GradientStop(Color.FromArgb(230, mainColor.R, mainColor.G, mainColor.B), 0.96));
                    CometBloomBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.08)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.92)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Comet head sweeps across perimeter back and forth with speed
                int cometCycleMs = Math.Max(900, (int)(1300 / Math.Max(0.5, profile.Speed)));
                Duration cometCycleDuration = new Duration(TimeSpan.FromMilliseconds(cometCycleMs));

                PointAnimation cometStartAnim = new PointAnimation
                {
                    From = new Point(-0.4, -0.4),
                    To = new Point(1.2, 1.2),
                    Duration = cometCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(cometStartAnim, CometGradientBrush);
                Storyboard.SetTargetProperty(cometStartAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(cometStartAnim);

                PointAnimation cometEndAnim = new PointAnimation
                {
                    From = new Point(0.4, 0.2),
                    To = new Point(1.8, 1.6),
                    Duration = cometCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(cometEndAnim, CometGradientBrush);
                Storyboard.SetTargetProperty(cometEndAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(cometEndAnim);

                PointAnimation bloomStartAnim = new PointAnimation
                {
                    From = new Point(-0.4, -0.4),
                    To = new Point(1.2, 1.2),
                    Duration = cometCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(bloomStartAnim, CometBloomBrush);
                Storyboard.SetTargetProperty(bloomStartAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(bloomStartAnim);

                PointAnimation bloomEndAnim = new PointAnimation
                {
                    From = new Point(0.4, 0.2),
                    To = new Point(1.8, 1.6),
                    Duration = cometCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(bloomEndAnim, CometBloomBrush);
                Storyboard.SetTargetProperty(bloomEndAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(bloomEndAnim);
            }
            else if (style == GlowStyle.Ripple)
            {
                BaseGlowLayer.Opacity = 0.20; // Soft ambient baseline
                RippleOverlay.Visibility = Visibility.Visible;

                RippleGradientBrush.GradientStops.Clear();
                if (isRgb)
                {
                    // Expanding concentric rainbow shockwave rings with bright crest
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.0));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.65));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(160, 124, 58, 237), 0.75));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 0.83));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.89));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.94));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.96));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.98));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 1.0));
                }
                else
                {
                    // Pure radial shockwave: transparent center -> soft slope -> bright wave crest -> transparent edge
                    RippleGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.0));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 0.65));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(100, mainColor.R, mainColor.G, mainColor.B), 0.80));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(220, mainColor.R, mainColor.G, mainColor.B), 0.88));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.94));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(160, mainColor.R, mainColor.G, mainColor.B), 0.97));
                    RippleGradientBrush.GradientStops.Add(new GradientStop(transparentColor, 1.0));
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.12)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity * 0.9, KeyTime.FromPercent(0.70)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Aspect ratio compensation for perfectly round wave reaching all corners
                double screenW = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
                double screenH = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
                if (screenW <= 0) screenW = 1920;
                if (screenH <= 0) screenH = 1080;
                double aspect = screenW / Math.Max(1.0, screenH);

                int rippleCycleMs = Math.Max(1100, (int)(1500 / Math.Max(0.5, profile.Speed)));
                Duration rippleCycleDuration = new Duration(TimeSpan.FromMilliseconds(rippleCycleMs));

                DoubleAnimation rippleRadiusXAnim = new DoubleAnimation
                {
                    From = 0.02,
                    To = 1.45,
                    Duration = rippleCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleRadiusXAnim, RippleGradientBrush);
                Storyboard.SetTargetProperty(rippleRadiusXAnim, new PropertyPath(RadialGradientBrush.RadiusXProperty));
                _currentStoryboard.Children.Add(rippleRadiusXAnim);

                DoubleAnimation rippleRadiusYAnim = new DoubleAnimation
                {
                    From = 0.02 * aspect,
                    To = 1.45 * aspect,
                    Duration = rippleCycleDuration,
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
            SweepContainer.Visibility = Visibility.Collapsed;
            CometContainer.Visibility = Visibility.Collapsed;
            RippleOverlay.Visibility = Visibility.Collapsed;
            BaseGlowLayer.Opacity = 1.0;
        }
    }
}
