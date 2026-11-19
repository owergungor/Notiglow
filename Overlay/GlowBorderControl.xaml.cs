using System;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;

namespace NotiGlow.Overlay
{
    public sealed partial class GlowBorderControl : UserControl
    {
        private Storyboard? _currentStoryboard;
        private DispatcherTimer? _loopTimer;
        private DateTime _animStartTime;
        private Action? _onCompletedCallback;

        public GlowBorderControl()
        {
            this.InitializeComponent();
            Opacity = 0;
        }

        public void ApplyProfile(AppProfile profile, Action? onCompleted = null)
        {
            _onCompletedCallback = onCompleted;
            StopAnimation();

            bool isRgb = ColorHelper.IsRgbSpectrum(profile.ColorHex);
            Color mainColor = isRgb ? Color.FromArgb(255, 255, 0, 77) : ColorHelper.ParseColor(profile.ColorHex);
            Color transparentColor = Color.FromArgb(0, mainColor.R, mainColor.G, mainColor.B);

            // Update edge sizes & bloom layers
            TopEdge.Height = profile.GlowSize;
            BottomEdge.Height = profile.GlowSize;
            LeftEdge.Width = profile.GlowSize;
            RightEdge.Width = profile.GlowSize;
            InnerBorder.BorderThickness = new Thickness(profile.Thickness);

            double cornerRadius = Math.Clamp(profile.CornerRadius, 0.0, 48.0);
            var cr = new CornerRadius(cornerRadius);
            InnerBorder.CornerRadius = cr;
            AmbientSpillBorder.CornerRadius = cr;
            SweepBloomBorder.CornerRadius = cr;
            SweepOverlay.CornerRadius = cr;
            CometBloomBorder.CornerRadius = cr;
            CometOverlay.CornerRadius = cr;

            double screenW = ActualWidth > 0 ? ActualWidth : 1920;
            double screenH = ActualHeight > 0 ? ActualHeight : 1080;

            LinearGradientBrush? sharedRgbBrush = null;
            if (isRgb)
            {
                sharedRgbBrush = GlowSpectrumBrushFactory.CreateScreenSpaceRgbBrush(screenW, screenH);
                InnerBorder.BorderBrush = sharedRgbBrush;
                AmbientSpillBorder.BorderBrush = sharedRgbBrush;

                TopEdge.Fill = sharedRgbBrush;
                BottomEdge.Fill = sharedRgbBrush;
                LeftEdge.Fill = sharedRgbBrush;
                RightEdge.Fill = sharedRgbBrush;
            }
            else
            {
                InnerBorder.BorderBrush = new SolidColorBrush(mainColor);
                SpillBrush.Color = mainColor;
                AmbientSpillBorder.BorderBrush = SpillBrush;

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

        private void StartStyleAnimation(
            GlowStyle style,
            double maxOpacity,
            int durationMs,
            Color mainColor,
            Color transparentColor,
            bool isRgb,
            LinearGradientBrush? rgbBrush,
            AppProfile profile,
            double screenW,
            double screenH)
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
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.12)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.88)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)) });

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, "Opacity");
                _currentStoryboard.Children.Add(fadeFrames);

                _currentStoryboard.Completed += OnStoryboardCompleted;
                _currentStoryboard.Begin();
                return;
            }

            if (style == GlowStyle.Pulse)
            {
                BaseGlowLayer.Opacity = 1.0;

                DoubleAnimationUsingKeyFrames keyFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero) });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.15)), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = maxOpacity * 0.25, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.35)), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.55)), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = maxOpacity * 0.25, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.75)), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = maxOpacity * 0.9, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.88)), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });

                Storyboard.SetTarget(keyFrames, this);
                Storyboard.SetTargetProperty(keyFrames, "Opacity");
                _currentStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Ambient)
            {
                BaseGlowLayer.Opacity = 1.0;

                DoubleAnimationUsingKeyFrames keyFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero) });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = maxOpacity * 0.85, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.30)), EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = maxOpacity * 0.70, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.70)), EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } });

                Storyboard.SetTarget(keyFrames, this);
                Storyboard.SetTargetProperty(keyFrames, "Opacity");
                _currentStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Sweep)
            {
                BaseGlowLayer.Opacity = 0.20;
                SweepContainer.Visibility = Visibility.Visible;
                SweepOverlay.BorderThickness = new Thickness(Math.Max(8, profile.Thickness * 2.2));
                SweepBloomBorder.BorderThickness = new Thickness(Math.Max(26, profile.GlowSize));

                SweepGradientBrush.GradientStops.Clear();
                SweepBloomBrush.GradientStops.Clear();

                if (isRgb)
                {
                    GlowSpectrumBrushFactory.PopulateSweepRgbStops(SweepGradientBrush.GradientStops, false);
                    GlowSpectrumBrushFactory.PopulateSweepRgbStops(SweepBloomBrush.GradientStops, true);
                }
                else
                {
                    SweepGradientBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 0.0 });
                    SweepGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(60, mainColor.R, mainColor.G, mainColor.B), Offset = 0.20 });
                    SweepGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(200, mainColor.R, mainColor.G, mainColor.B), Offset = 0.40 });
                    SweepGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0.50 });
                    SweepGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(200, mainColor.R, mainColor.G, mainColor.B), Offset = 0.60 });
                    SweepGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(60, mainColor.R, mainColor.G, mainColor.B), Offset = 0.80 });
                    SweepGradientBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 1.0 });

                    SweepBloomBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 0.0 });
                    SweepBloomBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(80, mainColor.R, mainColor.G, mainColor.B), Offset = 0.30 });
                    SweepBloomBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(180, mainColor.R, mainColor.G, mainColor.B), Offset = 0.50 });
                    SweepBloomBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(80, mainColor.R, mainColor.G, mainColor.B), Offset = 0.70 });
                    SweepBloomBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 1.0 });
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.10)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.90)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)) });

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, "Opacity");
                _currentStoryboard.Children.Add(fadeFrames);

                StartLoopTimer(style, profile.Speed, screenW, screenH);
            }
            else if (style == GlowStyle.Comet)
            {
                BaseGlowLayer.Opacity = 0.20;
                CometContainer.Visibility = Visibility.Visible;
                CometOverlay.BorderThickness = new Thickness(Math.Max(8, profile.Thickness * 2.2));
                CometBloomBorder.BorderThickness = new Thickness(Math.Max(28, profile.GlowSize * 1.1));

                CometGradientBrush.GradientStops.Clear();
                CometBloomBrush.GradientStops.Clear();

                if (isRgb)
                {
                    GlowSpectrumBrushFactory.PopulateCometRgbStops(CometGradientBrush.GradientStops, false);
                    GlowSpectrumBrushFactory.PopulateCometRgbStops(CometBloomBrush.GradientStops, true);
                }
                else
                {
                    CometGradientBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 0.0 });
                    CometGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(30, mainColor.R, mainColor.G, mainColor.B), Offset = 0.25 });
                    CometGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(120, mainColor.R, mainColor.G, mainColor.B), Offset = 0.55 });
                    CometGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(220, mainColor.R, mainColor.G, mainColor.B), Offset = 0.82 });
                    CometGradientBrush.GradientStops.Add(new GradientStop { Color = mainColor, Offset = 0.93 });
                    CometGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0.98 });
                    CometGradientBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 1.0 });

                    CometBloomBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 0.0 });
                    CometBloomBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(60, mainColor.R, mainColor.G, mainColor.B), Offset = 0.40 });
                    CometBloomBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(180, mainColor.R, mainColor.G, mainColor.B), Offset = 0.80 });
                    CometBloomBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(230, mainColor.R, mainColor.G, mainColor.B), Offset = 0.96 });
                    CometBloomBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 1.0 });
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.08)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.92)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)) });

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, "Opacity");
                _currentStoryboard.Children.Add(fadeFrames);

                StartLoopTimer(style, profile.Speed, screenW, screenH);
            }
            else if (style == GlowStyle.Ripple)
            {
                BaseGlowLayer.Opacity = 0.20;
                RippleOverlay.Visibility = Visibility.Visible;

                RippleGradientBrush.GradientStops.Clear();
                if (isRgb)
                {
                    GlowSpectrumBrushFactory.PopulateRippleRgbStops(RippleGradientBrush.GradientStops);
                }
                else
                {
                    RippleGradientBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 0.0 });
                    RippleGradientBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 0.65 });
                    RippleGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(100, mainColor.R, mainColor.G, mainColor.B), Offset = 0.80 });
                    RippleGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(220, mainColor.R, mainColor.G, mainColor.B), Offset = 0.88 });
                    RippleGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0.94 });
                    RippleGradientBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(160, mainColor.R, mainColor.G, mainColor.B), Offset = 0.97 });
                    RippleGradientBrush.GradientStops.Add(new GradientStop { Color = transparentColor, Offset = 1.0 });
                }

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.12)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = maxOpacity * 0.9, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.70)) });
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame { Value = 0.0, KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)) });

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, "Opacity");
                _currentStoryboard.Children.Add(fadeFrames);

                double centerX = screenW / 2.0;
                double centerY = screenH / 2.0;
                RippleGradientBrush.MappingMode = BrushMappingMode.Absolute;
                RippleGradientBrush.Center = new Point(centerX, centerY);
                RippleGradientBrush.GradientOrigin = new Point(centerX, centerY);

                StartLoopTimer(style, profile.Speed, screenW, screenH);
            }

            _currentStoryboard.Completed += OnStoryboardCompleted;
            _currentStoryboard.Begin();
        }

        private void StartLoopTimer(GlowStyle style, double speed, double screenW, double screenH)
        {
            _animStartTime = DateTime.UtcNow;
            int cycleMs = style switch
            {
                GlowStyle.Sweep => Math.Max(1000, (int)(1500 / Math.Max(0.5, speed))),
                GlowStyle.Comet => Math.Max(900, (int)(1300 / Math.Max(0.5, speed))),
                GlowStyle.Ripple => Math.Max(1100, (int)(1500 / Math.Max(0.5, speed))),
                _ => 1500
            };

            double maxRadius = Math.Sqrt((screenW / 2.0) * (screenW / 2.0) + (screenH / 2.0) * (screenH / 2.0)) * 1.10;

            _loopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _loopTimer.Tick += (s, e) =>
            {
                double elapsed = (DateTime.UtcNow - _animStartTime).TotalMilliseconds;
                double progress = (elapsed % cycleMs) / cycleMs;

                if (style == GlowStyle.Sweep)
                {
                    double sweepPos = -0.8 + (1.6 - (-0.8)) * progress;
                    SweepGradientBrush.StartPoint = new Point(sweepPos, sweepPos);
                    SweepGradientBrush.EndPoint = new Point(sweepPos + 0.6, sweepPos + 0.6);
                    SweepBloomBrush.StartPoint = SweepGradientBrush.StartPoint;
                    SweepBloomBrush.EndPoint = SweepGradientBrush.EndPoint;
                }
                else if (style == GlowStyle.Comet)
                {
                    double cometProgress = (progress <= 0.5) ? progress * 2.0 : (1.0 - progress) * 2.0;
                    double startPos = -0.4 + (1.2 - (-0.4)) * cometProgress;
                    CometGradientBrush.StartPoint = new Point(startPos, startPos);
                    CometGradientBrush.EndPoint = new Point(startPos + 0.8, startPos + 0.6);
                    CometBloomBrush.StartPoint = CometGradientBrush.StartPoint;
                    CometBloomBrush.EndPoint = CometGradientBrush.EndPoint;
                }
                else if (style == GlowStyle.Ripple)
                {
                    double radius = 10.0 + (maxRadius - 10.0) * progress;
                    RippleGradientBrush.RadiusX = radius;
                    RippleGradientBrush.RadiusY = radius;
                }
            };
            _loopTimer.Start();
        }

        private void OnStoryboardCompleted(object? sender, object e)
        {
            StopAnimation();
            _onCompletedCallback?.Invoke();
        }

        public void StopAnimation()
        {
            if (_loopTimer != null)
            {
                _loopTimer.Stop();
                _loopTimer = null;
            }
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
