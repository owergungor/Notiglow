using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Dialogs;

namespace NotiGlow.UI.Views
{
    public sealed partial class AdvancedView : UserControl
    {
        private SettingsService _settingsService = null!;
        private ProfileService _profileService = null!;
        private SettingsImportExportService _importExportService = null!;
        private GlowManager? _glowManager;
        private bool _isInitializing = false;

        public AdvancedView()
        {
            this.InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(RootScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);
                UpdateLocalizedTexts();
                UpdateStatusText();
            };
        }

        public void Initialize(SettingsService settingsService, ProfileService profileService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _profileService = profileService;
            _glowManager = glowManager;
            _importExportService = new SettingsImportExportService(settingsService, profileService);

            _settingsService.SettingsChanged += OnSettingsChanged;
            LoadSettings();
            UpdateLocalizedTexts();
        }

        private void OnLanguageChanged(object? sender, string langCode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLocalizedTexts();
                UpdateStatusText();
            });
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            DispatcherQueue.TryEnqueue(LoadSettings);
        }

        private void UpdateLocalizedTexts()
        {
            TxtTitle.Text = LocalizationService.Instance.GetString("Advanced.Title");
            TxtSubtitle.Text = LocalizationService.Instance.GetString("Advanced.Subtitle");
            TxtBtnTestAnimation.Text = LocalizationService.Instance.GetString("Common.TestAnimation");
            ToolTipService.SetToolTip(BtnTestAnimation, LocalizationService.Instance.GetString("Common.TestAnimationToolTip"));

            TxtAutoUpdateSection.Text = LocalizationService.Instance.GetString("Advanced.AutoUpdate");
            TxtAutomaticUpdates.Text = LocalizationService.Instance.GetString("Advanced.AutomaticUpdates");
            ToolTipService.SetToolTip(ToggleAutoUpdates, LocalizationService.Instance.GetString("Advanced.AutoCheckUpdatesToolTip"));

            TxtCheckFrequency.Text = LocalizationService.Instance.GetString("Advanced.CheckFrequency");
            TxtCheckFrequencyDesc.Text = LocalizationService.Instance.GetString("Advanced.CheckFrequencyDesc");

            CbiFreqStartup.Content = LocalizationService.Instance.GetString("Advanced.FreqStartup");
            CbiFreqDaily.Content = LocalizationService.Instance.GetString("Advanced.FreqDaily");
            CbiFreqWeekly.Content = LocalizationService.Instance.GetString("Advanced.FreqWeekly");
            CbiFreqMonthly.Content = LocalizationService.Instance.GetString("Advanced.FreqMonthly");

            TxtAccessibilitySection.Text = LocalizationService.Instance.GetString("Advanced.Accessibility");
            TxtOledMode.Text = LocalizationService.Instance.GetString("Advanced.OledMode");
            TxtOledModeDesc.Text = LocalizationService.Instance.GetString("Advanced.OledModeDesc");

            TxtReduceMotion.Text = LocalizationService.Instance.GetString("Advanced.ReduceMotion");
            TxtReduceMotionDesc.Text = LocalizationService.Instance.GetString("Advanced.ReduceMotionDesc");

            TxtReduceGlow.Text = LocalizationService.Instance.GetString("Advanced.ReduceGlow");
            TxtReduceGlowDesc.Text = LocalizationService.Instance.GetString("Advanced.ReduceGlowDesc");

            TxtBackupRestoreSection.Text = LocalizationService.Instance.GetString("Advanced.BackupRestore");
            TxtConfigManagement.Text = LocalizationService.Instance.GetString("Advanced.ConfigManagement");
            TxtConfigManagementDesc.Text = LocalizationService.Instance.GetString("Advanced.ConfigManagementDesc");

            TxtBtnExport.Text = LocalizationService.Instance.GetString("Advanced.ExportSettings");
            TxtBtnImport.Text = LocalizationService.Instance.GetString("Advanced.ImportSettings");
            TxtBtnReset.Text = LocalizationService.Instance.GetString("Advanced.FactoryReset");

            TxtDiagnosticsSection.Text = LocalizationService.Instance.GetString("Advanced.Diagnostics");
            TxtDebugLogging.Text = LocalizationService.Instance.GetString("Advanced.DebugLogging");
            TxtDebugLoggingDesc.Text = LocalizationService.Instance.GetString("Advanced.DebugLoggingDesc");

            TxtIdentityDebug.Text = LocalizationService.Instance.GetString("Advanced.IdentityDebug");
            TxtIdentityDebugDesc.Text = LocalizationService.Instance.GetString("Advanced.IdentityDebugDesc");

            TxtBtnCheckUpdates.Text = LocalizationService.Instance.GetString(PackageService.IsPackaged ? "Advanced.UpdateInStore" : "Advanced.CheckForUpdates");
            if (PackageService.IsPackaged)
            {
                ToolTipService.SetToolTip(BtnCheckUpdates, LocalizationService.Instance.GetString("Advanced.StoreManaged"));
            }
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

        private void LoadSettings()
        {
            if (_settingsService == null) return;
            _isInitializing = true;

            var settings = _settingsService.Current;
            ToggleOledMode.IsOn = settings.OledMode;
            ToggleReduceMotion.IsOn = settings.ReduceMotion;
            ToggleReduceGlow.IsOn = settings.ReduceGlow;
            ToggleDebugLogging.IsOn = settings.DebugLogging;
            ToggleIdentityDebug.IsOn = settings.ShowIdentityDebugInfo;

            // Auto Update Settings
            if (PackageService.IsPackaged)
            {
                ToggleAutoUpdates.IsOn = true;
                ToggleAutoUpdates.IsEnabled = false;
                ToolTipService.SetToolTip(ToggleAutoUpdates, LocalizationService.Instance.GetString("Advanced.StoreManaged"));
                if (CardCheckFrequency != null) CardCheckFrequency.Visibility = Visibility.Collapsed;
                if (CardAutoUpdates != null) CardAutoUpdates.Margin = new Thickness(0, 0, 0, 24);
                CmbUpdateFrequency.IsEnabled = false;
                ToolTipService.SetToolTip(BtnCheckUpdates, LocalizationService.Instance.GetString("Advanced.StoreManaged"));
                TxtBtnCheckUpdates.Text = LocalizationService.Instance.GetString("Advanced.UpdateInStore");
            }
            else
            {
                if (CardCheckFrequency != null) CardCheckFrequency.Visibility = Visibility.Visible;
                if (CardAutoUpdates != null) CardAutoUpdates.Margin = new Thickness(0, 0, 0, 12);
                TxtBtnCheckUpdates.Text = LocalizationService.Instance.GetString("Advanced.CheckForUpdates");
                ToolTipService.SetToolTip(BtnCheckUpdates, null);
                ToggleAutoUpdates.IsOn = settings.AutoCheckUpdates;
                CmbUpdateFrequency.IsEnabled = settings.AutoCheckUpdates;
                SelectUpdateFrequencyItem(settings.UpdateFrequency);
            }
            UpdateStatusText();

            _isInitializing = false;
        }

        private void SelectUpdateFrequencyItem(UpdateCheckFrequency frequency)
        {
            string tag = frequency.ToString();
            foreach (var item in CmbUpdateFrequency.Items)
            {
                if (item is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    CmbUpdateFrequency.SelectedItem = cbi;
                    return;
                }
            }
            if (CmbUpdateFrequency.Items.Count > 0)
                CmbUpdateFrequency.SelectedIndex = 0;
        }

        private void ToggleAutoUpdates_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null || PackageService.IsPackaged) return;
            var settings = _settingsService.Current;
            settings.AutoCheckUpdates = ToggleAutoUpdates.IsOn;
            CmbUpdateFrequency.IsEnabled = settings.AutoCheckUpdates;
            _settingsService.Save(settings);
        }

        private void CmbUpdateFrequency_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            if (CmbUpdateFrequency.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (Enum.TryParse<UpdateCheckFrequency>(tag, true, out var freq))
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
                    DispatcherQueue.TryEnqueue(() =>
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
                    bool confirmed = UpdateConfirmDialogWindow.ShowUpdateConfirm(info);

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

                            bool installConfirmed = ConfirmationDialogWindow.ShowConfirm(
                                string.IsNullOrEmpty(readyTitle) ? "Install Update" : readyTitle,
                                string.IsNullOrEmpty(readyMsg) ? $"Ready to install NotiGlow {info.LatestVersion}. The application will restart." : readyMsg,
                                confirmText: string.IsNullOrEmpty(installBtn) ? "Install & Restart" : installBtn,
                                cancelText: string.IsNullOrEmpty(cancelBtn) ? "Cancel" : cancelBtn,
                                isDestructive: false);

                            if (installConfirmed)
                            {
                                bool launched = UpdateService.ExecuteUpdateAndRestart(packagePath, () =>
                                {
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        if (Microsoft.UI.Xaml.Application.Current is App myApp)
                                        {
                                            myApp.ExitApplication();
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

        private void ToggleOledMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;
            settings.OledMode = ToggleOledMode.IsOn;
            _settingsService.Save(settings);
        }

        private void ToggleReduceMotion_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;
            settings.ReduceMotion = ToggleReduceMotion.IsOn;
            _settingsService.Save(settings);
            NotiGlow.UI.Animations.MotionPolicy.Update(settings.ReduceMotion, settings.ReduceAnimations);
        }

        private void ToggleReduceGlow_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;
            settings.ReduceGlow = ToggleReduceGlow.IsOn;
            _settingsService.Save(settings);
        }

        private void ToggleDebugLogging_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;
            settings.DebugLogging = ToggleDebugLogging.IsOn;
            _settingsService.Save(settings);
        }

        private void ToggleIdentityDebug_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;
            settings.ShowIdentityDebugInfo = ToggleIdentityDebug.IsOn;
            _settingsService.Save(settings);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var sfd = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    FileName = "NotiGlow-Settings.json",
                    Title = LocalizationService.Instance.GetString("Advanced.ExportSettings")
                };

                if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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
            catch (Exception ex)
            {
                LoggerService.LogError("Export settings failed", ex);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var ofd = new System.Windows.Forms.OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json",
                    Title = LocalizationService.Instance.GetString("Advanced.ImportSettings")
                };

                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
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
            catch (Exception ex)
            {
                LoggerService.LogError("Import settings failed", ex);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            string resetTitle = LocalizationService.Instance.GetString("Advanced.ResetConfirmTitle");
            string resetMsg = LocalizationService.Instance.GetString("Advanced.ResetConfirmMsg");
            string resetBtn = LocalizationService.Instance.GetString("Advanced.ResetToDefaults");
            string cancelBtn = LocalizationService.Instance.GetString("Common.Cancel");

            bool confirmed = ConfirmationDialogWindow.ShowConfirm(
                string.IsNullOrEmpty(resetTitle) ? "Reset Settings" : resetTitle,
                string.IsNullOrEmpty(resetMsg) ? "Are you sure you want to reset all settings to defaults?" : resetMsg,
                confirmText: string.IsNullOrEmpty(resetBtn) ? "Reset" : resetBtn,
                cancelText: string.IsNullOrEmpty(cancelBtn) ? "Cancel" : cancelBtn,
                isDestructive: true);

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
