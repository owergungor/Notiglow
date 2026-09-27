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

            double screenW = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            double screenH = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            if (screenW <= 0) screenW = 1920;
            if (screenH <= 0) screenH = 1080;

            LinearGradientBrush? sharedRgbBrush = null;
            if (isRgb)
            {
                // Unified screen-space absolute RGB spectrum across inner border, ambient spill, and all 4 edge blooms
                sharedRgbBrush = NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.CreateScreenSpaceRgbBrush(screenW, screenH);

                InnerBorder.BorderBrush = sharedRgbBrush;
                AmbientSpillBorder.BorderBrush = sharedRgbBrush;

                TopEdge.Fill = sharedRgbBrush;
                BottomEdge.Fill = sharedRgbBrush;
                LeftEdge.Fill = sharedRgbBrush;
                RightEdge.Fill = sharedRgbBrush;

                NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.ConfigureEdgeOpacityMasks(TopEdge, BottomEdge, LeftEdge, RightEdge);
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

            StartStyleAnimation(profile.Style, targetOpacity, duration, mainColor, transparentColor, isRgb, sharedRgbBrush, profile, screenW, screenH);
        }

        private void StartStyleAnimation(GlowStyle style, double maxOpacity, int durationMs, Color mainColor, Color transparentColor, bool isRgb, LinearGradientBrush? rgbBrush, AppProfile profile, double screenW, double screenH)
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

            // Animate RGB continuous spectrum transition if active (synchronized screen-space)
            if (isRgb && rgbBrush != null)
            {
                NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.AnimateScreenSpaceRgbBrush(_currentStoryboard, rgbBrush, screenW, screenH, durationMs);
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
                    NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.PopulateSweepRgbStops(SweepGradientBrush.GradientStops, false);
                    NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.PopulateSweepRgbStops(SweepBloomBrush.GradientStops, true);
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
                    NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.PopulateCometRgbStops(CometGradientBrush.GradientStops, false);
                    NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.PopulateCometRgbStops(CometBloomBrush.GradientStops, true);
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
                    NotiGlow.Core.Helpers.GlowSpectrumBrushFactory.PopulateRippleRgbStops(RippleGradientBrush.GradientStops);
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

                // True geometric circle shockwave expanding from the exact center (W/2, H/2) to all 4 corners
                double centerX = screenW / 2.0;
                double centerY = screenH / 2.0;
                RippleGradientBrush.MappingMode = BrushMappingMode.Absolute;
                RippleGradientBrush.Center = new Point(centerX, centerY);
                RippleGradientBrush.GradientOrigin = new Point(centerX, centerY);

                double diagonalRadius = Math.Sqrt((centerX * centerX) + (centerY * centerY));
                double maxRadius = diagonalRadius * 1.10;

                int rippleCycleMs = Math.Max(1100, (int)(1500 / Math.Max(0.5, profile.Speed)));
                Duration rippleCycleDuration = new Duration(TimeSpan.FromMilliseconds(rippleCycleMs));

                DoubleAnimation rippleRadiusXAnim = new DoubleAnimation
                {
                    From = 10.0,
                    To = maxRadius,
                    Duration = rippleCycleDuration,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleRadiusXAnim, RippleGradientBrush);
                Storyboard.SetTargetProperty(rippleRadiusXAnim, new PropertyPath(RadialGradientBrush.RadiusXProperty));
                _currentStoryboard.Children.Add(rippleRadiusXAnim);

                DoubleAnimation rippleRadiusYAnim = new DoubleAnimation
                {
                    From = 10.0,
                    To = maxRadius,
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
