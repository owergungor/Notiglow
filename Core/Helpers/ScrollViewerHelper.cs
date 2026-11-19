using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace NotiGlow.Core.Helpers
{
    public static class ScrollViewerHelper
    {
        private static readonly DependencyProperty IsWheelAttachedProperty =
            DependencyProperty.RegisterAttached(
                "IsWheelAttached",
                typeof(bool),
                typeof(ScrollViewerHelper),
                new PropertyMetadata(false));

        private static readonly SolidColorBrush TransparentHitTestBrush =
            new(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        private static readonly PointerEventHandler ScrollViewerWheelHandler = OnScrollViewerPointerWheelChanged;
        private static readonly PointerEventHandler ComboBoxWheelHandler = OnComboBoxPointerWheelChanged;

        public static void EnableReliableMouseWheel(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return;
            if (scrollViewer.GetValue(IsWheelAttachedProperty) is true) return;
            scrollViewer.SetValue(IsWheelAttachedProperty, true);

            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scrollViewer.VerticalScrollMode = ScrollMode.Enabled;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            scrollViewer.IsScrollInertiaEnabled = true;

            // Ensure hit-testable transparent background so empty areas capture wheel events
            if (scrollViewer.Background == null)
            {
                scrollViewer.Background = TransparentHitTestBrush;
            }

            scrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, ScrollViewerWheelHandler, handledEventsToo: true);
        }

        private static void OnScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                var point = e.GetCurrentPoint(sv);
                int delta = point.Properties.MouseWheelDelta;
                if (delta != 0)
                {
                    double currentOffset = sv.VerticalOffset;
                    double scrollAmount = -delta * 0.75;
                    double targetOffset = Math.Clamp(currentOffset + scrollAmount, 0, sv.ScrollableHeight);

                    if (Math.Abs(targetOffset - currentOffset) > 0.1)
                    {
                        sv.ChangeView(null, targetOffset, null, disableAnimation: false);
                        e.Handled = true;
                    }
                }
            }
        }

        public static void PreventClosedComboBoxWheelChange(ComboBox comboBox)
        {
            if (comboBox == null) return;
            if (comboBox.GetValue(IsWheelAttachedProperty) is true) return;
            comboBox.SetValue(IsWheelAttachedProperty, true);

            comboBox.AddHandler(UIElement.PointerWheelChangedEvent, ComboBoxWheelHandler, handledEventsToo: true);
        }

        private static void OnComboBoxPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ComboBox cb && !cb.IsDropDownOpen)
            {
                // Suppress ComboBox item cycling when closed so it doesn't swallow or alter settings
                e.Handled = true;
            }
        }

        public static void AttachToChildComboBoxes(DependencyObject root)
        {
            if (root == null) return;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is ComboBox cb)
                {
                    PreventClosedComboBoxWheelChange(cb);
                }
                AttachToChildComboBoxes(child);
            }
        }
    }
}
