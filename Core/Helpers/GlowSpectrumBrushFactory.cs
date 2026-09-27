using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace NotiGlow.Core.Helpers
{
    /// <summary>
    /// Unified factory and synchronization engine for RGB spectrum neon glows.
    /// Guarantees that Top, Bottom, Left, and Right edges, plus Core and Bloom layers,
    /// share identical coordinate phase and continuous perimeter interpolation with no edge seams.
    /// </summary>
    public static class GlowSpectrumBrushFactory
    {
        public static readonly Color[] SpectrumColors = ColorHelper.RgbSpectrumColors;

        /// <summary>
        /// Creates an absolute screen-space linear gradient brush mapping uniformly across all 4 edges.
        /// Because MappingMode is Absolute, any visual located at (x, y) samples the exact same color,
        /// ensuring zero phase mismatch or color jump at corners (including the right edge seam).
        /// </summary>
        public static LinearGradientBrush CreateScreenSpaceRgbBrush(double width, double height)
        {
            double w = width > 0 ? width : 1920;
            double h = height > 0 ? height : 1080;

            var brush = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(w, h),
                SpreadMethod = GradientSpreadMethod.Repeat
            };

            double step = 1.0 / (SpectrumColors.Length - 1);
            for (int i = 0; i < SpectrumColors.Length; i++)
            {
                brush.GradientStops.Add(new GradientStop(SpectrumColors[i], i * step));
            }

            return brush;
        }

        /// <summary>
        /// Calculates the exact screen center point (W/2, H/2) for true circular shockwaves.
        /// </summary>
        public static Point CalculateRippleCenter(double width, double height)
        {
            return new Point(width / 2.0, height / 2.0);
        }

        /// <summary>
        /// Calculates the maximum radius required to guarantee full coverage of all four screen corners.
        /// </summary>
        public static double CalculateRippleMaxRadius(double width, double height)
        {
            double halfW = width / 2.0;
            double halfH = height / 2.0;
            double diagonal = Math.Sqrt((halfW * halfW) + (halfH * halfH));
            return diagonal * 1.10;
        }

        /// <summary>
        /// Creates a true circular shockwave RadialGradientBrush with Absolute mapping,
        /// ensuring RadiusX == RadiusY in DIPs across any monitor aspect ratio (16:9, 21:9, square).
        /// </summary>
        public static RadialGradientBrush CreateRippleBrush(double width, double height, Color color, double radius)
        {
            Point center = CalculateRippleCenter(width, height);
            var brush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = center,
                GradientOrigin = center,
                RadiusX = radius,
                RadiusY = radius
            };

            // Annular shockwave: hollow center from 0.0 to 0.65, sharp wavefront at 0.85-0.95
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0.65));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(140, color.R, color.G, color.B), 0.80));
            brush.GradientStops.Add(new GradientStop(Colors.White, 0.92));
            brush.GradientStops.Add(new GradientStop(color, 0.97));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));

            return brush;
        }

        /// <summary>
        /// Configures uniform directional opacity masks for edge blooms so they fade naturally towards the screen interior.
        /// </summary>
        public static void ConfigureEdgeOpacityMasks(WpfRectangle top, WpfRectangle bottom, WpfRectangle left, WpfRectangle right)
        {
            top.OpacityMask = new LinearGradientBrush(
                Color.FromArgb(255, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                new Point(0.5, 0),
                new Point(0.5, 1));

            bottom.OpacityMask = new LinearGradientBrush(
                Color.FromArgb(255, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                new Point(0.5, 1),
                new Point(0.5, 0));

            left.OpacityMask = new LinearGradientBrush(
                Color.FromArgb(255, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                new Point(0, 0.5),
                new Point(1, 0.5));

            right.OpacityMask = new LinearGradientBrush(
                Color.FromArgb(255, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                new Point(1, 0.5),
                new Point(0, 0.5));
        }

        /// <summary>
        /// Populates gradient stops for Sweep beam in RGB mode using the unified spectrum palette.
        /// </summary>
        public static void PopulateSweepRgbStops(GradientStopCollection stops, bool isBloom)
        {
            stops.Clear();
            if (isBloom)
            {
                stops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 0.0));
                stops.Add(new GradientStop(Color.FromArgb(90, 255, 0, 77), 0.20));
                stops.Add(new GradientStop(Color.FromArgb(190, 0, 230, 118), 0.50));
                stops.Add(new GradientStop(Color.FromArgb(90, 124, 58, 237), 0.80));
                stops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 1.0));
            }
            else
            {
                stops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 0.0));
                stops.Add(new GradientStop(Color.FromArgb(140, 255, 0, 77), 0.15));
                stops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.35));
                stops.Add(new GradientStop(Colors.White, 0.50));
                stops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 0.65));
                stops.Add(new GradientStop(Color.FromArgb(150, 124, 58, 237), 0.85));
                stops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 1.0));
            }
        }

        /// <summary>
        /// Populates gradient stops for Comet head &amp; trail in RGB mode using the unified spectrum palette.
        /// </summary>
        public static void PopulateCometRgbStops(GradientStopCollection stops, bool isBloom)
        {
            stops.Clear();
            if (isBloom)
            {
                stops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 0.0));
                stops.Add(new GradientStop(Color.FromArgb(80, 41, 121, 255), 0.50));
                stops.Add(new GradientStop(Color.FromArgb(180, 255, 214, 0), 0.85));
                stops.Add(new GradientStop(Color.FromArgb(240, 255, 0, 77), 0.96));
                stops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 1.0));
            }
            else
            {
                stops.Add(new GradientStop(Color.FromArgb(0, 124, 58, 237), 0.0));
                stops.Add(new GradientStop(Color.FromArgb(60, 124, 58, 237), 0.25));
                stops.Add(new GradientStop(Color.FromArgb(160, 41, 121, 255), 0.50));
                stops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.72));
                stops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.86));
                stops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.94));
                stops.Add(new GradientStop(Colors.White, 0.98));
                stops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            }
        }

        /// <summary>
        /// Populates gradient stops for Ripple shockwave in RGB mode.
        /// Hollow center (0.0 to 0.65 transparent) followed by rainbow shockwave band with bright white crest.
        /// </summary>
        public static void PopulateRippleRgbStops(GradientStopCollection stops)
        {
            stops.Clear();
            stops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.0));
            stops.Add(new GradientStop(Color.FromArgb(0, 41, 121, 255), 0.65));
            stops.Add(new GradientStop(Color.FromArgb(160, 124, 58, 237), 0.75));
            stops.Add(new GradientStop(Color.FromRgb(0, 229, 255), 0.83));
            stops.Add(new GradientStop(Color.FromRgb(0, 230, 118), 0.89));
            stops.Add(new GradientStop(Colors.White, 0.94));
            stops.Add(new GradientStop(Color.FromRgb(255, 214, 0), 0.96));
            stops.Add(new GradientStop(Color.FromRgb(255, 0, 77), 0.98));
            stops.Add(new GradientStop(Color.FromArgb(0, 255, 0, 77), 1.0));
        }

        /// <summary>
        /// Animates the screen-space RGB brush continuously across the entire display.
        /// </summary>
        public static void AnimateScreenSpaceRgbBrush(Storyboard storyboard, LinearGradientBrush brush, double width, double height, int durationMs)
        {
            double w = width > 0 ? width : 1920;
            double h = height > 0 ? height : 1080;

            var rgbDuration = new Duration(TimeSpan.FromMilliseconds(Math.Max(1200, durationMs * 0.75)));

            PointAnimation rgbStartAnim = new PointAnimation
            {
                From = new Point(0, 0),
                To = new Point(w, h),
                Duration = rgbDuration,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(rgbStartAnim, brush);
            Storyboard.SetTargetProperty(rgbStartAnim, new PropertyPath(LinearGradientBrush.StartPointProperty));
            storyboard.Children.Add(rgbStartAnim);

            PointAnimation rgbEndAnim = new PointAnimation
            {
                From = new Point(w, h),
                To = new Point(2 * w, 2 * h),
                Duration = rgbDuration,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(rgbEndAnim, brush);
            Storyboard.SetTargetProperty(rgbEndAnim, new PropertyPath(LinearGradientBrush.EndPointProperty));
            storyboard.Children.Add(rgbEndAnim);
        }
    }
}
