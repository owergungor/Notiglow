using System;
using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Helpers;

namespace NotiGlow.UI.Controls
{
    public sealed partial class ColorPickerControl : UserControl
    {
        public static readonly DependencyProperty SelectedColorHexProperty =
            DependencyProperty.Register(
                nameof(SelectedColorHex),
                typeof(string),
                typeof(ColorPickerControl),
                new PropertyMetadata("#5865F2", OnSelectedColorHexChanged));

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

        public ColorPickerControl()
        {
            this.InitializeComponent();
            PopulatePresets();

            BtnRgbOption.PointerPressed += (s, e) =>
            {
                SelectedColorHex = "RGB";
            };
        }

        private void PopulatePresets()
        {
            PresetPalette.Items.Clear();
            foreach (var preset in Presets)
            {
                Color color = ColorHelper.ParseColor(preset.Hex);

                var border = new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(3),
                    Background = new SolidColorBrush(color),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    BorderThickness = new Thickness(1.5),
                    Tag = preset.Hex
                };

                ToolTipService.SetToolTip(border, $"{preset.Name} ({preset.Hex})");

                border.PointerPressed += (s, e) =>
                {
                    SelectedColorHex = preset.Hex;
                };

                PresetPalette.Items.Add(border);
            }
        }

        private static void OnSelectedColorHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPickerControl control && e.NewValue is string newHex)
            {
                control.UpdateVisuals(newHex);
                control.ColorChanged?.Invoke(control, newHex);
            }
        }

        private void UpdateVisuals(string hex)
        {
            if (_isUpdating) return;
            _isUpdating = true;
            try
            {
                bool isRgb = ColorHelper.IsRgbSpectrum(hex);
                TxtRgbCheck.Visibility = isRgb ? Visibility.Visible : Visibility.Collapsed;

                if (!isRgb)
                {
                    Color color = ColorHelper.ParseColor(hex);
                    ColorPreviewBrush.Color = color;
                    HexInput.Text = hex;
                    NativeColorPicker.Color = color;
                }
                else
                {
                    HexInput.Text = "RGB";
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void BtnToggleCustom_Click(object sender, RoutedEventArgs e)
        {
            PnlCustomHex.Visibility = PnlCustomHex.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void NativeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isUpdating) return;
            SelectedColorHex = ColorHelper.ToCanonicalHex(args.NewColor);
        }

        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            string text = HexInput.Text.Trim();
            if (ColorHelper.IsRgbSpectrum(text))
            {
                SelectedColorHex = "RGB";
            }
            else if (ColorHelper.TryParseHex(text, out _))
            {
                SelectedColorHex = text.StartsWith("#") ? text : "#" + text;
            }
        }

        private void BtnCopyHex_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = new DataPackage();
                package.SetText(SelectedColorHex);
                Clipboard.SetContent(package);
            }
            catch { }
        }

        public static double HueFromPosition(double x, double width) =>
            width <= 0 ? 0 : Math.Clamp(x / width, 0, 1) * 360;

        public static (double Saturation, double Value) SatValFromPosition(Windows.Foundation.Point pos, double width, double height) =>
            (width <= 0 ? 0 : Math.Clamp(pos.X / width, 0, 1),
             height <= 0 ? 0 : 1.0 - Math.Clamp(pos.Y / height, 0, 1));
    }
}
