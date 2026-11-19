using System;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.UI.Controls
{
    public sealed partial class AppProfileCard : UserControl
    {
        public AppProfile? Profile { get; private set; }

        public event EventHandler<AppProfile>? EditRequested;
        public event EventHandler<AppProfile>? DuplicateRequested;
        public event EventHandler<AppProfile>? DeleteRequested;
        public event EventHandler<AppProfile>? PreviewRequested;
        public event EventHandler<AppProfile>? ToggleChanged;

        public AppProfileCard()
        {
            this.InitializeComponent();
        }

        public void ConfigureForApplicationsList()
        {
            BtnDuplicate.Visibility = Visibility.Collapsed;
        }

        public void SetHighlighted(bool highlighted)
        {
            if (CardBorder == null) return;
            var res = Application.Current.Resources;
            if (highlighted)
            {
                if (res.TryGetValue("AccentColor", out var accent) && accent is Brush accentBrush)
                    CardBorder.BorderBrush = accentBrush;
                if (res.TryGetValue("NavActiveBackground", out var activeBg) && activeBg is Brush bgBrush)
                    CardBorder.Background = bgBrush;
                CardBorder.BorderThickness = new Thickness(1.5);
            }
            else
            {
                if (res.TryGetValue("BorderColor", out var border) && border is Brush borderBrush)
                    CardBorder.BorderBrush = borderBrush;
                if (res.TryGetValue("CardBackground", out var cardBg) && cardBg is Brush cardBrush)
                    CardBorder.Background = cardBrush;
                CardBorder.BorderThickness = new Thickness(1);
            }
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
            ToggleEnabled.IsOn = profile.Enabled;

            if (ColorHelper.IsRgbSpectrum(profile.ColorHex))
            {
                ColorBadge.Background = ColorHelper.CreateRainbowLinearBrush(new Point(0, 0), new Point(1, 1));
            }
            else
            {
                Color c = ColorHelper.ParseColor(profile.ColorHex);
                ColorBadgeBrush.Color = c;
                ColorBadge.Background = ColorBadgeBrush;
            }

            if (string.Equals(profile.Category, "Gaming", StringComparison.OrdinalIgnoreCase))
            {
                FallbackAppIcon.Glyph = "\uE7FC"; // Dedicated Gamepad glyph for gaming profiles
            }
            else
            {
                FallbackAppIcon.Glyph = "\uE71D";
            }

            LoadAppIconAsync(profile);
        }

        private async void LoadAppIconAsync(AppProfile profile)
        {
            if (ImgAppIcon == null || FallbackAppIcon == null) return;

            var cached = AppIconService.Instance.GetCachedIcon(profile.ExecutablePath, profile.AppId);
            if (cached != null)
            {
                ImgAppIcon.Source = cached;
                ImgAppIcon.Visibility = Visibility.Visible;
                FallbackAppIcon.Visibility = Visibility.Collapsed;
                return;
            }

            ImgAppIcon.Visibility = Visibility.Collapsed;
            FallbackAppIcon.Visibility = Visibility.Visible;

            var loaded = await AppIconService.Instance.GetAppIconAsync(profile.ExecutablePath, profile.AppId);
            if (loaded != null)
            {
                ImgAppIcon.Source = loaded;
                ImgAppIcon.Visibility = Visibility.Visible;
                FallbackAppIcon.Visibility = Visibility.Collapsed;
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

        private void ToggleEnabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (Profile != null && Profile.Enabled != ToggleEnabled.IsOn)
            {
                Profile.Enabled = ToggleEnabled.IsOn;
                ToggleChanged?.Invoke(this, Profile);
            }
        }
    }
}
