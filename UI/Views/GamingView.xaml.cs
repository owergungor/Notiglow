using System;
using System.Windows;
using System.Windows.Controls;
using GlowBorder.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace GlowBorder.UI.Views
{
    public partial class GamingView : UserControl
    {
        private SettingsService _settingsService = null!;
        private GlowManager? _glowManager;
        private bool _isInitializing = false;

        public GamingView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            _settingsService.SettingsChanged += OnSettingsChanged;

            LoadSettings();
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;
            _isInitializing = true;

            var settings = _settingsService.Current;
            ToggleGamingMode.IsChecked = settings.GamingModeEnabled;
            ToggleGlowDuringGames.IsChecked = settings.GlowDuringGames;
            ToggleReduceIntensityInGames.IsChecked = settings.ReduceIntensityInGames;
            ToggleReduceDurationInGames.IsChecked = settings.ReduceDurationInGames;
            ToggleOnlyImportantInGames.IsChecked = settings.OnlyImportantInGames;

            ListTrackedGames.ItemsSource = null;
            ListTrackedGames.ItemsSource = settings.TrackedGames;

            _isInitializing = false;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(LoadSettings);
        }

        private void ToggleGamingMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.GamingModeEnabled = ToggleGamingMode.IsChecked ?? false;
            _settingsService.Save(settings);
        }

        private void ToggleGlowDuringGames_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.GlowDuringGames = ToggleGlowDuringGames.IsChecked ?? true;
            _settingsService.Save(settings);
        }

        private void ToggleReduceIntensityInGames_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.ReduceIntensityInGames = ToggleReduceIntensityInGames.IsChecked ?? true;
            _settingsService.Save(settings);
        }

        private void ToggleReduceDurationInGames_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.ReduceDurationInGames = ToggleReduceDurationInGames.IsChecked ?? true;
            _settingsService.Save(settings);
        }

        private void ToggleOnlyImportantInGames_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            var settings = _settingsService.Current;
            settings.OnlyImportantInGames = ToggleOnlyImportantInGames.IsChecked ?? false;
            _settingsService.Save(settings);
        }

        private void BtnAddGame_Click(object sender, RoutedEventArgs e)
        {
            string exe = TxtAddGameExe.Text.Trim();
            if (string.IsNullOrWhiteSpace(exe)) return;

            if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) exe += ".exe";

            var settings = _settingsService.Current;
            if (!settings.TrackedGames.Contains(exe, StringComparer.OrdinalIgnoreCase))
            {
                settings.TrackedGames.Add(exe);
                _settingsService.Save(settings);
                TxtAddGameExe.Text = string.Empty;
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
