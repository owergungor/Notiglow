using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NotiGlow.Services;
using UserControl = System.Windows.Controls.UserControl;

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
            InitializeComponent();
            ApplyComboBoxItemTexts();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ApplyComboBoxItemTexts();
                UpdateStatusText();
            };
        }

        private void OnLanguageChanged(object? sender, string lang)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            if (Dispatcher.Thread != null && !Dispatcher.Thread.IsAlive) return;
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    ApplyComboBoxItemTexts();
                    UpdateStatusText();
                    BtnCheckUpdates.Content = LocalizationService.Instance.GetString(PackageService.IsPackaged ? "Advanced.UpdateInStore" : "Advanced.CheckForUpdates");
                    if (PackageService.IsPackaged) BtnCheckUpdates.ToolTip = LocalizationService.Instance.GetString("Advanced.StoreManaged");
                });
            }
            catch { }
        }

        private void UpdateStatusText()
        {
            if (TxtUpdateStatus == null || _settingsService == null) return;

            if (PackageService.GetUpdateRoute(PackageService.IsPackaged) == UpdateRoute.MicrosoftStorePage)
            {
                TxtUpdateStatus.Text = LocalizationService.Instance.GetString(
                    "Advanced.StoreVersionFormat",
                    UpdateService.CurrentVersionString);
                return;
            }

            var settings = _settingsService.Current;
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
            if (PackageService.IsPackaged)
            {
                ToggleAutoUpdates.IsChecked = true;
                ToggleAutoUpdates.IsEnabled = false;
                ToggleAutoUpdates.ToolTip = LocalizationService.Instance.GetString("Advanced.StoreManaged");
                CmbUpdateFrequency.IsEnabled = false;
                BtnCheckUpdates.ToolTip = LocalizationService.Instance.GetString("Advanced.StoreManaged");
                BtnCheckUpdates.Content = LocalizationService.Instance.GetString("Advanced.UpdateInStore");
            }
            else
            {
                BtnCheckUpdates.Content = LocalizationService.Instance.GetString("Advanced.CheckForUpdates");
                BtnCheckUpdates.ToolTip = null;
                ToggleAutoUpdates.IsChecked = settings.AutoCheckUpdates;
                CmbUpdateFrequency.IsEnabled = settings.AutoCheckUpdates;
                SelectUpdateFrequencyItem(settings.UpdateFrequency);
            }
            UpdateStatusText();

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
            if (PackageService.IsPackaged) return;
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

            if (PackageService.GetUpdateRoute(PackageService.IsPackaged) == UpdateRoute.MicrosoftStorePage)
            {
                PackageService.OpenStorePage();
                return;
            }

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
                    bool confirmed = NotiGlow.UI.Dialogs.UpdateConfirmDialogWindow.ShowUpdateConfirm(info, Window.GetWindow(this));

                    if (confirmed)
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
                            string readyTitle = LocalizationService.Instance.GetString("Advanced.ReadyToInstallTitle");
                            string readyMsg = LocalizationService.Instance.GetString("Advanced.ReadyToInstallMsg", info.LatestVersion);
                            string installBtn = LocalizationService.Instance.GetString("Advanced.InstallNow");
                            string cancelBtn = LocalizationService.Instance.GetString("Common.Cancel");

                            bool installConfirmed = NotiGlow.UI.Dialogs.ConfirmationDialogWindow.ShowConfirm(
                                string.IsNullOrEmpty(readyTitle) ? "Install Update" : readyTitle,
                                string.IsNullOrEmpty(readyMsg) ? $"Ready to install NotiGlow {info.LatestVersion}. The application will restart." : readyMsg,
                                confirmText: string.IsNullOrEmpty(installBtn) ? "Install & Restart" : installBtn,
                                cancelText: string.IsNullOrEmpty(cancelBtn) ? "Cancel" : cancelBtn,
                                isDestructive: false,
                                owner: Window.GetWindow(this));

                            if (installConfirmed)
                            {
                                bool launched = UpdateService.ExecuteUpdateAndRestart(packagePath, () =>
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

                                if (!launched)
                                {
                                    string failTitle = LocalizationService.Instance.GetString("Advanced.UpdateCheckFailed");
                                    ToastNotificationService.Instance.ShowError(
                                        "Failed to start updater process. Existing version will continue running.",
                                        string.IsNullOrEmpty(failTitle) ? "Update Error" : failTitle);
                                }
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
                string errMsg = LocalizationService.Instance.GetString("Advanced.UpdateCheckFailed");
                TxtUpdateStatus.Text = errMsg;
                PbUpdateProgress.Visibility = Visibility.Collapsed;
                LoggerService.LogWarning($"Update check error: {ex.Message}");
                ToastNotificationService.Instance.ShowWarning(
                    errMsg,
                    LocalizationService.Instance.GetString("Advanced.CheckForUpdates"));
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
            NotiGlow.UI.Animations.MotionPolicy.Update(settings.ReduceMotion, settings.ReduceAnimations);
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
                    ToastNotificationService.Instance.ShowSuccess(
                        LocalizationService.Instance.GetString("Advanced.ExportSuccessMsg"),
                        LocalizationService.Instance.GetString("Advanced.ExportSuccessTitle"));
                }
                else
                {
                    ToastNotificationService.Instance.ShowError(
                        LocalizationService.Instance.GetString("Advanced.ExportFailMsg"),
                        LocalizationService.Instance.GetString("Advanced.ExportFailTitle"));
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
                    ToastNotificationService.Instance.ShowSuccess(
                        LocalizationService.Instance.GetString("Advanced.ImportSuccessMsg"),
                        LocalizationService.Instance.GetString("Advanced.ImportSuccessTitle"));
                }
                else
                {
                    ToastNotificationService.Instance.ShowError(
                        LocalizationService.Instance.GetString("Advanced.ImportFailMsg"),
                        LocalizationService.Instance.GetString("Advanced.ImportFailTitle"));
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            string resetTitle = LocalizationService.Instance.GetString("Advanced.ResetConfirmTitle");
            string resetMsg = LocalizationService.Instance.GetString("Advanced.ResetConfirmMsg");
            string resetBtn = LocalizationService.Instance.GetString("Advanced.ResetToDefaults");
            string cancelBtn = LocalizationService.Instance.GetString("Common.Cancel");

            bool confirmed = NotiGlow.UI.Dialogs.ConfirmationDialogWindow.ShowConfirm(
                string.IsNullOrEmpty(resetTitle) ? "Reset Settings" : resetTitle,
                string.IsNullOrEmpty(resetMsg) ? "Are you sure you want to reset all settings to defaults?" : resetMsg,
                confirmText: string.IsNullOrEmpty(resetBtn) ? "Reset" : resetBtn,
                cancelText: string.IsNullOrEmpty(cancelBtn) ? "Cancel" : cancelBtn,
                isDestructive: true,
                owner: Window.GetWindow(this));

            if (confirmed)
            {
                _importExportService.ResetToDefaults();
                ToastNotificationService.Instance.ShowSuccess(
                    LocalizationService.Instance.GetString("Advanced.ResetCompleteMsg"),
                    LocalizationService.Instance.GetString("Advanced.ResetCompleteTitle"));
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
