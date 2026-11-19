using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace NotiGlow.UI.Animations
{
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

    public static class ButtonPressAnimationBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(ButtonPressAnimationBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static readonly DependencyProperty PressedScaleProperty =
            DependencyProperty.RegisterAttached(
                "PressedScale",
                typeof(double),
                typeof(ButtonPressAnimationBehavior),
                new PropertyMetadata(0.97));

        public static readonly DependencyProperty PressDurationMsProperty =
            DependencyProperty.RegisterAttached(
                "PressDurationMs",
                typeof(int),
                typeof(ButtonPressAnimationBehavior),
                new PropertyMetadata(80));

        public static readonly DependencyProperty ReleaseDurationMsProperty =
            DependencyProperty.RegisterAttached(
                "ReleaseDurationMs",
                typeof(int),
                typeof(ButtonPressAnimationBehavior),
                new PropertyMetadata(115));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        public static double GetPressedScale(DependencyObject obj) => (double)obj.GetValue(PressedScaleProperty);
        public static void SetPressedScale(DependencyObject obj, double value) => obj.SetValue(PressedScaleProperty, value);

        public static int GetPressDurationMs(DependencyObject obj) => (int)obj.GetValue(PressDurationMsProperty);
        public static void SetPressDurationMs(DependencyObject obj, int value) => obj.SetValue(PressDurationMsProperty, value);

        public static int GetReleaseDurationMs(DependencyObject obj) => (int)obj.GetValue(ReleaseDurationMsProperty);
        public static void SetReleaseDurationMs(DependencyObject obj, int value) => obj.SetValue(ReleaseDurationMsProperty, value);

        private static readonly DependencyProperty StateTrackerProperty =
            DependencyProperty.RegisterAttached(
                "StateTracker",
                typeof(PressStateTracker),
                typeof(ButtonPressAnimationBehavior),
                new PropertyMetadata(null));

        public static void InitializeGlobal()
        {
            // Global initialization hook
        }

        public static void ApplyToggleSwitchReduceMotion(object toggle, bool reduceMotion)
        {
            // Reduces animation motion on toggle switches if supported
        }

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                if ((bool)e.NewValue)
                {
                    Attach(element);
                }
                else
                {
                    Detach(element);
                }
            }
        }

        public static void Attach(UIElement element)
        {
            if (element.GetValue(StateTrackerProperty) is PressStateTracker) return;

            var tracker = new PressStateTracker(element);
            element.SetValue(StateTrackerProperty, tracker);
        }

        public static void Detach(UIElement element)
        {
            if (element.GetValue(StateTrackerProperty) is PressStateTracker tracker)
            {
                tracker.Detach();
                element.ClearValue(StateTrackerProperty);
            }
        }

        private sealed class PressStateTracker
        {
            private readonly UIElement _element;

            public PressStateTracker(UIElement element)
            {
                _element = element;
                _element.PointerPressed += OnPointerPressed;
                _element.PointerReleased += OnPointerReleased;
                _element.PointerCanceled += OnPointerCanceled;
                _element.PointerCaptureLost += OnPointerCaptureLost;
            }

            public void Detach()
            {
                _element.PointerPressed -= OnPointerPressed;
                _element.PointerReleased -= OnPointerReleased;
                _element.PointerCanceled -= OnPointerCanceled;
                _element.PointerCaptureLost -= OnPointerCaptureLost;
            }

            private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
            {
                // Pointer feedback handled natively by WinUI 3 visual states
            }

            private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
            {
            }

            private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
            {
            }

            private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
            {
            }
        }
    }
}
