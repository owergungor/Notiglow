using System;
using Windows.Graphics;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Win32;
using NotiGlow.Models;
using NotiGlow.Services;
using WinRT.Interop;

namespace NotiGlow.UI
{
    public sealed partial class MainWindow : Window
    {
        private SettingsService? _settingsService;
        private ProfileService? _profileService;
        private NotificationService? _notificationService;
        private GlowManager? _glowManager;

        private bool _isExplicitExit = false;
        private bool _isGeometryInitialized = false;
        private readonly IntPtr _hwnd;
        private readonly AppWindow _appWindow;
        private readonly NativeMethods.SubclassProc _subclassProc;
        private string _currentTag = "General";

        public IntPtr Hwnd => _hwnd;

        public MainWindow()
        {
            this.InitializeComponent();

            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            // Set Title & Initial Geometry (~1550 x 950 physical px default)
            Title = "NotiGlow";
            _appWindow.Title = "NotiGlow";
            ApplyWindowGeometry(null);

            // Track size and position changes for persistence
            _appWindow.Changed += (s, args) =>
            {
                if (args.DidSizeChange || args.DidPositionChange)
                {
                    OnWindowGeometryChanged();
                }
            };

            // Custom TitleBar & DesktopAcrylic Backdrop (Default)
            try
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            catch { }

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Hook close event to minimize to tray
            _appWindow.Closing += (s, e) =>
            {
                OnWindowGeometryChanged();
                if (_settingsService != null)
                {
                    _settingsService.Save(_settingsService.Current);
                }

                if (!_isExplicitExit)
                {
                    e.Cancel = true;
                    _appWindow.Hide();
                }
            };

            // Hook WM_SHOW_NOTIGLOW via subclassing
            _subclassProc = new NativeMethods.SubclassProc(WndProcSubclass);
            NativeMethods.SetWindowSubclass(_hwnd, _subclassProc, (UIntPtr)101, IntPtr.Zero);
            NativeMethods.AllowMessageInUIPI(_hwnd, NativeMethods.WM_SHOW_NOTIGLOW);

            // Toast Notifications Hook
            ToastNotificationService.Instance.ToastRequested += OnToastRequested;
            Closed += (s, e) =>
            {
                ToastNotificationService.Instance.ToastRequested -= OnToastRequested;
                NativeMethods.RemoveWindowSubclass(_hwnd, _subclassProc, (UIntPtr)101);
            };

            // Language & Localization Hook
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            UpdateLocalizedTexts();
            UpdateLanguageButtonVisuals();
        }

        private IntPtr WndProcSubclass(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == NativeMethods.WM_SHOW_NOTIGLOW)
            {
                LoggerService.LogInfo("Received WM_SHOW_NOTIGLOW signal. Restoring and activating MainWindow.");
                DispatcherQueue.TryEnqueue(() =>
                {
                    _appWindow.Show();
                    if (_appWindow.Presenter is OverlappedPresenter presenter)
                    {
                        if (presenter.State == OverlappedPresenterState.Minimized)
                        {
                            presenter.Restore();
                        }
                    }
                    NativeMethods.SetForegroundWindow(_hwnd);
                    Activate();
                });
                return IntPtr.Zero;
            }

            return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void OnToastRequested(ToastItem item)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ToastContainer == null) return;
                var control = new NotiGlow.UI.Controls.ToastNotificationControl(item);
                control.Dismissed += c =>
                {
                    DispatcherQueue.TryEnqueue(() => ToastContainer.Children.Remove(c));
                };

