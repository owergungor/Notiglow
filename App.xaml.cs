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
        private NotiGlow.Services.Ipc.WorkerSupervisor? _workerSupervisor;
        private NotiGlow.Services.Ipc.WorkerSyncCoordinator? _workerSyncCoordinator;
        private long _settingsSyncRevision = 0;
        private MainWindow? _mainWindow;

        public static App? CurrentInstance { get; private set; }
        public static bool IsRustWorkerOwningTray { get; internal set; } = false;
        public static MainWindow? MainWindowInstance { get; private set; }
        public static MainWindow? CurrentMainWindow => MainWindowInstance;
        public static string[] StartupArgs { get; set; } = Array.Empty<string>();

        public App()
        {
            CurrentInstance = this;
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
                    void HandleSettingsChanged()
                    {
                        ApplyCurrentSettingsTheme();
                        if (_settingsService.Current.AppLanguage != lastKnownLanguage)
                        {
                            lastKnownLanguage = _settingsService.Current.AppLanguage;
                            LocalizationService.Instance.SetLanguage(lastKnownLanguage, persist: false);
                        }
                        NotiGlow.UI.Animations.MotionPolicy.Update(_settingsService.Current.ReduceMotion, _settingsService.Current.ReduceAnimations);
                        if (_settingsService.Current.UseRustBackgroundWorker && _workerSyncCoordinator != null)
                        {
                            _ = _workerSyncCoordinator.BroadcastLocalSettingsChangeAsync();
                        }
                        else if (_settingsService.Current.UseRustBackgroundWorker && _workerSupervisor?.State == NotiGlow.Services.Ipc.WorkerHealthState.Healthy)
                        {
                            long rev = System.Threading.Interlocked.Increment(ref _settingsSyncRevision);
                            _ = _workerSupervisor.IpcClient.ReloadSettingsAsync(clearPendingUiSync: false, clientRevision: rev);
                        }
                    }

                    if (_mainWindow?.DispatcherQueue != null && !_mainWindow.DispatcherQueue.HasThreadAccess)
                    {
                        _mainWindow.DispatcherQueue.TryEnqueue(HandleSettingsChanged);
                    }
                    else
                    {
                        HandleSettingsChanged();
                    }
                };

                // Notification Received Event
                _notificationService.NotificationReceived += (s, notification) =>
                {
                    _glowManager.TriggerNotification(notification);
                };

                bool useRustWorker = _settingsService.Current.UseRustBackgroundWorker;
                bool useRustGameDetection = useRustWorker && _settingsService.Current.UseRustGameDetection;
                bool useRustGlowEngine = useRustWorker && _settingsService.Current.UseRustGlowEngine;
                if (useRustWorker)
                {
                    // Phase 3C/3D Dual-Run Guard: prevent C# services from duplicating Rust worker ownership.
                    IsRustWorkerOwningTray = true;
                    _notificationService.EnableExternalWorkerOwnership();
                    if (useRustGameDetection)
                    {
                        _glowManager.GameDetectionService.EnableExternalWorkerOwnership();
                    }
                }
                else
                {
                    IsRustWorkerOwningTray = false;
                    InitializeLegacyCSharpTray();
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

                if (!useRustWorker)
                {
                    // Legacy v4.2 path: start C# Notification listener asynchronously
                    _ = InitializeNotificationServiceAsync();
                }
                else
                {
                    // Phase 3C path: start/attach Rust Tray & Notification Worker with controlled fallback
                    string? customCfgDir = Environment.GetEnvironmentVariable("NOTIGLOW_CONFIG_DIR");
                    _workerSupervisor = new NotiGlow.Services.Ipc.WorkerSupervisor(
                        configDir: customCfgDir,
                        ipcOnly: false,
                        uiExecutablePath: Environment.ProcessPath)
                    {
                        AutoRestartOnCrash = true,
                        ExpectedWorkerVersion = "4.3.0"
                    };
                    _workerSyncCoordinator = new NotiGlow.Services.Ipc.WorkerSyncCoordinator(
                        _workerSupervisor,
                        _settingsService,
                        _notificationService,
                        onOpenUi: OpenSettingsWindow,
                        onOpenUpdates: OpenUpdateSettings,
                        onTestAnimation: () => _mainWindow?.DispatcherQueue?.TryEnqueue(TestAnimation),
                        onExitApp: () => _mainWindow?.DispatcherQueue?.TryEnqueue(ExitApplication),
                        gameDetectionService: _glowManager.GameDetectionService);

                    if (useRustGlowEngine)
                    {
                        _glowManager.EnableExternalGlowOwnership(
                            dispatcher: (profile, settings, isTest, activeGameId) =>
                            {
                                _ = _workerSupervisor.IpcClient.PlayGlowAsync(
                                    profile,
                                    settings,
                                    isTest: isTest,
                                    activeGameId: activeGameId);
                                return true;
                            },
                            stopAction: () => { _ = _workerSupervisor.IpcClient.StopGlowAsync(); });
                    }

                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            bool ok = await _workerSupervisor.EnsureStartedAsync().ConfigureAwait(false);
                            if (ok)
                            {
                                await _workerSyncCoordinator.SyncPendingStateFromWorkerAsync().ConfigureAwait(false);
                                _workerSyncCoordinator.StartEventPump(350);
                                LoggerService.LogInfo($"Rust Tray & Notification Worker active (PID={_workerSupervisor.WorkerPid}, Version={_workerSupervisor.ConnectedWorkerVersion}, Attached={_workerSupervisor.AttachedToExisting}).");
                            }
                            else
                            {
                                ActivateLegacyFallbackOnUiThread();
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerService.LogWarning($"Rust Worker startup fallback: {ex.Message}");
                            ActivateLegacyFallbackOnUiThread();
                        }
                    });
                }

                // Background update check if enabled
                if (!PackageService.IsPackaged && UpdateService.ShouldCheckForUpdates(_settingsService.Current, DateTime.UtcNow) && !isUpdatedRestart)
                {
                    _ = CheckForUpdatesInBackgroundAsync();
                }

                // Sync installed games in background on first run or when tracked games list is empty
                if (_settingsService.Current.TrackedGames == null || _settingsService.Current.TrackedGames.Count == 0)
                {
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
                }

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
                using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName)
                    .Concat(System.Diagnostics.Process.GetProcessesByName("GlowBorder"));
                bool found = false;
                foreach (var p in processes)
                {
                    using (p)
                    {
                        if (!found && p.Id != currentProcess.Id)
                        {
                            found = true;
                        }
                    }
                }
                return found;
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
            _mainWindow.DispatcherQueue.TryEnqueue(_mainWindow.ShowFromTray);
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

        private void InitializeLegacyCSharpTray()
        {
            if (_trayService != null) return;
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
        }

        private void ActivateLegacyFallbackOnUiThread()
        {
            LoggerService.LogWarning("Rust Background Worker unavailable; falling back to legacy C# TrayService + NotificationService + GameDetectionService + GlowManager.");
            IsRustWorkerOwningTray = false;
            _notificationService.DisableExternalWorkerOwnership();
            _glowManager?.GameDetectionService.DisableExternalWorkerOwnership();
            _glowManager?.DisableExternalGlowOwnership();
            if (_mainWindow?.DispatcherQueue != null)
            {
                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    InitializeLegacyCSharpTray();
                    _ = InitializeNotificationServiceAsync();
                });
            }
            else
            {
                _ = InitializeNotificationServiceAsync();
            }
        }

        public void CloseUiLeavingWorkerRunning()
        {
            CleanupResources(shutdownWorker: false);
            _mainWindow?.ForceExit();
            Exit();
        }

        public void ExitApplication()
        {
            CleanupResources(shutdownWorker: true);
            _mainWindow?.ForceExit();
            Exit();
        }

        private void CleanupResources(bool shutdownWorker = false)
        {
            try
            {
                _workerSyncCoordinator?.Dispose();
                _workerSyncCoordinator = null;
                _glowManager?.CloseAllOverlays();
                _trayService?.Dispose();
                _notificationService?.Stop();
                if (_workerSupervisor != null)
                {
                    try
                    {
                        if (shutdownWorker)
                        {
                            _workerSupervisor.ShutdownAsync(1500).GetAwaiter().GetResult();
                        }
                        else
                        {
                            _workerSupervisor.Detach();
                        }
                    }
                    catch { }
                    _workerSupervisor.Dispose();
                    _workerSupervisor = null;
                }

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
