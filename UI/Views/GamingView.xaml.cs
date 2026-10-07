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
        private GameDetectionService? _gameDetectionService;
        private bool _isInitializing = false;

        public GamingView()
        {
            LocalizationService.Instance.ApplyToWpfResources();
            LocalizationService.Instance.ApplyToWpfResources(this.Resources);
            InitializeComponent();
            if (TxtBtnTestGameAnimation != null)
            {
                TxtBtnTestGameAnimation.Text = LocalizationService.Instance.GetString("Gaming.TestGameAnimation");
            }
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
                Dispatcher.BeginInvoke(() =>
                {
                    if (TxtBtnTestGameAnimation != null)
                    {
                        TxtBtnTestGameAnimation.Text = LocalizationService.Instance.GetString("Gaming.TestGameAnimation");
                    }
                });
            }
            catch { }
        }

        public void Initialize(SettingsService settingsService, GlowManager? glowManager = null, GameDetectionService? gameDetectionService = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            _gameDetectionService = gameDetectionService ?? glowManager?.GameDetectionService;
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
            string? tooltip = glowDuringGames ? null : LocalizationService.Instance.GetString("Gaming.SubSettingsDisabledToolTip");
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
                    Title = LocalizationService.Instance.GetString("Gaming.SelectExecutableDialogTitle"),
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

        private async void BtnScanGames_Click(object sender, RoutedEventArgs e)
        {
            if (_gameDetectionService == null || _settingsService == null) return;

            BtnScanGames.IsEnabled = false;
            TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.ScanningGames");

            try
            {
                int newGames = await _gameDetectionService.ScanAndSyncTrackedGamesAsync();
                ListTrackedGames.ItemsSource = null;
                ListTrackedGames.ItemsSource = _settingsService.Current.TrackedGames;

                if (newGames > 0)
                {
                    TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.FoundGames", newGames);
                }
                else
                {
                    TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.NoNewGames");
                }
            }
            catch (Exception ex)
            {
                TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.ScanError");
                LoggerService.LogWarning($"Manual game scan failed: {ex.Message}");
            }
            finally
            {
                BtnScanGames.IsEnabled = true;
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

                // Unignore if previously deleted
                string fileName = System.IO.Path.GetFileName(trimmed);
                settings.IgnoredGames.RemoveAll(g =>
                    g.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(fileName) && System.IO.Path.GetFileName(g).Equals(fileName, StringComparison.OrdinalIgnoreCase)));

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
                    // Add to ignored games so automatic scan won't add it again
                    string fileName = System.IO.Path.GetFileName(gameItem);
                    if (!settings.IgnoredGames.Contains(gameItem, StringComparer.OrdinalIgnoreCase))
                    {
                        settings.IgnoredGames.Add(gameItem);
                    }
                    if (!string.IsNullOrEmpty(fileName) && !settings.IgnoredGames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    {
                        settings.IgnoredGames.Add(fileName);
                    }

                    _settingsService.Save(settings);
                    ListTrackedGames.ItemsSource = null;
                    ListTrackedGames.ItemsSource = settings.TrackedGames;
                }
            }
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;

            var trackedGames = _settingsService.Current.TrackedGames;
            if (trackedGames == null || trackedGames.Count == 0)
            {
                TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.EmptyTrackedGamesWarning");
                return;
            }

            string? selectedGame = ListTrackedGames.SelectedItem as string;
            bool triggered = _glowManager.TriggerGameAnimationTest(selectedGame);
            if (!triggered)
            {
                if (!_settingsService.Current.GlowDuringGames)
                {
                    TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.SubSettingsDisabledToolTip");
                }
            }
        }
    }
}
