using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.UI.Views
{
    public sealed partial class AppearanceView : UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;
        private bool _isLoading = false;
        private DispatcherTimer? _saveDebounceTimer;

        public AppearanceView()
        {
            this.InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(RootScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);
            };
            Unloaded += (s, e) => FlushPendingSave();
        }

        private void OnLanguageChanged(object? sender, string lang)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshStyleComboBox();
                UpdateLocalizedTexts();
            });
        }

        private void UpdateLocalizedTexts()
        {
            TxtAppearanceTitle.Text = LocalizationService.Instance.GetString("Appearance.Title");
            TxtAppearanceSubtitle.Text = LocalizationService.Instance.GetString("Appearance.Subtitle");
            TxtBtnTestAnimation.Text = LocalizationService.Instance.GetString("Common.TestAnimation");
            TxtSectionAnimation.Text = LocalizationService.Instance.GetString("Appearance.AnimationSection");
            TxtDefaultStyle.Text = LocalizationService.Instance.GetString("Appearance.DefaultStyle");
            TxtDefaultStyleDesc.Text = LocalizationService.Instance.GetString("Appearance.DefaultStyleDesc");
            TxtDuration.Text = LocalizationService.Instance.GetString("Appearance.Duration");
            TxtIntensity.Text = LocalizationService.Instance.GetString("Appearance.Intensity");
            TxtSectionGeometry.Text = LocalizationService.Instance.GetString("Appearance.GeometrySection");
            TxtThickness.Text = LocalizationService.Instance.GetString("Appearance.Thickness");
            TxtGlowSize.Text = LocalizationService.Instance.GetString("Appearance.GlowSize");
            TxtCornerRounding.Text = LocalizationService.Instance.GetString("Appearance.CornerRounding");
            TxtSectionColorPreview.Text = LocalizationService.Instance.GetString("Appearance.ColorSection");
            TxtLivePreview.Text = LocalizationService.Instance.GetString("Applications.LivePreview");
        }

        private void RefreshStyleComboBox()
        {
            if (CmbDefaultStyle == null) return;
            var currentTag = (CmbDefaultStyle.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            foreach (var item in CmbDefaultStyle.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is string tag)
                {
                    string styleKey = $"Style.{tag}";
                    string str = LocalizationService.Instance.GetString(styleKey);
                    if (!string.IsNullOrEmpty(str) && str != styleKey)
                    {
                        cbi.Content = str;
                    }
                }
            }
            if (!string.IsNullOrEmpty(currentTag))
            {
                foreach (ComboBoxItem item in CmbDefaultStyle.Items)
                {
                    if (item.Tag?.ToString() == currentTag)
                    {
                        CmbDefaultStyle.SelectedItem = item;
                        break;
                    }
                }
            }
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
                _saveDebounceTimer = new DispatcherTimer
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
            LoadSettings();
            UpdateLocalizedTexts();
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;
            _isLoading = true;

            var settings = _settingsService.Current;

            SldDefaultDuration.Value = settings.DefaultDurationMs;
            SldDefaultIntensity.Value = settings.DefaultIntensity * 100.0;
            SldDefaultThickness.Value = settings.DefaultThickness;
            SldDefaultGlowSize.Value = settings.DefaultGlowSize;
            SldCornerRounding.Value = settings.CornerRadius;
            DefaultColorPicker.SelectedColorHex = settings.DefaultColorHex;

            foreach (ComboBoxItem item in CmbDefaultStyle.Items)
            {
                if (string.Equals(item.Tag?.ToString(), settings.DefaultStyle.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    CmbDefaultStyle.SelectedItem = item;
                    break;
                }
            }

            _isLoading = false;
            UpdatePreview();
        }

        private void Default_Changed(object sender, object e)
        {
            if (_isLoading || _settingsService == null) return;

            var settings = _settingsService.Current;

            settings.DefaultDurationMs = (int)SldDefaultDuration.Value;
            settings.DefaultIntensity = Math.Round(SldDefaultIntensity.Value) / 100.0;
            settings.DefaultThickness = Math.Round(SldDefaultThickness.Value);
            settings.DefaultGlowSize = Math.Round(SldDefaultGlowSize.Value);
            settings.CornerRadius = Math.Round(SldCornerRounding.Value);

            if (CmbDefaultStyle.SelectedItem is ComboBoxItem styleItem &&
                Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                settings.DefaultStyle = style;
            }

            ThemeService.ApplyCornerRadius(settings.CornerRadius);

            ScheduleSettingsSave();
            UpdatePreview();
        }

        private void DefaultColorPicker_ColorChanged(object? sender, string colorHex)
        {
            if (_isLoading || _settingsService == null) return;

            _settingsService.Current.DefaultColorHex = colorHex;
            ScheduleSettingsSave();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            double sec = SldDefaultDuration.Value / 1000.0;
            TxtDurationVal.Text = $"{sec:0.#} s";
            TxtIntensityVal.Text = $"{Math.Round(SldDefaultIntensity.Value):0}%";
            TxtThicknessVal.Text = $"{Math.Round(SldDefaultThickness.Value):0} px";
            TxtGlowSizeVal.Text = $"{Math.Round(SldDefaultGlowSize.Value):0} px";
            TxtCornerRoundingVal.Text = $"{Math.Round(SldCornerRounding.Value):0} px";

            GlowStyle currentStyle = GlowStyle.Pulse;
            if (CmbDefaultStyle.SelectedItem is ComboBoxItem styleItem &&
                Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                currentStyle = style;
            }

            AppearancePreview.UpdatePreview(
                DefaultColorPicker.SelectedColorHex,
                SldDefaultThickness.Value,
                SldDefaultGlowSize.Value,
                SldDefaultIntensity.Value / 100.0,
                currentStyle,
                SldCornerRounding.Value);
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;

            var profile = new AppProfile
            {
                AppId = "AppearanceTest",
                Name = "NotiGlow Test",
                ColorHex = DefaultColorPicker.SelectedColorHex,
                DurationMs = (int)SldDefaultDuration.Value,
                Intensity = SldDefaultIntensity.Value / 100.0,
                Thickness = SldDefaultThickness.Value,
                GlowSize = SldDefaultGlowSize.Value,
                CornerRadius = SldCornerRounding.Value
            };

            if (CmbDefaultStyle.SelectedItem is ComboBoxItem styleItem &&
                Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                profile.Style = style;
            }

            _glowManager.TriggerProfile(profile);
        }
    }
}
