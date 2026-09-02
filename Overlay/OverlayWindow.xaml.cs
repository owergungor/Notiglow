using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using GlowBorder.Core.Helpers;
using GlowBorder.Core.Win32;
using GlowBorder.Models;

namespace GlowBorder.Overlay
{
    public partial class OverlayWindow : Window
    {
        private Screen _targetScreen;

        public OverlayWindow(Screen targetScreen)
        {
            InitializeComponent();
            _targetScreen = targetScreen;

            UpdatePosition();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeWindowClickThroughAndNoActivate(hwnd);
        }

        public void UpdatePosition()
        {
            Rectangle bounds = _targetScreen.Bounds;
            
            // Adjust bounds based on DPI scale factors
            DpiScale dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX > 0 ? 1.0 / dpi.DpiScaleX : 1.0;
            double scaleY = dpi.DpiScaleY > 0 ? 1.0 / dpi.DpiScaleY : 1.0;

            Left = bounds.X * scaleX;
            Top = bounds.Y * scaleY;
            Width = bounds.Width * scaleX;
            Height = bounds.Height * scaleY;
        }

        public void PlayGlow(AppProfile profile, Action? onFinished = null)
        {
            Dispatcher.Invoke(() =>
            {
                UpdatePosition();
                Show();
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                NativeMethods.MakeWindowClickThroughAndNoActivate(hwnd);

                GlowControl.ApplyProfile(profile, () =>
                {
                    Hide();
                    onFinished?.Invoke();
                });
            });
        }

        public void StopGlow()
        {
            Dispatcher.Invoke(() =>
            {
                GlowControl.StopAnimation();
                Hide();
            });
        }
    }
}
