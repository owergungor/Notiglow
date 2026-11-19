using System;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Win32;
using NotiGlow.Services;
using WinRT.Interop;

namespace NotiGlow.UI.Dialogs
{
    public sealed partial class ConfirmationDialogWindow : Window
    {
        private bool _confirmed = false;
        private bool _isOpen = true;
        private readonly IntPtr _hwnd;
        private readonly AppWindow _appWindow;

        public bool UserConfirmed => _confirmed;

        public ConfirmationDialogWindow()
        {
            this.InitializeComponent();
            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            _appWindow.Resize(new SizeInt32(480, 260));
            _appWindow.IsShownInSwitchers = false;

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            Closed += (s, e) =>
            {
                _isOpen = false;
            };
        }

        private void SetSingleButtonMode()
        {
            BtnCancel.Visibility = Visibility.Collapsed;
            ColCancel.Width = new GridLength(0);
            ColGap.Width = new GridLength(0);
            Grid.SetColumn(BtnConfirm, 0);
            Grid.SetColumnSpan(BtnConfirm, 3);
            BtnConfirm.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            _confirmed = true;
            _isOpen = false;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _confirmed = false;
            _isOpen = false;
            Close();
        }

        public void ShowModal()
        {
            Activate();
            while (_isOpen && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        public static bool ShowConfirm(
            string title,
            string message,
            string confirmText = "Confirm",
            string cancelText = "Cancel",
            bool isDestructive = false,
            object? owner = null)
        {
            var dialog = new ConfirmationDialogWindow();
            dialog.TxtTitle.Text = LocalizationService.GenerateSafeFallbackText(title);
            dialog.TxtMessage.Text = LocalizationService.GenerateSafeFallbackText(message);
            dialog.BtnConfirm.Content = LocalizationService.GenerateSafeFallbackText(confirmText);
            dialog.BtnCancel.Content = LocalizationService.GenerateSafeFallbackText(cancelText);

            if (isDestructive)
            {
                dialog.HeaderIcon.Glyph = "\uE711"; // Dismiss
                var red = Windows.UI.Color.FromArgb(255, 239, 68, 68);
                dialog.HeaderIcon.Foreground = new SolidColorBrush(red);
                dialog.IconBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(32, 239, 68, 68));
                dialog.IconBadge.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(64, 239, 68, 68));

                dialog.BtnConfirm.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38));
                dialog.BtnConfirm.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            }

            dialog.ShowModal();
            return dialog._confirmed;
        }

        public static void ShowWarning(
            string title,
            string message,
            string buttonText = "OK",
            object? owner = null)
        {
            var dialog = new ConfirmationDialogWindow();
            dialog.TxtTitle.Text = LocalizationService.GenerateSafeFallbackText(title);
            dialog.TxtMessage.Text = LocalizationService.GenerateSafeFallbackText(message);
            dialog.BtnConfirm.Content = LocalizationService.GenerateSafeFallbackText(buttonText);

            dialog.HeaderIcon.Glyph = "\uE7BA"; // Warning
            var amber = Windows.UI.Color.FromArgb(255, 245, 158, 11);
            dialog.HeaderIcon.Foreground = new SolidColorBrush(amber);
            dialog.IconBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(32, 245, 158, 11));
            dialog.IconBadge.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(64, 245, 158, 11));

            dialog.SetSingleButtonMode();
            dialog.ShowModal();
        }

        public static void ShowError(
            string title,
            string message,
            string buttonText = "Close",
            object? owner = null)
        {
            var dialog = new ConfirmationDialogWindow();
            dialog.TxtTitle.Text = LocalizationService.GenerateSafeFallbackText(title);
            dialog.TxtMessage.Text = LocalizationService.GenerateSafeFallbackText(message);
            dialog.BtnConfirm.Content = LocalizationService.GenerateSafeFallbackText(buttonText);

            dialog.HeaderIcon.Glyph = "\uEA39"; // ErrorBadge
            var red = Windows.UI.Color.FromArgb(255, 225, 29, 72);
            dialog.HeaderIcon.Foreground = new SolidColorBrush(red);
            dialog.IconBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(32, 225, 29, 72));
            dialog.IconBadge.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(64, 225, 29, 72));

            dialog.SetSingleButtonMode();
            dialog.ShowModal();
        }

        public static void ShowInfo(
            string title,
            string message,
            string buttonText = "OK",
            object? owner = null)
        {
            var dialog = new ConfirmationDialogWindow();
            dialog.TxtTitle.Text = LocalizationService.GenerateSafeFallbackText(title);
            dialog.TxtMessage.Text = LocalizationService.GenerateSafeFallbackText(message);
            dialog.BtnConfirm.Content = LocalizationService.GenerateSafeFallbackText(buttonText);

            dialog.HeaderIcon.Glyph = "\uE946"; // Info
            var blue = Windows.UI.Color.FromArgb(255, 59, 130, 246);
            dialog.HeaderIcon.Foreground = new SolidColorBrush(blue);
            dialog.IconBadge.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(32, 59, 130, 246));
            dialog.IconBadge.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(64, 59, 130, 246));

            dialog.SetSingleButtonMode();
            dialog.ShowModal();
        }
    }
}
