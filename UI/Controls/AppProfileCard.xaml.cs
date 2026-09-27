using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
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

        public void SetProfile(AppProfile profile)
        {
            Profile = profile;
            TxtAppName.Text = profile.Name;
            string categoryPrefix = !string.IsNullOrEmpty(profile.Category) ? $"{profile.Category} • " : string.Empty;
            TxtDetails.Text = $"{categoryPrefix}{profile.FormattedDuration} • {profile.FormattedIntensity} • {profile.Style} • Priority: {profile.Priority}";
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
