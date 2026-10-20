using System;
using System.Windows;
using System.Windows.Input;
using NotiGlow.Services;
using Application = System.Windows.Application;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace NotiGlow.UI.Dialogs
{
    public partial class UpdateConfirmDialogWindow : Window
    {
        private bool _confirmed = false;
        public bool UserConfirmed => _confirmed;
        public string CurrentVersion => TxtCurrentVersion?.Text ?? string.Empty;
        public string NewVersion => TxtNewVersion?.Text ?? string.Empty;

        public UpdateConfirmDialogWindow()
        {
            InitializeComponent();
        }

        public UpdateConfirmDialogWindow(string newVersion, string? currentVersion = null, string? releaseNotes = null) : this()
        {
            if (TxtNewVersion != null)
                TxtNewVersion.Text = newVersion;
            if (TxtCurrentVersion != null && currentVersion != null)
                TxtCurrentVersion.Text = currentVersion;
            if (!string.IsNullOrWhiteSpace(releaseNotes) && TxtReleaseNotes != null && ScrollReleaseNotes != null)
            {
                TxtReleaseNotes.Text = releaseNotes.Trim();
                ScrollReleaseNotes.Visibility = Visibility.Visible;
            }
        }

        public static bool ShowUpdateConfirm(UpdateInfo info, Window? owner = null)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                try
                {
                    return Application.Current.Dispatcher.Invoke(() => ShowUpdateConfirm(info, owner));
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed invoking update confirmation on UI thread", ex);
                    return false;
                }
            }

            var dialog = new UpdateConfirmDialogWindow();

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

            // Bind current and new versions
            string curVer = !string.IsNullOrWhiteSpace(info.CurrentVersion)
                ? info.CurrentVersion.TrimStart('v', 'V')
                : UpdateService.CurrentVersionString;
            dialog.TxtCurrentVersion.Text = $"v{curVer}";

            string latestVer = !string.IsNullOrWhiteSpace(info.LatestVersion)
                ? info.LatestVersion.TrimStart('v', 'V')
                : "2.5";
            dialog.TxtNewVersion.Text = $"v{latestVer}";

            // Optional Release Notes
            if (!string.IsNullOrWhiteSpace(info.ReleaseNotes))
            {
                dialog.TxtReleaseNotes.Text = info.ReleaseNotes.Trim();
                dialog.ScrollReleaseNotes.Visibility = Visibility.Visible;
            }

            dialog.ShowDialog();
            return dialog._confirmed;
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
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

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
    }
}
