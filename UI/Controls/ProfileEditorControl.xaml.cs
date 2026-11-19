using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.UI.Controls
{
    public class ProfileEditorSaveEventArgs : EventArgs
    {
        public AppProfile Profile { get; }
        public bool CustomGlowEnabled { get; }
        public bool IsNew { get; }
        public string? OriginalIdentifier { get; }

        public ProfileEditorSaveEventArgs(AppProfile profile, bool customGlowEnabled, bool isNew, string? originalIdentifier)
        {
            Profile = profile;
            CustomGlowEnabled = customGlowEnabled;
            IsNew = isNew;
            OriginalIdentifier = originalIdentifier;
        }
    }

    public sealed partial class ProfileEditorControl : UserControl
    {
        private AppProfile? _editingProfile;
        private bool _isNewProfile;
        private bool _isGameMode;
        private string? _originalIdentifier;
        private bool _isLoading;

        public event EventHandler<ProfileEditorSaveEventArgs>? SaveRequested;
        public event EventHandler? CancelRequested;

        public ProfileEditorControl()
        {
            this.InitializeComponent();
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(EditorScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);
            };
        }

        public void OpenForProfile(AppProfile profile, bool isNew, bool isGameMode = false, bool customGlowEnabled = true)
        {
            _isLoading = true;
            _isNewProfile = isNew;
            _isGameMode = isGameMode;
            _originalIdentifier = isNew ? null : AppCategoryHelper.GetPrimaryIdentifier(profile);

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
                CornerRadius = profile.CornerRadius,
                Style = profile.Style,
                Priority = profile.Priority,
                Category = isGameMode ? AppCategoryHelper.CategoryGaming : (string.IsNullOrWhiteSpace(profile.Category) ? AppCategoryHelper.CategoryGeneral : profile.Category),
                ExecutablePath = profile.ExecutablePath,
                IsCustom = profile.IsCustom
            };

            if (isGameMode)
            {
                string gameTitle = string.IsNullOrWhiteSpace(profile.Name) ? profile.AppId : profile.Name;
                TxtEditorTitle.Text = $"{LocalizationService.Instance.GetString("Applications.EditProfile")}: {gameTitle}";
                TxtEditorSubtitle.Text = LocalizationService.Instance.GetString("Gaming.PerGameOverrides");
                PnlGameCustomToggle.Visibility = Visibility.Visible;
                ToggleCustomGameGlow.IsOn = customGlowEnabled;
            }
            else
            {
                TxtEditorTitle.Text = isNew
                    ? LocalizationService.Instance.GetString("Applications.NewProfile")
                    : $"{LocalizationService.Instance.GetString("Applications.EditProfile")}: {profile.Name}";
                TxtEditorSubtitle.Text = LocalizationService.Instance.GetString("Applications.Subtitle");
                PnlGameCustomToggle.Visibility = Visibility.Collapsed;
            }

            TxtEditAppName.Text = _editingProfile.Name;
            TxtEditAppId.Text = !string.IsNullOrEmpty(_editingProfile.ExecutablePath)
                ? _editingProfile.ExecutablePath
                : _editingProfile.AppId;
            TxtEditAppId.IsEnabled = true;

            SldDuration.Value = Math.Clamp(_editingProfile.DurationMs, SldDuration.Minimum, SldDuration.Maximum);
            SldIntensity.Value = Math.Clamp(_editingProfile.Intensity * 100.0, SldIntensity.Minimum, SldIntensity.Maximum);
            SldGlowSize.Value = Math.Clamp(_editingProfile.GlowSize, SldGlowSize.Minimum, SldGlowSize.Maximum);
            SldAppCorner.Value = Math.Clamp(_editingProfile.CornerRadius, SldAppCorner.Minimum, SldAppCorner.Maximum);
            EditColorPicker.SelectedColorHex = _editingProfile.ColorHex;

            SelectComboByTag(CmbEditStyle, _editingProfile.Style.ToString());
            SelectComboByTag(CmbEditPriority, _editingProfile.Priority.ToString());
            SelectComboByTag(CmbEditCategory, AppCategoryHelper.IsGamingCategory(_editingProfile.Category)
                ? AppCategoryHelper.CategoryGaming
                : AppCategoryHelper.CategoryGeneral);

            BtnSaveProfile.Content = LocalizationService.Instance.GetString("Applications.SaveProfile");
            BtnCancelProfile.Content = LocalizationService.Instance.GetString("Common.Cancel");

            _isLoading = false;
            UpdateEditorPreview();
            EditorScrollViewer?.ChangeView(null, 0, null, disableAnimation: true);
        }

        public void RefreshLocalizedTitle()
        {
            if (_editingProfile == null) return;
            if (_isGameMode)
            {
                TxtEditorTitle.Text = $"{LocalizationService.Instance.GetString("Applications.EditProfile")}: {_editingProfile.Name}";
            }
            else
            {
                TxtEditorTitle.Text = _isNewProfile
                    ? LocalizationService.Instance.GetString("Applications.NewProfile")
                    : $"{LocalizationService.Instance.GetString("Applications.EditProfile")}: {_editingProfile.Name}";
            }
            BtnSaveProfile.Content = LocalizationService.Instance.GetString("Applications.SaveProfile");
            BtnCancelProfile.Content = LocalizationService.Instance.GetString("Common.Cancel");
        }

        private static void SelectComboByTag(ComboBox combo, string targetTag)
        {
            foreach (var obj in combo.Items)
            {
                if (obj is ComboBoxItem item &&
                    string.Equals(item.Tag?.ToString(), targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }

        private void UpdateEditorPreview()
        {
            if (_isLoading || _editingProfile == null) return;

            double sec = SldDuration.Value / 1000.0;
            TxtDurationVal.Text = $"{sec:0.#} s";
            TxtIntensityVal.Text = $"{Math.Round(SldIntensity.Value):0}%";
            TxtGlowSizeVal.Text = $"{Math.Round(SldGlowSize.Value):0} px";
            TxtAppCornerVal.Text = $"{Math.Round(SldAppCorner.Value):0} px";

            _editingProfile.ColorHex = EditColorPicker.SelectedColorHex;
            _editingProfile.GlowSize = Math.Clamp(Math.Round(SldGlowSize.Value), 0.0, 256.0);
            _editingProfile.CornerRadius = Math.Clamp(Math.Round(SldAppCorner.Value), 0.0, 48.0);
            _editingProfile.Intensity = Math.Round(SldIntensity.Value) / 100.0;
            _editingProfile.DurationMs = (int)SldDuration.Value;

            if (CmbEditStyle.SelectedItem is ComboBoxItem styleItem &&
                Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                _editingProfile.Style = style;
            }

            if (CmbEditPriority.SelectedItem is ComboBoxItem prioItem &&
                Enum.TryParse<NotificationPriority>(prioItem.Tag?.ToString(), true, out var prio))
            {
                _editingProfile.Priority = prio;
            }

            if (CmbEditCategory.SelectedItem is ComboBoxItem catItem && catItem.Tag != null)
            {
                string cat = catItem.Tag.ToString() ?? AppCategoryHelper.CategoryGeneral;
                _editingProfile.Category = cat;
                PnlGameCustomToggle.Visibility = AppCategoryHelper.IsGamingCategory(cat)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            EditEdgePreview.UpdatePreview(_editingProfile);
        }

        private void BtnBrowseAppId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dialog = new System.Windows.Forms.OpenFileDialog
                {
                    Title = "Select Executable File",
                    Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                    CheckFileExists = true,
                    Multiselect = false
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.FileName;
                    if (ExecutablePathHelper.TryApplySelectedExecutable(TxtEditAppId.Text, selectedPath, out string newAppId, out string? error))
                    {
                        TxtEditAppId.Text = selectedPath;
                        if (_editingProfile != null)
                        {
                            _editingProfile.ExecutablePath = selectedPath;
                            _editingProfile.AppId = newAppId;
                        }

                        if (string.IsNullOrWhiteSpace(TxtEditAppName.Text) || TxtEditAppName.Text == "New Application")
                        {
                            try
                            {
                                string friendlyName = Path.GetFileNameWithoutExtension(selectedPath);
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
            if (_editingProfile == null) return;

            string rawName = TxtEditAppName.Text.Trim();
            string rawIdOrPath = TxtEditAppId.Text.Trim();

            _editingProfile.Name = rawName;
            _editingProfile.AppId = ExecutablePathHelper.ResolveAppIdentifier(rawIdOrPath);

            // Preserve full path if user entered/browsed a path
            if (rawIdOrPath.Contains('\\') || rawIdOrPath.Contains('/'))
            {
                _editingProfile.ExecutablePath = rawIdOrPath;
            }
            else if (!string.IsNullOrEmpty(_editingProfile.ExecutablePath) &&
                     !string.Equals(Path.GetFileName(_editingProfile.ExecutablePath), _editingProfile.AppId, StringComparison.OrdinalIgnoreCase))
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

            if (CmbEditStyle.SelectedItem is ComboBoxItem styleItem &&
                Enum.TryParse<GlowStyle>(styleItem.Tag?.ToString(), true, out var style))
            {
                _editingProfile.Style = style;
            }

            if (CmbEditPriority.SelectedItem is ComboBoxItem prioItem &&
                Enum.TryParse<NotificationPriority>(prioItem.Tag?.ToString(), true, out var prio))
            {
                _editingProfile.Priority = prio;
            }

            string selectedCategory = AppCategoryHelper.CategoryGeneral;
            if (CmbEditCategory.SelectedItem is ComboBoxItem catItem && catItem.Tag != null)
            {
                selectedCategory = catItem.Tag.ToString() ?? AppCategoryHelper.CategoryGeneral;
            }
            _editingProfile.Category = selectedCategory;

            if (_isNewProfile)
            {
                _editingProfile.IsCustom = true;
            }

            bool customGlowEnabled = AppCategoryHelper.IsGamingCategory(selectedCategory)
                ? ToggleCustomGameGlow.IsOn
                : true;

            SaveRequested?.Invoke(this, new ProfileEditorSaveEventArgs(
                _editingProfile,
                customGlowEnabled,
                _isNewProfile,
                _originalIdentifier));
        }

        private void BtnCloseEditor_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SldDuration_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => UpdateEditorPreview();
        private void SldIntensity_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => UpdateEditorPreview();
        private void SldGlowSize_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => UpdateEditorPreview();
        private void SldAppCorner_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) => UpdateEditorPreview();
        private void EditColorPicker_ColorChanged(object? sender, string e) => UpdateEditorPreview();
        private void EditControl_Changed(object sender, SelectionChangedEventArgs e) => UpdateEditorPreview();
    }
}
