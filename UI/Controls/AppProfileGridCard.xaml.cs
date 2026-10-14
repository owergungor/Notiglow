using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;
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
            string prioText = profile.Priority switch
            {
                NotificationPriority.Low => LocalizationService.Instance.GetString("Applications.PriorityLow"),
                NotificationPriority.High => LocalizationService.Instance.GetString("Applications.PriorityHigh"),
                _ => LocalizationService.Instance.GetString("Applications.PriorityNormal")
            };
            string styleKey = $"Style.{profile.Style}";
            string styleText = LocalizationService.Instance.GetString(styleKey);
            if (string.IsNullOrEmpty(styleText) || styleText == styleKey) styleText = profile.Style.ToString();
            TxtStyle.Text = styleText;
            TxtPriority.Text = prioText;
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
