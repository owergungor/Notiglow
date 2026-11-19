using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI.Controls;

namespace NotiGlow.UI.Views
{
    public sealed partial class ApplicationsView : UserControl
    {
        private ProfileService? _profileService;
        private GlowManager? _glowManager;
        private SettingsService? _settingsService;
        private ApplicationSortCriterion _sortCriterion = ApplicationSortCriterion.Alphabetical;
        private bool _sortDescending = false;
        private bool _isDirty = true;
        private bool _isLoadedOnce = false;
        private string? _highlightedIdentifier;

        public bool IsDirty => _isDirty;

        public event EventHandler<CategoryNavigationEventArgs>? NavigateToCategoryRequested;

        public ApplicationsView()
        {
            this.InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(ListScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);
                UpdateLocalizedTexts();
                RefreshSortLabels();
            };
        }

        private void UpdateLocalizedTexts()
        {
            TxtAppTitle.Text = LocalizationService.Instance.GetString("Applications.Title");
            TxtAppSubtitle.Text = LocalizationService.Instance.GetString("Applications.Subtitle");
            TxtBtnScan.Text = LocalizationService.Instance.GetString("Applications.ScanApps");
            TxtBtnAdd.Text = LocalizationService.Instance.GetString("Applications.AddApp");
        }

        public void EnsureLoaded()
        {
            if (!_isDirty && _isLoadedOnce) return;
            RefreshAppCards();
        }

        private void HeaderGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (HeaderToolbarPanel == null || HeaderTitlePanel == null) return;

