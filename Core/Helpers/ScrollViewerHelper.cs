using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace NotiGlow.Core.Helpers
{
    public static class ScrollViewerHelper
    {
        public static void EnableReliableMouseWheel(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return;

            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scrollViewer.VerticalScrollMode = ScrollMode.Enabled;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollViewer.HorizontalScrollMode = ScrollMode.Disabled;
            scrollViewer.IsScrollInertiaEnabled = true;

            // Ensure hit-testable transparent background so empty areas capture wheel events
            if (scrollViewer.Background == null)
            {
                scrollViewer.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }

            scrollViewer.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler((sender, e) =>
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
            }), handledEventsToo: true);
        }

        public static void PreventClosedComboBoxWheelChange(ComboBox comboBox)
        {
            if (comboBox == null) return;

            comboBox.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler((s, e) =>
            {
                if (s is ComboBox cb && !cb.IsDropDownOpen)
                {
                    // Suppress ComboBox item cycling when closed so it doesn't swallow or alter settings
                    e.Handled = true;
                }
            }), handledEventsToo: true);
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
