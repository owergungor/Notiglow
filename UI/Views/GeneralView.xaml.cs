using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace NotiGlow.UI.Views
{
    public partial class GeneralView : UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;
        private NotificationService? _notificationService;

        public GeneralView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService, GlowManager glowManager, NotificationService? notificationService = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            _notificationService = notificationService;

            if (_notificationService != null)
            {
                _notificationService.AccessStatusChanged += (s, status) => Dispatcher?.Invoke(() => UpdateListenerStatus(status));
                UpdateListenerStatus(_notificationService.CurrentAccessStatus);
            }

            LoadSettings();
        }

        private void UpdateListenerStatus(Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus status)
        {
            if (status == Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Allowed)
            {
                IconListenerStatus.Symbol = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24;
                IconListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF25D366"));
                TxtListenerStatus.Text = "Active & Listening";
                TxtListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF25D366"));
                BtnFixAccess.Visibility = Visibility.Collapsed;
            }
            else if (status == Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Denied)
            {
                IconListenerStatus.Symbol = Wpf.Ui.Controls.SymbolRegular.DismissCircle24;
                IconListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFF5409"));
                TxtListenerStatus.Text = "Access Denied by Windows";
                TxtListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFF5409"));
                BtnFixAccess.Visibility = Visibility.Visible;
            }
            else
            {
                IconListenerStatus.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24;
                IconListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFD32A"));
                TxtListenerStatus.Text = "Permission Required";
                TxtListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFD32A"));
                BtnFixAccess.Visibility = Visibility.Visible;
            }
        }

        private void BtnFixAccess_Click(object sender, RoutedEventArgs e)
        {
            NotificationService.OpenWindowsNotificationSettings();
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
