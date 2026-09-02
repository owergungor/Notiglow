using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GlowBorder.Core.Helpers;
using GlowBorder.Models;
using GlowBorder.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace GlowBorder.UI.Views
{
    public partial class GeneralView : UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;

        public GeneralView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService, GlowManager glowManager)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;

            LoadSettings();
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;

            var current = _settingsService.Current;
            MasterToggle.IsChecked = current.MasterEnabled;
            ToggleStartWithWindows.IsChecked = AutoStartHelper.IsAutoStartEnabled();
            ToggleReduceAnimations.IsChecked = current.ReduceAnimations;

            UpdateThemeButtons(current.Theme);

            EdgePreview.UpdatePreview(current.DefaultColorHex, current.DefaultThickness, current.DefaultGlowSize, current.DefaultIntensity, current.DefaultStyle);
        }

        private void UpdateThemeButtons(AppTheme theme)
        {
            BtnThemeDark.IsChecked = (theme == AppTheme.Dark);
            BtnThemeLight.IsChecked = (theme == AppTheme.Light);
            BtnThemeSystem.IsChecked = (theme == AppTheme.System);
            BtnThemeNightBlue.IsChecked = (theme == AppTheme.LiquidGlass);
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null || sender is not ToggleButton clickedButton) return;

            AppTheme selectedTheme;
            if (clickedButton == BtnThemeDark)
                selectedTheme = AppTheme.Dark;
            else if (clickedButton == BtnThemeLight)
                selectedTheme = AppTheme.Light;
            else if (clickedButton == BtnThemeSystem)
                selectedTheme = AppTheme.System;
            else if (clickedButton == BtnThemeNightBlue)
                selectedTheme = AppTheme.LiquidGlass;
            else
                return;

            UpdateThemeButtons(selectedTheme);

            var settings = _settingsService.Current;
            if (settings.Theme != selectedTheme)
            {
                settings.Theme = selectedTheme;
                _settingsService.Save(settings);
            }
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null || _glowManager == null) return;

            var testProfile = new AppProfile
            {
                AppId = "TestApp",
                Name = "NotiGlow Test",
                ColorHex = _settingsService.Current.DefaultColorHex,
                DurationMs = _settingsService.Current.DefaultDurationMs,
                Intensity = _settingsService.Current.DefaultIntensity,
                Thickness = _settingsService.Current.DefaultThickness,
                GlowSize = _settingsService.Current.DefaultGlowSize,
                Style = _settingsService.Current.DefaultStyle
            };

            _glowManager.TriggerProfile(testProfile);
        }

        private void MasterToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            settings.MasterEnabled = MasterToggle.IsChecked == true;
            _settingsService.Save(settings);
        }

        private void ToggleStartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            bool enable = ToggleStartWithWindows.IsChecked == true;
            AutoStartHelper.SetAutoStart(enable);

            if (_settingsService != null)
            {
                var settings = _settingsService.Current;
                settings.StartWithWindows = enable;
                _settingsService.Save(settings);
            }
        }

        private void ToggleReduceAnimations_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            settings.ReduceAnimations = ToggleReduceAnimations.IsChecked == true;
            _settingsService.Save(settings);
        }
    }
}
