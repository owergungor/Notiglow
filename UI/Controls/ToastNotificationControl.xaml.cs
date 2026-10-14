using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NotiGlow.Services;
using NotiGlow.UI.Animations;
using Color = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;

namespace NotiGlow.UI.Controls
{
    public partial class ToastNotificationControl : UserControl
    {
        private readonly ToastItem _item;
        private readonly DispatcherTimer _autoDismissTimer;
        private bool _isDismissing = false;

        public event Action<ToastNotificationControl>? Dismissed;

        public ToastNotificationControl(ToastItem item)
        {
            _item = item;
            InitializeComponent();

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

            Loaded += OnLoaded;
        }

        private void ApplySeverityStyle(ToastSeverity severity)
        {
            Color accentColor;
            Wpf.Ui.Controls.SymbolRegular symbol;

            switch (severity)
            {
                case ToastSeverity.Error:
                    accentColor = Color.FromRgb(0xEF, 0x44, 0x44);
                    symbol = Wpf.Ui.Controls.SymbolRegular.DismissCircle24;
                    break;
                case ToastSeverity.Warning:
                    accentColor = Color.FromRgb(0xF5, 0x9E, 0x0B);
                    symbol = Wpf.Ui.Controls.SymbolRegular.Warning24;
                    break;
                case ToastSeverity.Success:
                    accentColor = Color.FromRgb(0x10, 0xB9, 0x81);
                    symbol = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24;
                    break;
                default:
                    accentColor = Color.FromRgb(0x58, 0x65, 0xF2);
                    symbol = Wpf.Ui.Controls.SymbolRegular.Info24;
                    break;
            }

            SeverityIcon.Symbol = symbol;
            SeverityIcon.Foreground = new SolidColorBrush(accentColor);
            SeverityIconContainer.Background = new SolidColorBrush(Color.FromArgb(0x22, accentColor.R, accentColor.G, accentColor.B));
            ToastBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (MotionPolicy.IsReduceMotion)
            {
                Opacity = 1.0;
                ToastTransform.Y = 0;
            }
            else
            {
                Opacity = 0.0;
                ToastTransform.Y = 16;

                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
                var slideUp = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };

                BeginAnimation(OpacityProperty, fadeIn);
                ToastTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
            }

            _autoDismissTimer.Start();
        }

        public void Dismiss()
        {
            if (_isDismissing) return;
            _isDismissing = true;
            _autoDismissTimer.Stop();

            if (MotionPolicy.IsReduceMotion)
            {
                Dismissed?.Invoke(this);
            }
            else
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
                var fadeOut = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
                var slideUp = new DoubleAnimation(ToastTransform.Y, -10, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };

                fadeOut.Completed += (s, e) => Dismissed?.Invoke(this);

                BeginAnimation(OpacityProperty, fadeOut);
                ToastTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
            }
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e)
        {
            Dismiss();
        }

        private void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _item.ActionCallback?.Invoke();
            }
            finally
            {
                Dismiss();
            }
        }
    }
}
