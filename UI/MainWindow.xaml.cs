using System;
using System.ComponentModel;
using System.Windows;
using GlowBorder.Services;
using Wpf.Ui.Controls;

namespace GlowBorder.UI
{
    public partial class MainWindow : FluentWindow
    {
        private SettingsService? _settingsService;
        private ProfileService? _profileService;
        private NotificationService? _notificationService;
        private GlowManager? _glowManager;
        private bool _isExplicitExit = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);
            GlowBorder.Core.Win32.NativeMethods.AllowMessageInUIPI(helper.Handle, GlowBorder.Core.Win32.NativeMethods.WM_SHOW_GLOWBORDER);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)GlowBorder.Core.Win32.NativeMethods.WM_SHOW_GLOWBORDER)
            {
                LoggerService.LogInfo("Received WM_SHOW_GLOWBORDER signal. Restoring and activating MainWindow.");
                Show();
                WindowState = WindowState.Normal;
                Activate();
                Focus();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Initialize(
            SettingsService settingsService,
            ProfileService profileService,
            NotificationService notificationService,
            GlowManager glowManager)
        {
            _settingsService = settingsService;
            _profileService = profileService;
            _notificationService = notificationService;
            _glowManager = glowManager;

            ViewGeneral.Initialize(_settingsService, _glowManager);
            ViewApplications.Initialize(_profileService, _glowManager);
            ViewAppearance.Initialize(_settingsService, _glowManager);
            ViewDisplay.Initialize(_settingsService, _glowManager);
            ViewGaming.Initialize(_settingsService, _glowManager);
            ViewNotifications.Initialize(_settingsService, _glowManager);
            ViewAdvanced.Initialize(_settingsService, _profileService, _glowManager);

            // Default tab ViewGeneral is visible by default in XAML
        }

        private void RootNavigationView_SelectionChanged(NavigationView sender, RoutedEventArgs args)
        {
            if (sender.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                ViewGeneral.Visibility = tag == "General" ? Visibility.Visible : Visibility.Collapsed;
                ViewApplications.Visibility = tag == "Applications" ? Visibility.Visible : Visibility.Collapsed;
                ViewAppearance.Visibility = tag == "Appearance" ? Visibility.Visible : Visibility.Collapsed;
                ViewDisplay.Visibility = tag == "Display" ? Visibility.Visible : Visibility.Collapsed;
                ViewGaming.Visibility = tag == "Gaming" ? Visibility.Visible : Visibility.Collapsed;
                ViewNotifications.Visibility = tag == "Notifications" ? Visibility.Visible : Visibility.Collapsed;
                ViewAdvanced.Visibility = tag == "Advanced" ? Visibility.Visible : Visibility.Collapsed;

                if (tag == "Applications")
                {
                    ViewApplications.RefreshAppCards();
                }
            }
        }

        public void ForceExit()
        {
            _isExplicitExit = true;
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitExit)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}
