using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GlowBorder.Models;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;

namespace GlowBorder.Overlay
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

            Color mainColor = GlowBorder.Core.Helpers.ColorHelper.ParseColor(profile.ColorHex);
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

            StartStyleAnimation(profile.Style, targetOpacity, duration);
        }

        private void StartStyleAnimation(GlowStyle style, double maxOpacity, int durationMs)
        {
            _currentStoryboard = new Storyboard();
            Duration duration = new Duration(TimeSpan.FromMilliseconds(durationMs));

            SweepOverlay.Visibility = Visibility.Collapsed;
            CometOverlay.Visibility = Visibility.Collapsed;
            RippleOverlay.Visibility = Visibility.Collapsed;

            if (style == GlowStyle.Pulse)
            {
                DoubleAnimationUsingKeyFrames keyFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.15), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.4, KeyTime.FromPercent(0.5), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.8), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseIn }));

                Storyboard.SetTarget(keyFrames, this);
                Storyboard.SetTargetProperty(keyFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Ambient)
            {
                DoubleAnimationUsingKeyFrames keyFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                keyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.7, KeyTime.FromPercent(0.25), new SineEase { EasingMode = EasingMode.EaseOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(maxOpacity * 0.3, KeyTime.FromPercent(0.6), new SineEase { EasingMode = EasingMode.EaseInOut }));
                keyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0), new SineEase { EasingMode = EasingMode.EaseIn }));

                Storyboard.SetTarget(keyFrames, this);
                Storyboard.SetTargetProperty(keyFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(keyFrames);
            }
            else if (style == GlowStyle.Sweep)
            {
                SweepOverlay.Visibility = Visibility.Visible;
                SweepOverlay.BorderThickness = new Thickness(InnerBorder.BorderThickness.Left * 2);
                SweepStop1.Color = InnerBorderBrush.Color;

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.15)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.85)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                PointAnimation startPointAnim = new PointAnimation
                {
                    From = new Point(0, 0),
                    To = new Point(1, 1),
                    Duration = duration,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                Storyboard.SetTarget(startPointAnim, SweepGradientBrush);
                Storyboard.SetTargetProperty(startPointAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _currentStoryboard.Children.Add(startPointAnim);
            }
            else if (style == GlowStyle.Comet)
            {
                CometOverlay.Visibility = Visibility.Visible;
                CometOverlay.BorderThickness = new Thickness(InnerBorder.BorderThickness.Left * 2.5);
                CometStop2.Color = InnerBorderBrush.Color;

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.1)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.9)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Traveling Comet Head
                PointAnimation cometAnim = new PointAnimation
                {
                    From = new Point(0, 0),
                    To = new Point(1, 0),
                    Duration = new Duration(TimeSpan.FromMilliseconds(durationMs * 0.7)),
                    RepeatBehavior = RepeatBehavior.Forever,
                    AutoReverse = true
                };
                Storyboard.SetTarget(cometAnim, CometGradientBrush);
                Storyboard.SetTargetProperty(cometAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _currentStoryboard.Children.Add(cometAnim);
            }
            else if (style == GlowStyle.Ripple)
            {
                RippleOverlay.Visibility = Visibility.Visible;
                RippleStop0.Color = InnerBorderBrush.Color;

                DoubleAnimationUsingKeyFrames fadeFrames = new DoubleAnimationUsingKeyFrames { Duration = duration };
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity, KeyTime.FromPercent(0.2)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(maxOpacity * 0.6, KeyTime.FromPercent(0.6)));
                fadeFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0)));

                Storyboard.SetTarget(fadeFrames, this);
                Storyboard.SetTargetProperty(fadeFrames, new PropertyPath(UserControl.OpacityProperty));
                _currentStoryboard.Children.Add(fadeFrames);

                // Ripple Expansion Animation
                DoubleAnimation rippleRadiusAnim = new DoubleAnimation
                {
                    From = 0.1,
                    To = 1.0,
                    Duration = duration,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(rippleRadiusAnim, RippleGradientBrush);
                Storyboard.SetTargetProperty(rippleRadiusAnim, new PropertyPath(RadialGradientBrush.RadiusXProperty));
                _currentStoryboard.Children.Add(rippleRadiusAnim);
            }

            _currentStoryboard.Completed += OnStoryboardCompleted;
            _currentStoryboard.Begin();
        }

        private void OnStoryboardCompleted(object? sender, EventArgs e)
        {
            Opacity = 0;
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
        }
    }
}
