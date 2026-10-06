using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NotiGlow.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace NotiGlow.UI.Views
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

            // Retain saved preferences for sub-settings
            ToggleReduceIntensityInGames.IsChecked = settings.ReduceIntensityInGames;
            ToggleReduceDurationInGames.IsChecked = settings.ReduceDurationInGames;
            ToggleOnlyImportantInGames.IsChecked = settings.OnlyImportantInGames;

            ApplyGameSubSettingsDependency(settings.GlowDuringGames);

            ListTrackedGames.ItemsSource = null;
            ListTrackedGames.ItemsSource = settings.TrackedGames;

            _isInitializing = false;
        }

        private void ApplyGameSubSettingsDependency(bool glowDuringGames)
        {
            // Toggle controls interaction and accessibility
            ToggleReduceIntensityInGames.IsEnabled = glowDuringGames;
            ToggleReduceIntensityInGames.Focusable = glowDuringGames;
            ToggleReduceIntensityInGames.IsHitTestVisible = glowDuringGames;

            ToggleReduceDurationInGames.IsEnabled = glowDuringGames;
            ToggleReduceDurationInGames.Focusable = glowDuringGames;
            ToggleReduceDurationInGames.IsHitTestVisible = glowDuringGames;

            ToggleOnlyImportantInGames.IsEnabled = glowDuringGames;
            ToggleOnlyImportantInGames.Focusable = glowDuringGames;
            ToggleOnlyImportantInGames.IsHitTestVisible = glowDuringGames;

            // Card container visual & interaction states
            CardReduceIntensityInGames.IsEnabled = glowDuringGames;
            CardReduceIntensityInGames.Focusable = glowDuringGames;
            CardReduceIntensityInGames.Opacity = glowDuringGames ? 1.0 : 0.45;

            CardReduceDurationInGames.IsEnabled = glowDuringGames;
            CardReduceDurationInGames.Focusable = glowDuringGames;
            CardReduceDurationInGames.Opacity = glowDuringGames ? 1.0 : 0.45;

            CardOnlyImportantInGames.IsEnabled = glowDuringGames;
            CardOnlyImportantInGames.Focusable = glowDuringGames;
            CardOnlyImportantInGames.Opacity = glowDuringGames ? 1.0 : 0.45;

            // Contextual tooltips explaining disabled state
            string? tooltip = glowDuringGames ? null : "Glow during games kapalıyken bu ayar uygulanamaz.";
            CardReduceIntensityInGames.ToolTip = tooltip;
            CardReduceDurationInGames.ToolTip = tooltip;
            CardOnlyImportantInGames.ToolTip = tooltip;
            ToggleReduceIntensityInGames.ToolTip = tooltip;
            ToggleReduceDurationInGames.ToolTip = tooltip;
            ToggleOnlyImportantInGames.ToolTip = tooltip;

            // Muted header typography when disabled
            System.Windows.Media.Brush? primaryBrush = TryFindResource("TextPrimary") as System.Windows.Media.Brush;
            System.Windows.Media.Brush? mutedBrush = TryFindResource("TextTertiary") as System.Windows.Media.Brush ?? TryFindResource("TextSecondary") as System.Windows.Media.Brush;

            TxtReduceIntensityTitle.Foreground = glowDuringGames ? primaryBrush : mutedBrush;
            TxtReduceDurationTitle.Foreground = glowDuringGames ? primaryBrush : mutedBrush;
            TxtOnlyImportantTitle.Foreground = glowDuringGames ? primaryBrush : mutedBrush;
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
            bool isGlow = ToggleGlowDuringGames.IsChecked ?? true;
            settings.GlowDuringGames = isGlow;
            _settingsService.Save(settings);

            ApplyGameSubSettingsDependency(isGlow);
        }

        private void ToggleReduceIntensityInGames_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;

            // Enforce dependency at UI/ViewModel level
            if (!settings.GlowDuringGames)
            {
                ToggleReduceIntensityInGames.IsChecked = settings.ReduceIntensityInGames;
                return;
            }

            settings.ReduceIntensityInGames = ToggleReduceIntensityInGames.IsChecked ?? true;
            _settingsService.Save(settings);
        }

        private void ToggleReduceDurationInGames_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;

            // Enforce dependency at UI/ViewModel level
            if (!settings.GlowDuringGames)
            {
                ToggleReduceDurationInGames.IsChecked = settings.ReduceDurationInGames;
                return;
            }

            settings.ReduceDurationInGames = ToggleReduceDurationInGames.IsChecked ?? true;
            _settingsService.Save(settings);
        }

        private void ToggleOnlyImportantInGames_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;
            var settings = _settingsService.Current;

            // Enforce dependency at UI/ViewModel level
            if (!settings.GlowDuringGames)
            {
                ToggleOnlyImportantInGames.IsChecked = settings.OnlyImportantInGames;
                return;
            }

            settings.OnlyImportantInGames = ToggleOnlyImportantInGames.IsChecked ?? false;
            _settingsService.Save(settings);
        }

        private void BtnAddGamePicker_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                    Title = "Select Game Executable",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    AddGameToSettings(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error picking game executable file", ex);
            }
        }

        private void AddGameToSettings(string gameEntry)
        {
            if (string.IsNullOrWhiteSpace(gameEntry) || _settingsService == null) return;

            string trimmed = gameEntry.Trim();
            var settings = _settingsService.Current;

            bool exists = settings.TrackedGames.Exists(g =>
                g.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                System.IO.Path.GetFileName(g).Equals(System.IO.Path.GetFileName(trimmed), StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                settings.TrackedGames.Add(trimmed);
                _settingsService.Save(settings);
                ListTrackedGames.ItemsSource = null;
                ListTrackedGames.ItemsSource = settings.TrackedGames;
            }
        }

        private void BtnRemoveGame_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is string gameItem && _settingsService != null)
            {
                var settings = _settingsService.Current;
                if (settings.TrackedGames.Remove(gameItem))
                {
                    _settingsService.Save(settings);
                    ListTrackedGames.ItemsSource = null;
                    ListTrackedGames.ItemsSource = settings.TrackedGames;
                }
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
