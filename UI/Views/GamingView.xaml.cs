using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Controls;

namespace NotiGlow.UI.Views
{
    public sealed partial class GamingView : UserControl
    {
        private SettingsService _settingsService = null!;
        private GlowManager? _glowManager;
        private GameDetectionService? _gameDetectionService;
        private ProfileService? _profileService;
        private bool _isDirty = true;
        private bool _isLoadedOnce = false;
        private string? _highlightedIdentifier;

        public bool IsDirty => _isDirty;

        public event EventHandler<CategoryNavigationEventArgs>? NavigateToCategoryRequested;

        public GamingView()
        {
            this.InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(MainScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);

                UpdateLocalizedTexts();
                if (_isDirty || !_isLoadedOnce)
                {
                    RenderTrackedGameCards();
                }
            };
        }

        private void OnLanguageChanged(object? sender, string lang)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLocalizedTexts();
                RenderTrackedGameCards();
                if (PnlEditGameOverride.Visibility == Visibility.Visible)
                {
                    PnlEditGameOverride.RefreshLocalizedTitle();
                }
            });
        }

        public void Initialize(
            SettingsService settingsService,
            GlowManager? glowManager = null,
            GameDetectionService? gameDetectionService = null,
            ProfileService? profileService = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            _gameDetectionService = gameDetectionService ?? glowManager?.GameDetectionService;
            _profileService = profileService;

            if (_profileService != null)
            {
                _profileService.ProfilesChanged += (s, e) =>
                {
                    _isDirty = true;
                    if (Visibility == Visibility.Visible)
                    {
                        DispatcherQueue.TryEnqueue(() => RenderTrackedGameCards());
                    }
                };
            }

            LoadSettings();
            UpdateLocalizedTexts();
        }

        private void UpdateLocalizedTexts()
        {
            TxtGamingTitle.Text = LocalizationService.Instance.GetString("Gaming.Title");
            TxtGamingSubtitle.Text = LocalizationService.Instance.GetString("Gaming.Subtitle");
            TxtBtnTestGameAnimation.Text = LocalizationService.Instance.GetString("Gaming.TestGameAnimation");
            TxtSectionGamingMode.Text = LocalizationService.Instance.GetString("Gaming.ActivationSection");
            TxtGamingMode.Text = LocalizationService.Instance.GetString("Gaming.Mode");
            TxtGamingModeDesc.Text = LocalizationService.Instance.GetString("Gaming.ModeDesc");
            TxtBehaviorRules.Text = LocalizationService.Instance.GetString("Gaming.BehaviorRules");
            TxtGlowDuringGamesTitle.Text = LocalizationService.Instance.GetString("Gaming.GlowDuringGames");
            TxtGlowDuringGamesDesc.Text = LocalizationService.Instance.GetString("Gaming.GlowDuringGamesDesc");
            TxtReduceIntensityTitle.Text = LocalizationService.Instance.GetString("Gaming.ReduceIntensity");
            TxtReduceIntensityDesc.Text = LocalizationService.Instance.GetString("Gaming.ReduceIntensityDesc");
            TxtReduceDurationTitle.Text = LocalizationService.Instance.GetString("Gaming.ReduceDuration");
            TxtReduceDurationDesc.Text = LocalizationService.Instance.GetString("Gaming.ReduceDurationDesc");
            TxtOnlyImportantTitle.Text = LocalizationService.Instance.GetString("Gaming.OnlyImportant");
            TxtOnlyImportantDesc.Text = LocalizationService.Instance.GetString("Gaming.OnlyImportantDesc");
            TxtTrackedGamesHeader.Text = LocalizationService.Instance.GetString("Gaming.TrackedGames");
            TxtDetectionStatus.Text = LocalizationService.Instance.GetString("Gaming.DetectionSupport");
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;

            ToggleGamingMode.IsOn = settings.GamingModeEnabled;
            ToggleGlowDuringGames.IsOn = settings.GlowDuringGames;
            ToggleReduceIntensityInGames.IsOn = settings.ReduceIntensityInGames;
            ToggleReduceDurationInGames.IsOn = settings.ReduceDurationInGames;
            ToggleOnlyImportantInGames.IsOn = settings.OnlyImportantInGames;

            RenderTrackedGameCards();
        }

        private List<(string Entry, AppProfile? Profile, string NormId)> CollectUniqueGamingEntries()
        {
            var result = new List<(string Entry, AppProfile? Profile, string NormId)>();
            var seenNorms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var settings = _settingsService.Current;
            var games = settings.TrackedGames ?? new List<string>();

            foreach (var gamePath in games)
            {
                if (string.IsNullOrWhiteSpace(gamePath)) continue;
                if (gamePath.Contains(@"\Windows\SystemApps\", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileName(gamePath.Trim()).Equals("AppActions.exe", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string normId = AppSettings.NormalizeGameIdentifier(gamePath);
                if (string.IsNullOrEmpty(normId)) continue;

                var profile = _profileService?.Profiles.FirstOrDefault(p => AppCategoryHelper.MatchesIdentifier(p, gamePath));
                string friendlyName = AppCategoryHelper.ResolveFriendlyGameName(gamePath, profile);
                bool isSharedExe = AppCategoryHelper.IsSharedEngineExecutable(normId);
                string dedupKey = isSharedExe ? $"{normId}::{friendlyName}" : normId;

                if (!seenNorms.Add(dedupKey) || (!string.IsNullOrEmpty(friendlyName) && !seenNames.Add(friendlyName)))
                    continue;

                result.Add((gamePath, profile, normId));
            }

            if (_profileService != null)
            {
                foreach (var profile in _profileService.Profiles)
                {
                    if (!AppCategoryHelper.IsGamingCategory(profile.Category)) continue;
                    string entry = AppCategoryHelper.GetPrimaryIdentifier(profile);
                    if (entry.Contains(@"\Windows\SystemApps\", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(entry.Trim()).Equals("AppActions.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string normId = AppSettings.NormalizeGameIdentifier(entry);
                    if (string.IsNullOrEmpty(normId)) continue;

                    string friendlyName = AppCategoryHelper.ResolveFriendlyGameName(entry, profile);
                    bool isSharedExe = AppCategoryHelper.IsSharedEngineExecutable(normId);
                    string dedupKey = isSharedExe ? $"{normId}::{friendlyName}" : normId;

                    if (!seenNorms.Add(dedupKey) || (!string.IsNullOrEmpty(friendlyName) && !seenNames.Add(friendlyName)))
                        continue;

                    result.Add((entry, profile, normId));
                }
            }

            return result;
        }

        public void RenderTrackedGameCards()
        {
            if (_settingsService == null) return;
            _isDirty = false;
            _isLoadedOnce = true;
            PnlTrackedGames.Children.Clear();

            var settings = _settingsService.Current;
            var entries = CollectUniqueGamingEntries();

            if (entries.Count == 0)
            {
                var emptyBorder = new Border
                {
                    Background = (Brush)Application.Current.Resources["CardBackground"],
                    BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16)
                };
                emptyBorder.Child = new TextBlock
                {
                    Text = LocalizationService.Instance.GetString("Gaming.NoTrackedGames"),
                    Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                PnlTrackedGames.Children.Add(emptyBorder);
                return;
            }

            var destructiveStyle = Application.Current.Resources.TryGetValue("DestructiveIconButtonStyle", out var styleObj)
                ? styleObj as Style
                : null;

            foreach (var (gamePath, matchingProfile, normId) in entries)
            {
                string friendlyName = AppCategoryHelper.ResolveFriendlyGameName(gamePath, matchingProfile);

                GameGlowOverride? ov = null;
                settings.GameOverrides?.TryGetValue(normId, out ov);

                var card = new Border
                {
                    Tag = normId,
                    Background = (Brush)Application.Current.Resources["CardBackground"],
                    BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                if (!string.IsNullOrEmpty(_highlightedIdentifier) &&
                    string.Equals(AppSettings.NormalizeGameIdentifier(_highlightedIdentifier), normId, StringComparison.OrdinalIgnoreCase))
                {
                    ApplyCardHighlight(card, true);
                }

                var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Left Section: Game Icon + Title & Path
                var leftGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
                leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var iconBorder = new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = (Brush)Application.Current.Resources["ControlBackground"],
                    BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                    BorderThickness = new Thickness(1)
                };

                var iconGrid = new Grid();
                var imgIcon = new Image
                {
                    Width = 30,
                    Height = 30,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                var fallbackIcon = new FontIcon
                {
                    Glyph = "\uE7FC",
                    FontSize = 20,
                    Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Visibility = Visibility.Visible
                };
                iconGrid.Children.Add(imgIcon);
                iconGrid.Children.Add(fallbackIcon);
                iconBorder.Child = iconGrid;
                Grid.SetColumn(iconBorder, 0);
                leftGrid.Children.Add(iconBorder);

                LoadGameIconAsync(imgIcon, fallbackIcon, gamePath);

                var sp = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = friendlyName,
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["TextPrimary"],
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                sp.Children.Add(new TextBlock
                {
                    Text = gamePath,
                    Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(sp, 1);
                leftGrid.Children.Add(sp);

                Grid.SetColumn(leftGrid, 0);
                grid.Children.Add(leftGrid);

                // Right Section: Color Swatch + Customize (Pencil) + Remove (Trash with red hover)
                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };

                // Optional Color Swatch indicator matching Applications list cards
                string colorHex = ov?.ColorHex ?? matchingProfile?.ColorHex ?? settings.DefaultColorHex;
                var swatch = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)Application.Current.Resources["BorderColor"],
                    Background = ColorHelper.IsRgbSpectrum(colorHex)
                        ? ColorHelper.CreateRainbowLinearBrush(new Windows.Foundation.Point(0, 0), new Windows.Foundation.Point(1, 1))
                        : new SolidColorBrush(ColorHelper.ParseColor(colorHex))
                };
                actions.Children.Add(swatch);

                string capturedId = gamePath;
                string capturedName = friendlyName;

                // Customize Icon Button (Edit/Pencil)
                var btnEdit = new Button
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = new FontIcon { Glyph = "\uE70F", FontSize = 12 }
                };
                AutomationProperties.SetName(btnEdit, "Customize");
                ToolTipService.SetToolTip(btnEdit, "Customize");
                btnEdit.Click += (s, e) => OpenGameOverridesEditor(capturedId);

                // Remove Icon Button (Trash with red PointerOver / Pressed)
                var btnRemove = new Button
                {
                    Padding = new Thickness(8, 6, 8, 6),
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = new FontIcon { Glyph = "\uE74D", FontSize = 12 }
                };
                if (destructiveStyle != null)
                {
                    btnRemove.Style = destructiveStyle;
                }
                AutomationProperties.SetName(btnRemove, "Remove");
                ToolTipService.SetToolTip(btnRemove, "Remove");
                btnRemove.Click += (s, e) => ConfirmAndRemoveGameAsync(capturedId, capturedName);

                actions.Children.Add(btnEdit);
                actions.Children.Add(btnRemove);
                Grid.SetColumn(actions, 1);
                grid.Children.Add(actions);

                card.Child = grid;
                PnlTrackedGames.Children.Add(card);
            }
        }

        private static void ApplyCardHighlight(Border card, bool highlighted)
        {
            var res = Application.Current.Resources;
            if (highlighted)
            {
                if (res.TryGetValue("AccentColor", out var accent) && accent is Brush accentBrush)
                    card.BorderBrush = accentBrush;
                if (res.TryGetValue("NavActiveBackground", out var activeBg) && activeBg is Brush bgBrush)
                    card.Background = bgBrush;
                card.BorderThickness = new Thickness(1.5);
            }
            else
            {
                if (res.TryGetValue("BorderColor", out var border) && border is Brush borderBrush)
                    card.BorderBrush = borderBrush;
                if (res.TryGetValue("CardBackground", out var cardBg) && cardBg is Brush cardBrush)
                    card.Background = cardBrush;
                card.BorderThickness = new Thickness(1);
            }
        }

        public void RefreshAndHighlightGame(string identifier)
        {
            _highlightedIdentifier = identifier;
            PnlEditGameOverride.Visibility = Visibility.Collapsed;
            RenderTrackedGameCards();

            string targetNorm = AppSettings.NormalizeGameIdentifier(identifier);
            DispatcherQueue.TryEnqueue(async () =>
            {
                Border? matchedCard = null;
                foreach (var child in PnlTrackedGames.Children)
                {
                    if (child is Border border && border.Tag is string tag &&
                        string.Equals(tag, targetNorm, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedCard = border;
                        break;
                    }
                }

                if (matchedCard != null)
                {
                    ApplyCardHighlight(matchedCard, true);
                    matchedCard.StartBringIntoView();

                    if (!NotiGlow.UI.Animations.MotionPolicy.IsReduceMotion)
                    {
                        await Task.Delay(2600);
                        if (_highlightedIdentifier == identifier)
                        {
                            _highlightedIdentifier = null;
                            ApplyCardHighlight(matchedCard, false);
                        }
                    }
                }
            });
        }

        private async void ConfirmAndRemoveGameAsync(string gamePath, string friendlyName)
        {
            string message = $"Are you sure you want to remove '{friendlyName}' from tracked games? Its custom glow overrides will also be removed.";

            bool confirmed = await DialogService.ShowDestructiveConfirmAsync(
                this.XamlRoot,
                title: "Remove Game?",
                message: message,
                primaryButtonText: "Remove",
                cancelButtonText: "Cancel");

            if (confirmed)
            {
                AppCategoryHelper.RemoveGameCompletely(gamePath, _profileService, _settingsService);
                RenderTrackedGameCards();
            }
        }

        private async void LoadGameIconAsync(Image imgIcon, FontIcon fallbackIcon, string gamePath)
        {
            try
            {
                var cached = AppIconService.Instance.GetCachedIcon(gamePath, gamePath);
                if (cached != null)
                {
                    imgIcon.Source = cached;
                    imgIcon.Visibility = Visibility.Visible;
                    fallbackIcon.Visibility = Visibility.Collapsed;
                    return;
                }

                var loaded = await AppIconService.Instance.GetAppIconAsync(gamePath, gamePath);
                if (loaded != null)
                {
                    imgIcon.Source = loaded;
                    imgIcon.Visibility = Visibility.Visible;
                    fallbackIcon.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        public void OpenGameOverridesEditor(string gameEntry)
        {
            string normId = AppSettings.NormalizeGameIdentifier(gameEntry);
            var settings = _settingsService.Current;

            GameGlowOverride? ov = null;
            settings.GameOverrides?.TryGetValue(normId, out ov);

            var existingProfile = _profileService?.Profiles.FirstOrDefault(p => AppCategoryHelper.MatchesIdentifier(p, gameEntry));

            string friendlyName = AppCategoryHelper.ResolveFriendlyGameName(gameEntry, existingProfile);

            var editorProfile = new AppProfile
            {
                AppId = existingProfile?.AppId ?? Path.GetFileName(gameEntry),
                Name = !string.IsNullOrEmpty(friendlyName) ? friendlyName : gameEntry,
                ExecutablePath = existingProfile?.ExecutablePath ?? (gameEntry.Contains('\\') || gameEntry.Contains('/') ? gameEntry : null),
                Category = AppCategoryHelper.CategoryGaming,
                Enabled = existingProfile?.Enabled ?? true,
                Priority = existingProfile?.Priority ?? NotificationPriority.Normal,
                ColorHex = ov?.ColorHex ?? existingProfile?.ColorHex ?? settings.DefaultColorHex,
                DurationMs = ov?.DurationMs ?? existingProfile?.DurationMs ?? (settings.CommonGamingDurationMs ?? 3000),
                Intensity = ov?.Intensity ?? existingProfile?.Intensity ?? (settings.CommonGamingIntensity ?? 0.8),
                GlowSize = ov?.GlowSize ?? existingProfile?.GlowSize ?? (settings.CommonGamingGlowSize ?? 32.0),
                CornerRadius = ov?.CornerRadius ?? existingProfile?.CornerRadius ?? (settings.CommonGamingCornerRadius ?? 0.0),
                Style = ov?.Style ?? existingProfile?.Style ?? settings.DefaultStyle,
                Thickness = existingProfile?.Thickness ?? settings.DefaultThickness,
                IsCustom = existingProfile?.IsCustom ?? true
            };

            PnlEditGameOverride.OpenForProfile(
                editorProfile,
                isNew: false,
                isGameMode: true,
                customGlowEnabled: ov?.CustomGlowEnabled ?? true);
            PnlEditGameOverride.Visibility = Visibility.Visible;
        }

        private void PnlEditGameOverride_SaveRequested(object? sender, ProfileEditorSaveEventArgs e)
        {
            AppCategoryHelper.ApplyCategorySync(
                e.Profile,
                _profileService,
                _settingsService,
                e.CustomGlowEnabled,
                e.OriginalIdentifier);

            PnlEditGameOverride.Visibility = Visibility.Collapsed;
            RenderTrackedGameCards();

            string targetId = AppCategoryHelper.GetPrimaryIdentifier(e.Profile);
            if (NavigateToCategoryRequested != null)
            {
                NavigateToCategoryRequested.Invoke(this, new CategoryNavigationEventArgs(e.Profile.Category, targetId));
            }
            else if (AppCategoryHelper.IsGamingCategory(e.Profile.Category))
            {
                RefreshAndHighlightGame(targetId);
            }
        }

        private void PnlEditGameOverride_CancelRequested(object? sender, EventArgs e)
        {
            PnlEditGameOverride.Visibility = Visibility.Collapsed;
        }

        private async void BtnScanGames_Click(object sender, RoutedEventArgs e)
        {
            if (_gameDetectionService == null) return;
            BtnScanGames.IsEnabled = false;
            try
            {
                var detected = await _gameDetectionService.DetectInstalledGamesAsync();
                int added = await _gameDetectionService.ScanAndSyncTrackedGamesAsync(_profileService);
                RenderTrackedGameCards();

                string msg = $"Scan complete. {detected.Count} games detected ({added} newly added).";
                ToastNotificationService.Instance.ShowSuccess(msg, "Game Scan");
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Game scan error", ex);
                ToastNotificationService.Instance.ShowError("Scanning games failed.", "Scan Error");
            }
            finally
            {
                BtnScanGames.IsEnabled = true;
            }
        }

        private void ToggleGamingMode_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.GamingModeEnabled != ToggleGamingMode.IsOn)
            {
                settings.GamingModeEnabled = ToggleGamingMode.IsOn;
                _settingsService.Save(settings);
            }
        }

        private void ToggleGlowDuringGames_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.GlowDuringGames != ToggleGlowDuringGames.IsOn)
            {
                settings.GlowDuringGames = ToggleGlowDuringGames.IsOn;
                _settingsService.Save(settings);
            }
        }

        private void ToggleReduceIntensityInGames_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.ReduceIntensityInGames != ToggleReduceIntensityInGames.IsOn)
            {
                settings.ReduceIntensityInGames = ToggleReduceIntensityInGames.IsOn;
                _settingsService.Save(settings);
            }
        }

        private void ToggleReduceDurationInGames_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.ReduceDurationInGames != ToggleReduceDurationInGames.IsOn)
            {
                settings.ReduceDurationInGames = ToggleReduceDurationInGames.IsOn;
                _settingsService.Save(settings);
            }
        }

        private void ToggleOnlyImportantInGames_Toggled(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;
            var settings = _settingsService.Current;
            if (settings.OnlyImportantInGames != ToggleOnlyImportantInGames.IsOn)
            {
                settings.OnlyImportantInGames = ToggleOnlyImportantInGames.IsOn;
                _settingsService.Save(settings);
            }
        }

        public void EnsureLoaded()
        {
            if (!_isDirty && _isLoadedOnce) return;
            RenderTrackedGameCards();
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;

            var testProfile = new AppProfile
            {
                AppId = "GameTest",
                Name = "Game Glow Test",
                Category = "Gaming",
                ColorHex = _settingsService.Current.DefaultColorHex,
                DurationMs = 2500,
                Intensity = 0.6,
                Style = GlowStyle.Comet,
                Thickness = 6,
                GlowSize = 36
            };
            _glowManager.TriggerProfile(testProfile);
        }
    }
}
