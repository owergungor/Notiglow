using System;
using System.Windows;
using System.Windows.Controls;
using NotiGlow.Models;
using NotiGlow.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace NotiGlow.UI.Views
{
    public partial class AppearanceView : UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;
        private bool _isLoading = false;

        private System.Windows.Threading.DispatcherTimer? _saveDebounceTimer;

        public AppearanceView()
        {
            InitializeComponent();
            Unloaded += (s, e) => FlushPendingSave();
            IsVisibleChanged += (s, e) =>
            {
                if (!IsVisible)
                {
                    FlushPendingSave();
                }
            };
        }

        private void FlushPendingSave()
        {
            if (_saveDebounceTimer != null && _saveDebounceTimer.IsEnabled)
            {
                _saveDebounceTimer.Stop();
                if (_settingsService != null)
                {
                    _settingsService.Save(_settingsService.Current);
                }
            }
        }

        private void ScheduleSettingsSave()
        {
            if (_settingsService == null) return;

            if (_saveDebounceTimer == null)
            {
                _saveDebounceTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                _saveDebounceTimer.Tick += (s, e) =>
                {
                    _saveDebounceTimer.Stop();
                    if (_settingsService != null)
                    {
                        _settingsService.Save(_settingsService.Current);
                    }
                };
            }

            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        public void Initialize(SettingsService settingsService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            LoadDefaults();
        }

        private void LoadDefaults()
        {
            if (_settingsService == null) return;
            _isLoading = true;

            var current = _settingsService.Current;
            SldDefDuration.Value = current.DefaultDurationMs;
            SldDefIntensity.Value = Math.Round(current.DefaultIntensity * 100);
            SldDefGlowSize.Value = Math.Round(current.DefaultGlowSize);
            DefColorPicker.SelectedColorHex = current.DefaultColorHex;

            foreach (ComboBoxItem item in CmbDefaultStyle.Items)
            {
                if (item.Tag?.ToString() == current.DefaultStyle.ToString())
                {
                    CmbDefaultStyle.SelectedItem = item;
                    break;
                }
            }

            _isLoading = false;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (_settingsService == null || _isLoading) return;

            double sec = SldDefDuration.Value / 1000.0;
            TxtDefDurationVal.Text = (sec % 1 == 0) ? $"{sec:0}s ({SldDefDuration.Value} ms)" : $"{sec:0.0}s ({SldDefDuration.Value} ms)";
            TxtDefIntensityVal.Text = $"{Math.Round(SldDefIntensity.Value):0}%";
            TxtDefGlowSizeVal.Text = $"{Math.Round(SldDefGlowSize.Value):0} px";

            var settings = _settingsService.Current;
            settings.DefaultDurationMs = (int)SldDefDuration.Value;
            settings.DefaultIntensity = Math.Round(SldDefIntensity.Value) / 100.0;
            settings.DefaultGlowSize = Math.Round(SldDefGlowSize.Value);
            settings.DefaultColorHex = DefColorPicker.SelectedColorHex;

            if (CmbDefaultStyle.SelectedItem is ComboBoxItem item && Enum.TryParse<GlowStyle>(item.Tag?.ToString(), out var style))
            {
                settings.DefaultStyle = style;
            }

            ScheduleSettingsSave();

            AppearanceEdgePreview.UpdatePreview(settings.DefaultColorHex, settings.DefaultThickness, settings.DefaultGlowSize, settings.DefaultIntensity, settings.DefaultStyle);
        }

        private void Default_Changed(object sender, SelectionChangedEventArgs e) => UpdatePreview();
        private void Default_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreview();
        private void DefColorPicker_ColorChanged(object? sender, string e) => UpdatePreview();

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;
            var testProfile = new AppProfile
            {
                AppId = "TestApp",
                Name = "NotiGlow Test",
                ColorHex = _settingsService.Current.DefaultColorHex,
                DurationMs = _settingsService.Current.DefaultDurationMs,
                Intensity = _settingsService.Current.DefaultIntensity,
                Style = _settingsService.Current.DefaultStyle,
                Thickness = _settingsService.Current.DefaultThickness,
                GlowSize = _settingsService.Current.DefaultGlowSize
            };
            _glowManager.TriggerProfile(testProfile);
        }
    }
}
