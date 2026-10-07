using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NotiGlow.Services;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace NotiGlow.UI.Views
{
    public partial class AdvancedView : UserControl
    {
        private SettingsService _settingsService = null!;
        private ProfileService _profileService = null!;
        private SettingsImportExportService _importExportService = null!;
        private GlowManager? _glowManager;
        private bool _isInitializing = false;

        public AdvancedView()
        {
            LocalizationService.Instance.ApplyToWpfResources();
            LocalizationService.Instance.ApplyToWpfResources(this.Resources);
            InitializeComponent();
            ApplyComboBoxItemTexts();
            Loaded += (s, e) =>
            {
                LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            };
            Unloaded += (s, e) =>
            {
                LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
            };
        }

        private void OnLanguageChanged(object? sender, string lang)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            if (Dispatcher.Thread != null && !Dispatcher.Thread.IsAlive) return;
            try
            {
                Dispatcher.BeginInvoke(ApplyComboBoxItemTexts);
            }
            catch { }
        }

        private void ApplyComboBoxItemTexts()
        {
            if (CmbUpdateFrequency == null) return;
            foreach (var item in CmbUpdateFrequency.Items)
            {
                if (item is ComboBoxItem cbi && cbi.Tag is string tag)
                {
                    string key = tag switch
                    {
                        "OnStartup" => "Advanced.FreqStartup",
                        "Daily" => "Advanced.FreqDaily",
                        "Weekly" => "Advanced.FreqWeekly",
                        "Monthly" => "Advanced.FreqMonthly",
                        _ => ""
                    };
                    if (!string.IsNullOrEmpty(key))
                    {
                        cbi.Content = LocalizationService.Instance.GetString(key);
                    }
                }
            }
        }

        public void Initialize(SettingsService settingsService, ProfileService profileService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _profileService = profileService;
            _glowManager = glowManager;
            _importExportService = new SettingsImportExportService(settingsService, profileService);

            _settingsService.SettingsChanged += OnSettingsChanged;
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;
            _isInitializing = true;

            var settings = _settingsService.Current;
            ToggleOledMode.IsChecked = settings.OledMode;
            ToggleReduceMotion.IsChecked = settings.ReduceMotion;
            ToggleReduceGlow.IsChecked = settings.ReduceGlow;
            ToggleDebugLogging.IsChecked = settings.DebugLogging;
            ToggleIdentityDebug.IsChecked = settings.ShowIdentityDebugInfo;

            // Auto Update Settings
            ToggleAutoUpdates.IsChecked = settings.AutoCheckUpdates;
            CmbUpdateFrequency.IsEnabled = settings.AutoCheckUpdates;
            SelectUpdateFrequencyItem(settings.UpdateFrequency);

            if (settings.LastUpdateCheck.HasValue)
            {
                TxtUpdateStatus.Text = LocalizationService.Instance.GetString(
                    "Advanced.LastCheckFormat",
                    UpdateService.CurrentVersionString,
                    settings.LastUpdateCheck.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            }
            else
            {
                TxtUpdateStatus.Text = LocalizationService.Instance.GetString(
                    "Advanced.CurrentVersionFormat",
                    UpdateService.CurrentVersionString);
            }

            _isInitializing = false;
        }

        private void SelectUpdateFrequencyItem(NotiGlow.Models.UpdateCheckFrequency frequency)
        {
            string tag = frequency.ToString();
            foreach (var item in CmbUpdateFrequency.Items)
            {
                if (item is ComboBoxItem cbi)
                {
                    string itemTag = cbi.Tag?.ToString() ?? "";
                    string itemContent = cbi.Content?.ToString() ?? "";
                    if (string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(itemContent, tag, StringComparison.OrdinalIgnoreCase) ||
                        (frequency == NotiGlow.Models.UpdateCheckFrequency.OnStartup &&
                         (string.Equals(itemTag, "Startup", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(itemContent, "On Startup", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(itemContent, "Açılışta", StringComparison.OrdinalIgnoreCase))))
                    {
                        CmbUpdateFrequency.SelectedItem = cbi;
                        return;
                    }
                }
            }
            // Fallback default
            if (CmbUpdateFrequency.Items.Count > 0)
                CmbUpdateFrequency.SelectedIndex = 0;
        }

        private void ToggleAutoUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;
            settings.AutoCheckUpdates = ToggleAutoUpdates.IsChecked == true;
            CmbUpdateFrequency.IsEnabled = settings.AutoCheckUpdates;
            _settingsService.Save(settings);
        }

        private void CmbUpdateFrequency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            if (CmbUpdateFrequency.SelectedItem is ComboBoxItem item)
            {
                string tag = item.Tag?.ToString() ?? item.Content?.ToString() ?? "";
                if (Enum.TryParse<NotiGlow.Models.UpdateCheckFrequency>(tag, true, out var freq) ||
                    (string.Equals(tag, "On Startup", StringComparison.OrdinalIgnoreCase) && (freq = NotiGlow.Models.UpdateCheckFrequency.OnStartup) == NotiGlow.Models.UpdateCheckFrequency.OnStartup) ||
                    (string.Equals(tag, "Açılışta", StringComparison.OrdinalIgnoreCase) && (freq = NotiGlow.Models.UpdateCheckFrequency.OnStartup) == NotiGlow.Models.UpdateCheckFrequency.OnStartup))
                {
                    var settings = _settingsService.Current;
                    if (settings.UpdateFrequency != freq)
                    {
                        settings.UpdateFrequency = freq;
                        _settingsService.Save(settings);
                    }
                }
            }
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            BtnCheckUpdates.IsEnabled = false;
            TxtUpdateStatus.Text = LocalizationService.Instance.GetString("Advanced.CheckingUpdates");
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
                    var result = MessageBox.Show(
                        LocalizationService.Instance.GetString("Advanced.UpdateAvailableMsg", info.LatestVersion),
                        LocalizationService.Instance.GetString("Advanced.UpdateAvailableTitle"),
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
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
                            TxtUpdateStatus.Text = LocalizationService.Instance.GetString("Advanced.UpdateReadyInstalling");
                            var installPrompt = MessageBox.Show(
                                LocalizationService.Instance.GetString("Advanced.ReadyToInstallMsg", info.LatestVersion),
                                LocalizationService.Instance.GetString("Advanced.ReadyToInstallTitle"),
                                MessageBoxButton.OKCancel,
                                MessageBoxImage.Information);

                            if (installPrompt == MessageBoxResult.OK)
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
                    TxtUpdateStatus.Text = LocalizationService.Instance.GetString("Advanced.UpToDate", UpdateService.CurrentVersionString);
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

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(LoadSettings);
        }

        private void ToggleOledMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.OledMode = ToggleOledMode.IsChecked ?? false;
            _settingsService.Save(settings);
        }

        private void ToggleReduceMotion_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.ReduceMotion = ToggleReduceMotion.IsChecked ?? false;
            _settingsService.Save(settings);
        }

        private void ToggleReduceGlow_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.ReduceGlow = ToggleReduceGlow.IsChecked ?? false;
            _settingsService.Save(settings);
        }

        private void ToggleDebugLogging_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.DebugLogging = ToggleDebugLogging.IsChecked ?? true;
            _settingsService.Save(settings);
        }

        private void ToggleIdentityDebug_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.ShowIdentityDebugInfo = ToggleIdentityDebug.IsChecked ?? false;
            _settingsService.Save(settings);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                FileName = "NotiGlow-Settings.json"
            };

            if (sfd.ShowDialog() == true)
            {
                bool success = _importExportService.ExportSettings(sfd.FileName);
                if (success)
                {
                    MessageBox.Show(
                        LocalizationService.Instance.GetString("Advanced.ExportSuccessMsg"),
                        LocalizationService.Instance.GetString("Advanced.ExportSuccessTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        LocalizationService.Instance.GetString("Advanced.ExportFailMsg"),
                        LocalizationService.Instance.GetString("Advanced.ExportFailTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json"
            };

            if (ofd.ShowDialog() == true)
            {
                bool success = _importExportService.ImportSettings(ofd.FileName);
                if (success)
                {
                    MessageBox.Show(
                        LocalizationService.Instance.GetString("Advanced.ImportSuccessMsg"),
                        LocalizationService.Instance.GetString("Advanced.ImportSuccessTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        LocalizationService.Instance.GetString("Advanced.ImportFailMsg"),
                        LocalizationService.Instance.GetString("Advanced.ImportFailTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                LocalizationService.Instance.GetString("Advanced.ResetConfirmMsg"),
                LocalizationService.Instance.GetString("Advanced.ResetConfirmTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _importExportService.ResetToDefaults();
                MessageBox.Show(
                    LocalizationService.Instance.GetString("Advanced.ResetCompleteMsg"),
                    LocalizationService.Instance.GetString("Advanced.ResetCompleteTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;
            var testProfile = new NotiGlow.Models.AppProfile
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
