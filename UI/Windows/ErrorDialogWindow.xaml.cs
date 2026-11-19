using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NotiGlow.Core.Win32;
using NotiGlow.Services;
using WinRT.Interop;

namespace NotiGlow.UI.Dialogs
{
    public sealed partial class ErrorDialogWindow : Window
    {
        private bool _isOpen = true;
        private readonly IntPtr _hwnd;
        private readonly AppWindow _appWindow;

        public ErrorDialogWindow()
        {
            this.InitializeComponent();
            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            _appWindow.Resize(new SizeInt32(520, 360));
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

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _isOpen = false;
            Close();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var package = new DataPackage();
                string copyText = $"{TxtTitle.Text}\n\n{TxtSummary.Text}\n\n{TxtMessage.Text}";
                if (!string.IsNullOrEmpty(TxtTechnicalDetails.Text))
                {
                    copyText += $"\n\nDetails:\n{TxtTechnicalDetails.Text}";
                }
                package.SetText(copyText);
                Clipboard.SetContent(package);
            }
            catch { }
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

        public static void ShowError(
            string title,
            string message,
            Exception? exception = null,
            bool isFatal = false,
            string? customLogPath = null,
            object? owner = null)
        {
            try
            {
                var dialog = new ErrorDialogWindow();
                dialog.TxtTitle.Text = LocalizationService.GenerateSafeFallbackText(title);
                dialog.TxtSummary.Text = LocalizationService.GenerateSafeFallbackText(message);

                if (exception != null)
                {
                    dialog.TxtMessage.Text = exception.Message;
                    dialog.TxtTechnicalDetails.Text = exception.ToString();
                    dialog.TxtTechnicalDetails.Visibility = Visibility.Visible;
                }

                dialog.ShowModal();
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to show ErrorDialogWindow modal", ex);
            }
        }

        private static readonly System.Text.RegularExpressions.Regex SecretSanitizeRegex =
            new(@"(token|password|secret|pwd)=([^&\s]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return SecretSanitizeRegex.Replace(text, "$1=[REDACTED]");
        }

        private void BtnCopyLogPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(TxtLogPath.Text);
                Clipboard.SetContent(pkg);
            }
            catch { }
        }

        private void BtnCopyDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(TxtDetails.Text);
                Clipboard.SetContent(pkg);
            }
            catch { }
        }

        private void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            _isOpen = false;
            Close();
        }
    }
}

