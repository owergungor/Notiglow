using System;
using System.IO;
using System.Linq;
using System.Windows;
using GlowBorder.Models;
using GlowBorder.Services;
using GlowBorder.UI;
using Wpf.Ui.Appearance;

namespace GlowBorder
{
    public partial class App : System.Windows.Application
    {
        private static System.Threading.Mutex? _singleInstanceMutex;
        private static bool _hasMutexOwnership = false;
        private SettingsService _settingsService = null!;
        private ProfileService _profileService = null!;
        private NotificationService _notificationService = null!;
        private GlowManager _glowManager = null!;
        private TrayService? _trayService;
        private MainWindow? _mainWindow;

        public App()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += (s, e) =>
            {
                LoggerService.LogStartupError("Unhandled Dispatcher Exception", e.Exception);
                ShowExceptionDialog("Unhandled UI Exception", e.Exception);
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LoggerService.LogStartupError("Unhandled AppDomain Exception", ex);
                    ShowExceptionDialog("Unhandled App Domain Exception", ex);
                }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LoggerService.LogStartupError("Unobserved Task Exception", e.Exception);
                e.SetObserved();
            };

            try
            {
                Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (_settingsService?.Current?.Theme == AppTheme.System)
                    {
                        Dispatcher?.Invoke(() => ApplyTheme(AppTheme.System));
                    }
                };
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                LoggerService.LogStartupPhase("START");
                LoggerService.LogStartupPhase("Runtime initialized");
                GlowBorder.UI.Animations.ButtonPressAnimationBehavior.InitializeGlobal();

