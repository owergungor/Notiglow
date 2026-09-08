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

        private Views.ApplicationsView? _viewApplications;
        public Views.ApplicationsView ViewApplications
        {
            get
            {
                if (_viewApplications == null)
                {
                    _viewApplications = new Views.ApplicationsView { Visibility = Visibility.Collapsed, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
                    if (_profileService != null && _glowManager != null)
                    {
                        _viewApplications.Initialize(_profileService, _glowManager);
                    }
                    RootContentGrid.Children.Add(_viewApplications);
                }
                return _viewApplications;
            }
        }

        private Views.AppearanceView? _viewAppearance;
        public Views.AppearanceView ViewAppearance
        {
            get
            {
                if (_viewAppearance == null)
                {
                    _viewAppearance = new Views.AppearanceView { Visibility = Visibility.Collapsed, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
                    if (_settingsService != null)
                    {
                        _viewAppearance.Initialize(_settingsService, _glowManager);
                    }
                    RootContentGrid.Children.Add(_viewAppearance);
                }
                return _viewAppearance;
            }
        }

        private Views.DisplayView? _viewDisplay;
        public Views.DisplayView ViewDisplay
        {
            get
            {
                if (_viewDisplay == null)
                {
                    _viewDisplay = new Views.DisplayView { Visibility = Visibility.Collapsed, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
                    if (_settingsService != null && _glowManager != null)
                    {
                        _viewDisplay.Initialize(_settingsService, _glowManager);
                    }
                    RootContentGrid.Children.Add(_viewDisplay);
                }
                return _viewDisplay;
            }
        }

        private Views.GamingView? _viewGaming;
        public Views.GamingView ViewGaming
        {
            get
            {
                if (_viewGaming == null)
                {
                    _viewGaming = new Views.GamingView { Visibility = Visibility.Collapsed, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
                    if (_settingsService != null && _glowManager != null)
                    {
                        _viewGaming.Initialize(_settingsService, _glowManager);
                    }
                    RootContentGrid.Children.Add(_viewGaming);
                }
                return _viewGaming;
            }
        }

        private Views.NotificationsView? _viewNotifications;
        public Views.NotificationsView ViewNotifications
        {
            get
            {
                if (_viewNotifications == null)
                {
                    _viewNotifications = new Views.NotificationsView { Visibility = Visibility.Collapsed, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
                    if (_settingsService != null && _glowManager != null)
                    {
                        _viewNotifications.Initialize(_settingsService, _glowManager);
                    }
                    RootContentGrid.Children.Add(_viewNotifications);
                }
                return _viewNotifications;
            }
        }

        private Views.AdvancedView? _viewAdvanced;
        public Views.AdvancedView ViewAdvanced
        {
            get
            {
                if (_viewAdvanced == null)
                {
                    _viewAdvanced = new Views.AdvancedView { Visibility = Visibility.Collapsed, HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
                    if (_settingsService != null && _profileService != null && _glowManager != null)
                    {
                        _viewAdvanced.Initialize(_settingsService, _profileService, _glowManager);
                    }
                    RootContentGrid.Children.Add(_viewAdvanced);
                }
                return _viewAdvanced;
            }
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

            ViewGeneral.Initialize(_settingsService, _glowManager, _notificationService);

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
            if (RootNavigationView != null)
            {
                RootNavigationView.PaneOpened += (s, e) => UpdateNavSelectionVisuals(_currentTag);
                RootNavigationView.PaneClosed += (s, e) => UpdateNavSelectionVisuals(_currentTag);
            }
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

            bool isGeneral = string.Equals(tag, "General", StringComparison.OrdinalIgnoreCase);
            bool isApps = string.Equals(tag, "Applications", StringComparison.OrdinalIgnoreCase);
            bool isAppearance = string.Equals(tag, "Appearance", StringComparison.OrdinalIgnoreCase);
            bool isDisplay = string.Equals(tag, "Display", StringComparison.OrdinalIgnoreCase);
            bool isGaming = string.Equals(tag, "Gaming", StringComparison.OrdinalIgnoreCase);
            bool isNotifications = string.Equals(tag, "Notifications", StringComparison.OrdinalIgnoreCase);
            bool isAdvanced = string.Equals(tag, "Advanced", StringComparison.OrdinalIgnoreCase);

            ViewGeneral.Visibility = isGeneral ? Visibility.Visible : Visibility.Collapsed;

            if (isApps || _viewApplications != null)
            {
                ViewApplications.Visibility = isApps ? Visibility.Visible : Visibility.Collapsed;
            }
            if (isAppearance || _viewAppearance != null)
            {
                ViewAppearance.Visibility = isAppearance ? Visibility.Visible : Visibility.Collapsed;
            }
            if (isDisplay || _viewDisplay != null)
            {
                ViewDisplay.Visibility = isDisplay ? Visibility.Visible : Visibility.Collapsed;
            }
            if (isGaming || _viewGaming != null)
            {
                ViewGaming.Visibility = isGaming ? Visibility.Visible : Visibility.Collapsed;
            }
            if (isNotifications || _viewNotifications != null)
            {
                ViewNotifications.Visibility = isNotifications ? Visibility.Visible : Visibility.Collapsed;
            }
            if (isAdvanced || _viewAdvanced != null)
            {
                ViewAdvanced.Visibility = isAdvanced ? Visibility.Visible : Visibility.Collapsed;
            }

            if (isApps)
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
                    double targetWidth = (RootNavigationView != null && !RootNavigationView.IsPaneOpen)
                        ? 40
                        : Math.Max(36, activeItem.ActualWidth - 12);
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
