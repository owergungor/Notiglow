using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace GlowBorder.UI.Animations
{
    /// <summary>
    /// Convenient alias for ButtonPressAnimationBehavior.
    /// </summary>
    public static class PressAnimation
    {
        public static readonly DependencyProperty IsEnabledProperty = ButtonPressAnimationBehavior.IsEnabledProperty;
        public static readonly DependencyProperty PressedScaleProperty = ButtonPressAnimationBehavior.PressedScaleProperty;
        public static readonly DependencyProperty PressDurationMsProperty = ButtonPressAnimationBehavior.PressDurationMsProperty;
        public static readonly DependencyProperty ReleaseDurationMsProperty = ButtonPressAnimationBehavior.ReleaseDurationMsProperty;

        public static bool GetIsEnabled(DependencyObject obj) => ButtonPressAnimationBehavior.GetIsEnabled(obj);
        public static void SetIsEnabled(DependencyObject obj, bool value) => ButtonPressAnimationBehavior.SetIsEnabled(obj, value);

        public static double GetPressedScale(DependencyObject obj) => ButtonPressAnimationBehavior.GetPressedScale(obj);
        public static void SetPressedScale(DependencyObject obj, double value) => ButtonPressAnimationBehavior.SetPressedScale(obj, value);

        public static int GetPressDurationMs(DependencyObject obj) => ButtonPressAnimationBehavior.GetPressDurationMs(obj);
        public static void SetPressDurationMs(DependencyObject obj, int value) => ButtonPressAnimationBehavior.SetPressDurationMs(obj, value);

        public static int GetReleaseDurationMs(DependencyObject obj) => ButtonPressAnimationBehavior.GetReleaseDurationMs(obj);
        public static void SetReleaseDurationMs(DependencyObject obj, int value) => ButtonPressAnimationBehavior.SetReleaseDurationMs(obj, value);
    }

    /// <summary>
    /// Provides a modern Windows 11 / Fluent style physical press animation behavior for buttons and interactive controls.
    /// Scales the element slightly (e.g. 1.0 -> 0.96) on press and smoothly returns to 1.0 on release or mouse leave.
    /// </summary>
    public static class ButtonPressAnimationBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ButtonPressAnimationBehavior),
                new FrameworkPropertyMetadata(false, OnIsEnabledChanged));

        public static readonly DependencyProperty PressedScaleProperty =
            DependencyProperty.RegisterAttached(
                "PressedScale",
                typeof(double),
                typeof(ButtonPressAnimationBehavior),
                new FrameworkPropertyMetadata(0.97));

        public static readonly DependencyProperty PressDurationMsProperty =
            DependencyProperty.RegisterAttached(
                "PressDurationMs",
                typeof(int),
                typeof(ButtonPressAnimationBehavior),
                new FrameworkPropertyMetadata(80));

        public static readonly DependencyProperty ReleaseDurationMsProperty =
            DependencyProperty.RegisterAttached(
                "ReleaseDurationMs",
                typeof(int),
                typeof(ButtonPressAnimationBehavior),
                new FrameworkPropertyMetadata(115));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static double GetPressedScale(DependencyObject obj) => (double)obj.GetValue(PressedScaleProperty);
        public static void SetPressedScale(DependencyObject obj, double value) => obj.SetValue(PressedScaleProperty, value);

        public static int GetPressDurationMs(DependencyObject obj) => (int)obj.GetValue(PressDurationMsProperty);
        public static void SetPressDurationMs(DependencyObject obj, int value) => obj.SetValue(PressDurationMsProperty, value);

        public static int GetReleaseDurationMs(DependencyObject obj) => (int)obj.GetValue(ReleaseDurationMsProperty);
        public static void SetReleaseDurationMs(DependencyObject obj, int value) => obj.SetValue(ReleaseDurationMsProperty, value);

        private static readonly DependencyProperty IsAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsAttached",
                typeof(bool),
                typeof(ButtonPressAnimationBehavior),
                new PropertyMetadata(false));

        public static bool GetIsAttached(DependencyObject obj) => (bool)obj.GetValue(IsAttachedProperty);

        private static readonly DependencyProperty StateTrackerProperty =
            DependencyProperty.RegisterAttached(
                "StateTracker",
                typeof(PressStateTracker),
                typeof(ButtonPressAnimationBehavior),
                new PropertyMetadata(null));

        static ButtonPressAnimationBehavior()
        {
            InitializeGlobal();
        }

        private static bool _globalInitialized = false;

        public static void InitializeGlobal()
        {
            if (_globalInitialized) return;
            _globalInitialized = true;

            try
            {
                EventManager.RegisterClassHandler(
                    typeof(ButtonBase),
                    UIElement.PreviewMouseLeftButtonDownEvent,
                    new System.Windows.Input.MouseButtonEventHandler(OnGlobalPreviewMouseDown),
                    true);

                EventManager.RegisterClassHandler(
                    typeof(Wpf.Ui.Controls.NavigationViewItem),
                    UIElement.PreviewMouseLeftButtonDownEvent,
                    new System.Windows.Input.MouseButtonEventHandler(OnGlobalPreviewMouseDown),
                    true);
            }
            catch
            {
                // Ignored in design time or non-WPF host environments
            }
        }

        private static bool IsExcluded(UIElement element)
        {
            if (element is Wpf.Ui.Controls.TitleBarButton) return true;

            try
            {
                DependencyObject? current = element;
                while (current != null)
                {
                    if (current is Wpf.Ui.Controls.TitleBar) return true;
                    if (current is System.Windows.Controls.Slider) return true;
                    if (current is System.Windows.Controls.ComboBox) return true;
                    if (current is System.Windows.Controls.TextBox) return true;
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            catch
            {
                // Fallback safe
            }

            return false;
        }

        private static void OnGlobalPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not UIElement element || IsExcluded(element)) return;

            if (!GetIsAttached(element))
            {
                Attach(element);
            }
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            bool isEnabled = (bool)e.NewValue;
            bool isAttached = (bool)element.GetValue(IsAttachedProperty);

            if (isEnabled && !isAttached)
            {
                Attach(element);
            }
            else if (!isEnabled && isAttached)
            {
                Detach(element);
            }
        }

        public static void Attach(UIElement element)
        {
            if (element == null) return;

            element.SetValue(IsAttachedProperty, true);

            // Ensure RenderTransformOrigin is centered (0.5, 0.5) so scaling is symmetrical
            element.RenderTransformOrigin = new Point(0.5, 0.5);

            var tracker = new PressStateTracker(element);
            element.SetValue(StateTrackerProperty, tracker);
            tracker.Attach();
        }

        public static void Detach(UIElement element)
        {
            if (element == null) return;

            element.SetValue(IsAttachedProperty, false);

            if (element.GetValue(StateTrackerProperty) is PressStateTracker tracker)
            {
                tracker.Detach();
                element.ClearValue(StateTrackerProperty);
            }
        }

        private sealed class PressStateTracker
        {
            private readonly WeakReference<UIElement> _elementRef;
            private ScaleTransform? _scaleTransform;
            private bool _isPressed;
            private DependencyPropertyDescriptor? _isPressedDescriptor;
            private EventHandler? _isPressedChangedHandler;

            public PressStateTracker(UIElement element)
            {
                _elementRef = new WeakReference<UIElement>(element);
            }

            public void Attach()
            {
                if (!_elementRef.TryGetTarget(out var element)) return;

                _scaleTransform = EnsureScaleTransform(element);

                // Listen for preview mouse down and up with handledEventsToo = true
                element.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), true);
                element.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnPreviewMouseUp), true);
                element.MouseLeave += OnMouseLeave;
                element.LostMouseCapture += OnLostMouseCapture;

                // If element is a ButtonBase (Button, ToggleButton, RepeatButton, etc.), monitor IsPressed for keyboard & capture changes
                if (element is ButtonBase button)
                {
                    _isPressedDescriptor = DependencyPropertyDescriptor.FromProperty(ButtonBase.IsPressedProperty, typeof(ButtonBase));
                    _isPressedChangedHandler = (s, e) =>
                    {
                        if (s is ButtonBase b)
                        {
                            if (b.IsPressed)
                            {
                                Animate(true);
                            }
                            else
                            {
                                Animate(false);
                            }
                        }
                    };
                    _isPressedDescriptor?.AddValueChanged(button, _isPressedChangedHandler);
                }
            }

            public void Detach()
            {
                if (!_elementRef.TryGetTarget(out var element)) return;

                element.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown));
                element.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnPreviewMouseUp));
                element.MouseLeave -= OnMouseLeave;
                element.LostMouseCapture -= OnLostMouseCapture;

                if (element is ButtonBase button && _isPressedDescriptor != null && _isPressedChangedHandler != null)
                {
                    _isPressedDescriptor.RemoveValueChanged(button, _isPressedChangedHandler);
                }

                // Reset scale immediately
                if (_scaleTransform != null)
                {
                    _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    _scaleTransform.ScaleX = 1.0;
                    _scaleTransform.ScaleY = 1.0;
                }
            }

            private void OnPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                Animate(true);
            }

            private void OnPreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                Animate(false);
            }

            private void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
            {
                Animate(false);
            }

            private void OnLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
            {
                Animate(false);
            }

            private void Animate(bool pressed)
            {
                if (_isPressed == pressed) return;
                _isPressed = pressed;

                if (!_elementRef.TryGetTarget(out var element)) return;

                if (!element.Dispatcher.CheckAccess())
                {
                    element.Dispatcher.BeginInvoke(new Action(() => Animate(pressed)));
                    return;
                }

                if (_scaleTransform == null)
                {
                    _scaleTransform = EnsureScaleTransform(element);
                }

                double targetScale = pressed ? GetPressedScale(element) : 1.0;
                int durationMs = pressed ? GetPressDurationMs(element) : GetReleaseDurationMs(element);

                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var animX = new DoubleAnimation
                {
                    To = targetScale,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = ease
                };

                var animY = new DoubleAnimation
                {
                    To = targetScale,
                    Duration = TimeSpan.FromMilliseconds(durationMs),
                    EasingFunction = ease
                };

                // HandoffBehavior.SnapshotAndReplace ensures rapid successive clicks smoothly interpolate from the current scale
                _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animX, HandoffBehavior.SnapshotAndReplace);
                _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animY, HandoffBehavior.SnapshotAndReplace);
            }

            private static ScaleTransform EnsureScaleTransform(UIElement element)
            {
                if (element.RenderTransform is ScaleTransform existingSt)
                {
                    return existingSt;
                }

                if (element.RenderTransform is TransformGroup tg)
                {
                    foreach (var child in tg.Children)
                    {
                        if (child is ScaleTransform childSt)
                        {
                            return childSt;
                        }
                    }

                    var newSt = new ScaleTransform(1.0, 1.0);
                    tg.Children.Add(newSt);
                    return newSt;
                }

                if (element.RenderTransform == null || element.RenderTransform == Transform.Identity)
                {
                    var st = new ScaleTransform(1.0, 1.0);
                    element.RenderTransform = st;
                    return st;
                }

                // If it is another transform type, wrap in TransformGroup
                var oldTransform = element.RenderTransform;
                var group = new TransformGroup();
                group.Children.Add(oldTransform);
                var createdSt = new ScaleTransform(1.0, 1.0);
                group.Children.Add(createdSt);
                element.RenderTransform = group;
                return createdSt;
            }
        }
    }
}