                // Single Instance Check
                const string mutexName = "GlowBorder_SingleInstance_Mutex_8697";
                bool isNewInstance = false;
                try
                {
                    _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out isNewInstance);
                    if (isNewInstance)
                    {
                        _hasMutexOwnership = true;
                    }
                    else
                    {
                        // Try acquiring mutex if previous process exited/abandoned it
                        try
                        {
                            if (_singleInstanceMutex.WaitOne(0))
                            {
                                isNewInstance = true;
                                _hasMutexOwnership = true;
                            }
                        }
                        catch (System.Threading.AbandonedMutexException)
                        {
                            isNewInstance = true;
                            _hasMutexOwnership = true;
                            LoggerService.LogWarning("Acquired abandoned single-instance mutex.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"Mutex creation warning: {ex.Message}");
                    isNewInstance = true;
                    _hasMutexOwnership = false;
                }

                if (!isNewInstance)
                {
                    if (IsAnotherInstanceRunning())
                    {
                        LoggerService.LogInfo("Another active GlowBorder process found. Signaling existing instance and exiting.");
                        GlowBorder.Core.Win32.NativeMethods.SignalExistingInstance();
                        Shutdown();
                        return;
                    }
                    else
                    {
                        LoggerService.LogWarning("Mutex indicated existing instance, but no running process was found. Proceeding with startup.");
                    }
                }

                // Initialize Services
                LoggerService.LogInfo("Starting Glow Border application...");

                _settingsService = new SettingsService();
                LoggerService.LogStartupPhase("Settings loaded");

                _profileService = new ProfileService();
                LoggerService.LogStartupPhase("Profiles loaded");

                _glowManager = new GlowManager(_settingsService, _profileService);
                LoggerService.LogStartupPhase("Glow manager initialized");

                _notificationService = new NotificationService();

                // Apply Theme
                ApplyTheme(_settingsService.Current.Theme);
                _settingsService.SettingsChanged += (s, ev) => ApplyTheme(_settingsService.Current.Theme);

                // Wire Notification Event safely with Dispatcher check
                _notificationService.NotificationReceived += (s, notification) =>
                {
                    if (Dispatcher != null && !Dispatcher.HasShutdownStarted)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            _glowManager.TriggerNotification(notification);
                        });
                    }
                };

                // System Tray Setup
                try
                {
                    _trayService = new TrayService(
                        _settingsService,
                        OpenSettingsWindow,
                        TestAnimation,
                        ExitApplication
                    );
                    LoggerService.LogStartupPhase("Tray initialized");
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed to initialize System Tray", ex);
                    LoggerService.LogStartupPhase("Tray initialized (with error)");
                }

                bool isAutoStart = e.Args.Contains("--autostart");

                // Initialize Main Window
                _mainWindow = new MainWindow();
                _mainWindow.Initialize(_settingsService, _profileService, _notificationService, _glowManager);
                LoggerService.LogStartupPhase("MainWindow created");

                if (!isAutoStart)
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                    _mainWindow.Focus();
                    LoggerService.LogStartupPhase("MainWindow shown and activated");
                }
                else
                {
                    LoggerService.LogInfo("Started in background mode (--autostart)");
                }

                // Start notification listener asynchronously (non-blocking for app startup)
                _ = InitializeNotificationServiceAsync();

                LoggerService.LogStartupPhase("READY");
            }
            catch (Exception ex)
            {
                HandleStartupFailure("Startup Exception", ex);
            }
        }

        private async System.Threading.Tasks.Task InitializeNotificationServiceAsync()
        {
            try
            {
                await _notificationService.InitializeAsync();
                LoggerService.LogStartupPhase("Notification service initialized");
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Notification service initialization failed", ex);
                LoggerService.LogStartupPhase("Notification service initialized (failed)");
            }
        }

        private static bool IsAnotherInstanceRunning()
        {
            try
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
                foreach (var p in processes)
                {
                    if (p.Id != currentProcess.Id)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // In case process check fails, assume no other process
            }
            return false;
        }

        private void HandleStartupFailure(string phase, Exception ex)
        {
            LoggerService.LogStartupError(phase, ex);
            ShowExceptionDialog("Startup Error", ex);
            Shutdown();
        }

        private static void ShowExceptionDialog(string title, Exception ex)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logPath = Path.Combine(localAppData, "GlowBorder", "startup.log");
                string shortMsg = ex.Message;
                string message = $"Glow Border could not start or encountered an error.\n\nError: {shortMsg}\n\nDetailed information was written to:\n{logPath}";

                System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            catch
            {
                try
                {
                    System.Windows.Forms.MessageBox.Show(ex.Message, title, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
                catch
                {
                    // Fallback ignored
                }
            }
        }

        private void ApplyTheme(AppTheme theme)
        {
            try
            {
                bool isDark = theme switch
                {
                    AppTheme.Light => false,
                    AppTheme.Dark or AppTheme.LiquidGlass => true,
                    AppTheme.System or _ => GetWindowsSystemThemeIsDark()
                };

                try
                {
                    ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
                }
                catch { }

                var res = System.Windows.Application.Current.Resources;
                if (theme == AppTheme.LiquidGlass)
                {
                    SetBrush(res, "WindowBackground", "#0C0E17");
                    SetBrush(res, "SidebarBackground", "#141726");
                    SetBrush(res, "CardBackground", "#1D2136");
                    SetBrush(res, "CardBackgroundSecondary", "#171A2B");
                    SetBrush(res, "TextPrimary", "#FFFFFF");
                    SetBrush(res, "TextSecondary", "#C5CBE3");
                    SetBrush(res, "TextMuted", "#868CAE");
                    SetBrush(res, "TextDisabled", "#5A607C");
                    SetBrush(res, "BorderColor", "#2C3352");
                    SetBrush(res, "DividerColor", "#20253D");
                    SetBrush(res, "InputBackground", "#151828");
                    SetBrush(res, "InputBorder", "#2C3352");
                    SetBrush(res, "AccentColor", "#5865F2");
                    SetBrush(res, "ControlBackground", "#1E2238");
                    SetBrush(res, "ControlHoverBackground", "#2A2F4C");
                    SetBrush(res, "ControlPressedBackground", "#343A5D");
                    SetBrush(res, "ControlDisabledBackground", "#131624");
                    SetBrush(res, "GlassOverlay", "#1CFFFFFF");
                    SetBrush(res, "GlassBorder", "#455865F2");

                    SetBrush(res, "CardControlBorderBrush", "#2C3352");
                    SetBrush(res, "CardControlHeaderBorderBrush", "#20253D");
                    SetBrush(res, "CardControlSeparatorBrush", "#20253D");
                    SetBrush(res, "ComboBoxBorderBrush", "#2C3352");
                    SetBrush(res, "ComboBoxDropDownBackground", "#1D2136");
                    SetBrush(res, "ComboBoxDropDownBorderBrush", "#2C3352");
                    SetBrush(res, "NavActiveBackground", "#242A45");
                    SetBrush(res, "NavActiveHoverBackground", "#2C3354");
                    SetBrush(res, "NavIndicatorColor", "#5865F2");
                    SetBrush(res, "SliderTrackBackground", "#363E5E");
                    SetBrush(res, "SliderTrackHoverBackground", "#46507A");
                    SetBrush(res, "SliderThumbBackground", "#FFFFFF");
                    SetBrush(res, "SliderThumbBorder", "#5865F2");
                }
                else if (isDark)
                {
                    SetBrush(res, "WindowBackground", "#141414");
                    SetBrush(res, "SidebarBackground", "#181818");
                    SetBrush(res, "CardBackground", "#202020");
                    SetBrush(res, "CardBackgroundSecondary", "#1B1B1B");
                    SetBrush(res, "TextPrimary", "#FFFFFF");
                    SetBrush(res, "TextSecondary", "#B8B8B8");
                    SetBrush(res, "TextMuted", "#808080");
                    SetBrush(res, "TextDisabled", "#555555");
                    SetBrush(res, "BorderColor", "#383838");
                    SetBrush(res, "DividerColor", "#282828");
                    SetBrush(res, "InputBackground", "#1C1C1C");
                    SetBrush(res, "InputBorder", "#383838");
                    SetBrush(res, "AccentColor", "#5865F2");
                    SetBrush(res, "ControlBackground", "#262626");
                    SetBrush(res, "ControlHoverBackground", "#303030");
                    SetBrush(res, "ControlPressedBackground", "#383838");
                    SetBrush(res, "ControlDisabledBackground", "#181818");
                    SetBrush(res, "GlassOverlay", "#00000000");
                    SetBrush(res, "GlassBorder", "#383838");

                    SetBrush(res, "CardControlBorderBrush", "#383838");
                    SetBrush(res, "CardControlHeaderBorderBrush", "#282828");
                    SetBrush(res, "CardControlSeparatorBrush", "#282828");
                    SetBrush(res, "ComboBoxBorderBrush", "#383838");
                    SetBrush(res, "ComboBoxDropDownBackground", "#202020");
                    SetBrush(res, "ComboBoxDropDownBorderBrush", "#383838");
                    SetBrush(res, "NavActiveBackground", "#2E2E2E");
                    SetBrush(res, "NavActiveHoverBackground", "#363636");
                    SetBrush(res, "NavIndicatorColor", "#5865F2");
                    SetBrush(res, "SliderTrackBackground", "#404552");
                    SetBrush(res, "SliderTrackHoverBackground", "#505668");
                    SetBrush(res, "SliderThumbBackground", "#FFFFFF");
                    SetBrush(res, "SliderThumbBorder", "#5865F2");
                }
                else
                {
                    SetBrush(res, "WindowBackground", "#F5F5F7");
                    SetBrush(res, "SidebarBackground", "#F0F0F2");
                    SetBrush(res, "CardBackground", "#FFFFFF");
                    SetBrush(res, "CardBackgroundSecondary", "#F7F7F8");
                    SetBrush(res, "TextPrimary", "#1A1A1A");
                    SetBrush(res, "TextSecondary", "#5F6368");
                    SetBrush(res, "TextMuted", "#777777");
                    SetBrush(res, "TextDisabled", "#A0A0A0");
                    SetBrush(res, "BorderColor", "#D9D9DE");
                    SetBrush(res, "DividerColor", "#E2E2E7");
                    SetBrush(res, "InputBackground", "#FFFFFF");
                    SetBrush(res, "InputBorder", "#D9D9DE");
                    SetBrush(res, "AccentColor", "#5865F2");
                    SetBrush(res, "ControlBackground", "#FFFFFF");
                    SetBrush(res, "ControlHoverBackground", "#EBEBEF");
                    SetBrush(res, "ControlPressedBackground", "#E0E0E5");
                    SetBrush(res, "ControlDisabledBackground", "#F0F0F2");
                    SetBrush(res, "GlassOverlay", "#00000000");
                    SetBrush(res, "GlassBorder", "#D9D9DE");

                    SetBrush(res, "CardControlBorderBrush", "#D9D9DE");
                    SetBrush(res, "CardControlHeaderBorderBrush", "#E2E2E7");
                    SetBrush(res, "CardControlSeparatorBrush", "#E2E2E7");
                    SetBrush(res, "ComboBoxBorderBrush", "#D9D9DE");
                    SetBrush(res, "ComboBoxDropDownBackground", "#FFFFFF");
                    SetBrush(res, "ComboBoxDropDownBorderBrush", "#D9D9DE");
                    SetBrush(res, "NavActiveBackground", "#E6E6E6");
                    SetBrush(res, "NavActiveHoverBackground", "#DADAE0");
                    SetBrush(res, "NavIndicatorColor", "#5865F2");
                    SetBrush(res, "SliderTrackBackground", "#D0D3DB");
                    SetBrush(res, "SliderTrackHoverBackground", "#B8BAC6");
                    SetBrush(res, "SliderThumbBackground", "#FFFFFF");
                    SetBrush(res, "SliderThumbBorder", "#5865F2");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed applying theme", ex);
            }
        }

        private static void SetBrush(ResourceDictionary res, string key, string hexColor)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
            res[key] = new System.Windows.Media.SolidColorBrush(color);
        }

        private static bool GetWindowsSystemThemeIsDark()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val)
                {
                    return val == 0;
                }
            }
            catch { }
            return true;
        }

        public void OpenSettingsWindow()
        {
            if (_mainWindow == null) return;
            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        public void TestAnimation()
        {
            var testProfile = new AppProfile
            {
                AppId = "TestProfile",
                Name = "Glow Border Test",
                ColorHex = _settingsService.Current.DefaultColorHex,
                DurationMs = _settingsService.Current.DefaultDurationMs,
                Intensity = _settingsService.Current.DefaultIntensity,
                Thickness = _settingsService.Current.DefaultThickness,
                GlowSize = _settingsService.Current.DefaultGlowSize,
                Style = _settingsService.Current.DefaultStyle
            };

            _glowManager.TriggerProfile(testProfile);
        }

        public void ExitApplication()
        {
            CleanupResources();
            _mainWindow?.ForceExit();
            Shutdown();
        }

        private void CleanupResources()
        {
            try
            {
                _glowManager?.StopAllOverlays();
                _trayService?.Dispose();
                _notificationService?.Stop();

                if (_singleInstanceMutex != null)
                {
                    if (_hasMutexOwnership)
                    {
                        try
                        {
                            _singleInstanceMutex.ReleaseMutex();
                        }
                        catch { }
                    }
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error during application cleanup", ex);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            CleanupResources();
            LoggerService.LogInfo("NotiGlow application exited.");
            base.OnExit(e);
        }
    }
}
