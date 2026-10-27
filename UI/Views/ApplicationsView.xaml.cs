using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace NotiGlow.UI.Views
{
    public partial class ApplicationsView : UserControl
    {
        private ProfileService? _profileService;
        private GlowManager? _glowManager;
        private SettingsService? _settingsService;
        private AppProfile? _editingProfile;
        private bool _isNewProfile = false;
        private bool _isLoadedOnce = false;
        private bool _isDirty = true;
        private bool _hasInitialScanRun = false;
        private ApplicationSortCriterion _sortCriterion = ApplicationSortCriterion.Alphabetical;
        private bool _sortDescending;
        private bool _ignoreNextSortSelectionChanged;
        public bool IsDirty => _isDirty;

        public void EnsureLoaded()
        {
            if (!_isLoadedOnce || _isDirty)
            {
                _isLoadedOnce = true;
                RefreshAppCards();
            }
        }

        public ApplicationsView()
        {
            InitializeComponent();
            CmbApplicationSort.ItemContainerGenerator.StatusChanged += (s, e) => AttachSortItemHandlers();
            CmbApplicationSort.Loaded += (s, e) => AttachSortItemHandlers();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += async (s, e) =>
            {
                EnsureLoaded();
                if (_profileService != null && !_hasInitialScanRun)
                {
                    _hasInitialScanRun = true;
                    int newlyAdded = await _profileService.ScanAndSyncInstalledAppsAsync();
                    if (newlyAdded > 0)
                    {
                        RefreshAppCards();
                    }
                }
            };
        }

        private void OnLanguageChanged(object? sender, string lang)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    RefreshAppCards();
                    RefreshSortLabels();
                    if (_editingProfile != null && PnlEditProfile.Visibility == Visibility.Visible)
                    {
                        TxtEditorTitle.Text = _isNewProfile
                            ? LocalizationService.Instance.GetString("Applications.NewProfile")
                            : $"{LocalizationService.Instance.GetString("Applications.EditProfile")}: {_editingProfile.Name}";
                    }
                });
            }
            catch { }
        }

        public void Initialize(ProfileService profileService, GlowManager glowManager, SettingsService? settingsService = null)
        {
            _profileService = profileService;
            _glowManager = glowManager;
            _settingsService = settingsService;
            _sortCriterion = ApplicationSortCriterion.Alphabetical;
            _sortDescending = false;
            CmbApplicationSort.SelectedIndex = 0;
            RefreshSortLabels();

            if (_settingsService != null)
            {
                ApplyViewMode(_settingsService.Current.ApplicationsViewMode);
            }

            _profileService.ProfilesChanged += (s, e) =>
            {
                _isDirty = true;
                if (IsVisible)
                {
                    RefreshAppCards();
                }
            };
            RefreshAppCards();
        }

        private void RefreshSortLabels()
        {
            if (CmbApplicationSort == null || CmbApplicationSort.Items.Count < 2) return;
            if (CmbApplicationSort.IsDropDownOpen) return;            var alphabetical = (ComboBoxItem)CmbApplicationSort.Items[0];            var recentlyAdded = (ComboBoxItem)CmbApplicationSort.Items[1];            alphabetical.Content = LocalizationService.Instance.GetString(                _sortCriterion == ApplicationSortCriterion.Alphabetical && _sortDescending                    ? "Applications.SortAlphabeticalDescendingShort"                    : "Applications.SortAlphabeticalAscendingShort");            recentlyAdded.Content = LocalizationService.Instance.GetString(                _sortCriterion == ApplicationSortCriterion.RecentlyAdded && !_sortDescending                    ? "Applications.SortFirstAddedShort"                    : "Applications.SortRecentlyAddedShort");            CmbApplicationSort.SelectedItem = _sortCriterion == ApplicationSortCriterion.Alphabetical                ? alphabetical                : recentlyAdded;        }

        private void AttachSortItemHandlers()
        {
            if (CmbApplicationSort?.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) return;
            foreach (var item in CmbApplicationSort.Items)
            {
                if (CmbApplicationSort.ItemContainerGenerator.ContainerFromItem(item) is ComboBoxItem container)
                {
                    container.PreviewMouseLeftButtonDown -= SortItem_PreviewMouseLeftButtonDown;
                    container.PreviewMouseLeftButtonDown += SortItem_PreviewMouseLeftButtonDown;
                }
            }
        }

        private void SortItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not ComboBoxItem item || !ReferenceEquals(item, CmbApplicationSort.SelectedItem) || item.Tag is not string tag) return;
            if (!Enum.TryParse(tag, out ApplicationSortCriterion criterion)) return;
            _sortCriterion = criterion;
            _sortDescending = !_sortDescending;
            _ignoreNextSortSelectionChanged = true;
            RefreshSortLabels();
            RefreshAppCards();
        }

        private void CmbApplicationSort_DropDownOpened(object sender, EventArgs e)        {            ((ComboBoxItem)CmbApplicationSort.Items[0]).Content = LocalizationService.Instance.GetString("Applications.SortAlphabeticalAscending");            ((ComboBoxItem)CmbApplicationSort.Items[1]).Content = LocalizationService.Instance.GetString("Applications.SortRecentlyAdded");        }        private void CmbApplicationSort_DropDownClosed(object sender, EventArgs e)        {            _ignoreNextSortSelectionChanged = false;            RefreshSortLabels();        }        private void CmbApplicationSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbApplicationSort.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
            if (!Enum.TryParse(tag, out ApplicationSortCriterion criterion)) return;
            if (_ignoreNextSortSelectionChanged)
            {
                _ignoreNextSortSelectionChanged = false;
                return;
            }
            if (criterion == _sortCriterion) return;
            _sortCriterion = criterion;
            _sortDescending = criterion == ApplicationSortCriterion.RecentlyAdded;
            RefreshSortLabels();
            RefreshAppCards();
        }

        private void RbViewMode_Click(object sender, RoutedEventArgs e)
        {
            var mode = (RbGridView.IsChecked == true) ? ApplicationViewMode.Grid : ApplicationViewMode.List;
            ApplyViewMode(mode);

            if (_settingsService != null)
            {
                _settingsService.Current.ApplicationsViewMode = mode;
                _settingsService.Save(_settingsService.Current);
            }
        }

        public void ApplyViewMode(ApplicationViewMode mode)
        {
            if (mode == ApplicationViewMode.Grid)
            {
                RbGridView.IsChecked = true;
                RbListView.IsChecked = false;
                ListScrollViewer.Visibility = Visibility.Collapsed;
                GridScrollViewer.Visibility = Visibility.Visible;
            }
            else
            {
                RbListView.IsChecked = true;
                RbGridView.IsChecked = false;
                ListScrollViewer.Visibility = Visibility.Visible;
                GridScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        public void RefreshAppCards()
        {
            if (_profileService == null) return;
            _isDirty = false;
            _isLoadedOnce = true;

            PnlAppCards.Children.Clear();
            PnlAppGridCards.Children.Clear();
            PnlGamingCards.Children.Clear();
            PnlGamingGridCards.Children.Clear();

            var settings = _settingsService?.Current ?? new AppSettings();
            var trackedGames = settings.TrackedGames ?? new List<string>();

            // Build normalized set of game identifiers
            var gameIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in trackedGames)
            {
                string norm = AppSettings.NormalizeGameIdentifier(g);
                if (!string.IsNullOrEmpty(norm)) gameIds.Add(norm);
                string exeName = Path.GetFileName(g);
                if (!string.IsNullOrEmpty(exeName)) gameIds.Add(exeName);
            }

            // 1. Populate Normal Tracked Apps (excluding any app that belongs to Gaming Apps)
            foreach (var profile in ApplicationProfileSorter.Sort(_profileService.Profiles, _sortCriterion, _sortDescending))
            {
                string profId = AppSettings.NormalizeGameIdentifier(profile.AppId);
                string profExe = !string.IsNullOrEmpty(profile.ExecutablePath) ? AppSettings.NormalizeGameIdentifier(profile.ExecutablePath) : string.Empty;

                bool isGaming = string.Equals(profile.Category, "Gaming", StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(profId) && gameIds.Contains(profId)) ||
                                (!string.IsNullOrEmpty(profExe) && gameIds.Contains(profExe));

                if (isGaming)
                {
                    // Belongs in the Gaming Apps section below to prevent duplicate
                    continue;
                }

                // List Mode Card
                var listCard = new AppProfileCard();
                listCard.ConfigureForApplicationsList();
                listCard.SetProfile(profile);
                listCard.EditRequested += (s, p) => OpenEditor(p, false);
                listCard.DuplicateRequested += (s, p) => DuplicateProfile(p);
                listCard.DeleteRequested += (s, p) => DeleteProfile(p);
                listCard.PreviewRequested += (s, p) => _glowManager?.TriggerProfile(p);
                listCard.ToggleChanged += (s, p) => _profileService.AddOrUpdateProfile(p);
                PnlAppCards.Children.Add(listCard);

                // Grid Mode Card
                var gridCard = new AppProfileGridCard();
                gridCard.SetProfile(profile);
                gridCard.EditRequested += (s, p) => OpenEditor(p, false);
                gridCard.DuplicateRequested += (s, p) => DuplicateProfile(p);
                gridCard.DeleteRequested += (s, p) => DeleteProfile(p);
                gridCard.PreviewRequested += (s, p) => _glowManager?.TriggerProfile(p);
                gridCard.ToggleChanged += (s, p) => _profileService.AddOrUpdateProfile(p);
                PnlAppGridCards.Children.Add(gridCard);
            }

            // 2. Populate Gaming Apps Section (strictly trackedGames to match GamingView)
            bool hasGames = trackedGames.Count > 0;
            SectionGamingHeader.Visibility = hasGames ? Visibility.Visible : Visibility.Collapsed;
            SectionGamingGridHeader.Visibility = hasGames ? Visibility.Visible : Visibility.Collapsed;
            PnlGamingCards.Visibility = hasGames ? Visibility.Visible : Visibility.Collapsed;
            PnlGamingGridCards.Visibility = hasGames ? Visibility.Visible : Visibility.Collapsed;

            if (hasGames)
            {
                var renderedGameNorms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var gamePath in trackedGames)
                {
                    if (string.IsNullOrWhiteSpace(gamePath)) continue;

                    string normId = AppSettings.NormalizeGameIdentifier(gamePath);
                    if (renderedGameNorms.Contains(normId)) continue;
                    renderedGameNorms.Add(normId);

                    string gameName = Path.GetFileNameWithoutExtension(gamePath);
                    if (string.IsNullOrWhiteSpace(gameName)) gameName = gamePath;

                    // Get or create representative profile for this tracked game
                    var gameProfile = _profileService.GetProfile(gamePath, gameName);
                    if (gameProfile == null)
                    {
                        GameGlowOverride? ov = null;
                        if (settings.GameOverrides != null) settings.GameOverrides.TryGetValue(normId, out ov);

                        bool customOn = ov != null && ov.CustomGlowEnabled;
                        int dur = customOn && ov?.DurationMs.HasValue == true ? ov.DurationMs.Value : (settings.CommonGamingDurationMs ?? 3000);
                        double intens = customOn && ov?.Intensity.HasValue == true ? ov.Intensity.Value : (settings.CommonGamingIntensity ?? 0.8);
                        double gSize = customOn && ov?.GlowSize.HasValue == true ? ov.GlowSize.Value : (settings.CommonGamingGlowSize ?? 32.0);
                        double cr = customOn && ov?.CornerRadius.HasValue == true ? ov.CornerRadius.Value : (settings.CommonGamingCornerRadius ?? 0.0);
                        string col = customOn && !string.IsNullOrWhiteSpace(ov?.ColorHex) ? ov.ColorHex : settings.DefaultColorHex;

                        gameProfile = new AppProfile
                        {
                            AppId = gamePath,
                            Name = gameName,
                            ExecutablePath = gamePath,
                            Category = "Gaming",
                            DurationMs = dur,
                            Intensity = intens,
                            GlowSize = gSize,
                            CornerRadius = cr,
                            ColorHex = col,
                            Enabled = true,
                            Style = GlowStyle.Pulse
                        };
                    }
                    else
                    {
                        gameProfile.Category = "Gaming";
                    }

                    // Gaming List Card
                    var gameListCard = new AppProfileCard();
                    gameListCard.SetProfile(gameProfile);
                    gameListCard.PreviewRequested += (s, p) => _glowManager?.TriggerGameAnimationTest(gamePath);
                    gameListCard.EditRequested += (s, p) => OpenEditor(p, false);
                    gameListCard.DuplicateRequested += (s, p) => DuplicateProfile(p);
                    gameListCard.DeleteRequested += (s, p) =>
                    {
                        settings.TrackedGames?.RemoveAll(g => AppSettings.NormalizeGameIdentifier(g) == normId);
                        settings.GameOverrides?.Remove(normId);
                        _settingsService?.Save(settings);
                        _profileService.RemoveProfile(p.AppId);
                        RefreshAppCards();
                    };
                    gameListCard.ToggleChanged += (s, p) =>
                    {
                        p.Enabled = !p.Enabled;
                        _profileService.AddOrUpdateProfile(p);
                    };
                    PnlGamingCards.Children.Add(gameListCard);

                    // Gaming Grid Card
                    var gameGridCard = new AppProfileGridCard();
                    gameGridCard.SetProfile(gameProfile);
                    gameGridCard.PreviewRequested += (s, p) => _glowManager?.TriggerGameAnimationTest(gamePath);
                    gameGridCard.EditRequested += (s, p) => OpenEditor(p, false);
                    gameGridCard.DuplicateRequested += (s, p) => DuplicateProfile(p);
                    gameGridCard.DeleteRequested += (s, p) =>
                    {
                        settings.TrackedGames?.RemoveAll(g => AppSettings.NormalizeGameIdentifier(g) == normId);
                        settings.GameOverrides?.Remove(normId);
                        _settingsService?.Save(settings);
                        _profileService.RemoveProfile(p.AppId);
                        RefreshAppCards();
                    };
                    gameGridCard.ToggleChanged += (s, p) =>
                    {
                        p.Enabled = !p.Enabled;
                        _profileService.AddOrUpdateProfile(p);
                    };
                    PnlGamingGridCards.Children.Add(gameGridCard);
                }
            }
        }

        private void DuplicateProfile(AppProfile profile)
        {
            if (_profileService == null) return;
            var cloned = profile.Clone();
            _profileService.AddOrUpdateProfile(cloned);
            ToastNotificationService.Instance.ShowSuccess(
                LocalizationService.Instance.GetString("Applications.ProfileCreatedMsg", cloned.Name),
                LocalizationService.Instance.GetString("Applications.ProfileCreatedTitle"));
        }

        private void OpenEditor(AppProfile profile, bool isNew)
        {
            _editingProfile = new AppProfile
            {
                AppId = profile.AppId,
                Name = profile.Name,
                ExecutablePath = profile.ExecutablePath,
                Enabled = profile.Enabled,
                ColorHex = profile.ColorHex,
                DurationMs = profile.DurationMs,
                Intensity = profile.Intensity,
                Thickness = profile.Thickness,
                GlowSize = profile.GlowSize,
                CornerRadius = profile.CornerRadius,
                Style = profile.Style,
                Priority = profile.Priority,
                Category = profile.Category,
                IsCustom = profile.IsCustom
            };
            _isNewProfile = isNew;

            TxtEditorTitle.Text = isNew
                ? LocalizationService.Instance.GetString("Applications.NewProfile")
                : $"{LocalizationService.Instance.GetString("Applications.EditProfile")}: {profile.Name}";
            TxtEditAppName.Text = _editingProfile.Name;
            TxtEditAppId.Text = _editingProfile.AppId;

            SldDuration.Value = _editingProfile.DurationMs;
            SldIntensity.Value = Math.Round(_editingProfile.Intensity * 100);
            SldGlowSize.Value = Math.Clamp(Math.Round(_editingProfile.GlowSize), 0.0, 256.0);
            SldAppCorner.Value = Math.Clamp(Math.Round(_editingProfile.CornerRadius), 0.0, 48.0);
            TxtAppCornerVal.Text = $"{SldAppCorner.Value:0} px";

            EditColorPicker.SelectedColorHex = _editingProfile.ColorHex;

            foreach (ComboBoxItem item in CmbEditStyle.Items)
            {
                if (string.Equals(item.Tag?.ToString(), _editingProfile.Style.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    CmbEditStyle.SelectedItem = item;
                    break;
                }
            }

            foreach (ComboBoxItem item in CmbEditPriority.Items)
            {
                if (string.Equals(item.Tag?.ToString(), _editingProfile.Priority.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    CmbEditPriority.SelectedItem = item;
                    break;
                }
            }

            var currentSettingsForEdit = _settingsService?.Current;
            string normEditId = AppSettings.NormalizeGameIdentifier(!string.IsNullOrEmpty(_editingProfile.ExecutablePath) ? _editingProfile.ExecutablePath : _editingProfile.AppId);
            bool isAlreadyGame = currentSettingsForEdit?.TrackedGames != null && currentSettingsForEdit.TrackedGames.Any(g => AppSettings.NormalizeGameIdentifier(g) == normEditId);
            string currentCat = (isAlreadyGame || string.Equals(_editingProfile.Category, "Gaming", StringComparison.OrdinalIgnoreCase)) ? "Gaming" : "General";
            foreach (ComboBoxItem item in CmbEditCategory.Items)
            {
                if (string.Equals(item.Tag?.ToString(), currentCat, StringComparison.OrdinalIgnoreCase))
                {
                    CmbEditCategory.SelectedItem = item;
                    break;
                }
            }

            UpdateEditorPreview();
            PnlEditProfile.Visibility = Visibility.Visible;
        }

        private void UpdateEditorPreview()
        {
            if (_editingProfile == null) return;

            double sec = SldDuration.Value / 1000.0;
            TxtDurationVal.Text = $"{sec:0.#} s";
            TxtIntensityVal.Text = $"{Math.Round(SldIntensity.Value):0}%";
            TxtGlowSizeVal.Text = $"{Math.Round(SldGlowSize.Value):0} px";
            if (TxtAppCornerVal != null)
            {
                TxtAppCornerVal.Text = $"{Math.Round(SldAppCorner.Value):0} px";
            }

            _editingProfile.ColorHex = EditColorPicker.SelectedColorHex;
            _editingProfile.GlowSize = Math.Clamp(Math.Round(SldGlowSize.Value), 0.0, 256.0);
            _editingProfile.CornerRadius = Math.Clamp(Math.Round(SldAppCorner.Value), 0.0, 48.0);
            _editingProfile.Intensity = Math.Round(SldIntensity.Value) / 100.0;
            _editingProfile.DurationMs = (int)SldDuration.Value;

            if (CmbEditStyle.SelectedItem is ComboBoxItem styleItem && Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                _editingProfile.Style = style;
            }

            if (CmbEditPriority.SelectedItem is ComboBoxItem prioItem && Enum.TryParse<NotificationPriority>(prioItem.Tag?.ToString(), true, out var prio))
            {
                _editingProfile.Priority = prio;
            }

            EditEdgePreview.UpdatePreview(_editingProfile);
        }

        private void BtnAddApplication_Click(object sender, RoutedEventArgs e)
        {
            var newProfile = new AppProfile
            {
                AppId = "custom_app",
                Name = "New Application",
                Enabled = true,
                ColorHex = "#FFFFFF",
                DurationMs = 4000,
                Intensity = 0.8,
                Thickness = 4,
                GlowSize = 32,
                CornerRadius = 0.0,
                Style = GlowStyle.Pulse
            };
            OpenEditor(newProfile, true);
        }


        private void BtnBrowseAppId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Executable File",
                    Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                bool? result = dialog.ShowDialog();
                if (result == true)
                {
                    string selectedPath = dialog.FileName;
                    if (ExecutablePathHelper.TryApplySelectedExecutable(TxtEditAppId.Text, selectedPath, out string newAppId, out string? error))
                    {
                        TxtEditAppId.Text = newAppId;
                        if (_editingProfile != null)
                        {
                            _editingProfile.ExecutablePath = selectedPath;
                        }

                        // Auto-fill Application Name if empty or default placeholder
                        if (string.IsNullOrWhiteSpace(TxtEditAppName.Text) || TxtEditAppName.Text == "New Application")
                        {
                            try
                            {
                                string friendlyName = System.IO.Path.GetFileNameWithoutExtension(selectedPath);
                                if (!string.IsNullOrWhiteSpace(friendlyName))
                                {
                                    TxtEditAppName.Text = char.ToUpperInvariant(friendlyName[0]) + (friendlyName.Length > 1 ? friendlyName.Substring(1) : "");
                                }
                            }
                            catch { }
                        }
                    }
                    else if (!string.IsNullOrEmpty(error))
                    {
                        ToastNotificationService.Instance.ShowWarning(error, LocalizationService.Instance.GetString("Applications.InvalidFileTitle"));
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to show OpenFileDialog for application selection", ex);
                ToastNotificationService.Instance.ShowError(
                    LocalizationService.Instance.GetString("Applications.InvalidFileMsg"),
                    LocalizationService.Instance.GetString("Applications.InvalidFileTitle"));
            }
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_editingProfile == null || _profileService == null) return;

            _editingProfile.Name = TxtEditAppName.Text.Trim();
            _editingProfile.AppId = ExecutablePathHelper.ResolveAppIdentifier(TxtEditAppId.Text.Trim());

            if (!string.IsNullOrEmpty(_editingProfile.ExecutablePath) &&
                !string.Equals(System.IO.Path.GetFileName(_editingProfile.ExecutablePath), _editingProfile.AppId, StringComparison.OrdinalIgnoreCase))
            {
                _editingProfile.ExecutablePath = null;
            }

            if (string.IsNullOrEmpty(_editingProfile.Name) || string.IsNullOrEmpty(_editingProfile.AppId))
            {
                ToastNotificationService.Instance.ShowWarning(
                    LocalizationService.Instance.GetString("Applications.ValidationMsg"),
                    LocalizationService.Instance.GetString("Applications.ValidationTitle"));
                return;
            }

            _editingProfile.DurationMs = (int)SldDuration.Value;
            _editingProfile.Intensity = Math.Round(SldIntensity.Value) / 100.0;
            _editingProfile.GlowSize = Math.Clamp(Math.Round(SldGlowSize.Value), 0.0, 256.0);
            _editingProfile.CornerRadius = Math.Clamp(Math.Round(SldAppCorner.Value), 0.0, 48.0);
            _editingProfile.ColorHex = EditColorPicker.SelectedColorHex;

            if (CmbEditStyle.SelectedItem is ComboBoxItem styleItem && Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                _editingProfile.Style = style;
            }

            if (CmbEditPriority.SelectedItem is ComboBoxItem prioItem && Enum.TryParse<NotificationPriority>(prioItem.Tag?.ToString(), true, out var prio))
            {
                _editingProfile.Priority = prio;
            }

            string selectedCategory = "General";
            if (CmbEditCategory.SelectedItem is ComboBoxItem catItem && catItem.Tag != null)
            {
                selectedCategory = catItem.Tag.ToString() ?? "General";
            }
            _editingProfile.Category = selectedCategory;

            var currentSettings = _settingsService?.Current;
            if (currentSettings != null)
            {
                string normId = AppSettings.NormalizeGameIdentifier(!string.IsNullOrEmpty(_editingProfile.ExecutablePath) ? _editingProfile.ExecutablePath : _editingProfile.AppId);

                if (string.Equals(selectedCategory, "Gaming", StringComparison.OrdinalIgnoreCase))
                {
                    string gameTarget = !string.IsNullOrEmpty(_editingProfile.ExecutablePath) ? _editingProfile.ExecutablePath : _editingProfile.AppId;
                    currentSettings.TrackedGames ??= new List<string>();
                    if (!currentSettings.TrackedGames.Any(g => AppSettings.NormalizeGameIdentifier(g) == normId))
                    {
                        currentSettings.TrackedGames.Add(gameTarget);
                    }
                    currentSettings.GameOverrides ??= new Dictionary<string, GameGlowOverride>(StringComparer.OrdinalIgnoreCase);
                    if (!currentSettings.GameOverrides.TryGetValue(normId, out var existingOv))
                    {
                        existingOv = new GameGlowOverride();
                        currentSettings.GameOverrides[normId] = existingOv;
                    }
                    existingOv.CustomGlowEnabled = true;
                    existingOv.DurationMs = _editingProfile.DurationMs;
                    existingOv.Intensity = _editingProfile.Intensity;
                    existingOv.GlowSize = _editingProfile.GlowSize;
                    existingOv.CornerRadius = _editingProfile.CornerRadius;
                    existingOv.ColorHex = _editingProfile.ColorHex;
                    _settingsService?.Save(currentSettings);
                }
                else
                {
                    if (currentSettings.TrackedGames != null && currentSettings.TrackedGames.RemoveAll(g => AppSettings.NormalizeGameIdentifier(g) == normId) > 0)
                    {
                        currentSettings.GameOverrides?.Remove(normId);
                        _settingsService?.Save(currentSettings);
                    }
                }
            }

            if (_isNewProfile)
            {
                _editingProfile.IsCustom = true;
            }

            _profileService.AddOrUpdateProfile(_editingProfile);
            RefreshAppCards();
            PnlEditProfile.Visibility = Visibility.Collapsed;
        }

        private async void BtnScanApplications_Click(object sender, RoutedEventArgs e)
        {
            if (_profileService == null) return;
            BtnScanApplications.IsEnabled = false;
            try
            {
                int newlyFound = await _profileService.ScanAndSyncInstalledAppsAsync();
                RefreshAppCards();
                string msg = LocalizationService.Instance.GetString("Applications.ScanComplete", _profileService.Profiles.Count);
                ToastNotificationService.Instance.ShowSuccess(msg, LocalizationService.Instance.GetString("Applications.ScanApps"));
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Application scan error", ex);
                ToastNotificationService.Instance.ShowError("Scanning failed. Existing profiles were preserved.", "Scan Error");
            }
            finally
            {
                BtnScanApplications.IsEnabled = true;
            }
        }

        private void DeleteProfile(AppProfile profile)
        {
            string confirmTitle = LocalizationService.Instance.GetString("Applications.ConfirmDeleteTitle");
            string confirmMsg = LocalizationService.Instance.GetString("Applications.ConfirmDeleteMsg", profile.Name);
            string deleteBtn = LocalizationService.Instance.GetString("Applications.Delete");
            string cancelBtn = LocalizationService.Instance.GetString("Common.Cancel");

            bool confirmed = NotiGlow.UI.Dialogs.ConfirmationDialogWindow.ShowConfirm(
                string.IsNullOrEmpty(confirmTitle) ? "Delete Profile" : confirmTitle,
                string.IsNullOrEmpty(confirmMsg) ? $"Are you sure you want to delete profile '{profile.Name}'?" : confirmMsg,
                confirmText: string.IsNullOrEmpty(deleteBtn) ? "Delete" : deleteBtn,
                cancelText: string.IsNullOrEmpty(cancelBtn) ? "Cancel" : cancelBtn,
                isDestructive: true,
                owner: Window.GetWindow(this));

            if (confirmed)
            {
                _profileService?.RemoveProfile(profile.AppId);
            }
        }

        private void BtnCloseEditor_Click(object sender, RoutedEventArgs e) => PnlEditProfile.Visibility = Visibility.Collapsed;
        private void SldDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
        private void SldIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
        private void SldGlowSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
        private void SldAppCorner_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
        private void EditColorPicker_ColorChanged(object? sender, string e) => UpdateEditorPreview();
        private void EditControl_Changed(object sender, SelectionChangedEventArgs e) => UpdateEditorPreview();

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null) return;

            if (PnlEditProfile.Visibility == Visibility.Visible && _editingProfile != null)
            {
                UpdateEditorPreview();
                _glowManager.TriggerProfile(_editingProfile);
                return;
            }

            var profile = _profileService?.Profiles.FirstOrDefault() ?? new AppProfile
            {
                AppId = "TestApp",
                Name = "NotiGlow Test",
                ColorHex = "#FFFFFF",
                DurationMs = 4000,
                Intensity = 0.8,
                Style = GlowStyle.Pulse,
                Thickness = 4,
                GlowSize = 32,
                CornerRadius = 0.0
            };
            _glowManager.TriggerProfile(profile);
        }
    }
}