                if (ToastContainer.Children.Count >= 4)
                {
                    if (ToastContainer.Children[0] is NotiGlow.UI.Controls.ToastNotificationControl oldest)
                    {
                        oldest.Dismiss();
                    }
                }
                ToastContainer.Children.Add(control);
            });
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

            // Taskbar Visibility
            _appWindow.IsShownInSwitchers = _settingsService.Current.ShowInTaskbar;

            _settingsService.SettingsChanged += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _appWindow.IsShownInSwitchers = _settingsService.Current.ShowInTaskbar;
                    if (Microsoft.UI.Xaml.Application.Current is App app)
                    {
                        app.ApplyCurrentSettingsTheme();
                    }
                    ApplyBackdropMaterial(_settingsService.Current.WindowMaterial);
                    ApplyThemeVisuals(_settingsService.Current.ColorTheme, _settingsService.Current.ThemeMode);
                });
            };

            ThemeService.ThemeChanged += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (_settingsService != null)
                    {
                        ApplyBackdropMaterial(_settingsService.Current.WindowMaterial);
                        ApplyThemeVisuals(_settingsService.Current.ColorTheme, _settingsService.Current.ThemeMode);
                    }
                });
            };

            ApplyBackdropMaterial(_settingsService.Current.WindowMaterial);
            ApplyThemeVisuals(_settingsService.Current.ColorTheme, _settingsService.Current.ThemeMode);
            ApplyWindowGeometry(_settingsService.Current);
            NavigateToTag("General");
        }

        public void ApplyWindowGeometry(AppSettings? settings)
        {
            try
            {
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea == null) return;

                uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
                if (dpi == 0) dpi = 96;
                double dpiScale = dpi / 96.0;

                // 1. If saved geometry exists and is valid
                if (settings?.WindowWidth.HasValue == true && settings?.WindowHeight.HasValue == true &&
                    settings.WindowWidth.Value >= 600 && settings.WindowHeight.Value >= 400)
                {
                    int savedW = settings.WindowWidth.Value;
                    int savedH = settings.WindowHeight.Value;

                    // Clamp to work area so previous oversized runs don't lock the window
                    int clampedW = Math.Min(savedW, displayArea.WorkArea.Width);
                    int clampedH = Math.Min(savedH, displayArea.WorkArea.Height);
                    _appWindow.Resize(new SizeInt32(clampedW, clampedH));

                    if (settings.WindowX.HasValue && settings.WindowY.HasValue)
                    {
                        int x = settings.WindowX.Value;
                        int y = settings.WindowY.Value;
                        if (x >= displayArea.WorkArea.X - 100 &&
                            x < displayArea.WorkArea.X + displayArea.WorkArea.Width - 100 &&
                            y >= displayArea.WorkArea.Y &&
                            y < displayArea.WorkArea.Y + displayArea.WorkArea.Height - 100)
                        {
                            _appWindow.Move(new PointInt32(x, y));
                        }
                        else
                        {
                            CenterWindow(displayArea, clampedW, clampedH);
                        }
                    }
                    else
                    {
                        CenterWindow(displayArea, clampedW, clampedH);
                    }

                    if (settings.WindowMaximized && _appWindow.Presenter is OverlappedPresenter op)
                    {
                        op.Maximize();
                    }

                    _isGeometryInitialized = true;
                    return;
                }

                // 2. Default comfortable launch size: approximately 1550 x 950 physical pixels
                // AppWindow.Resize directly operates in physical screen pixels
                int targetPhysicalW = 1550;
                int targetPhysicalH = 950;

                // Ensure the window fits comfortably within the current monitor's work area
                // Maintain safety margins so it never touches or overflows the taskbar or screen edges
                int maxAllowedW = Math.Max(700, displayArea.WorkArea.Width - 48);
                int maxAllowedH = Math.Max(500, displayArea.WorkArea.Height - 48);

                // Do not exceed 92% of the work area so it does not open looking maximized on any screen
                maxAllowedW = Math.Min(maxAllowedW, (int)Math.Round(displayArea.WorkArea.Width * 0.92));
                maxAllowedH = Math.Min(maxAllowedH, (int)Math.Round(displayArea.WorkArea.Height * 0.92));

                int finalW = Math.Min(targetPhysicalW, maxAllowedW);
                int finalH = Math.Min(targetPhysicalH, maxAllowedH);

                _appWindow.Resize(new SizeInt32(finalW, finalH));
                CenterWindow(displayArea, finalW, finalH);
                _isGeometryInitialized = true;
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"ApplyWindowGeometry warning: {ex.Message}");
                _isGeometryInitialized = true;
            }
        }

        private void CenterWindow(DisplayArea displayArea, int width, int height)
        {
            int posX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
            int posY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
            _appWindow.Move(new PointInt32(Math.Max(displayArea.WorkArea.X, posX), Math.Max(displayArea.WorkArea.Y, posY)));
        }

        private void OnWindowGeometryChanged()
        {
            if (!_isGeometryInitialized || _settingsService == null || _appWindow == null) return;
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                {
                    _settingsService.Current.WindowMaximized = true;
                    return;
                }
                if (presenter.State == OverlappedPresenterState.Minimized)
                {
                    return;
                }
                _settingsService.Current.WindowMaximized = false;
                _settingsService.Current.WindowWidth = _appWindow.Size.Width;
                _settingsService.Current.WindowHeight = _appWindow.Size.Height;
                _settingsService.Current.WindowX = _appWindow.Position.X;
                _settingsService.Current.WindowY = _appWindow.Position.Y;
            }
        }

        public void ApplyBackdropMaterial(WindowMaterial material)
        {
            try
            {
                switch (material)
                {
                    case WindowMaterial.Mica:
                        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
                        break;
                    case WindowMaterial.MicaAlt:
                        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
                        break;
                    case WindowMaterial.Acrylic:
                    default:
                        SystemBackdrop = new DesktopAcrylicBackdrop();
                        break;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"ApplyBackdropMaterial warning: {ex.Message}");
                try
                {
                    SystemBackdrop = new DesktopAcrylicBackdrop();
                }
                catch { }
            }
        }

        public void ApplyThemeVisuals(ColorTheme colorTheme, ThemeMode mode)
        {
            try
            {
                bool isDark = ThemeService.GetIsDark(mode);
                if (this.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = mode switch
                    {
                        ThemeMode.Light => ElementTheme.Light,
                        ThemeMode.Dark => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    };
                }

                if (_appWindow?.TitleBar != null)
                {
                    _appWindow.TitleBar.ButtonForegroundColor = isDark ? Windows.UI.Color.FromArgb(255, 237, 237, 237) : Windows.UI.Color.FromArgb(255, 15, 15, 15);
                    _appWindow.TitleBar.ButtonHoverForegroundColor = isDark ? Windows.UI.Color.FromArgb(255, 255, 255, 255) : Windows.UI.Color.FromArgb(255, 0, 0, 0);
                    _appWindow.TitleBar.ButtonHoverBackgroundColor = isDark ? Windows.UI.Color.FromArgb(30, 255, 255, 255) : Windows.UI.Color.FromArgb(30, 0, 0, 0);
                    _appWindow.TitleBar.ButtonPressedBackgroundColor = isDark ? Windows.UI.Color.FromArgb(50, 255, 255, 255) : Windows.UI.Color.FromArgb(50, 0, 0, 0);
                    _appWindow.TitleBar.ButtonInactiveForegroundColor = isDark ? Windows.UI.Color.FromArgb(120, 237, 237, 237) : Windows.UI.Color.FromArgb(120, 15, 15, 15);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"ApplyThemeVisuals warning: {ex.Message}");
            }
        }

        private Views.ApplicationsView? _viewApplications;
        public Views.ApplicationsView ViewApplications
        {
            get
            {
                if (_viewApplications == null)
                {
                    _viewApplications = new Views.ApplicationsView { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                    if (_profileService != null && _glowManager != null)
                    {
                        _viewApplications.Initialize(_profileService, _glowManager, _settingsService);
                    }
                    _viewApplications.NavigateToCategoryRequested += OnCategoryNavigationRequested;
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
                    _viewAppearance = new Views.AppearanceView { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
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
                    _viewDisplay = new Views.DisplayView { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
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
                    _viewGaming = new Views.GamingView { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                    if (_settingsService != null && _glowManager != null)
                    {
                        _viewGaming.Initialize(_settingsService, _glowManager, _glowManager.GameDetectionService, _profileService);
                    }
                    _viewGaming.NavigateToCategoryRequested += OnCategoryNavigationRequested;
                    RootContentGrid.Children.Add(_viewGaming);
                }
                return _viewGaming;
            }
        }

        private void OnCategoryNavigationRequested(object? sender, Core.Helpers.CategoryNavigationEventArgs e)
        {
            if (Core.Helpers.AppCategoryHelper.IsGamingCategory(e.TargetCategory))
            {
                _viewApplications?.RefreshAppCards();
                NavigateToTag("Gaming");
                ViewGaming.RefreshAndHighlightGame(e.TargetIdentifier);
            }
            else
            {
                _viewGaming?.RenderTrackedGameCards();
                NavigateToTag("Applications");
                ViewApplications.RefreshAndHighlightProfile(e.TargetIdentifier);
            }
        }

        private Views.NotificationsView? _viewNotifications;
        public Views.NotificationsView ViewNotifications
        {
            get
            {
                if (_viewNotifications == null)
                {
                    _viewNotifications = new Views.NotificationsView { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
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
                    _viewAdvanced = new Views.AdvancedView { Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
                    if (_settingsService != null && _profileService != null && _glowManager != null)
                    {
                        _viewAdvanced.Initialize(_settingsService, _profileService, _glowManager);
                    }
                    RootContentGrid.Children.Add(_viewAdvanced);
                }
                return _viewAdvanced;
            }
        }

        private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string tag)
            {
                NavigateToTag(tag);
            }
        }

        private void RootNavigationView_PaneOpened(NavigationView sender, object args)
        {
            UpdateLanguageButtonDisplay(true);
        }

        private void RootNavigationView_PaneClosed(NavigationView sender, object args)
        {
            UpdateLanguageButtonDisplay(false);
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
                ViewApplications.EnsureLoaded();
            }
            if (isGaming)
            {
                ViewGaming.EnsureLoaded();
            }
        }

        private void UpdateNavSelectionVisuals(string tag)
        {
            if (RootNavigationView == null) return;

            foreach (var item in RootNavigationView.MenuItems)
            {
                if (item is NavigationViewItem navItem && string.Equals(navItem.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                {
                    RootNavigationView.SelectedItem = navItem;
                    break;
                }
            }
        }

        private void OnLanguageChanged(object? sender, string langCode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateLocalizedTexts();
                UpdateLanguageButtonVisuals();
            });
        }

        private void UpdateLocalizedTexts()
        {
            NavItemGeneral.Content = LocalizationService.Instance.GetString("Nav.General");
            NavItemApplications.Content = LocalizationService.Instance.GetString("Nav.Applications");
            NavItemAppearance.Content = LocalizationService.Instance.GetString("Nav.Appearance");
            NavItemDisplay.Content = LocalizationService.Instance.GetString("Nav.Display");
            NavItemGaming.Content = LocalizationService.Instance.GetString("Nav.Gaming");
            NavItemNotifications.Content = LocalizationService.Instance.GetString("Nav.Notifications");
            NavItemAdvanced.Content = LocalizationService.Instance.GetString("Nav.Advanced");
            TxtLanguageLabel.Text = LocalizationService.Instance.GetString("Language.Language");
        }

        public void UpdateLanguageButtonDisplay(bool isPaneOpen)
        {
            if (BtnLanguage == null) return;
            if (isPaneOpen)
            {
                TxtLanguageLabel.Visibility = Visibility.Visible;
                PnlLanguageBadge.Visibility = Visibility.Visible;
                BtnLanguage.Width = double.NaN;
                BtnLanguage.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            else
            {
                TxtLanguageLabel.Visibility = Visibility.Collapsed;
                PnlLanguageBadge.Visibility = Visibility.Collapsed;
                BtnLanguage.Width = 38;
                BtnLanguage.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }

        public void UpdateLanguageButtonVisuals()
        {
            var langItem = LocalizationService.Instance.CurrentLanguageItem;
            if (TxtCurrentLangCode != null) TxtCurrentLangCode.Text = langItem.ShortCode;
            if (TxtCurrentLangFlag != null) TxtCurrentLangFlag.Text = langItem.FlagEmoji;
            if (BtnLanguage != null)
            {
                string langName = LocalizationService.Instance.GetString("Language.Language");
                ToolTipService.SetToolTip(BtnLanguage, $"{langName}: {langItem.DisplayName} ({langItem.ShortCode})");
            }
        }

        private void LanguageItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string langCode)
            {
                LocalizationService.Instance.SetLanguage(langCode, persist: true);
            }
        }

        public void ForceExit()
        {
            _isExplicitExit = true;
            Close();
        }
    }
}
