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
    public partial class AppProfileGridCard : UserControl
    {
        public AppProfile? Profile { get; private set; }

        public event EventHandler<AppProfile>? EditRequested;
        public event EventHandler<AppProfile>? DuplicateRequested;
        public event EventHandler<AppProfile>? DeleteRequested;
        public event EventHandler<AppProfile>? PreviewRequested;
        public event EventHandler<AppProfile>? ToggleChanged;

        public AppProfileGridCard()
        {
            InitializeComponent();
        }

        public void SetProfile(AppProfile profile)
        {
            Profile = profile;
            TxtAppName.Text = profile.Name;
            TxtCategory.Text = !string.IsNullOrEmpty(profile.Category) ? profile.Category : "Application";
            TxtDurationAndIntensity.Text = $"{profile.FormattedDuration} • {profile.FormattedIntensity}";
            TxtStyle.Text = profile.Style.ToString();
            TxtPriority.Text = profile.Priority.ToString();
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
