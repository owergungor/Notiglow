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
            MotionPolicy.Update(current.ReduceAnimations);

            // Load Theme Mode
            BtnModeSystem.IsChecked = (current.ThemeMode == ThemeMode.System);
            BtnModeLight.IsChecked = (current.ThemeMode == ThemeMode.Light);
            BtnModeDark.IsChecked = (current.ThemeMode == ThemeMode.Dark);

            // Load Color Theme
            UpdateColorThemeSelection(current.ColorTheme);

            // Load Auto Update Settings
            ToggleAutoUpdates.IsChecked = current.AutoCheckUpdates;
            if (current.LastUpdateCheck.HasValue)
            {
                TxtUpdateStatus.Text = $"Current: v{UpdateService.CurrentVersionString} • Last check: {current.LastUpdateCheck.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
            }
        }

        private void UpdateColorThemeSelection(ColorTheme colorTheme)
        {
            foreach (var child in ColorThemesPanel.Children)
            {
                if (child is RadioButton rb && rb.Tag is string tag && Enum.TryParse<ColorTheme>(tag, out var t))
                {
                    rb.IsChecked = (t == colorTheme);
                }
            }
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

        private void ColorTheme_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null || sender is not RadioButton clickedRb || clickedRb.Tag is not string tag) return;

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

        private void ToggleAutoUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            settings.AutoCheckUpdates = ToggleAutoUpdates.IsChecked == true;
            _settingsService.Save(settings);
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            BtnCheckUpdates.IsEnabled = false;
            TxtUpdateStatus.Text = "Checking for updates...";
            PbUpdateProgress.Visibility = Visibility.Collapsed;

            try
            {
                var updater = new UpdateService(_settingsService);
                updater.StatusChanged += (s, ev) =>
                {
                    Dispatcher?.Invoke(() =>
                    {
                        TxtUpdateStatus.Text = ev.Message;
                        if (ev.Status == UpdateStatus.Downloading)
                        {
                            PbUpdateProgress.Visibility = Visibility.Visible;
                            PbUpdateProgress.Value = ev.Progress;
                        }
                    });
                };

                var info = await updater.CheckForUpdatesAsync();

                if (info.IsUpdateAvailable)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"A new version (v{info.LatestVersion}) of NotiGlow is available!\n\n" +
                        $"Would you like to download, verify, and automatically install the update now?",
                        "Update Available",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        PbUpdateProgress.Visibility = Visibility.Visible;
                        PbUpdateProgress.Value = 0;

                        var progress = new Progress<double>(p =>
                        {
                            PbUpdateProgress.Value = p;
                        });

                        string? packagePath = await updater.DownloadAndVerifyPackageAsync(info, progress);

                        if (!string.IsNullOrEmpty(packagePath) && System.IO.File.Exists(packagePath))
                        {
                            TxtUpdateStatus.Text = $"Update ready. Installing...";
                            var installPrompt = System.Windows.MessageBox.Show(
                                $"NotiGlow v{info.LatestVersion} downloaded and verified successfully!\n\n" +
                                "The application will now close, apply the update, and automatically restart. Proceed?",
                                "Ready to Install",
                                System.Windows.MessageBoxButton.OKCancel,
                                System.Windows.MessageBoxImage.Information);

                            if (installPrompt == System.Windows.MessageBoxResult.OK)
                            {
                                UpdateService.ExecuteUpdateAndRestart(packagePath, () =>
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (System.Windows.Application.Current is App myApp)
                                        {
                                            myApp.ExitApplication();
                                        }
                                        else
                                        {
                                            System.Windows.Application.Current.Shutdown();
                                        }
                                    });
                                });
                            }
                        }
                    }
                }
                else
                {
                    TxtUpdateStatus.Text = $"NotiGlow v{UpdateService.CurrentVersionString} is up to date.";
                    PbUpdateProgress.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                TxtUpdateStatus.Text = "Update check failed.";
                PbUpdateProgress.Visibility = Visibility.Collapsed;
                LoggerService.LogWarning($"Update check error: {ex.Message}");
            }
            finally
            {
                BtnCheckUpdates.IsEnabled = true;
            }
        }
    }
}
