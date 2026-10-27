using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
using UserControl = System.Windows.Controls.UserControl;
using Color = System.Windows.Media.Color;

namespace NotiGlow.UI.Controls
{
    public partial class AppProfileCard : UserControl
    {
        public AppProfile? Profile { get; private set; }

        public event EventHandler<AppProfile>? EditRequested;
        public event EventHandler<AppProfile>? DuplicateRequested;
        public event EventHandler<AppProfile>? DeleteRequested;
        public event EventHandler<AppProfile>? PreviewRequested;
        public event EventHandler<AppProfile>? ToggleChanged;

        public AppProfileCard()
        {
            InitializeComponent();
        }

        public void ConfigureForApplicationsList()
        {
            BtnDuplicate.Visibility = Visibility.Collapsed;
            BtnEdit.Appearance = ControlAppearance.Transparent;
            BtnDelete.Appearance = ControlAppearance.Transparent;
            BtnEdit.Background = System.Windows.Media.Brushes.Transparent;
            BtnDelete.Background = System.Windows.Media.Brushes.Transparent;
            BtnEdit.BorderBrush = System.Windows.Media.Brushes.Transparent;
            BtnDelete.BorderBrush = System.Windows.Media.Brushes.Transparent;
            BtnEdit.Margin = new Thickness(0, 0, 4, 0);
            BtnDelete.Margin = new Thickness(0, 0, 8, 0);
            BtnEdit.MinWidth = 36;
            BtnDelete.Width = 36;
            BtnDelete.Content = null;
            string deleteLabel = LocalizationService.Instance.GetString("Applications.CardDelete");
            BtnDelete.ToolTip = deleteLabel;
            AutomationProperties.SetName(BtnDelete, deleteLabel);
        }

        public void SetProfile(AppProfile profile)
        {
            Profile = profile;
            DataContext = profile;
            TxtAppName.Text = !string.IsNullOrWhiteSpace(profile.Name) ? profile.Name : profile.AppId;
            string prioText = profile.Priority switch
            {
                NotificationPriority.Low => LocalizationService.Instance.GetString("Applications.PriorityLowShort"),
                NotificationPriority.High => LocalizationService.Instance.GetString("Applications.PriorityHighShort"),
                _ => LocalizationService.Instance.GetString("Applications.PriorityNormalShort")
            };
            string styleKey = $"Style.{profile.Style}";
            string styleText = LocalizationService.Instance.GetString(styleKey);
            if (string.IsNullOrEmpty(styleText) || styleText == styleKey) styleText = profile.Style.ToString();
            TxtDetails.Text = $"{profile.FormattedDuration.Replace(" ", string.Empty)} • {profile.FormattedIntensity} • {styleText} • {prioText}";
            ToggleEnabled.IsChecked = profile.Enabled;

            if (ColorHelper.IsRgbSpectrum(profile.ColorHex))
            {
                ColorBadge.Background = ColorHelper.CreateRainbowLinearBrush(new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
            }
            else
            {
                Color c = ColorHelper.ParseColor(profile.ColorHex);
                ColorBadgeBrush.Color = c;
                ColorBadge.Background = ColorBadgeBrush;
            }
        }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (Profile != null) PreviewRequested?.Invoke(this, Profile);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (Profile != null) EditRequested?.Invoke(this, Profile);
        }

        private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
        {
            if (Profile != null) DuplicateRequested?.Invoke(this, Profile);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (Profile != null) DeleteRequested?.Invoke(this, Profile);
        }

        private void ToggleEnabled_Click(object sender, RoutedEventArgs e)
        {
            if (Profile != null)
            {
                Profile.Enabled = ToggleEnabled.IsChecked == true;
                ToggleChanged?.Invoke(this, Profile);
            }
        }
    }
}
