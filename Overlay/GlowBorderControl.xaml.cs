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

            Color mainColor = NotiGlow.Core.Helpers.ColorHelper.ParseColor(profile.ColorHex);
            Color transparentColor = Color.FromArgb(0, mainColor.R, mainColor.G, mainColor.B);

            // Update edge sizes & bloom layers
            TopEdge.Height = profile.GlowSize;
            BottomEdge.Height = profile.GlowSize;
            LeftEdge.Width = profile.GlowSize;
            RightEdge.Width = profile.GlowSize;
            InnerBorder.BorderThickness = new Thickness(profile.Thickness);
            SpillBrush.Color = mainColor;

            // Update Gradient Colors
            TopStop0.Color = mainColor;
            TopStop1.Color = transparentColor;

            BottomStop0.Color = mainColor;
            BottomStop1.Color = transparentColor;

            LeftStop0.Color = mainColor;
            LeftStop1.Color = transparentColor;

            RightStop0.Color = mainColor;
            RightStop1.Color = transparentColor;

            InnerBorderBrush.Color = mainColor;

            double targetOpacity = Math.Clamp(profile.Intensity, 0.05, 1.0);
            int duration = Math.Max(500, (int)(profile.DurationMs / Math.Max(0.5, profile.Speed)));

            StartStyleAnimation(profile.Style, targetOpacity, duration, mainColor, transparentColor);
        }

        private void StartStyleAnimation(GlowStyle style, double maxOpacity, int durationMs, Color mainColor, Color transparentColor)
        {
            _currentStoryboard = new Storyboard();
            Duration duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

            SweepOverlay.Visibility = Visibility.Collapsed;
            CometOverlay.Visibility = Visibility.Collapsed;
            RippleOverlay.Visibility = Visibility.Collapsed;

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
                BaseGlowLayer.Opacity = 0.20;
                SweepOverlay.Visibility = Visibility.Visible;
                SweepOverlay.BorderThickness = new Thickness(Math.Max(8, InnerBorder.BorderThickness.Left * 3));
                SweepStop0.Color = transparentColor;
                SweepStop1.Color = Color.FromArgb(255, mainColor.R, mainColor.G, mainColor.B);
                SweepStop2.Color = transparentColor;

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
                BaseGlowLayer.Opacity = 0.10;
                CometOverlay.Visibility = Visibility.Visible;
                CometOverlay.BorderThickness = new Thickness(Math.Max(10, InnerBorder.BorderThickness.Left * 3.5));
                CometStop0.Color = transparentColor;
                CometStop1.Color = Color.FromArgb(180, mainColor.R, mainColor.G, mainColor.B);
                CometStop2.Color = Color.FromArgb(255, 255, 255, 255); // Brilliant white head

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
                BaseGlowLayer.Opacity = 0.15;
                RippleOverlay.Visibility = Visibility.Visible;
                RippleOverlay.BorderThickness = new Thickness(Math.Max(12, TopEdge.Height * 1.5));
                RippleStop0.Color = Color.FromArgb(255, mainColor.R, mainColor.G, mainColor.B);
                RippleStop1.Color = Color.FromArgb(140, mainColor.R, mainColor.G, mainColor.B);
                RippleStop2.Color = transparentColor;

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.12)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity * 0.7, KeyTime.FromPercent(0.65)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Ripple Expansion Animation (Shockwave)
                DoubleAnimation rippleRadiusXAnim = new DoubleAnimation
                {
                    From = 0.05,
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
                    From = 0.05,
                    To = 1.3,
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
