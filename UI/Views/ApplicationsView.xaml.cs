using System;
using System.Windows;
using System.Windows.Controls;
using GlowBorder.Models;
using GlowBorder.Services;
using GlowBorder.UI.Controls;
using UserControl = System.Windows.Controls.UserControl;
using MessageBox = System.Windows.MessageBox;

namespace GlowBorder.UI.Views
{
    public partial class ApplicationsView : UserControl
    {
        private ProfileService? _profileService;
        private GlowManager? _glowManager;
        private AppProfile? _editingProfile;
        private bool _isNewProfile = false;

        public ApplicationsView()
        {
            InitializeComponent();
        }

        public void Initialize(ProfileService profileService, GlowManager glowManager)
        {
            _profileService = profileService;
            _glowManager = glowManager;

            _profileService.ProfilesChanged += (s, e) => RefreshAppCards();
            RefreshAppCards();
        }

        public void RefreshAppCards()
        {
            if (_profileService == null) return;

            PnlAppCards.Children.Clear();
            foreach (var profile in _profileService.Profiles)
            {
                var card = new AppProfileCard();
                card.SetProfile(profile);

                card.EditRequested += (s, p) => OpenEditor(p, false);
                card.DuplicateRequested += (s, p) => DuplicateProfile(p);
                card.DeleteRequested += (s, p) => DeleteProfile(p);
                card.PreviewRequested += (s, p) => _glowManager?.TriggerProfile(p);
                card.ToggleChanged += (s, p) => _profileService.AddOrUpdateProfile(p);

                PnlAppCards.Children.Add(card);
            }
        }

        private void DuplicateProfile(AppProfile profile)
        {
            if (_profileService == null) return;
            var cloned = profile.Clone();
            _profileService.AddOrUpdateProfile(cloned);
            MessageBox.Show($"Profile '{cloned.Name}' created!", "Profile Duplicated", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenEditor(AppProfile profile, bool isNew)
        {
            _editingProfile = new AppProfile
            {
                AppId = profile.AppId,
                Name = profile.Name,
                Enabled = profile.Enabled,
                ColorHex = profile.ColorHex,
                DurationMs = profile.DurationMs,
                Intensity = profile.Intensity,
                Thickness = profile.Thickness,
                GlowSize = profile.GlowSize,
                Style = profile.Style,
                Priority = profile.Priority
            };
            _isNewProfile = isNew;

            TxtEditorTitle.Text = isNew ? "Add New Application Profile" : $"Edit Profile: {profile.Name}";
            TxtEditAppName.Text = _editingProfile.Name;
            TxtEditAppId.Text = _editingProfile.AppId;

            SldDuration.Value = _editingProfile.DurationMs;
            SldIntensity.Value = Math.Round(_editingProfile.Intensity * 100);
            SldThickness.Value = Math.Round(_editingProfile.Thickness);
            SldGlowSize.Value = Math.Round(_editingProfile.GlowSize);

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

            UpdateEditorPreview();
            PnlEditProfile.Visibility = Visibility.Visible;
        }

        private void UpdateEditorPreview()
        {
            if (_editingProfile == null) return;

            double sec = SldDuration.Value / 1000.0;
            TxtDurationVal.Text = (sec % 1 == 0) ? $"{sec:0}s ({SldDuration.Value} ms)" : $"{sec:0.0}s ({SldDuration.Value} ms)";
            TxtIntensityVal.Text = $"{Math.Round(SldIntensity.Value):0}%";
            TxtThicknessVal.Text = $"{Math.Round(SldThickness.Value):0} px";
            TxtGlowSizeVal.Text = $"{Math.Round(SldGlowSize.Value):0} px";

            _editingProfile.ColorHex = EditColorPicker.SelectedColorHex;
            _editingProfile.Thickness = Math.Round(SldThickness.Value);
            _editingProfile.GlowSize = Math.Round(SldGlowSize.Value);
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
                ColorHex = "#5865F2",
                DurationMs = 4000,
                Intensity = 0.8,
                Thickness = 4,
                GlowSize = 30,
                Style = GlowStyle.Pulse
            };
            OpenEditor(newProfile, true);
        }

        private void BtnTestNotification_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _editingProfile == null) return;
            UpdateEditorPreview();
            _glowManager.TriggerProfile(_editingProfile);
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_editingProfile == null || _profileService == null) return;

            _editingProfile.Name = TxtEditAppName.Text.Trim();
            _editingProfile.AppId = TxtEditAppId.Text.Trim();

            if (string.IsNullOrEmpty(_editingProfile.Name) || string.IsNullOrEmpty(_editingProfile.AppId))
            {
                MessageBox.Show("Please enter valid Application Name and App Identifier.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _editingProfile.DurationMs = (int)SldDuration.Value;
            _editingProfile.Intensity = Math.Round(SldIntensity.Value) / 100.0;
            _editingProfile.Thickness = Math.Round(SldThickness.Value);
            _editingProfile.GlowSize = Math.Round(SldGlowSize.Value);
            _editingProfile.ColorHex = EditColorPicker.SelectedColorHex;

            if (CmbEditStyle.SelectedItem is ComboBoxItem styleItem && Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                _editingProfile.Style = style;
            }

            if (CmbEditPriority.SelectedItem is ComboBoxItem prioItem && Enum.TryParse<NotificationPriority>(prioItem.Tag?.ToString(), true, out var prio))
            {
                _editingProfile.Priority = prio;
            }

            _profileService.AddOrUpdateProfile(_editingProfile);
            PnlEditProfile.Visibility = Visibility.Collapsed;
        }

        private void DeleteProfile(AppProfile profile)
        {
            var result = MessageBox.Show($"Are you sure you want to remove profile for {profile.Name}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _profileService?.RemoveProfile(profile.AppId);
            }
        }

        private void BtnCloseEditor_Click(object sender, RoutedEventArgs e) => PnlEditProfile.Visibility = Visibility.Collapsed;
        private void SldDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
        private void SldIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
        private void SldThickness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
        private void SldGlowSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateEditorPreview();
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
                ColorHex = "#5865F2",
                DurationMs = 4000,
                Intensity = 0.8,
                Style = GlowStyle.Pulse,
                Thickness = 4,
                GlowSize = 30
            };
            _glowManager.TriggerProfile(profile);
        }
    }
}
