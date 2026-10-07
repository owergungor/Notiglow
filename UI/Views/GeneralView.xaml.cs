using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Animations;
using UserControl = System.Windows.Controls.UserControl;
using ThemeMode = NotiGlow.Models.ThemeMode;
using RadioButton = System.Windows.Controls.RadioButton;

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

            LocalizationService.Instance.LanguageChanged += (s, e) =>
            {
                Dispatcher?.Invoke(() =>
                {
                    if (_notificationService != null)
                    {
                        UpdateListenerStatus(_notificationService.CurrentAccessStatus);
                    }
                });
            };

            LoadSettings();
        }

        private void UpdateListenerStatus(Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus status)
        {
            if (status == Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Allowed)
            {
                IconListenerStatus.Symbol = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24;
                IconListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF25D366"));
                TxtListenerStatus.Text = LocalizationService.Instance.GetString("General.ListenerActive");
                TxtListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF25D366"));
                BtnFixAccess.Visibility = Visibility.Collapsed;
            }
            else if (status == Windows.UI.Notifications.Management.UserNotificationListenerAccessStatus.Denied)
            {
                IconListenerStatus.Symbol = Wpf.Ui.Controls.SymbolRegular.DismissCircle24;
                IconListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFF5409"));
                TxtListenerStatus.Text = LocalizationService.Instance.GetString("General.ListenerPermissionRequired");
                TxtListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFF5409"));
                BtnFixAccess.Visibility = Visibility.Visible;
            }
            else
            {
                IconListenerStatus.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24;
                IconListenerStatus.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFD32A"));
                TxtListenerStatus.Text = LocalizationService.Instance.GetString("General.ListenerPermissionRequired");
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
            MotionPolicy.Update(current.ReduceAnimations);

            // Load Theme Mode
            BtnModeSystem.IsChecked = (current.ThemeMode == ThemeMode.System);
            BtnModeLight.IsChecked = (current.ThemeMode == ThemeMode.Light);
            BtnModeDark.IsChecked = (current.ThemeMode == ThemeMode.Dark);

            // Load Color Theme
            UpdateColorThemeSelection(current.ColorTheme);
        }

        private void UpdateColorThemeSelection(ColorTheme colorTheme)
        {
            if (CmbColorTheme == null) return;

            string targetTag = colorTheme.ToString();
            ComboBoxItem? matchingItem = null;
            ComboBoxItem? standardItem = null;

            foreach (var item in CmbColorTheme.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is string tag)
                {
                    if (string.Equals(tag, targetTag, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingItem = cbi;
                        break;
                    }
                    if (string.Equals(tag, "Standard", StringComparison.OrdinalIgnoreCase))
                    {
                        standardItem = cbi;
                    }
                }
            }

            // Fallback safely to Standard if tag is unknown
            CmbColorTheme.SelectedItem = matchingItem ?? standardItem;
        }

        private void ThemeMode_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            ThemeMode selectedMode = ThemeMode.System;
            if (BtnModeLight.IsChecked == true)
                selectedMode = ThemeMode.Light;
            else if (BtnModeDark.IsChecked == true)
                selectedMode = ThemeMode.Dark;
            else if (BtnModeSystem.IsChecked == true)
                selectedMode = ThemeMode.System;

            var settings = _settingsService.Current;
            if (settings.ThemeMode != selectedMode)
            {
                settings.ThemeMode = selectedMode;
                _settingsService.Save(settings);
                ThemeService.ApplyTheme(settings.ColorTheme, settings.ThemeMode);
            }
        }

        private void CmbColorTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingsService == null || CmbColorTheme.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;

            if (Enum.TryParse<ColorTheme>(tag, out var selectedTheme))
            {
                var settings = _settingsService.Current;
                if (settings.ColorTheme != selectedTheme)
                {
                    settings.ColorTheme = selectedTheme;
                    _settingsService.Save(settings);
                    ThemeService.ApplyTheme(settings.ColorTheme, settings.ThemeMode);
                }
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
            MotionPolicy.Update(settings.ReduceAnimations);
        }
    }
}
