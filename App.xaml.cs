using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using NotiGlow.Models;
using NotiGlow.Services;
using NotiGlow.UI;
using WinRT.Interop;

namespace NotiGlow
{
    public sealed partial class App : Application
    {
        private static System.Threading.Mutex? _singleInstanceMutex;
        private static bool _hasMutexOwnership = false;
        private SettingsService _settingsService = null!;
        private ProfileService _profileService = null!;
        private NotificationService _notificationService = null!;
        private GlowManager _glowManager = null!;
        private TrayService? _trayService;
        private MainWindow? _mainWindow;

        public static MainWindow? MainWindowInstance { get; private set; }
        public static MainWindow? CurrentMainWindow => MainWindowInstance;
        public static string[] StartupArgs { get; set; } = Array.Empty<string>();

        public App()
        {
            this.InitializeComponent();

            UnhandledException += (s, e) =>
            {
                LoggerService.LogStartupError("Unhandled UI Exception", e.Exception);
                bool isFatal = e.Exception is OutOfMemoryException or StackOverflowException or AccessViolationException;
                ShowExceptionDialog("Unhandled UI Exception", e.Exception, isFatal);
                if (!isFatal)
                {
                    e.Handled = true;
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LoggerService.LogStartupError("Unhandled AppDomain Exception", ex);
                    ShowExceptionDialog("Unhandled App Domain Exception", ex, isFatal: true);
                }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LoggerService.LogStartupError("Unobserved Task Exception", e.Exception);
                e.SetObserved();
            };
        }

        public App(string[] args) : this()
        {
            StartupArgs = args;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                LoggerService.LogStartupPhase("START");
                LoggerService.LogStartupPhase("Runtime initialized");

                // Single Instance Check
                const string mutexName = "NotiGlow_SingleInstance_Mutex_8697";
                const string legacyMutexName = "GlowBorder_SingleInstance_Mutex_8697";
                bool isNewInstance = false;
                try
                {
                    _singleInstanceMutex = new System.Threading.Mutex(true, mutexName, out isNewInstance);
                    if (isNewInstance)
                    {
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
                        Exit();
                        return;
                    }
                    else
                    {
                        LoggerService.LogWarning("Mutex indicated existing instance, but no running process was found. Proceeding with startup.");
                    }
                }

                // Initialize Core Services
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

                // Apply MotionPolicy
                NotiGlow.UI.Animations.MotionPolicy.Update(_settingsService.Current.ReduceMotion, _settingsService.Current.ReduceAnimations);

                // Apply Theme
                ApplyCurrentSettingsTheme();
                string lastKnownLanguage = _settingsService.Current.AppLanguage;
                _settingsService.SettingsChanged += (s, ev) =>
                {
                    ApplyCurrentSettingsTheme();
                    if (_settingsService.Current.AppLanguage != lastKnownLanguage)
                    {
                        lastKnownLanguage = _settingsService.Current.AppLanguage;
                        LocalizationService.Instance.SetLanguage(lastKnownLanguage, persist: false);
                    }
                    NotiGlow.UI.Animations.MotionPolicy.Update(_settingsService.Current.ReduceMotion, _settingsService.Current.ReduceAnimations);
                };

                // Notification Received Event
                _notificationService.NotificationReceived += (s, notification) =>
                {
                    _glowManager.TriggerNotification(notification);
                };

                // System Tray Setup
                try
                {
                    _trayService = new TrayService(
                        _settingsService,
                        OpenSettingsWindow,
                        TestAnimation,
                        ExitApplication,
                        OpenUpdateSettings
                    );
                    LoggerService.LogStartupPhase("Tray initialized");
                }
                catch (Exception ex)
                {
                    LoggerService.LogError("Failed to initialize System Tray", ex);
                    LoggerService.LogStartupPhase("Tray initialized (with error)");
                }

                bool isAutoStart = StartupArgs.Contains("--autostart");
                bool isUpdatedRestart = StartupArgs.Contains("--updated");

                // Initialize MainWindow
                _mainWindow = new MainWindow();
                MainWindowInstance = _mainWindow;
                _mainWindow.Initialize(_settingsService, _profileService, _notificationService, _glowManager);
                LoggerService.LogStartupPhase("MainWindow created");

                if (!isAutoStart)
                {
                    _mainWindow.Activate();
                    LoggerService.LogStartupPhase("MainWindow activated");
                }
                else
                {
                    LoggerService.LogInfo("Started in background mode (--autostart)");
                }

                if (isUpdatedRestart)
                {
                    _hasCheckedUpdatesThisSession = true;
                    LoggerService.LogInfo($"NotiGlow started successfully after update to version {UpdateService.CurrentVersionString} (--updated).");
                }

                // Start Notification listener asynchronously
                _ = InitializeNotificationServiceAsync();

                // Background update check if enabled
                if (!PackageService.IsPackaged && UpdateService.ShouldCheckForUpdates(_settingsService.Current, DateTime.UtcNow) && !isUpdatedRestart)
                {
                    _ = CheckForUpdatesInBackgroundAsync();
                }

                // Sync installed games in background
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
            catch { }
            return false;
        }

