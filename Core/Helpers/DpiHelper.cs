using System.Windows;
using System.Windows.Media;

namespace NotiGlow.Core.Helpers
{
    public static class DpiHelper
    {
        public static System.Windows.Point TransformToDeviceIndependentPixels(Visual visual, double x, double y)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget != null)
            {
                Matrix matrix = source.CompositionTarget.TransformFromDevice;
                return matrix.Transform(new System.Windows.Point(x, y));
            }
            return new System.Windows.Point(x, y);
        }

        public static Rect GetDpiScaledRect(System.Drawing.Rectangle bounds, Visual? visual = null)
        {
            double scaleX = 1.0;
            double scaleY = 1.0;

            if (visual != null)
            {
                var source = PresentationSource.FromVisual(visual);
                if (source?.CompositionTarget != null)
                {
                    scaleX = 1.0 / source.CompositionTarget.TransformToDevice.M11;
                    scaleY = 1.0 / source.CompositionTarget.TransformToDevice.M22;
                }
            }

            return new Rect(
                bounds.X * scaleX,
                bounds.Y * scaleY,
                bounds.Width * scaleX,
                bounds.Height * scaleY
            );
        }
    }
}
