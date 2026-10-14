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
                    RenderTrackedGameCards();
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

            ToggleReduceIntensityInGames.IsChecked = settings.ReduceIntensityInGames;
            ToggleReduceDurationInGames.IsChecked = settings.ReduceDurationInGames;
            ToggleOnlyImportantInGames.IsChecked = settings.OnlyImportantInGames;

            ApplyGameSubSettingsDependency(settings.GlowDuringGames);
            ApplyViewMode(settings.GamingViewMode);

            RenderTrackedGameCards();

            _isInitializing = false;
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
            Dispatcher.Invoke(LoadSettings);
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

                var gameOverride = settings.GetGameOverride(gameEntry);
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

            // 2: Badge & Overrides Status
            var badgeStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };

            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 3, 7, 3),
                Background = hasOverrides ? (TryFindResource("GlassOverlay") as Brush) : (TryFindResource("CardBackgroundSecondary") as Brush),
                BorderBrush = hasOverrides ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("BorderColor") as Brush),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = hasOverrides
                        ? LocalizationService.Instance.GetString("Gaming.CustomBadge")
                        : LocalizationService.Instance.GetString("Gaming.InheritsGlobal"),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = hasOverrides ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextMuted") as Brush)
                }
            };
            badgeStack.Children.Add(badgeBorder);

            string summaryText = hasOverrides
                ? $"{gameOverride?.DurationMs ?? _settingsService.Current.DefaultDurationMs}ms • {(int)((gameOverride?.Intensity ?? _settingsService.Current.DefaultIntensity) * 100)}% • {gameOverride?.GlowSize ?? 30}px"
                : LocalizationService.Instance.GetString("Gaming.InheritGlobal");

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

            var badge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = hasOverrides ? (TryFindResource("GlassOverlay") as Brush) : (TryFindResource("CardBackgroundSecondary") as Brush),
                BorderBrush = hasOverrides ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("BorderColor") as Brush),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = hasOverrides
                        ? LocalizationService.Instance.GetString("Gaming.CustomBadge")
                        : LocalizationService.Instance.GetString("Gaming.InheritsGlobal"),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = hasOverrides ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextMuted") as Brush)
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
                Text = gameOverride?.DurationMs.HasValue == true ? $"{gameOverride.DurationMs.Value} ms" : $"{_settingsService.Current.DefaultDurationMs} ms*",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = gameOverride?.DurationMs.HasValue == true ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextSecondary") as Brush),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            specsStack.Children.Add(durGrid);

            // Intensity
            var intGrid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            intGrid.Children.Add(new TextBlock { Text = LocalizationService.Instance.GetString("Gaming.GlowIntensity"), FontSize = 11, Foreground = TryFindResource("TextMuted") as Brush });
            intGrid.Children.Add(new TextBlock
            {
                Text = gameOverride?.Intensity.HasValue == true ? $"{(int)(gameOverride.Intensity.Value * 100)}%" : $"{(int)(_settingsService.Current.DefaultIntensity * 100)}%*",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = gameOverride?.Intensity.HasValue == true ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextSecondary") as Brush),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            specsStack.Children.Add(intGrid);

            // Blur
            var blurGrid = new Grid();
            blurGrid.Children.Add(new TextBlock { Text = LocalizationService.Instance.GetString("Gaming.GlowBlurSize"), FontSize = 11, Foreground = TryFindResource("TextMuted") as Brush });
            blurGrid.Children.Add(new TextBlock
            {
                Text = gameOverride?.GlowSize.HasValue == true ? $"{gameOverride.GlowSize.Value:0} px" : "30 px*",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = gameOverride?.GlowSize.HasValue == true ? (TryFindResource("AccentColor") as Brush) : (TryFindResource("TextSecondary") as Brush),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            });
            specsStack.Children.Add(blurGrid);

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
            var currentOverride = settings.GetGameOverride(gameEntry);

            string fileName = Path.GetFileName(gameEntry);
            TxtEditGameTitle.Text = $"{LocalizationService.Instance.GetString("Gaming.PerGameOverrides")}: {fileName}";
            TxtEditGameIdentifier.Text = gameEntry;

            // Duration
            if (currentOverride?.DurationMs.HasValue == true)
            {
                ChkOverrideDuration.IsChecked = true;
                SldGameDuration.Value = currentOverride.DurationMs.Value;
                SldGameDuration.IsEnabled = true;
                TxtDurationInheritHint.Text = LocalizationService.Instance.GetString("Gaming.CustomOverrides");
                TxtDurationInheritHint.Foreground = TryFindResource("AccentColor") as Brush;
            }
            else
            {
                ChkOverrideDuration.IsChecked = false;
                SldGameDuration.Value = settings.DefaultDurationMs;
                SldGameDuration.IsEnabled = false;
                TxtDurationInheritHint.Text = LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{settings.DefaultDurationMs} ms");
                TxtDurationInheritHint.Foreground = TryFindResource("TextMuted") as Brush;
            }
            TxtEditDurationVal.Text = $"{(int)SldGameDuration.Value} ms";

            // Intensity
            if (currentOverride?.Intensity.HasValue == true)
            {
                ChkOverrideIntensity.IsChecked = true;
                SldGameIntensity.Value = Math.Round(currentOverride.Intensity.Value * 100);
                SldGameIntensity.IsEnabled = true;
                TxtIntensityInheritHint.Text = LocalizationService.Instance.GetString("Gaming.CustomOverrides");
                TxtIntensityInheritHint.Foreground = TryFindResource("AccentColor") as Brush;
            }
            else
            {
                ChkOverrideIntensity.IsChecked = false;
                SldGameIntensity.Value = Math.Round(settings.DefaultIntensity * 100);
                SldGameIntensity.IsEnabled = false;
                TxtIntensityInheritHint.Text = LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)(settings.DefaultIntensity * 100)}%");
                TxtIntensityInheritHint.Foreground = TryFindResource("TextMuted") as Brush;
            }
            TxtEditIntensityVal.Text = $"{(int)SldGameIntensity.Value}%";

            // Blur Glow Size
            if (currentOverride?.GlowSize.HasValue == true)
            {
                ChkOverrideGlowSize.IsChecked = true;
                SldGameGlowSize.Value = currentOverride.GlowSize.Value;
                SldGameGlowSize.IsEnabled = true;
                TxtGlowSizeInheritHint.Text = LocalizationService.Instance.GetString("Gaming.CustomOverrides");
                TxtGlowSizeInheritHint.Foreground = TryFindResource("AccentColor") as Brush;
            }
            else
            {
                ChkOverrideGlowSize.IsChecked = false;
                SldGameGlowSize.Value = 30;
                SldGameGlowSize.IsEnabled = false;
                TxtGlowSizeInheritHint.Text = LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", "30 px");
                TxtGlowSizeInheritHint.Foreground = TryFindResource("TextMuted") as Brush;
            }
            TxtEditGlowSizeVal.Text = $"{(int)SldGameGlowSize.Value} px";

            PnlEditGameOverride.Visibility = Visibility.Visible;
        }

        private void ChkOverride_Click(object sender, RoutedEventArgs e)
        {
            var settings = _settingsService.Current;

            SldGameDuration.IsEnabled = ChkOverrideDuration.IsChecked ?? false;
            TxtDurationInheritHint.Text = (ChkOverrideDuration.IsChecked ?? false)
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{settings.DefaultDurationMs} ms");
            TxtDurationInheritHint.Foreground = (ChkOverrideDuration.IsChecked ?? false)
                ? (TryFindResource("AccentColor") as Brush)
                : (TryFindResource("TextMuted") as Brush);

            SldGameIntensity.IsEnabled = ChkOverrideIntensity.IsChecked ?? false;
            TxtIntensityInheritHint.Text = (ChkOverrideIntensity.IsChecked ?? false)
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", $"{(int)(settings.DefaultIntensity * 100)}%");
            TxtIntensityInheritHint.Foreground = (ChkOverrideIntensity.IsChecked ?? false)
                ? (TryFindResource("AccentColor") as Brush)
                : (TryFindResource("TextMuted") as Brush);

            SldGameGlowSize.IsEnabled = ChkOverrideGlowSize.IsChecked ?? false;
            TxtGlowSizeInheritHint.Text = (ChkOverrideGlowSize.IsChecked ?? false)
                ? LocalizationService.Instance.GetString("Gaming.CustomOverrides")
                : LocalizationService.Instance.GetString("Gaming.InheritsGlobalDefault", "30 px");
            TxtGlowSizeInheritHint.Foreground = (ChkOverrideGlowSize.IsChecked ?? false)
                ? (TryFindResource("AccentColor") as Brush)
                : (TryFindResource("TextMuted") as Brush);
        }

        private void SldGameDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtEditDurationVal != null)
            {
                TxtEditDurationVal.Text = $"{(int)e.NewValue} ms";
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
            var overrideSettings = new GameGlowOverride
            {
                GameIdentifier = AppSettings.NormalizeGameIdentifier(_editingGameEntry),
                DurationMs = (ChkOverrideDuration.IsChecked ?? false) ? (int)SldGameDuration.Value : null,
                Intensity = (ChkOverrideIntensity.IsChecked ?? false) ? Math.Round(SldGameIntensity.Value) / 100.0 : null,
                GlowSize = (ChkOverrideGlowSize.IsChecked ?? false) ? Math.Round(SldGameGlowSize.Value) : null
            };

            settings.SetGameOverride(_editingGameEntry, overrideSettings.HasOverrides ? overrideSettings : null);
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
            settings.SetGameOverride(_editingGameEntry, null);
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
