using System;
using Windows.Foundation;

namespace NotiGlow.Core.Helpers
{
    public static class DpiHelper
    {
        public static Point TransformToDeviceIndependentPixels(double x, double y, double scale = 1.0)
        {
            if (scale > 0)
            {
                return new Point(x / scale, y / scale);
            }
            return new Point(x, y);
        }

        public static Rect GetDpiScaledRect(System.Drawing.Rectangle bounds, double scale = 1.0)
        {
            double scaleFactor = scale > 0 ? 1.0 / scale : 1.0;

            return new Rect(
                bounds.X * scaleFactor,
                bounds.Y * scaleFactor,
                bounds.Width * scaleFactor,
                bounds.Height * scaleFactor
            );
        }
    }
}
