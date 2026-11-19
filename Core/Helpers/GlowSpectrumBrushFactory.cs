using System;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

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

        public static LinearGradientBrush CreateEdgeLinearBrush(double x1, double y1, double x2, double y2, int edgeIndex = 0)
        {
            return CreateScreenSpaceRgbBrush(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        /// <summary>
        /// Creates an absolute screen-space linear gradient brush mapping uniformly across all 4 edges.
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
                brush.GradientStops.Add(new GradientStop { Color = SpectrumColors[i], Offset = i * step });
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
        /// Creates a true circular shockwave RadialGradientBrush with Absolute mapping.
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
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, color.R, color.G, color.B), Offset = 0.0 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, color.R, color.G, color.B), Offset = 0.65 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(140, color.R, color.G, color.B), Offset = 0.80 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0.92 });
            brush.GradientStops.Add(new GradientStop { Color = color, Offset = 0.97 });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, color.R, color.G, color.B), Offset = 1.0 });

            return brush;
        }

        /// <summary>
        /// Populates gradient stops for Sweep beam in RGB mode using the unified spectrum palette.
        /// </summary>
        public static void PopulateSweepRgbStops(GradientStopCollection stops, bool isBloom)
        {
            stops.Clear();
            if (isBloom)
            {
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 0, 77), Offset = 0.0 });
                stops.Add(new GradientStop { Color = Color.FromArgb(90, 255, 0, 77), Offset = 0.20 });
                stops.Add(new GradientStop { Color = Color.FromArgb(190, 0, 230, 118), Offset = 0.50 });
                stops.Add(new GradientStop { Color = Color.FromArgb(90, 124, 58, 237), Offset = 0.80 });
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 124, 58, 237), Offset = 1.0 });
            }
            else
            {
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 0, 77), Offset = 0.0 });
                stops.Add(new GradientStop { Color = Color.FromArgb(140, 255, 0, 77), Offset = 0.15 });
                stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 214, 0), Offset = 0.35 });
                stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0.50 });
                stops.Add(new GradientStop { Color = Color.FromArgb(255, 0, 229, 255), Offset = 0.65 });
                stops.Add(new GradientStop { Color = Color.FromArgb(150, 124, 58, 237), Offset = 0.85 });
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 124, 58, 237), Offset = 1.0 });
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
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 124, 58, 237), Offset = 0.0 });
                stops.Add(new GradientStop { Color = Color.FromArgb(80, 41, 121, 255), Offset = 0.50 });
                stops.Add(new GradientStop { Color = Color.FromArgb(180, 255, 214, 0), Offset = 0.85 });
                stops.Add(new GradientStop { Color = Color.FromArgb(240, 255, 0, 77), Offset = 0.96 });
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 0, 77), Offset = 1.0 });
            }
            else
            {
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 124, 58, 237), Offset = 0.0 });
                stops.Add(new GradientStop { Color = Color.FromArgb(60, 124, 58, 237), Offset = 0.25 });
                stops.Add(new GradientStop { Color = Color.FromArgb(160, 41, 121, 255), Offset = 0.50 });
                stops.Add(new GradientStop { Color = Color.FromArgb(255, 0, 230, 118), Offset = 0.72 });
                stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 214, 0), Offset = 0.86 });
                stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 0, 77), Offset = 0.94 });
                stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0.98 });
                stops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1.0 });
            }
        }

        /// <summary>
        /// Populates gradient stops for Ripple shockwave in RGB mode.
        /// Hollow center (0.0 to 0.65 transparent) followed by rainbow shockwave band with bright white crest.
        /// </summary>
        public static void PopulateRippleRgbStops(System.Collections.Generic.IList<GradientStop> stops)
        {
            stops.Clear();
            stops.Add(new GradientStop { Color = Color.FromArgb(0, 41, 121, 255), Offset = 0.0 });
            stops.Add(new GradientStop { Color = Color.FromArgb(0, 41, 121, 255), Offset = 0.65 });
            stops.Add(new GradientStop { Color = Color.FromArgb(160, 124, 58, 237), Offset = 0.75 });
            stops.Add(new GradientStop { Color = Color.FromArgb(255, 0, 229, 255), Offset = 0.83 });
            stops.Add(new GradientStop { Color = Color.FromArgb(255, 0, 230, 118), Offset = 0.89 });
            stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0.94 });
            stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 214, 0), Offset = 0.96 });
            stops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 0, 77), Offset = 0.98 });
            stops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 0, 77), Offset = 1.0 });
        }
    }
}
