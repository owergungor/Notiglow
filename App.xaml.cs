using System;
using System.IO;
using System.Linq;
using System.Windows;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI;
using Wpf.Ui.Appearance;

namespace NotiGlow
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
                NotiGlow.UI.Animations.ButtonPressAnimationBehavior.InitializeGlobal();

                // Single Instance Check
                const string mutexName = "NotiGlow_SingleInstance_Mutex_8697";
                const string legacyMutexName = "GlowBorder_SingleInstance_Mutex_8697";
                bool isNewInstance = false;
                try
                {
                    _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out isNewInstance);
                    if (isNewInstance)
                    {
                        // Check if a legacy instance is still running
                        if (System.Threading.Mutex.TryOpenExisting(legacyMutexName, out var legacyMutex))
                        {
                            legacyMutex.Dispose();
                            isNewInstance = false;
                            _hasMutexOwnership = false;
                        }
                        else
                        {
                            _hasMutexOwnership = true;
                        }
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
                        LoggerService.LogInfo("Another active NotiGlow/GlowBorder process found. Signaling existing instance and exiting.");
                        NotiGlow.Core.Win32.NativeMethods.SignalExistingInstance();
                        Shutdown();
                        return;
                    }
                    else
                    {
                        LoggerService.LogWarning("Mutex indicated existing instance, but no running process was found. Proceeding with startup.");
                    }
                }

                // Initialize Services
                LoggerService.LogInfo("Starting NotiGlow application...");

                _settingsService = new SettingsService();
                LoggerService.LogStartupPhase("Settings loaded");

                LocalizationService.Instance.Initialize(_settingsService);
                LoggerService.LogStartupPhase("Localization initialized");

                _profileService = new ProfileService();
                LoggerService.LogStartupPhase("Profiles loaded");

                _glowManager = new GlowManager(_settingsService, _profileService);
                LoggerService.LogStartupPhase("Glow manager initialized");

                _notificationService = new NotificationService();

                // Apply Theme
                ApplyTheme(_settingsService.Current.Theme);
                _settingsService.SettingsChanged += (s, ev) =>
                {
                    ApplyTheme(_settingsService.Current.Theme);
                    LocalizationService.Instance.SetLanguage(_settingsService.Current.AppLanguage, persist: false);
                };

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

                // Check for updates asynchronously in background if enabled according to frequency
                if (UpdateService.ShouldCheckForUpdates(_settingsService.Current, DateTime.UtcNow))
                {
                    _ = CheckForUpdatesInBackgroundAsync();
                }

                // Sync installed games from Steam & Epic asynchronously in background (non-blocking)
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(2500);
                        await _glowManager.GameDetectionService.ScanAndSyncTrackedGamesAsync();
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogWarning($"Background game detection sync error: {ex.Message}");
                    }
                });

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

        private static bool _hasCheckedUpdatesThisSession = false;

        private async System.Threading.Tasks.Task CheckForUpdatesInBackgroundAsync()
        {
            if (_hasCheckedUpdatesThisSession) return;
            _hasCheckedUpdatesThisSession = true;

            try
            {
                // Delay 5 seconds after startup to ensure zero impact on boot/initial render
                await System.Threading.Tasks.Task.Delay(5000);
                var updater = new UpdateService(_settingsService);
                await updater.CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Background update check failed: {ex.Message}");
            }
        }

        private static bool IsAnotherInstanceRunning()
        {
            try
            {
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName)
                    .Concat(System.Diagnostics.Process.GetProcessesByName("GlowBorder"));
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
                string logPath = Path.Combine(localAppData, "NotiGlow", "startup.log");
                string shortMsg = ex.Message;
                string message = $"NotiGlow could not start or encountered an error.\n\nError: {shortMsg}\n\nDetailed information was written to:\n{logPath}";

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
            var settings = _settingsService?.Current;
            ColorTheme colorTheme = settings?.ColorTheme ?? ColorTheme.Standard;
            NotiGlow.Models.ThemeMode mode = theme switch
            {
                AppTheme.Light => NotiGlow.Models.ThemeMode.Light,
                AppTheme.System => NotiGlow.Models.ThemeMode.System,
                AppTheme.LiquidGlass => NotiGlow.Models.ThemeMode.Dark,
                _ => NotiGlow.Models.ThemeMode.Dark
            };
            if (theme == AppTheme.LiquidGlass)
            {
                colorTheme = ColorTheme.Indigo;
            }
            ThemeService.ApplyTheme(colorTheme, mode);
        }

        public void ApplyCurrentSettingsTheme()
        {
            if (_settingsService?.Current == null) return;
            var settings = _settingsService.Current;
            ThemeService.ApplyTheme(settings.ColorTheme, settings.ThemeMode);
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
                Name = "NotiGlow Test",
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