            HeaderTitlePanel.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            HeaderToolbarPanel.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));

            double titleDesired = HeaderTitlePanel.DesiredSize.Width;
            double toolbarDesired = HeaderToolbarPanel.DesiredSize.Width;
            double minSpacing = 28.0;

            if (e.NewSize.Width < (titleDesired + toolbarDesired + minSpacing))
            {
                Grid.SetRow(HeaderToolbarPanel, 1);
                Grid.SetColumn(HeaderToolbarPanel, 0);
                Grid.SetColumnSpan(HeaderToolbarPanel, 2);
                HeaderToolbarPanel.HorizontalAlignment = HorizontalAlignment.Left;
                HeaderToolbarPanel.Margin = new Thickness(0, 12, 0, 0);
            }
            else
            {
                Grid.SetRow(HeaderToolbarPanel, 0);
                Grid.SetColumn(HeaderToolbarPanel, 1);
                Grid.SetColumnSpan(HeaderToolbarPanel, 1);
                HeaderToolbarPanel.HorizontalAlignment = HorizontalAlignment.Right;
                HeaderToolbarPanel.Margin = new Thickness(0);
            }
        }

        private void OnLanguageChanged(object? sender, string lang)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLocalizedTexts();
                RefreshAppCards();
                RefreshSortLabels();
                if (PnlEditProfile.Visibility == Visibility.Visible)
                {
                    PnlEditProfile.RefreshLocalizedTitle();
                }
            });
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

            // Backward compatibility: migrate legacy Grid view preference to List-only view
            if (_settingsService != null && _settingsService.Current.ApplicationsViewMode != ApplicationViewMode.List)
            {
                AppCategoryHelper.NormalizeApplicationsViewMode(_settingsService.Current);
                _settingsService.Save(_settingsService.Current);
            }

            _profileService.ProfilesChanged += (s, e) =>
            {
                _isDirty = true;
                if (Visibility == Visibility.Visible)
                {
                    DispatcherQueue.TryEnqueue(() => RefreshAppCards());
                }
            };
            RefreshAppCards();
        }

        private void RefreshSortLabels()
        {
            if (CmbApplicationSort == null || CmbApplicationSort.Items.Count < 2) return;
            var alphabetical = (ComboBoxItem)CmbApplicationSort.Items[0];
            var recentlyAdded = (ComboBoxItem)CmbApplicationSort.Items[1];
            alphabetical.Content = LocalizationService.Instance.GetString(
                _sortCriterion == ApplicationSortCriterion.Alphabetical && _sortDescending
                    ? "Applications.SortAlphabeticalDescendingShort"
                    : "Applications.SortAlphabeticalAscendingShort");
            recentlyAdded.Content = LocalizationService.Instance.GetString(
                _sortCriterion == ApplicationSortCriterion.RecentlyAdded && !_sortDescending
                    ? "Applications.SortFirstAddedShort"
                    : "Applications.SortRecentlyAddedShort");
        }

        private void CmbApplicationSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbApplicationSort.SelectedItem is not ComboBoxItem item || item.Tag is not string tag) return;
            if (!Enum.TryParse(tag, out ApplicationSortCriterion criterion)) return;

            if (criterion == _sortCriterion)
            {
                _sortDescending = !_sortDescending;
            }
            else
            {
                _sortCriterion = criterion;
                _sortDescending = criterion == ApplicationSortCriterion.RecentlyAdded;
            }
            RefreshSortLabels();
            RefreshAppCards();
        }

        public void RefreshAppCards()
        {
            if (_profileService == null) return;
            _isDirty = false;
            _isLoadedOnce = true;

            PnlAppCards.Children.Clear();

            var settings = _settingsService?.Current ?? new AppSettings();
            var trackedGames = settings.TrackedGames ?? new List<string>();

            foreach (var profile in ApplicationProfileSorter.Sort(_profileService.Profiles, _sortCriterion, _sortDescending))
            {
                // Strictly exclude Gaming profiles — they belong exclusively in the Gaming tab
                if (AppCategoryHelper.IsGamingProfile(profile, trackedGames))
                {
                    continue;
                }

                var listCard = new AppProfileCard();
                listCard.ConfigureForApplicationsList();
                listCard.SetProfile(profile);
                listCard.EditRequested += (s, p) => OpenEditor(p, false);
                listCard.DuplicateRequested += (s, p) => DuplicateProfile(p);
                listCard.DeleteRequested += (s, p) => DeleteProfile(p);
                listCard.PreviewRequested += (s, p) => _glowManager?.TriggerProfile(p);
                listCard.ToggleChanged += (s, p) => _profileService.AddOrUpdateProfile(p);

                if (!string.IsNullOrEmpty(_highlightedIdentifier) &&
                    AppCategoryHelper.MatchesIdentifier(profile, _highlightedIdentifier))
                {
                    listCard.SetHighlighted(true);
                }

                PnlAppCards.Children.Add(listCard);
            }
        }

        public void RefreshAndHighlightProfile(string identifier)
        {
            _highlightedIdentifier = identifier;
            PnlEditProfile.Visibility = Visibility.Collapsed;
            RefreshAppCards();

            DispatcherQueue.TryEnqueue(async () =>
            {
                AppProfileCard? matchedCard = null;
                foreach (var child in PnlAppCards.Children)
                {
                    if (child is AppProfileCard card && card.Profile != null &&
                        AppCategoryHelper.MatchesIdentifier(card.Profile, identifier))
                    {
                        matchedCard = card;
                        break;
                    }
                }

                if (matchedCard != null)
                {
                    matchedCard.SetHighlighted(true);
                    matchedCard.StartBringIntoView();

                    if (!NotiGlow.UI.Animations.MotionPolicy.IsReduceMotion)
                    {
                        await Task.Delay(2600);
                        if (_highlightedIdentifier == identifier)
                        {
                            _highlightedIdentifier = null;
                            matchedCard.SetHighlighted(false);
                        }
                    }
                }
            });
        }

        private void DuplicateProfile(AppProfile source)
        {
            if (_profileService == null) return;
            var duplicate = new AppProfile
            {
                AppId = $"{source.AppId}_copy_{DateTime.Now:mmss}",
                Name = $"{source.Name} (Copy)",
                Enabled = source.Enabled,
                ColorHex = source.ColorHex,
                DurationMs = source.DurationMs,
                Intensity = source.Intensity,
                Thickness = source.Thickness,
                GlowSize = source.GlowSize,
                CornerRadius = source.CornerRadius,
                Style = source.Style,
                Priority = source.Priority,
                Category = source.Category,
                ExecutablePath = source.ExecutablePath,
                IsCustom = true
            };
            _profileService.AddOrUpdateProfile(duplicate);
            RefreshAppCards();
        }

        public void OpenEditor(AppProfile profile, bool isNew)
        {
            PnlEditProfile.OpenForProfile(profile, isNew, isGameMode: false, customGlowEnabled: true);
            PnlEditProfile.Visibility = Visibility.Visible;
        }

        private void BtnAddApplication_Click(object sender, RoutedEventArgs e)
        {
            var newProfile = new AppProfile
            {
                AppId = "custom_app",
                Name = "New Application",
                Category = AppCategoryHelper.CategoryGeneral,
                Enabled = true,
                ColorHex = _settingsService?.Current.DefaultColorHex ?? "#FFFFFF",
                DurationMs = _settingsService?.Current.DefaultDurationMs ?? 4000,
                Intensity = _settingsService?.Current.DefaultIntensity ?? 0.8,
                Thickness = _settingsService?.Current.DefaultThickness ?? 4,
                GlowSize = _settingsService?.Current.DefaultGlowSize ?? 32,
                CornerRadius = _settingsService?.Current.CornerRadius ?? 0.0,
                Style = _settingsService?.Current.DefaultStyle ?? GlowStyle.Pulse
            };
            OpenEditor(newProfile, true);
        }

        private void PnlEditProfile_SaveRequested(object? sender, ProfileEditorSaveEventArgs e)
        {
            if (_profileService == null) return;

            AppCategoryHelper.ApplyCategorySync(
                e.Profile,
                _profileService,
                _settingsService,
                e.CustomGlowEnabled,
                e.OriginalIdentifier);

            PnlEditProfile.Visibility = Visibility.Collapsed;
            RefreshAppCards();

            string targetId = AppCategoryHelper.GetPrimaryIdentifier(e.Profile);
            if (NavigateToCategoryRequested != null)
            {
                NavigateToCategoryRequested.Invoke(this, new CategoryNavigationEventArgs(e.Profile.Category, targetId));
            }
            else if (!AppCategoryHelper.IsGamingCategory(e.Profile.Category))
            {
                RefreshAndHighlightProfile(targetId);
            }
        }

        private void PnlEditProfile_CancelRequested(object? sender, EventArgs e)
        {
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
                isDestructive: true);

            if (confirmed)
            {
                _profileService?.RemoveProfile(profile.AppId);
                RefreshAppCards();
            }
        }
    }
}
