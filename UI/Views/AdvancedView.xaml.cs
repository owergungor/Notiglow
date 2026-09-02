using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using GlowBorder.Services;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace GlowBorder.UI.Views
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

            _isInitializing = false;
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
                    MessageBox.Show("Settings successfully exported!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to export settings.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("Settings successfully imported!", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to import settings. Invalid file format.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset all NotiGlow settings and application profiles to default?", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _importExportService.ResetToDefaults();
                MessageBox.Show("NotiGlow has been reset to default settings.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;
            var testProfile = new GlowBorder.Models.AppProfile
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
