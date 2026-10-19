using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NotiGlow.Services;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace NotiGlow.UI.Dialogs
{
    public partial class ConfirmationDialogWindow : Window
    {
        private bool _confirmed = false;
        public bool UserConfirmed => _confirmed;

        public ConfirmationDialogWindow()
        {
            InitializeComponent();
            HeaderGrid.MouseDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };
        }

        private static void PositionDialog(ConfirmationDialogWindow dialog, Window? owner)
        {
            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void SetSingleButtonMode()
        {
            BtnCancel.Visibility = Visibility.Collapsed;
            ColCancel.Width = new GridLength(0);
            ColGap.Width = new GridLength(0);
            Grid.SetColumn(BtnConfirm, 0);
            Grid.SetColumnSpan(BtnConfirm, 3);
            BtnConfirm.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        }

        public static bool ShowConfirm(
            string title,
            string message,
            string confirmText = "Confirm",
            string cancelText = "Cancel",
            bool isDestructive = false,
            Window? owner = null)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(() =>
                        ShowConfirm(title, message, confirmText, cancelText, isDestructive, owner));
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed invoking confirmation dialog on UI thread", ex);
                    return false;
                }
            }

            var dialog = new ConfirmationDialogWindow();
            PositionDialog(dialog, owner);

            dialog.TxtTitle.Text = title;
            dialog.TxtMessage.Text = message;
            dialog.BtnConfirm.Content = confirmText;
            dialog.BtnCancel.Content = cancelText;

            if (isDestructive)
            {
                dialog.HeaderIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.DismissCircle24;
                dialog.HeaderIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
                dialog.IconBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xEF, 0x44, 0x44));
                dialog.IconBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xEF, 0x44, 0x44));

                dialog.BtnConfirm.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26));
                dialog.BtnConfirm.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0x26, 0x26));
                dialog.BtnConfirm.Foreground = System.Windows.Media.Brushes.White;
            }

            dialog.ShowDialog();
            return dialog._confirmed;
        }

        public static void ShowWarning(
            string title,
            string message,
            string buttonText = "OK",
            Window? owner = null)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        ShowWarning(title, message, buttonText, owner));
                    return;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed invoking warning dialog on UI thread", ex);
                    return;
                }
            }

            var dialog = new ConfirmationDialogWindow();
            PositionDialog(dialog, owner);

            dialog.TxtTitle.Text = title;
            dialog.TxtMessage.Text = message;
            dialog.BtnConfirm.Content = buttonText;

            dialog.HeaderIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24;
            dialog.HeaderIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0x9E, 0x0B));
            dialog.IconBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            dialog.IconBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xF5, 0x9E, 0x0B));

            dialog.SetSingleButtonMode();
            dialog.ShowDialog();
        }

        public static void ShowError(
            string title,
            string message,
            string buttonText = "Close",
            Window? owner = null)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        ShowError(title, message, buttonText, owner));
                    return;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed invoking error dialog on UI thread", ex);
                    return;
                }
            }

            var dialog = new ConfirmationDialogWindow();
            PositionDialog(dialog, owner);

            dialog.TxtTitle.Text = title;
            dialog.TxtMessage.Text = message;
            dialog.BtnConfirm.Content = buttonText;

            dialog.HeaderIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.DismissCircle24;
            dialog.HeaderIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
            dialog.IconBadge.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xEF, 0x44, 0x44));
            dialog.IconBadge.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xEF, 0x44, 0x44));

            dialog.SetSingleButtonMode();
            dialog.ShowDialog();
        }

        public static void ShowInfo(
            string title,
            string message,
            string buttonText = "OK",
            Window? owner = null)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        ShowInfo(title, message, buttonText, owner));
                    return;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed invoking info dialog on UI thread", ex);
                    return;
                }
            }

            var dialog = new ConfirmationDialogWindow();
            PositionDialog(dialog, owner);

            dialog.TxtTitle.Text = title;
            dialog.TxtMessage.Text = message;
            dialog.BtnConfirm.Content = buttonText;

            dialog.HeaderIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Info24;
            dialog.SetSingleButtonMode();
            dialog.ShowDialog();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            _confirmed = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _confirmed = false;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                _confirmed = false;
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                _confirmed = true;
                Close();
                e.Handled = true;
            }
        }
    }
}
