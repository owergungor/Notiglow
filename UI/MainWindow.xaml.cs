using System;
using System.ComponentModel;
using System.Windows;
using NotiGlow.Services;
using Wpf.Ui.Controls;

namespace NotiGlow.UI
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
            InitializeTitleBarIcon();
        }

        private void InitializeTitleBarIcon()
        {
            try
            {
                var assembly = typeof(MainWindow).Assembly;
                using var resourceStream = assembly.GetManifestResourceStream("NotiGlow.g.resources");
                if (resourceStream != null)
                {
                    using var reader = new System.Resources.ResourceReader(resourceStream);
                    reader.GetResourceData("assets/notiglowlogo.png", out _, out byte[] data);
                    if (data != null && data.Length > 4)
                    {
                        int len = BitConverter.ToInt32(data, 0);
                        using var ms = new System.IO.MemoryStream(data, 4, len);
                        var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                            ms,
                            System.Windows.Media.Imaging.BitmapCreateOptions.None,
                            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                        frame.Freeze();

                        if (AppTitleBarIcon != null)
                        {
                            AppTitleBarIcon.Source = frame;
                        }
                        Icon = frame;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"TitleBar icon resource loading fallback: {ex.Message}");
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source?.AddHook(WndProc);
            NotiGlow.Core.Win32.NativeMethods.AllowMessageInUIPI(helper.Handle, NotiGlow.Core.Win32.NativeMethods.WM_SHOW_NOTIGLOW);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)NotiGlow.Core.Win32.NativeMethods.WM_SHOW_NOTIGLOW)
            {
                LoggerService.LogInfo("Received WM_SHOW_NOTIGLOW signal. Restoring and activating MainWindow.");
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

            NavigateToTag("General");
        }

        private System.Windows.Media.ScaleTransform? _activeSelectionBoxScale;

        private void InitializeActiveSelectionBoxTransform()
        {
            if (ActiveSelectionBox == null || _activeSelectionBoxScale != null) return;
            ActiveSelectionBox.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            _activeSelectionBoxScale = new System.Windows.Media.ScaleTransform(1.0, 1.0);
            ActiveSelectionBox.RenderTransform = _activeSelectionBoxScale;

            if (RootNavigationView != null)
            {
                RootNavigationView.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new System.Windows.Input.MouseButtonEventHandler((s, e) => AnimateActiveSelectionBoxPress(true)), true);
                RootNavigationView.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new System.Windows.Input.MouseButtonEventHandler((s, e) => AnimateActiveSelectionBoxPress(false)), true);
                RootNavigationView.MouseLeave += (s, e) => AnimateActiveSelectionBoxPress(false);
                RootNavigationView.LostMouseCapture += (s, e) => AnimateActiveSelectionBoxPress(false);
            }
        }

        private void AnimateActiveSelectionBoxPress(bool pressed)
        {
            if (_activeSelectionBoxScale == null) return;
            double targetScale = pressed ? 0.97 : 1.0;
            int duration = pressed ? 80 : 115;
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            var anim = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = targetScale,
                Duration = TimeSpan.FromMilliseconds(duration),
                EasingFunction = ease
            };
            _activeSelectionBoxScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, anim, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);
            _activeSelectionBoxScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, anim, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeActiveSelectionBoxTransform();
            UpdateNavSelectionVisuals(_currentTag);
        }

        private void RootNavigationView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateNavSelectionVisuals(_currentTag);
        }

        private string _currentTag = "General";

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is NavigationViewItem navItem && navItem.Tag is string tag)
            {
                NavigateToTag(tag);
            }
        }

        private void RootNavigationView_SelectionChanged(NavigationView sender, RoutedEventArgs args)
        {
            if (sender.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string tag)
            {
                NavigateToTag(tag);
            }
        }

        public void NavigateToTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            _currentTag = tag;

            UpdateNavSelectionVisuals(tag);

            ViewGeneral.Visibility = string.Equals(tag, "General", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            ViewApplications.Visibility = string.Equals(tag, "Applications", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            ViewAppearance.Visibility = string.Equals(tag, "Appearance", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            ViewDisplay.Visibility = string.Equals(tag, "Display", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            ViewGaming.Visibility = string.Equals(tag, "Gaming", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            ViewNotifications.Visibility = string.Equals(tag, "Notifications", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            ViewAdvanced.Visibility = string.Equals(tag, "Advanced", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;

            if (string.Equals(tag, "Applications", StringComparison.OrdinalIgnoreCase))
            {
                ViewApplications.RefreshAppCards();
            }
        }

        public void UpdateNavSelectionVisuals(string tag)
        {
            if (RootNavigationView == null || RootNavigationView.MenuItems == null) return;

            NavigationViewItem? activeItem = null;
            foreach (var menuItem in RootNavigationView.MenuItems)
            {
                if (menuItem is NavigationViewItem navItem)
                {
                    bool isActive = string.Equals(navItem.Tag as string, tag, StringComparison.OrdinalIgnoreCase);
                    navItem.IsActive = isActive;
                    if (isActive)
                    {
                        activeItem = navItem;
                        navItem.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "AccentColor");
                        navItem.FontWeight = FontWeights.SemiBold;
                    }
                    else
                    {
                        navItem.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "TextSecondary");
                        navItem.FontWeight = FontWeights.Normal;
                    }
                }
            }

            if (activeItem != null)
            {
                PositionSelectionOverlay(activeItem);
            }
        }

        private void PositionSelectionOverlay(NavigationViewItem activeItem)
        {
            if (SidebarOverlayCanvas == null || ActiveSelectionBox == null) return;

            if (!activeItem.IsLoaded)
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (s, e) =>
                {
                    activeItem.Loaded -= loadedHandler;
                    PositionSelectionOverlay(activeItem);
                };
                activeItem.Loaded += loadedHandler;
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (activeItem.ActualHeight <= 0 || activeItem.ActualWidth <= 0)
                    {
                        activeItem.UpdateLayout();
                    }

                    if (activeItem.ActualHeight <= 0 || activeItem.ActualWidth <= 0) return;

                    var transform = activeItem.TransformToVisual(SidebarOverlayCanvas);
                    System.Windows.Point origin = transform.Transform(new System.Windows.Point(0, 0));

                    double targetTop = origin.Y;
                    double targetLeft = 6;
                    double targetWidth = Math.Max(36, activeItem.ActualWidth - 12);
                    double targetHeight = Math.Max(32, activeItem.ActualHeight);

                    ActiveSelectionBox.Width = targetWidth;
                    ActiveSelectionBox.Height = targetHeight;
                    System.Windows.Controls.Canvas.SetLeft(ActiveSelectionBox, targetLeft);
                    System.Windows.Controls.Canvas.SetTop(ActiveSelectionBox, targetTop);
                    ActiveSelectionBox.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed positioning selection overlay", ex);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public System.Windows.Controls.Canvas? SelectionOverlayCanvas => SidebarOverlayCanvas;
        public System.Windows.Controls.Border? SelectionBox => ActiveSelectionBox;

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
