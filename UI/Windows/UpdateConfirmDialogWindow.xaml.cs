using System;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NotiGlow.Core.Win32;
using NotiGlow.Services;
using WinRT.Interop;

namespace NotiGlow.UI.Dialogs
{
    public sealed partial class UpdateConfirmDialogWindow : Window
    {
        private bool _confirmed = false;
        private bool _isOpen = true;
        private readonly IntPtr _hwnd;
        private readonly AppWindow _appWindow;

        public bool UserConfirmed => _confirmed;
        public string NewVersion { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;

        public UpdateConfirmDialogWindow()
        {
            this.InitializeComponent();
            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            _appWindow.Resize(new SizeInt32(500, 360));
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

        public UpdateConfirmDialogWindow(string newVersion, string? currentVersion = null, string? releaseNotes = null)
            : this()
        {
            NewVersion = newVersion;
            CurrentVersion = currentVersion ?? UpdateService.CurrentVersionString;
            TxtNewVersion.Text = $"New: v{newVersion}";
            TxtCurrentVersion.Text = $"Current: v{CurrentVersion}";
            if (!string.IsNullOrWhiteSpace(releaseNotes))
            {
                TxtReleaseNotes.Text = releaseNotes;
            }
        }

        private void BtnInstall_Click(object sender, RoutedEventArgs e)
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

        public static bool ShowUpdateConfirm(UpdateInfo info, object? owner = null)
        {
            return ShowUpdate(info.LatestVersion, info.CurrentVersion, info.ReleaseNotes, owner);
        }

        public static bool ShowUpdate(
            string newVersion,
            string? currentVersion = null,
            string? releaseNotes = null,
            object? owner = null)
        {
            var dialog = new UpdateConfirmDialogWindow(newVersion, currentVersion, releaseNotes);
            dialog.ShowModal();
            return dialog._confirmed;
        }
    }
}
