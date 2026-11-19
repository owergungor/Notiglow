using System;
using System.Drawing;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NotiGlow.Core.Win32;
using NotiGlow.Models;
using WinRT.Interop;

namespace NotiGlow.Overlay
{
    public sealed partial class OverlayWindow : Window
    {
        private System.Windows.Forms.Screen _targetScreen;
        private IntPtr _hwnd;
        private AppWindow _appWindow;

        public OverlayWindow(System.Windows.Forms.Screen targetScreen)
        {
            this.InitializeComponent();
            this.SystemBackdrop = new TransparentBackdrop();
            _targetScreen = targetScreen;

            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            ConfigureWindow();
            UpdatePosition();
        }

        private void ConfigureWindow()
        {
            _appWindow.IsShownInSwitchers = false;

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsAlwaysOnTop = true;
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            try
            {
                var margins = new NativeMethods.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                NativeMethods.DwmExtendFrameIntoClientArea(_hwnd, ref margins);
            }
            catch { }

            NativeMethods.MakeWindowClickThroughAndNoActivate(_hwnd);
        }

        public void UpdatePosition()
        {
            Rectangle bounds = _targetScreen.Bounds;
            _appWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }

        public void PlayGlow(AppProfile profile, Action? onFinished = null)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdatePosition();
                _appWindow.Show();
                NativeMethods.MakeWindowClickThroughAndNoActivate(_hwnd);

                GlowControl.ApplyProfile(profile, () =>
                {
                    _appWindow.Hide();
                    onFinished?.Invoke();
                });
            });
        }

        public void StopGlow()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                GlowControl.StopAnimation();
                _appWindow.Hide();
            });
        }
    }
}
