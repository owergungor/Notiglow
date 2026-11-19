using System;
using Windows.UI.Notifications.Management;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using ThemeMode = NotiGlow.Models.ThemeMode;

namespace NotiGlow.UI.Views
{
    public sealed partial class GeneralView : UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;
        private NotificationService? _notificationService;
        private bool _isUpdatingLanguageSelection = false;

        public GeneralView()
        {
            this.InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(RootScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);
                UpdateLocalizedTexts();
            };
        }

        public void Initialize(SettingsService settingsService, GlowManager glowManager, NotificationService? notificationService = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            _notificationService = notificationService;

            if (_notificationService != null)
            {
                _notificationService.AccessStatusChanged += OnAccessStatusChanged;
                UpdateListenerStatus(_notificationService.CurrentAccessStatus);
            }

            LoadSettings();
            UpdateLocalizedTexts();
        }

        private void OnAccessStatusChanged(object? sender, UserNotificationListenerAccessStatus status)
        {
            DispatcherQueue.TryEnqueue(() => UpdateListenerStatus(status));
        }

        private void OnLanguageChanged(object? sender, string languageCode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_notificationService != null)
                {
                    UpdateListenerStatus(_notificationService.CurrentAccessStatus);
                }
                UpdateLanguageSelection(languageCode);
                UpdateLocalizedTexts();
            });
        }

        public void UpdateLocalizedTexts()
        {
            TxtGeneralTitle.Text = LocalizationService.Instance.GetString("General.Title");
            TxtGeneralSubtitle.Text = LocalizationService.Instance.GetString("General.Subtitle");
            TxtBtnTestAnimation.Text = LocalizationService.Instance.GetString("Common.TestAnimation");
            TxtSectionSystem.Text = LocalizationService.Instance.GetString("General.SystemSection");
            TxtActivation.Text = LocalizationService.Instance.GetString("General.Activation");
            TxtActivationDesc.Text = LocalizationService.Instance.GetString("General.ActivationDesc");
            TxtSectionPermissions.Text = LocalizationService.Instance.GetString("General.PermissionsSection");
            TxtListener.Text = LocalizationService.Instance.GetString("General.Listener");
            TxtListenerDesc.Text = LocalizationService.Instance.GetString("General.ListenerDesc");
            TxtStartWithWindows.Text = LocalizationService.Instance.GetString("General.StartWithWindows");
            TxtStartWithWindowsDesc.Text = LocalizationService.Instance.GetString("General.StartWithWindowsDesc");
            TxtShowInTaskbar.Text = LocalizationService.Instance.GetString("General.ShowInTaskbar");
            TxtShowInTaskbarDesc.Text = LocalizationService.Instance.GetString("General.ShowInTaskbarDesc");
            TxtSectionPersonalization.Text = LocalizationService.Instance.GetString("General.PersonalizationSection");
            TxtReduceAnimations.Text = LocalizationService.Instance.GetString("General.ReduceAnimations");
            TxtReduceAnimationsDesc.Text = LocalizationService.Instance.GetString("General.ReduceAnimationsDesc");
            TxtThemeMode.Text = LocalizationService.Instance.GetString("General.ThemeMode");
            TxtThemeModeDesc.Text = LocalizationService.Instance.GetString("General.ThemeModeDesc");
            BtnModeSystem.Content = LocalizationService.Instance.GetString("General.ThemeSystem");
            BtnModeLight.Content = LocalizationService.Instance.GetString("General.ThemeLight");
            BtnModeDark.Content = LocalizationService.Instance.GetString("General.ThemeDark");
            TxtColorTheme.Text = LocalizationService.Instance.GetString("General.ColorTheme");
            TxtColorThemeDesc.Text = LocalizationService.Instance.GetString("General.ColorThemeDesc");
            TxtWindowMaterial.Text = LocalizationService.Instance.GetString("General.WindowMaterial");
            TxtWindowMaterialDesc.Text = LocalizationService.Instance.GetString("General.WindowMaterialDesc");
            TxtMatAcrylic.Text = LocalizationService.Instance.GetString("Material.Acrylic");
            TxtMatMica.Text = LocalizationService.Instance.GetString("Material.Mica");
            TxtMatMicaAlt.Text = LocalizationService.Instance.GetString("Material.MicaAlt");
            TxtLanguage.Text = LocalizationService.Instance.GetString("General.Language");
            TxtLanguageDesc.Text = LocalizationService.Instance.GetString("General.LanguageDesc");
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;

            MasterToggle.IsOn = settings.MasterEnabled;
            ToggleStartWithWindows.IsOn = settings.StartWithWindows;
            ToggleShowInTaskbar.IsOn = settings.ShowInTaskbar;
            ToggleReduceAnimations.IsOn = settings.ReduceAnimations;

            switch (settings.ThemeMode)
            {
                case ThemeMode.Light:
                    BtnModeLight.IsChecked = true;
                    break;
                case ThemeMode.Dark:
                    BtnModeDark.IsChecked = true;
                    break;
                default:
                    BtnModeSystem.IsChecked = true;
                    break;
            }

            string themeName = settings.ColorTheme.ToString();
            bool themeMatched = false;
            foreach (ComboBoxItem item in CmbColorTheme.Items)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (string.Equals(tag, themeName, StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(themeName, "Windows", StringComparison.OrdinalIgnoreCase) && string.Equals(tag, "Windows", StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(themeName, "System", StringComparison.OrdinalIgnoreCase) && string.Equals(tag, "Windows", StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(themeName, "Standard", StringComparison.OrdinalIgnoreCase) && string.Equals(tag, "Blue", StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(themeName, "Vercel", StringComparison.OrdinalIgnoreCase) && string.Equals(tag, "Blue", StringComparison.OrdinalIgnoreCase)) ||
                    (string.Equals(themeName, "Blue", StringComparison.OrdinalIgnoreCase) && string.Equals(tag, "Blue", StringComparison.OrdinalIgnoreCase)))
                {
                    CmbColorTheme.SelectedItem = item;
                    themeMatched = true;
                    break;
                }
            }
            if (!themeMatched && CmbColorTheme.Items.Count > 0)
            {
                CmbColorTheme.SelectedIndex = 0; // Default to Windows (System Accent)
            }

            string matName = settings.WindowMaterial.ToString();
            foreach (ComboBoxItem item in CmbWindowMaterial.Items)
            {
                if (item.Tag?.ToString() == matName)
                {
                    CmbWindowMaterial.SelectedItem = item;
                    break;
                }
            }

            UpdateLanguageSelection(settings.AppLanguage);
        }

        private void UpdateLanguageSelection(string languageCode)
        {
            _isUpdatingLanguageSelection = true;
            try
            {
                foreach (ComboBoxItem item in CmbLanguage.Items)
                {
                    if (item.Tag is string tag && tag.Equals(languageCode, StringComparison.OrdinalIgnoreCase))
                    {
                        CmbLanguage.SelectedItem = item;
                        break;
                    }
                }
            }
            finally
            {
                _isUpdatingLanguageSelection = false;
            }
        }

        private void UpdateListenerStatus(UserNotificationListenerAccessStatus status)
        {
            if (status == UserNotificationListenerAccessStatus.Allowed)
            {
                IconListenerStatus.Glyph = "\uE73E"; // Checkmark
                IconListenerStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 211, 102));
                TxtListenerStatus.Text = LocalizationService.Instance.GetString("General.ListenerActive");
                TxtListenerStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 37, 211, 102));
                BtnFixAccess.Visibility = Visibility.Collapsed;
            }
            else
            {
                IconListenerStatus.Glyph = "\uE7BA"; // Warning
                IconListenerStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
                TxtListenerStatus.Text = LocalizationService.Instance.GetString("General.ListenerDenied");
                TxtListenerStatus.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
                BtnFixAccess.Visibility = Visibility.Visible;
            }
        }

        private void MasterToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.MasterEnabled != MasterToggle.IsOn)
            {
                settings.MasterEnabled = MasterToggle.IsOn;
                _settingsService.Save(settings);
            }
        }

        private void ToggleStartWithWindows_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.StartWithWindows != ToggleStartWithWindows.IsOn)
            {
                settings.StartWithWindows = ToggleStartWithWindows.IsOn;
                _settingsService.Save(settings);
                AutoStartHelper.SetAutoStart(settings.StartWithWindows);
            }
        }

        private void ToggleShowInTaskbar_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.ShowInTaskbar != ToggleShowInTaskbar.IsOn)
            {
                settings.ShowInTaskbar = ToggleShowInTaskbar.IsOn;
                _settingsService.Save(settings);

                if (App.CurrentMainWindow != null)
                {
                    App.CurrentMainWindow.AppWindow.IsShownInSwitchers = settings.ShowInTaskbar;
                }
            }
        }

        private void ToggleReduceAnimations_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.ReduceAnimations != ToggleReduceAnimations.IsOn)
            {
                settings.ReduceAnimations = ToggleReduceAnimations.IsOn;
                _settingsService.Save(settings);
                NotiGlow.UI.Animations.MotionPolicy.Update(settings.ReduceAnimations);
            }
        }

        private void ThemeMode_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            ThemeMode mode = ThemeMode.System;
            if (BtnModeLight.IsChecked == true) mode = ThemeMode.Light;
            else if (BtnModeDark.IsChecked == true) mode = ThemeMode.Dark;

            var settings = _settingsService.Current;
            if (settings.ThemeMode != mode)
            {
                settings.ThemeMode = mode;
                _settingsService.Save(settings);
                ThemeService.ApplyTheme(settings.ColorTheme, mode);
                App.CurrentMainWindow?.ApplyThemeVisuals(settings.ColorTheme, mode);
            }
        }

        private void CmbColorTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingsService == null) return;
            if (CmbColorTheme.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (Enum.TryParse<ColorTheme>(tag, out var parsedTheme))
                {
                    var settings = _settingsService.Current;
                    if (settings.ColorTheme != parsedTheme)
                    {
                        settings.ColorTheme = parsedTheme;
                        _settingsService.Save(settings);
                        ThemeService.ApplyTheme(parsedTheme, settings.ThemeMode);
                        App.CurrentMainWindow?.ApplyThemeVisuals(parsedTheme, settings.ThemeMode);
                    }
                }
            }
        }

        private void CmbWindowMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingsService == null) return;
            if (CmbWindowMaterial.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (Enum.TryParse<WindowMaterial>(tag, out var parsedMaterial))
                {
                    var settings = _settingsService.Current;
                    if (settings.WindowMaterial != parsedMaterial)
                    {
                        settings.WindowMaterial = parsedMaterial;
                        _settingsService.Save(settings);
                        App.CurrentMainWindow?.ApplyBackdropMaterial(parsedMaterial);
                    }
                }
            }
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLanguageSelection) return;
            if (CmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string lang)
            {
                LocalizationService.Instance.SetLanguage(lang, persist: true);
            }
        }

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

        private async void BtnFixAccess_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:notifications"));
            }
            catch { }
        }
    }
}