        private void HandleStartupFailure(string phase, Exception ex)
        {
            LoggerService.LogStartupError(phase, ex);
            ShowExceptionDialog("Startup Error", ex, isFatal: true);
            Exit();
        }

        private void ShowExceptionDialog(string title, Exception ex, bool isFatal = false)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logPath = Path.Combine(localAppData, "NotiGlow", "startup.log");

                string displayTitle = LocalizationService.Instance?.GetString(isFatal ? "Error.StartupTitle" : "Error.UnhandledTitle") ?? title;
                string displayMessage = LocalizationService.Instance?.GetString(isFatal ? "Error.StartupDesc" : "Error.UnhandledDesc")
                    ?? $"NotiGlow encountered an unexpected error: {ex.Message}";

                NotiGlow.UI.Dialogs.ErrorDialogWindow.ShowError(
                    displayTitle,
                    displayMessage,
                    ex,
                    isFatal,
                    logPath);
            }
            catch
            {
                try
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string logPath = Path.Combine(localAppData, "NotiGlow", "startup.log");
                    string shortMsg = ex.Message;
                    string message = $"NotiGlow could not start or encountered an error.\n\nError: {shortMsg}\n\nDetailed information was written to:\n{logPath}";
                    NotiGlow.UI.Dialogs.ConfirmationDialogWindow.ShowError(title, message, "OK");
                }
                catch { }
            }
        }

        public void ApplyCurrentSettingsTheme()
        {
            if (_settingsService?.Current == null) return;
            var settings = _settingsService.Current;
            ThemeService.ApplyTheme(settings.ColorTheme, settings.ThemeMode);
            _trayService?.RefreshTheme();
            _mainWindow?.ApplyBackdropMaterial(settings.WindowMaterial);
            _mainWindow?.ApplyThemeVisuals(settings.ColorTheme, settings.ThemeMode);
        }

        public void OpenSettingsWindow()
        {
            if (_mainWindow == null) return;
            _mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                _mainWindow.AppWindow.Show();
                if (_mainWindow.AppWindow.Presenter is OverlappedPresenter presenter)
                {
                    if (presenter.State == OverlappedPresenterState.Minimized)
                    {
                        presenter.Restore();
                    }
                }
                var hwnd = WindowNative.GetWindowHandle(_mainWindow);
                NotiGlow.Core.Win32.NativeMethods.SetForegroundWindow(hwnd);
                _mainWindow.Activate();
            });
        }

        private void OpenUpdateSettings()
        {
            OpenSettingsWindow();
            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                _mainWindow.NavigateToTag("Advanced");
            });
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
            Exit();
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
    }
}
