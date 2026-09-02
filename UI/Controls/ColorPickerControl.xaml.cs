using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Cursors = System.Windows.Input.Cursors;
using Brushes = System.Windows.Media.Brushes;
using Clipboard = System.Windows.Clipboard;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace GlowBorder.UI.Controls
{
    public partial class ColorPickerControl : UserControl
    {
        public static readonly DependencyProperty SelectedColorHexProperty =
            DependencyProperty.Register(
                nameof(SelectedColorHex),
                typeof(string),
                typeof(ColorPickerControl),
                new FrameworkPropertyMetadata("#5865F2", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorHexChanged));

        public string SelectedColorHex
        {
            get => (string)GetValue(SelectedColorHexProperty);
            set => SetValue(SelectedColorHexProperty, value);
        }

        public event EventHandler<string>? ColorChanged;

        private record ColorPreset(string Hex, string Name);

        private static readonly List<ColorPreset> Presets = new()
        {
            new("#5865F2", "Discord Blue"),
            new("#24A1DE", "Telegram Cyan"),
            new("#6264A7", "Teams Purple"),
            new("#A55EEA", "Violet"),
            new("#FF007F", "Neon Pink"),
            new("#FF5252", "Crimson Red"),
            new("#FF793F", "Vibrant Orange"),
            new("#FFD32A", "Warm Yellow"),
            new("#1DB954", "Spotify Green"),
            new("#25D366", "WhatsApp Green"),
            new("#66C0F4", "Steam Blue"),
            new("#FFFFFF", "Pure White")
        };

        private bool _isUpdating = false;
        private bool _isDraggingSatVal = false;
        private double _currentHue = 234;
        private double _currentSat = 0.63;
        private double _currentVal = 0.95;

        public ColorPickerControl()
        {
            InitializeComponent();
            PopulatePresets();
        }

        private void PopulatePresets()
        {
            PresetPalette.Children.Clear();
            foreach (var preset in Presets)
            {
                Color color;
                try
                {
                    color = (Color)ColorConverter.ConvertFromString(preset.Hex);
                }
                catch
                {
                    color = Colors.Purple;
                }

                bool isWhite = preset.Hex.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase);

                var border = new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(3),
                    Background = new SolidColorBrush(color),
                    BorderBrush = isWhite ? new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    BorderThickness = new Thickness(1.5),
                    ToolTip = $"{preset.Name} ({preset.Hex})",
                    Cursor = Cursors.Hand,
                    Tag = preset.Hex
                };

                var checkIcon = new TextBlock
                {
                    Text = "✓",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = isWhite ? Brushes.Black : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                    Visibility = SelectedColorHex.Equals(preset.Hex, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed
                };

                border.Child = checkIcon;
                GlowBorder.UI.Animations.ButtonPressAnimationBehavior.Attach(border);

                Action handleSelection = () =>
                {
                    SelectedColorHex = preset.Hex;
                    UpdateVisuals(preset.Hex);
                };

                border.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    handleSelection();
                    e.Handled = true;
                };

                border.MouseLeftButtonDown += (s, e) =>
                {
                    handleSelection();
                    e.Handled = true;
                };

                PresetPalette.Children.Add(border);
            }
        }

        private static void OnSelectedColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPickerControl control && !control._isUpdating)
            {
                string newHex = (string)e.NewValue;
                control.UpdateVisuals(newHex);
            }
        }

        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            string input = HexInput.Text.Trim();
            if (!input.StartsWith("#")) input = "#" + input;

            if (input.Length == 7 || input.Length == 9)
            {
                try
                {
                    Color color = (Color)ColorConverter.ConvertFromString(input);
                    _isUpdating = true;
                    SelectedColorHex = input;
                    ColorPreviewBrush.Color = color;
                    
                    var (h, s, v) = ColorToHsv(color);
                    _currentHue = h;
                    _currentSat = s;
                    _currentVal = v;

                    SldHue.Value = h;
                    HueBaseBrush.Color = ColorFromHsv(h, 1.0, 1.0);
                    UpdateThumbPosition();

                    _isUpdating = false;

                    ColorChanged?.Invoke(this, input);
                    UpdateSelectionVisuals(input);
                }
                catch
                {
                    // Invalid hex string during typing
                }
            }
        }

        private void UpdateVisuals(string hex)
        {
            _isUpdating = true;
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                HexInput.Text = hex;
                Color color = (Color)ColorConverter.ConvertFromString(hex);
                ColorPreviewBrush.Color = color;

                var (h, s, v) = ColorToHsv(color);
                _currentHue = h;
                _currentSat = s;
                _currentVal = v;

                SldHue.Value = h;
                HueBaseBrush.Color = ColorFromHsv(h, 1.0, 1.0);
                UpdateThumbPosition();

                ColorChanged?.Invoke(this, hex);
                UpdateSelectionVisuals(hex);
            }
            catch
            {
                // Fallback
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateSelectionVisuals(string selectedHex)
        {
            foreach (var child in PresetPalette.Children)
            {
                if (child is Border b && b.Tag is string hex)
                {
                    bool isSelected = hex.Equals(selectedHex, StringComparison.OrdinalIgnoreCase);
                    b.BorderThickness = isSelected ? new Thickness(3) : new Thickness(1.5);
                    if (isSelected)
                    {
                        b.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5865F2"));
                    }
                    else
                    {
                        bool isWhite = hex.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase);
                        b.BorderBrush = isWhite ? new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
                    }

                    if (b.Child is TextBlock checkIcon)
                    {
                        checkIcon.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private void BtnToggleCustom_Click(object sender, RoutedEventArgs e)
        {
            PnlCustomHex.Visibility = PnlCustomHex.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (PnlCustomHex.Visibility == Visibility.Visible)
            {
                UpdateThumbPosition();
            }
        }

        private void BtnCopyHex_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(HexInput.Text);
            }
            catch
            {
                // Silently handle clipboard exceptions
            }
        }

        private void SldHue_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdating) return;
            _currentHue = SldHue.Value;
            HueBaseBrush.Color = ColorFromHsv(_currentHue, 1.0, 1.0);
            ApplyHsvColor();
        }

        private void SatValCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSatVal = true;
            SatValCanvas.CaptureMouse();
            UpdateSatValFromPoint(e.GetPosition(SatValCanvas));
        }

        private void SatValCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingSatVal)
            {
                _isDraggingSatVal = false;
                SatValCanvas.ReleaseMouseCapture();
            }
        }

        private void SatValCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSatVal)
            {
                UpdateSatValFromPoint(e.GetPosition(SatValCanvas));
            }
        }

        private void UpdateSatValFromPoint(Point pos)
        {
            double width = SatValCanvas.ActualWidth > 0 ? SatValCanvas.ActualWidth : 250;
            double height = SatValCanvas.ActualHeight > 0 ? SatValCanvas.ActualHeight : 130;

            double x = Math.Clamp(pos.X, 0, width);
            double y = Math.Clamp(pos.Y, 0, height);

            _currentSat = x / width;
            _currentVal = 1.0 - (y / height);

            Canvas.SetLeft(SatValThumb, Math.Clamp(x - 7, 0, width - 14));
            Canvas.SetTop(SatValThumb, Math.Clamp(y - 7, 0, height - 14));

            ApplyHsvColor();
        }

        private void UpdateThumbPosition()
        {
            double width = SatValCanvas.ActualWidth > 0 ? SatValCanvas.ActualWidth : 250;
            double height = SatValCanvas.ActualHeight > 0 ? SatValCanvas.ActualHeight : 130;

            double x = _currentSat * width;
            double y = (1.0 - _currentVal) * height;

            Canvas.SetLeft(SatValThumb, Math.Clamp(x - 7, 0, width - 14));
            Canvas.SetTop(SatValThumb, Math.Clamp(y - 7, 0, height - 14));
        }

        private void ApplyHsvColor()
        {
            Color color = ColorFromHsv(_currentHue, _currentSat, _currentVal);
            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            _isUpdating = true;
            HexInput.Text = hex;
            ColorPreviewBrush.Color = color;
            SelectedColorHex = hex;
            _isUpdating = false;

            ColorChanged?.Invoke(this, hex);
            UpdateSelectionVisuals(hex);
        }

        public static Color ColorFromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            double vVal = value * 255;
            byte v = Convert.ToByte(Math.Clamp(vVal, 0, 255));
            byte p = Convert.ToByte(Math.Clamp(vVal * (1 - saturation), 0, 255));
            byte q = Convert.ToByte(Math.Clamp(vVal * (1 - f * saturation), 0, 255));
            byte t = Convert.ToByte(Math.Clamp(vVal * (1 - (1 - f) * saturation), 0, 255));

            if (hi == 0) return Color.FromRgb(v, t, p);
            if (hi == 1) return Color.FromRgb(q, v, p);
            if (hi == 2) return Color.FromRgb(p, v, t);
            if (hi == 3) return Color.FromRgb(p, q, v);
            if (hi == 4) return Color.FromRgb(t, p, v);
            return Color.FromRgb(v, p, q);
        }

        public static (double h, double s, double v) ColorToHsv(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta > 0)
            {
                if (Math.Abs(max - r) < 0.001) h = 60 * (((g - b) / delta) % 6);
                else if (Math.Abs(max - g) < 0.001) h = 60 * (((b - r) / delta) + 2);
                else if (Math.Abs(max - b) < 0.001) h = 60 * (((r - g) / delta) + 4);
            }
            if (h < 0) h += 360;

            double s = Math.Abs(max) < 0.001 ? 0 : delta / max;
            double v = max;

            return (h, s, v);
        }
    }
}
