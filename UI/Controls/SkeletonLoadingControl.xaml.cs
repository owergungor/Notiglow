using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotiGlow.UI.Animations;
using UserControl = System.Windows.Controls.UserControl;
using Point = System.Windows.Point;

namespace NotiGlow.UI.Controls
{
    public partial class SkeletonLoadingControl : UserControl
    {
        private Storyboard? _shimmerStoryboard;
        private readonly List<LinearGradientBrush> _masks = new();

        public SkeletonLoadingControl()
        {
            InitializeComponent();
            _masks.Add(ShimmerMask0);
            _masks.Add(ShimmerMask1);
            _masks.Add(ShimmerMask2);
            _masks.Add(ShimmerMask3);
            _masks.Add(ShimmerMask4);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            MotionPolicy.PolicyChanged += OnMotionPolicyChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateShimmerState();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopShimmer();
        }

        private void OnMotionPolicyChanged(object? sender, EventArgs e)
        {
            Dispatcher?.Invoke(UpdateShimmerState);
        }

        private void UpdateShimmerState()
        {
            if (MotionPolicy.IsReduceMotion)
            {
                StopShimmer();
            }
            else
            {
                StartShimmer();
            }
        }

        private void StartShimmer()
        {
            StopShimmer();

            if (MotionPolicy.IsReduceMotion) return;

            _shimmerStoryboard = new Storyboard
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            foreach (var mask in _masks)
            {
                var startAnim = new PointAnimation
                {
                    From = new Point(-1.5, 0),
                    To = new Point(1.5, 0),
                    Duration = TimeSpan.FromMilliseconds(1300),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(startAnim, mask);
                Storyboard.SetTargetProperty(startAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
                _shimmerStoryboard.Children.Add(startAnim);

                var endAnim = new PointAnimation
                {
                    From = new Point(-0.5, 0),
                    To = new Point(2.5, 0),
                    Duration = TimeSpan.FromMilliseconds(1300),
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                Storyboard.SetTarget(endAnim, mask);
                Storyboard.SetTargetProperty(endAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
                _shimmerStoryboard.Children.Add(endAnim);
            }

            _shimmerStoryboard.Begin();
        }

        private void StopShimmer()
        {
            if (_shimmerStoryboard != null)
            {
                _shimmerStoryboard.Stop();
                _shimmerStoryboard = null;
            }

            // Reset masks to neutral static state
            foreach (var mask in _masks)
            {
                mask.StartPoint = new Point(0, 0);
                mask.EndPoint = new Point(1, 0);
            }
        }
    }
}
