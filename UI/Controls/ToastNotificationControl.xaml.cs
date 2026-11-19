using System;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Services;

namespace NotiGlow.UI.Controls
{
    public sealed partial class ToastNotificationControl : UserControl
    {
        private readonly ToastItem _item;
        private readonly DispatcherTimer _autoDismissTimer;
        private bool _isDismissing = false;

        public event Action<ToastNotificationControl>? Dismissed;

        public ToastNotificationControl(ToastItem item)
        {
            _item = item;
            this.InitializeComponent();

            TxtTitle.Text = item.Title;
            TxtMessage.Text = item.Message;

            ApplySeverityStyle(item.Severity);

            if (!string.IsNullOrEmpty(item.ActionText))
            {
                BtnAction.Content = item.ActionText;
                BtnAction.Visibility = Visibility.Visible;
            }

            _autoDismissTimer = new DispatcherTimer
            {
                Interval = item.Duration
            };
            _autoDismissTimer.Tick += (s, e) =>
            {
                _autoDismissTimer.Stop();
                Dismiss();
            };

            Loaded += (s, e) => _autoDismissTimer.Start();
        }

        private void ApplySeverityStyle(ToastSeverity severity)
        {
            Color accentColor;
            string glyph;

            switch (severity)
            {
                case ToastSeverity.Error:
                    accentColor = Color.FromArgb(255, 0xEF, 0x44, 0x44);
                    glyph = "\uE711";
                    break;
                case ToastSeverity.Warning:
                    accentColor = Color.FromArgb(255, 0xF5, 0x9E, 0x0B);
                    glyph = "\uE7BA";
                    break;
                case ToastSeverity.Success:
                    accentColor = Color.FromArgb(255, 0x10, 0xB9, 0x81);
                    glyph = "\uE73E";
                    break;
                default:
                    accentColor = Color.FromArgb(255, 0x58, 0x65, 0xF2);
                    glyph = "\uE946";
                    break;
            }

            SeverityIcon.Glyph = glyph;
            SeverityIcon.Foreground = new SolidColorBrush(accentColor);
            SeverityIconContainer.Background = new SolidColorBrush(Color.FromArgb(0x22, accentColor.R, accentColor.G, accentColor.B));
            ToastBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Dismiss();
        }

        private void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            _item.ActionCallback?.Invoke();
            Dismiss();
        }

        public void Dismiss()
        {
            if (_isDismissing) return;
            _isDismissing = true;
            _autoDismissTimer.Stop();
            Dismissed?.Invoke(this);
        }
    }
}
