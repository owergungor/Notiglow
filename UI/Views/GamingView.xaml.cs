using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using NotiGlow.Models;
using NotiGlow.Services;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;
using Orientation = System.Windows.Controls.Orientation;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace NotiGlow.UI.Views
{
    public partial class GamingView : UserControl
    {
        private SettingsService _settingsService = null!;
        private GlowManager? _glowManager;
        private GameDetectionService? _gameDetectionService;
        private bool _isInitializing = false;
        private string? _editingGameEntry;
        private bool _isLoadedOnce = false;
        private bool _isDirty = true;

        public bool IsDirty => _isDirty;

        public void EnsureLoaded()
        {
            if (!_isLoadedOnce || _isDirty)
            {
                _isLoadedOnce = true;
                _isDirty = false;
                RenderTrackedGameCards();
            }
        }

        public GamingView()
        {
            InitializeComponent();

            if (TxtBtnTestGameAnimation != null)
            {
                TxtBtnTestGameAnimation.Text = LocalizationService.Instance.GetString("Gaming.TestGameAnimation");
            }

            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                UpdateLocalizedTexts();
                EnsureLoaded();
            };
        }

        private void UpdateLocalizedTexts()
        {
            if (TxtBtnTestGameAnimation != null)
            {
                TxtBtnTestGameAnimation.Text = LocalizationService.Instance.GetString("Gaming.TestGameAnimation");
            }
            if (TxtDetectionStatus != null)
            {
                TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.DetectionSupport");
            }
        }

        private void OnLanguageChanged(object? sender, string lang)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            if (Dispatcher.Thread != null && !Dispatcher.Thread.IsAlive) return;
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    UpdateLocalizedTexts();
                    _isDirty = true;
                    if (IsVisible)
                    {
                        RenderTrackedGameCards();
                    }
                    if (PnlEditGameOverride.Visibility == Visibility.Visible && !string.IsNullOrEmpty(_editingGameEntry))
                    {
                        OpenGameOverridesEditor(_editingGameEntry);
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

        private bool _isLoadingCommonGamingSettings = false;

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

            // Load Common Gaming Glow Settings
            _isLoadingCommonGamingSettings = true;
            try
            {
                int dur = settings.CommonGamingDurationMs ?? 3000;
                double intens = settings.CommonGamingIntensity ?? 0.8;
                double glow = settings.CommonGamingGlowSize ?? 32.0;
                double corner = settings.CommonGamingCornerRadius ?? 0.0;

                SldCommonGamingDuration.Value = dur;
                TxtCommonGamingDurationVal.Text = $"{dur / 1000.0:0.#} s";

                SldCommonGamingIntensity.Value = Math.Round(intens * 100);
                TxtCommonGamingIntensityVal.Text = $"{(int)Math.Round(intens * 100)}%";

                SldCommonGamingGlowSize.Value = glow;
                TxtCommonGamingGlowSizeVal.Text = $"{(int)glow} px";

                if (SldCommonGamingCornerRadius != null)
                {
                    SldCommonGamingCornerRadius.Value = corner;
                }
                if (TxtCommonGamingCornerRadiusVal != null)
                {
                    TxtCommonGamingCornerRadiusVal.Text = $"{(int)corner} px";
                }
            }
            finally
            {
                _isLoadingCommonGamingSettings = false;
            }

            ApplyGameSubSettingsDependency(settings.GlowDuringGames);
            ApplyViewMode(settings.GamingViewMode);

            RenderTrackedGameCards();

            _isInitializing = false;
        }

        private void SldCommonGamingDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCommonGamingDurationVal != null)
            {
                TxtCommonGamingDurationVal.Text = $"{e.NewValue / 1000.0:0.#} s";
            }

            if (_isLoadingCommonGamingSettings || _isInitializing || _settingsService == null) return;
            SaveCommonGamingSettings();
        }

        private void SldCommonGamingIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCommonGamingIntensityVal != null)
            {
                TxtCommonGamingIntensityVal.Text = $"{(int)e.NewValue}%";
            }

            if (_isLoadingCommonGamingSettings || _isInitializing || _settingsService == null) return;
            SaveCommonGamingSettings();
        }

        private void SldCommonGamingGlowSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCommonGamingGlowSizeVal != null)
            {
                TxtCommonGamingGlowSizeVal.Text = $"{(int)e.NewValue} px";
            }

            if (_isLoadingCommonGamingSettings || _isInitializing || _settingsService == null) return;
            SaveCommonGamingSettings();
        }

        private void SldCommonGamingCornerRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtCommonGamingCornerRadiusVal != null)
            {
                TxtCommonGamingCornerRadiusVal.Text = $"{(int)e.NewValue} px";
            }

            if (_isLoadingCommonGamingSettings || _isInitializing || _settingsService == null) return;
            SaveCommonGamingSettings();
        }

        private void SaveCommonGamingSettings()
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            int dur = (int)SldCommonGamingDuration.Value;
            double intens = Math.Round(SldCommonGamingIntensity.Value) / 100.0;
            double glow = Math.Round(SldCommonGamingGlowSize.Value);
            double corner = SldCommonGamingCornerRadius != null ? Math.Round(SldCommonGamingCornerRadius.Value) : 0.0;

            settings.SetCommonGamingSettings(dur, intens, glow, corner);
            _settingsService.Save(settings);
            RenderTrackedGameCards();
        }

        private void ApplyGameSubSettingsDependency(bool glowDuringGames)
        {
            ToggleReduceIntensityInGames.IsEnabled = glowDuringGames;
            ToggleReduceIntensityInGames.Focusable = glowDuringGames;
            ToggleReduceIntensityInGames.IsHitTestVisible = glowDuringGames;

            ToggleReduceDurationInGames.IsEnabled = glowDuringGames;
            ToggleReduceDurationInGames.Focusable = glowDuringGames;
            ToggleReduceDurationInGames.IsHitTestVisible = glowDuringGames;

            ToggleOnlyImportantInGames.IsEnabled = glowDuringGames;
            ToggleOnlyImportantInGames.Focusable = glowDuringGames;
            ToggleOnlyImportantInGames.IsHitTestVisible = glowDuringGames;

            CardReduceIntensityInGames.IsEnabled = glowDuringGames;
            CardReduceIntensityInGames.Focusable = glowDuringGames;
            CardReduceIntensityInGames.Opacity = glowDuringGames ? 1.0 : 0.45;

            CardReduceDurationInGames.IsEnabled = glowDuringGames;
            CardReduceDurationInGames.Focusable = glowDuringGames;
            CardReduceDurationInGames.Opacity = glowDuringGames ? 1.0 : 0.45;

            CardOnlyImportantInGames.IsEnabled = glowDuringGames;
            CardOnlyImportantInGames.Focusable = glowDuringGames;
            CardOnlyImportantInGames.Opacity = glowDuringGames ? 1.0 : 0.45;

            string? tooltip = glowDuringGames ? null : LocalizationService.Instance.GetString("Gaming.SubSettingsDisabledToolTip");
            CardReduceIntensityInGames.ToolTip = tooltip;
            CardReduceDurationInGames.ToolTip = tooltip;
            CardOnlyImportantInGames.ToolTip = tooltip;
            ToggleReduceIntensityInGames.ToolTip = tooltip;
            ToggleReduceDurationInGames.ToolTip = tooltip;
            ToggleOnlyImportantInGames.ToolTip = tooltip;

            Brush? primaryBrush = TryFindResource("TextPrimary") as Brush;
            Brush? mutedBrush = TryFindResource("TextTertiary") as Brush ?? TryFindResource("TextSecondary") as Brush;

            TxtReduceIntensityTitle.Foreground = glowDuringGames ? primaryBrush : mutedBrush;
            TxtReduceDurationTitle.Foreground = glowDuringGames ? primaryBrush : mutedBrush;
            TxtOnlyImportantTitle.Foreground = glowDuringGames ? primaryBrush : mutedBrush;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            _isDirty = true;
            if (!IsVisible || _isLoadingCommonGamingSettings || _isInitializing) return;
            try
            {
                Dispatcher.Invoke(LoadSettings);
            }
            catch { }
        }

        #region View Switching & Game Card Rendering

        public void ApplyViewMode(ApplicationViewMode mode)
        {
            if (mode == ApplicationViewMode.Grid)
            {
                RbGameGridView.IsChecked = true;
                RbGameListView.IsChecked = false;
                PnlGameList.Visibility = Visibility.Collapsed;
                PnlGameGrid.Visibility = Visibility.Visible;
            }
            else
            {
                RbGameListView.IsChecked = true;
                RbGameGridView.IsChecked = false;
                PnlGameList.Visibility = Visibility.Visible;
                PnlGameGrid.Visibility = Visibility.Collapsed;
            }
        }

        public void RbGameViewMode_Click(object sender, RoutedEventArgs e)
        {
            var mode = (RbGameGridView.IsChecked == true) ? ApplicationViewMode.Grid : ApplicationViewMode.List;
            ApplyViewMode(mode);

            if (_settingsService != null)
            {
                _settingsService.Current.GamingViewMode = mode;
                _settingsService.Save(_settingsService.Current);
            }
        }

        public void RenderTrackedGameCards()
        {
            if (_settingsService == null) return;
            _isDirty = false;
            _isLoadedOnce = true;
            var settings = _settingsService.Current;
            var games = settings.TrackedGames;

            PnlGameList.Children.Clear();
            PnlGameGrid.Children.Clear();

            if (games == null || games.Count == 0)
            {
                PnlEmptyGames.Visibility = Visibility.Visible;
                return;
            }

            PnlEmptyGames.Visibility = Visibility.Collapsed;

            foreach (var gameEntry in games)
            {
                if (string.IsNullOrWhiteSpace(gameEntry)) continue;

                string normId = AppSettings.NormalizeGameIdentifier(gameEntry);
                GameGlowOverride? gameOverride = null;
                if (settings.GameOverrides != null)
                {
                    settings.GameOverrides.TryGetValue(normId, out gameOverride);
                }
                bool hasOverrides = gameOverride != null && gameOverride.HasOverrides;

                // Create List Card
                var listCard = CreateGameListCard(gameEntry, gameOverride, hasOverrides);
                PnlGameList.Children.Add(listCard);

                // Create Grid Card
                var gridCard = CreateGameGridCard(gameEntry, gameOverride, hasOverrides);
                PnlGameGrid.Children.Add(gridCard);
            }
        }

        private FrameworkElement CreateGameListCard(string gameEntry, GameGlowOverride? gameOverride, bool hasOverrides)
        {
            var settings = _settingsService?.Current ?? new AppSettings();
            string fileName = Path.GetFileName(gameEntry);
            if (string.IsNullOrEmpty(fileName)) fileName = gameEntry;

            var card = new Border
            {
                Background = TryFindResource("CardBackground") as Brush,
                BorderBrush = TryFindResource("BorderColor") as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                SnapsToDevicePixels = true
            };

            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badge & Summary
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

            // 0: Controller Icon
            var iconBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = TryFindResource("CardBackgroundSecondary") as Brush,
                BorderBrush = TryFindResource("BorderColor") as Brush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = SymbolRegular.Games24,
                    FontSize = 16,
                    Foreground = (TryFindResource("AccentColor") as Brush) ?? System.Windows.Media.Brushes.DodgerBlue,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            // 1: Name & Path
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            textStack.Children.Add(new TextBlock
            {
                Text = fileName,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = TryFindResource("TextPrimary") as Brush,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            textStack.Children.Add(new TextBlock
            {
                Text = gameEntry,
                FontSize = 11,
                Foreground = TryFindResource("TextSecondary") as Brush,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // 2: Badge, Custom Glow Switch & Overrides Status
            var badgeStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };

            bool isCustomGlow = gameOverride != null && gameOverride.CustomGlowEnabled;
            var toggleCustomGlow = new Wpf.Ui.Controls.ToggleSwitch
            {
                IsChecked = isCustomGlow,
                Content = LocalizationService.Instance.GetString("Gaming.CustomGlow"),
                ToolTip = LocalizationService.Instance.GetString("Gaming.CustomGlowToolTip"),
                Margin = new Thickness(0, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            toggleCustomGlow.Click += (s, e) =>
            {
                if (_settingsService == null) return;
                bool enabled = toggleCustomGlow.IsChecked ?? false;
                var ov = _settingsService.Current.GetGameOverride(gameEntry) ?? new GameGlowOverride();
                ov.CustomGlowEnabled = enabled;
                if (enabled)
                {
                    ov.DurationMs ??= _settingsService.Current.CommonGamingDurationMs ?? 3000;
                    ov.Intensity ??= _settingsService.Current.CommonGamingIntensity ?? 0.8;
                    ov.GlowSize ??= _settingsService.Current.CommonGamingGlowSize ?? 32.0;
                    ov.CornerRadius ??= _settingsService.Current.CommonGamingCornerRadius ?? 0.0;
                }
                _settingsService.Current.SetGameOverride(gameEntry, ov);
                _settingsService.Save(_settingsService.Current);
                RenderTrackedGameCards();
            };
            badgeStack.Children.Add(toggleCustomGlow);

            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 3, 7, 3),
                Background = isCustomGlow ? (TryFindResource("GlassOverlay") as Brush) : (TryFindResource("CardBackgroundSecondary") as Brush),
                BorderBrush = isCustomGlow ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("BorderColor") as Brush),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = isCustomGlow
                        ? LocalizationService.Instance.GetString("Gaming.CustomGlow")
                        : LocalizationService.Instance.GetString("Gaming.InheritsGlobal"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isCustomGlow ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextMuted") as Brush)
                }
            };
            badgeStack.Children.Add(badgeBorder);

            string summaryText = isCustomGlow && gameOverride != null
                ? $"{((gameOverride.DurationMs ?? settings.CommonGamingDurationMs ?? 3000) / 1000.0):0.#}s • {(int)((gameOverride.Intensity ?? settings.CommonGamingIntensity ?? 0.8) * 100)}% • {(gameOverride.GlowSize ?? settings.CommonGamingGlowSize ?? 32.0):0}px • {(int)(gameOverride.CornerRadius ?? settings.CommonGamingCornerRadius ?? 0.0)}px"
                : $"{((settings.CommonGamingDurationMs ?? 3000) / 1000.0):0.#}s • {(int)((settings.CommonGamingIntensity ?? 0.8) * 100)}% • {(settings.CommonGamingGlowSize ?? 32.0):0}px • {(int)(settings.CommonGamingCornerRadius ?? 0.0)}px";

            badgeStack.Children.Add(new TextBlock
            {
                Text = summaryText,
                FontSize = 11,
                Foreground = TryFindResource("TextMuted") as Brush,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(badgeStack, 2);
            grid.Children.Add(badgeStack);

            // 3: Actions (Edit Overrides, Test Animation, Remove)
            var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnEdit = new Wpf.Ui.Controls.Button
            {
                Appearance = ControlAppearance.Secondary,
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = SymbolRegular.Wrench24 },
                ToolTip = LocalizationService.Instance.GetString("Gaming.EditGameSettings"),
                Height = 30,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = TryFindResource("TextPrimary") as Brush
            };
            btnEdit.Click += (s, e) => OpenGameOverridesEditor(gameEntry);
            actionStack.Children.Add(btnEdit);

            var btnTest = new Wpf.Ui.Controls.Button
            {
                Appearance = ControlAppearance.Secondary,
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = SymbolRegular.Play24 },
                ToolTip = LocalizationService.Instance.GetString("Gaming.TestGameAnimationToolTip"),
                Height = 30,
                Width = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = TryFindResource("TextPrimary") as Brush
            };
            btnTest.Click += (s, e) => _glowManager?.TriggerGameAnimationTest(gameEntry);
            actionStack.Children.Add(btnTest);

            var btnRemove = new Wpf.Ui.Controls.Button
            {
                Appearance = ControlAppearance.Transparent,
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = SymbolRegular.Delete24, Foreground = (TryFindResource("TextSecondary") as Brush) ?? System.Windows.Media.Brushes.Gray },
                ToolTip = LocalizationService.Instance.GetString("Gaming.RemoveGameToolTip"),
                Height = 30,
                Width = 30,
                Padding = new Thickness(0)
            };
            btnRemove.Click += (s, e) => RemoveGame(gameEntry);
            actionStack.Children.Add(btnRemove);

            Grid.SetColumn(actionStack, 3);
            grid.Children.Add(actionStack);

            card.Child = grid;
            return card;
        }

        private FrameworkElement CreateGameGridCard(string gameEntry, GameGlowOverride? gameOverride, bool hasOverrides)
        {
            var settings = _settingsService?.Current ?? new AppSettings();
            string fileName = Path.GetFileName(gameEntry);
            if (string.IsNullOrEmpty(fileName)) fileName = gameEntry;

            var card = new Border
            {
                Width = 246,
                Background = TryFindResource("CardBackground") as Brush,
                BorderBrush = TryFindResource("BorderColor") as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Margin = new Thickness(6),
                SnapsToDevicePixels = true
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Specs
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer actions

            // Top Row: Icon + Name + Badge
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = TryFindResource("CardBackgroundSecondary") as Brush,
                BorderBrush = TryFindResource("BorderColor") as Brush,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Wpf.Ui.Controls.SymbolIcon
                {
                    Symbol = SymbolRegular.Games24,
                    FontSize = 14,
                    Foreground = (TryFindResource("AccentColor") as Brush) ?? System.Windows.Media.Brushes.DodgerBlue,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(iconBorder, 0);
            topGrid.Children.Add(iconBorder);

            var titleBlock = new TextBlock
            {
                Text = fileName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = TryFindResource("TextPrimary") as Brush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = gameEntry
            };
            Grid.SetColumn(titleBlock, 1);
            topGrid.Children.Add(titleBlock);

            bool isCustomGlowGrid = gameOverride != null && gameOverride.CustomGlowEnabled;
            var badge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = isCustomGlowGrid ? (TryFindResource("GlassOverlay") as Brush) : (TryFindResource("CardBackgroundSecondary") as Brush),
                BorderBrush = isCustomGlowGrid ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("BorderColor") as Brush),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = isCustomGlowGrid
                        ? LocalizationService.Instance.GetString("Gaming.CustomGlow")
                        : LocalizationService.Instance.GetString("Gaming.InheritsGlobal"),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isCustomGlowGrid ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextMuted") as Brush)
                }
            };
            Grid.SetColumn(badge, 2);
            topGrid.Children.Add(badge);

            Grid.SetRow(topGrid, 0);
            grid.Children.Add(topGrid);

            // Middle: Specs Chips
            var specsBorder = new Border
            {
                Background = TryFindResource("CardBackgroundSecondary") as Brush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 10, 0, 10)
            };
            var specsStack = new StackPanel();

            // Duration
            var durGrid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            durGrid.Children.Add(new TextBlock { Text = LocalizationService.Instance.GetString("Gaming.GlowDuration"), FontSize = 11, Foreground = TryFindResource("TextMuted") as Brush });
            durGrid.Children.Add(new TextBlock
            {
                Text = hasOverrides && gameOverride?.DurationMs != null ? $"{gameOverride.DurationMs.Value / 1000.0:0.#} s" : $"{((settings.CommonGamingDurationMs ?? 3000) / 1000.0):0.#} s",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = hasOverrides && gameOverride?.DurationMs != null ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextSecondary") as Brush),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            specsStack.Children.Add(durGrid);

            // Intensity
            var intGrid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            intGrid.Children.Add(new TextBlock { Text = LocalizationService.Instance.GetString("Gaming.GlowIntensity"), FontSize = 11, Foreground = TryFindResource("TextMuted") as Brush });
            intGrid.Children.Add(new TextBlock
            {
                Text = hasOverrides && gameOverride?.Intensity != null ? $"{(int)(gameOverride.Intensity.Value * 100)}%" : $"{(int)((settings.CommonGamingIntensity ?? 0.6) * 100)}%",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = hasOverrides && gameOverride?.Intensity != null ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextSecondary") as Brush),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            specsStack.Children.Add(intGrid);

            // Blur
            var blurGrid = new Grid();
            blurGrid.Children.Add(new TextBlock { Text = LocalizationService.Instance.GetString("Gaming.GlowBlurSize"), FontSize = 11, Foreground = TryFindResource("TextMuted") as Brush });
            blurGrid.Children.Add(new TextBlock
            {
                Text = hasOverrides && gameOverride?.GlowSize != null ? $"{gameOverride.GlowSize.Value:0} px" : $"{settings.CommonGamingGlowSize ?? 30.0:0} px",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = hasOverrides && gameOverride?.GlowSize != null ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextSecondary") as Brush),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            specsStack.Children.Add(blurGrid);

            // Corner Rounding
            var crGrid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            crGrid.Children.Add(new TextBlock { Text = LocalizationService.Instance.GetString("Appearance.CornerRounding") ?? "Corner Rounding", FontSize = 11, Foreground = TryFindResource("TextMuted") as Brush });
            crGrid.Children.Add(new TextBlock
            {
                Text = isCustomGlowGrid && gameOverride?.CornerRadius != null ? $"{gameOverride.CornerRadius.Value:0} px" : $"{settings.CommonGamingCornerRadius ?? 0.0:0} px",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = isCustomGlowGrid && gameOverride?.CornerRadius != null ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextSecondary") as Brush),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            specsStack.Children.Add(crGrid);

            // Custom Glow Switch inside Grid Card
            var toggleCustomGlowGrid = new Wpf.Ui.Controls.ToggleSwitch
            {
                IsChecked = isCustomGlowGrid,
                Content = LocalizationService.Instance.GetString("Gaming.CustomGlow"),
                ToolTip = LocalizationService.Instance.GetString("Gaming.CustomGlowToolTip"),
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            toggleCustomGlowGrid.Click += (s, e) =>
            {
                if (_settingsService == null) return;
                bool enabled = toggleCustomGlowGrid.IsChecked ?? false;
                var ov = _settingsService.Current.GetGameOverride(gameEntry) ?? new GameGlowOverride();
                ov.CustomGlowEnabled = enabled;
                if (enabled)
                {
                    ov.DurationMs ??= _settingsService.Current.CommonGamingDurationMs ?? 3000;
                    ov.Intensity ??= _settingsService.Current.CommonGamingIntensity ?? 0.8;
                    ov.GlowSize ??= _settingsService.Current.CommonGamingGlowSize ?? 32.0;
                    ov.CornerRadius ??= _settingsService.Current.CommonGamingCornerRadius ?? 0.0;
                }
                _settingsService.Current.SetGameOverride(gameEntry, ov);
                _settingsService.Save(_settingsService.Current);
                RenderTrackedGameCards();
            };
            specsStack.Children.Add(toggleCustomGlowGrid);

            specsBorder.Child = specsStack;
            Grid.SetRow(specsBorder, 1);
            grid.Children.Add(specsBorder);

            // Footer Actions
            var actionGrid = new Grid();
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnEdit = new Wpf.Ui.Controls.Button
            {
                Appearance = ControlAppearance.Secondary,
                Content = LocalizationService.Instance.GetString("Common.Edit"),
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = SymbolRegular.Wrench24 },
                ToolTip = LocalizationService.Instance.GetString("Gaming.EditGameSettings"),
                Height = 28,
                Padding = new Thickness(8, 2, 8, 2),
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0),
                Foreground = TryFindResource("TextPrimary") as Brush
            };
            btnEdit.Click += (s, e) => OpenGameOverridesEditor(gameEntry);
            Grid.SetColumn(btnEdit, 0);
            actionGrid.Children.Add(btnEdit);

            var btnTest = new Wpf.Ui.Controls.Button
            {
                Appearance = ControlAppearance.Secondary,
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = SymbolRegular.Play24 },
                ToolTip = LocalizationService.Instance.GetString("Gaming.TestGameAnimationToolTip"),
                Height = 28,
                Width = 28,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 4, 0),
                Foreground = TryFindResource("TextPrimary") as Brush
            };
            btnTest.Click += (s, e) => _glowManager?.TriggerGameAnimationTest(gameEntry);
            Grid.SetColumn(btnTest, 1);
            actionGrid.Children.Add(btnTest);

            var btnRemove = new Wpf.Ui.Controls.Button
            {
                Appearance = ControlAppearance.Transparent,
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = SymbolRegular.Delete24, Foreground = (TryFindResource("TextSecondary") as Brush) ?? System.Windows.Media.Brushes.Gray },
                ToolTip = LocalizationService.Instance.GetString("Gaming.RemoveGameToolTip"),
                Height = 28,
                Width = 28,
                Padding = new Thickness(0)
            };
            btnRemove.Click += (s, e) => RemoveGame(gameEntry);
            Grid.SetColumn(btnRemove, 2);
            actionGrid.Children.Add(btnRemove);

            Grid.SetRow(actionGrid, 2);
            grid.Children.Add(actionGrid);

            card.Child = grid;
            return card;
        }

        #endregion

        #region Per-Game Overrides Editor Drawer

        private void OpenGameOverridesEditor(string gameEntry)
        {
            _editingGameEntry = gameEntry;
            var settings = _settingsService.Current;
            string id = AppSettings.NormalizeGameIdentifier(gameEntry);
            GameGlowOverride? gameOv = null;
            if (settings.GameOverrides != null)
            {
                settings.GameOverrides.TryGetValue(id, out gameOv);
            }

            int commonDur = settings.CommonGamingDurationMs ?? 3000;
            double commonIntens = settings.CommonGamingIntensity ?? 0.6;
            double commonGlow = settings.CommonGamingGlowSize ?? 30.0;

            string fileName = Path.GetFileName(gameEntry);
            if (string.IsNullOrEmpty(fileName)) fileName = gameEntry;
            TxtEditGameTitle.Text = $"{LocalizationService.Instance.GetString("Gaming.PerGameOverrides")}: {fileName}";
            TxtEditGameIdentifier.Text = gameEntry;

            // Duration
            bool hasDur = gameOv != null && gameOv.DurationMs.HasValue;
            ChkOverrideDuration.IsChecked = hasDur;
            SldGameDuration.Value = hasDur ? gameOv!.DurationMs!.Value : commonDur;
            SldGameDuration.IsEnabled = hasDur;
            TxtDurationInheritHint.Text = hasDur
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{commonDur / 1000.0:0.#} s");
            TxtDurationInheritHint.Foreground = (hasDur ? TryFindResource("AccentColor") : TryFindResource("TextMuted")) as Brush;
            TxtEditDurationVal.Text = $"{SldGameDuration.Value / 1000.0:0.#} s";

            // Intensity
            bool hasIntens = gameOv != null && gameOv.Intensity.HasValue;
            ChkOverrideIntensity.IsChecked = hasIntens;
            SldGameIntensity.Value = Math.Round((hasIntens ? gameOv!.Intensity!.Value : commonIntens) * 100);
            SldGameIntensity.IsEnabled = hasIntens;
            TxtIntensityInheritHint.Text = hasIntens
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)(commonIntens * 100)}%");
            TxtIntensityInheritHint.Foreground = (hasIntens ? TryFindResource("AccentColor") : TryFindResource("TextMuted")) as Brush;
            TxtEditIntensityVal.Text = $"{(int)SldGameIntensity.Value}%";

            // Blur Glow Size
            bool hasGlow = gameOv != null && gameOv.GlowSize.HasValue;
            ChkOverrideGlowSize.IsChecked = hasGlow;
            SldGameGlowSize.Value = hasGlow ? gameOv!.GlowSize!.Value : commonGlow;
            SldGameGlowSize.IsEnabled = hasGlow;
            TxtGlowSizeInheritHint.Text = hasGlow
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)commonGlow} px");
            TxtGlowSizeInheritHint.Foreground = (hasGlow ? TryFindResource("AccentColor") : TryFindResource("TextMuted")) as Brush;
            TxtEditGlowSizeVal.Text = $"{(int)SldGameGlowSize.Value} px";

            // Corner Rounding
            double commonCorner = settings.CommonGamingCornerRadius ?? 0.0;
            bool hasCorner = gameOv != null && gameOv.CornerRadius.HasValue;
            if (ChkOverrideCornerRadius != null)
            {
                ChkOverrideCornerRadius.IsChecked = hasCorner;
            }
            if (SldGameCornerRadius != null)
            {
                SldGameCornerRadius.Value = hasCorner ? gameOv!.CornerRadius!.Value : commonCorner;
                SldGameCornerRadius.IsEnabled = hasCorner;
            }
            if (TxtCornerRadiusInheritHint != null)
            {
                TxtCornerRadiusInheritHint.Text = hasCorner
                    ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                    : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)commonCorner} px");
                TxtCornerRadiusInheritHint.Foreground = (hasCorner ? TryFindResource("AccentColor") : TryFindResource("TextMuted")) as Brush;
            }
            if (TxtEditCornerRadiusVal != null && SldGameCornerRadius != null)
            {
                TxtEditCornerRadiusVal.Text = $"{(int)SldGameCornerRadius.Value} px";
            }

            PnlEditGameOverride.Visibility = Visibility.Visible;
        }

        private void ChkOverride_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.Current;
            int commonDur = settings.CommonGamingDurationMs ?? 3000;
            double commonIntens = settings.CommonGamingIntensity ?? 0.6;
            double commonGlow = settings.CommonGamingGlowSize ?? 30.0;

            SldGameDuration.IsEnabled = ChkOverrideDuration.IsChecked ?? false;
            TxtDurationInheritHint.Text = (ChkOverrideDuration.IsChecked ?? false)
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{commonDur / 1000.0:0.#} s");
            TxtDurationInheritHint.Foreground = (ChkOverrideDuration.IsChecked ?? false)
                ? (TryFindResource("AccentColor") as Brush)
                : (TryFindResource("TextMuted") as Brush);

            SldGameIntensity.IsEnabled = ChkOverrideIntensity.IsChecked ?? false;
            TxtIntensityInheritHint.Text = (ChkOverrideIntensity.IsChecked ?? false)
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)(commonIntens * 100)}%");
            TxtIntensityInheritHint.Foreground = (ChkOverrideIntensity.IsChecked ?? false)
                ? (TryFindResource("AccentColor") as Brush)
                : (TryFindResource("TextMuted") as Brush);

            SldGameGlowSize.IsEnabled = ChkOverrideGlowSize.IsChecked ?? false;
            TxtGlowSizeInheritHint.Text = (ChkOverrideGlowSize.IsChecked ?? false)
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)commonGlow} px");
            TxtGlowSizeInheritHint.Foreground = (ChkOverrideGlowSize.IsChecked ?? false)
                ? (TryFindResource("AccentColor") as Brush)
                : (TryFindResource("TextMuted") as Brush);

            double commonCorner = settings.CommonGamingCornerRadius ?? 0.0;
            if (SldGameCornerRadius != null && ChkOverrideCornerRadius != null)
            {
                SldGameCornerRadius.IsEnabled = ChkOverrideCornerRadius.IsChecked ?? false;
            }
            if (TxtCornerRadiusInheritHint != null && ChkOverrideCornerRadius != null)
            {
                TxtCornerRadiusInheritHint.Text = (ChkOverrideCornerRadius.IsChecked ?? false)
                    ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                    : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)commonCorner} px");
                TxtCornerRadiusInheritHint.Foreground = (ChkOverrideCornerRadius.IsChecked ?? false)
                    ? (TryFindResource("AccentColor") as Brush)
                    : (TryFindResource("TextMuted") as Brush);
            }
        }

        private void SldGameCornerRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtEditCornerRadiusVal != null)
            {
                TxtEditCornerRadiusVal.Text = $"{(int)e.NewValue} px";
            }
        }

        private void SldGameDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtEditDurationVal != null)
            {
                TxtEditDurationVal.Text = $"{e.NewValue / 1000.0:0.#} s";
            }
        }

        private void SldGameIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtEditIntensityVal != null)
            {
                TxtEditIntensityVal.Text = $"{(int)e.NewValue}%";
            }
        }

        private void SldGameGlowSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtEditGlowSizeVal != null)
            {
                TxtEditGlowSizeVal.Text = $"{(int)e.NewValue} px";
            }
        }

        private void BtnSaveGameOverrides_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingGameEntry) || _settingsService == null) return;

            var settings = _settingsService.Current;
            int? dur = (ChkOverrideDuration.IsChecked ?? false) ? (int)SldGameDuration.Value : null;
            double? intens = (ChkOverrideIntensity.IsChecked ?? false) ? Math.Round(SldGameIntensity.Value) / 100.0 : null;
            double? glow = (ChkOverrideGlowSize.IsChecked ?? false) ? Math.Round(SldGameGlowSize.Value) : null;
            double? corner = (ChkOverrideCornerRadius?.IsChecked ?? false) && SldGameCornerRadius != null ? Math.Round(SldGameCornerRadius.Value) : null;

            string id = AppSettings.NormalizeGameIdentifier(_editingGameEntry);
            if (dur.HasValue || intens.HasValue || glow.HasValue || corner.HasValue)
            {
                var ov = new GameGlowOverride
                {
                    GameIdentifier = id,
                    DurationMs = dur,
                    Intensity = intens,
                    GlowSize = glow,
                    CornerRadius = corner,
                    CustomGlowEnabled = true
                };
                settings.GameOverrides ??= new Dictionary<string, GameGlowOverride>(StringComparer.OrdinalIgnoreCase);
                settings.GameOverrides[id] = ov;
            }
            else
            {
                settings.GameOverrides?.Remove(id);
            }

            _settingsService.Save(settings);

            PnlEditGameOverride.Visibility = Visibility.Collapsed;
            RenderTrackedGameCards();

            ToastNotificationService.Instance.ShowSuccess(
                LocalizationService.Instance.GetString("Gaming.PerGameOverrides"),
                LocalizationService.Instance.GetString("Common.Save"));
        }

        private void BtnResetGameOverrides_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_editingGameEntry) || _settingsService == null) return;

            var settings = _settingsService.Current;
            string id = AppSettings.NormalizeGameIdentifier(_editingGameEntry);
            settings.GameOverrides?.Remove(id);
            _settingsService.Save(settings);

            PnlEditGameOverride.Visibility = Visibility.Collapsed;
            RenderTrackedGameCards();

            ToastNotificationService.Instance.ShowSuccess(
                LocalizationService.Instance.GetString("Gaming.InheritGlobal"),
                LocalizationService.Instance.GetString("Gaming.ResetToDefault"));
        }

        private void BtnCloseGameEditor_Click(object sender, RoutedEventArgs e)
        {
            PnlEditGameOverride.Visibility = Visibility.Collapsed;
            _editingGameEntry = null;
        }

        #endregion

        #region Actions & Event Handlers

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
                ToastNotificationService.Instance.ShowError(
                    LocalizationService.Instance.GetString("Gaming.ScanError"));
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
                RenderTrackedGameCards();

                if (newGames > 0)
                {
                    string msg = LocalizationService.Instance.GetString("Gaming.FoundGames", newGames);
                    TxtDetectionStatus.Text = msg;
                    ToastNotificationService.Instance.ShowSuccess(msg);
                }
                else
                {
                    string msg = LocalizationService.Instance.GetString("Gaming.NoNewGames");
                    TxtDetectionStatus.Text = msg;
                    ToastNotificationService.Instance.ShowInfo(msg);
                }
            }
            catch (Exception ex)
            {
                TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.ScanError");
                LoggerService.LogWarning($"Manual game scan failed: {ex.Message}");
                ToastNotificationService.Instance.ShowWarning(
                    LocalizationService.Instance.GetString("Gaming.ScanError"));
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
                Path.GetFileName(g).Equals(Path.GetFileName(trimmed), StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                settings.TrackedGames.Add(trimmed);

                string fileName = Path.GetFileName(trimmed);
                settings.IgnoredGames.RemoveAll(g =>
                    g.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(fileName) && Path.GetFileName(g).Equals(fileName, StringComparison.OrdinalIgnoreCase)));

                _settingsService.Save(settings);
                RenderTrackedGameCards();

                ToastNotificationService.Instance.ShowSuccess(
                    $"{Path.GetFileName(trimmed)} added",
                    LocalizationService.Instance.GetString("Gaming.TrackedGames"));
            }
        }

        private void RemoveGame(string gameItem)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;

            if (settings.TrackedGames.Remove(gameItem))
            {
                string fileName = Path.GetFileName(gameItem);
                if (!settings.IgnoredGames.Contains(gameItem, StringComparer.OrdinalIgnoreCase))
                {
                    settings.IgnoredGames.Add(gameItem);
                }
                if (!string.IsNullOrEmpty(fileName) && !settings.IgnoredGames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    settings.IgnoredGames.Add(fileName);
                }

                // Also clean up overrides for this game
                settings.SetGameOverride(gameItem, null);

                _settingsService.Save(settings);
                RenderTrackedGameCards();

                ToastNotificationService.Instance.ShowInfo(
                    $"{fileName} removed",
                    LocalizationService.Instance.GetString("Gaming.TrackedGames"));
            }
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;

            var trackedGames = _settingsService.Current.TrackedGames;
            if (trackedGames == null || trackedGames.Count == 0)
            {
                string warning = LocalizationService.Instance.GetString("Gaming.EmptyTrackedGamesWarning");
                TxtDetectionStatus.Text = warning;
                ToastNotificationService.Instance.ShowWarning(warning);
                return;
            }

            string? selectedGame = trackedGames[0];
            bool triggered = _glowManager.TriggerGameAnimationTest(selectedGame);
            if (!triggered)
            {
                if (!_settingsService.Current.GlowDuringGames)
                {
                    string disabledMsg = LocalizationService.Instance.GetString("Gaming.SubSettingsDisabledToolTip");
                    TxtDetectionStatus.Text = disabledMsg;
                    ToastNotificationService.Instance.ShowWarning(disabledMsg);
                }
            }
        }

        #endregion
    }
}
