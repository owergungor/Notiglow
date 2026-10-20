using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public record LanguageItem(string Code, string DisplayName, string ShortCode, string FlagEmoji = "");

    public class LocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public const string DefaultLanguage = "en-US";

        public static readonly IReadOnlyList<LanguageItem> SupportedLanguages = new[]
        {
            new LanguageItem("en-US", "English", "EN", "🇬🇧"),
            new LanguageItem("es-ES", "Español", "ES", "🇪🇸"),
            new LanguageItem("fr-FR", "Français", "FR", "🇫🇷"),
            new LanguageItem("de-DE", "Deutsch", "DE", "🇩🇪"),
            new LanguageItem("tr-TR", "Türkçe", "TR", "🇹🇷")
        };

        private static readonly HashSet<string> SupportedCodes = new(
            SupportedLanguages.Select(l => l.Code),
            StringComparer.OrdinalIgnoreCase
        );

        private SettingsService? _settingsService;
        private string _currentLanguage = DefaultLanguage;
        private CultureInfo _currentCulture = new(DefaultLanguage);

        public string CurrentLanguage => _currentLanguage;
        public CultureInfo CurrentCulture => _currentCulture;
        public LanguageItem CurrentLanguageItem =>
            SupportedLanguages.FirstOrDefault(l => l.Code.Equals(_currentLanguage, StringComparison.OrdinalIgnoreCase))
            ?? SupportedLanguages[0];

        public event EventHandler<string>? LanguageChanged;

        public string this[string key] => GetString(key);

        public LocalizationService()
        {
            _currentLanguage = DefaultLanguage;
            _currentCulture = new CultureInfo(DefaultLanguage);
        }

        public void Initialize(SettingsService settingsService)
        {
            _settingsService = settingsService;
            string configuredLang = _settingsService.Current.AppLanguage;
            SetLanguage(configuredLang, persist: false);
        }

        public static bool IsSupported(string? code)
        {
            return !string.IsNullOrWhiteSpace(code) && SupportedCodes.Contains(code.Trim());
        }

        public static string NormalizeLanguageCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return DefaultLanguage;
            string trimmed = code.Trim();
            var match = SupportedLanguages.FirstOrDefault(l =>
                l.Code.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            return match != null ? match.Code : DefaultLanguage;
        }

        public void SetLanguage(string languageCode, bool persist = true)
        {
            string validatedCode = NormalizeLanguageCode(languageCode);

            _currentLanguage = validatedCode;
            try
            {
                _currentCulture = new CultureInfo(validatedCode);
            }
            catch
            {
                _currentCulture = new CultureInfo(DefaultLanguage);
            }

            if (persist && _settingsService != null)
            {
                var settings = _settingsService.Current;
                if (!string.Equals(settings.AppLanguage, validatedCode, StringComparison.OrdinalIgnoreCase))
                {
                    settings.AppLanguage = validatedCode;
                    _settingsService.Save(settings);
                }
            }

            ApplyToWpfResources();

            LanguageChanged?.Invoke(this, validatedCode);
        }

        public string GetString(string key, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            string? value = null;

            if (_translations.TryGetValue(_currentLanguage, out var dict))
            {
                dict.TryGetValue(key, out value);
            }

            if (string.IsNullOrEmpty(value) && !_currentLanguage.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                if (_translations.TryGetValue(DefaultLanguage, out var fallbackDict))
                {
                    fallbackDict.TryGetValue(key, out value);
                }
            }

            if (string.IsNullOrEmpty(value))
            {
                value = key;
            }

            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(_currentCulture, value, args);
                }
                catch
                {
                    return value;
                }
            }

            return value;
        }

        public IReadOnlyDictionary<string, string> GetRawDictionary(string languageCode)
        {
            string code = NormalizeLanguageCode(languageCode);
            if (_translations.TryGetValue(code, out var dict))
            {
                return dict;
            }
            return _translations[DefaultLanguage];
        }

        public IReadOnlyList<string> GetAllKeys()
        {
            return _translations[DefaultLanguage].Keys.ToList();
        }

        private ResourceDictionary? _activeLocalizationDictionary;

        public void ApplyToWpfResources(ResourceDictionary? target = null)
        {
            try
            {
                var currentDict = GetRawDictionary(_currentLanguage);
                var fallbackDict = _translations[DefaultLanguage];

                // Create a clean new ResourceDictionary populated with all localized strings
                var newDict = new ResourceDictionary();
                foreach (var kvp in fallbackDict)
                {
                    string key = kvp.Key;
                    string val = currentDict.TryGetValue(key, out var localizedVal) && !string.IsNullOrEmpty(localizedVal)
                        ? localizedVal
                        : kvp.Value;

                    newDict[key] = val;
                }

                if (target != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in newDict)
                    {
                        target[entry.Key] = entry.Value;
                    }
                    return;
                }

                var app = System.Windows.Application.Current;
                if (app == null) return;

                if (app.Dispatcher != null)
                {
                    if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished) return;
                    if (app.Dispatcher.Thread != null && !app.Dispatcher.Thread.IsAlive) return;

                    if (!app.Dispatcher.CheckAccess())
                    {
                        try
                        {
                            app.Dispatcher.BeginInvoke(() => ApplyToWpfResources(null));
                        }
                        catch { }
                        return;
                    }
                }

                if (app != null)
                {
                    // Swap MergedDictionaries entry to trigger comprehensive visual tree DynamicResource invalidation
                    if (_activeLocalizationDictionary != null)
                    {
                        app.Resources.MergedDictionaries.Remove(_activeLocalizationDictionary);
                    }

                    _activeLocalizationDictionary = newDict;
                    app.Resources.MergedDictionaries.Add(_activeLocalizationDictionary);

                    // Also mirror directly onto app.Resources for direct lookups
                    foreach (System.Collections.DictionaryEntry entry in newDict)
                    {
                        app.Resources[entry.Key] = entry.Value;
                    }

                    // Invalidate visual roots for any open windows to guarantee redraw
                    foreach (System.Windows.Window window in app.Windows)
                    {
                        try
                        {
                            window.InvalidateVisual();
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Failed applying localized strings to WPF resources: {ex.Message}");
            }
        }

        #region Translation Dictionaries

        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["en-US"] = new(StringComparer.Ordinal)
            {
                // App / Title
                ["App.Title"] = "NotiGlow",
                ["App.TitleBar"] = "NotiGlow {0} — Ambient Notification Utility",

                // Navigation
                ["Nav.General"] = "General",
                ["Nav.Applications"] = "Applications",
                ["Nav.Appearance"] = "Appearance",
                ["Nav.Display"] = "Display",
                ["Nav.Gaming"] = "Gaming",
                ["Nav.Notifications"] = "Notifications",
                ["Nav.Advanced"] = "Advanced",
                ["Nav.Language"] = "Language",

                // Language
                ["Language.Language"] = "Language",
                ["Language.English"] = "English",
                ["Language.Spanish"] = "Español",
                ["Language.French"] = "Français",
                ["Language.German"] = "Deutsch",
                ["Language.Turkish"] = "Türkçe",
                ["Language.SelectLanguage"] = "Select Language",
                ["Language.ChangeLanguageHelp"] = "Change application language",

                // Common Actions
                ["Common.Cancel"] = "Cancel",
                ["Common.Save"] = "Save",
                ["Common.Close"] = "Close",
                ["Common.Browse"] = "Browse...",
                ["Common.Export"] = "Export",
                ["Common.Import"] = "Import",
                ["Common.Reset"] = "Reset",
                ["Common.Delete"] = "Delete",
                ["Common.Edit"] = "Edit",
                ["Common.Duplicate"] = "Duplicate",
                ["Common.Preview"] = "Preview",
                ["Common.Active"] = "Active",
                ["Common.Inactive"] = "Inactive",
                ["Common.Enabled"] = "Enabled",
                ["Common.Disabled"] = "Disabled",
                ["Common.Yes"] = "Yes",
                ["Common.No"] = "No",
                ["Common.Ok"] = "OK",
                ["Common.Settings"] = "Settings",
                ["Common.TestAnimation"] = "Test Animation",
                ["Common.TestAnimationToolTip"] = "Test notification glow animation",

                // General View
                ["General.Title"] = "General Settings",
                ["General.Subtitle"] = "Control how NotiGlow behaves and handles ambient animations.",
                ["General.Activation"] = "NotiGlow Activation",
                ["General.ActivationDesc"] = "Enables or disables NotiGlow notification effects.",
                ["General.Listener"] = "Notification Listener",
                ["General.ListenerDesc"] = "Windows Notification Listener access status for catching app toasts.",
                ["General.ListenerActive"] = "Active & Listening",
                ["General.ListenerPermissionRequired"] = "Permission Required",
                ["General.ListenerStopped"] = "Service Stopped",
                ["General.OpenSettings"] = "Open Settings",
                ["General.StartWithWindows"] = "Start with Windows",
                ["General.StartWithWindowsDesc"] = "Automatically starts NotiGlow when you sign in to Windows.",
                ["General.ReduceAnimations"] = "Reduce Animations",
                ["General.ReduceAnimationsDesc"] = "Reduces animation effects to make the interface and notifications less dynamic.",
                ["General.ThemeMode"] = "Theme Mode",
                ["General.ThemeModeDesc"] = "Choose between Windows system preference, Light, or Dark mode.",
                ["General.ThemeSystem"] = "System",
                ["General.ThemeLight"] = "Light",
                ["General.ThemeDark"] = "Dark",
                ["General.ThemeSystemToolTip"] = "Follow Windows system theme",
                ["General.ThemeLightToolTip"] = "Force Light mode",
                ["General.ThemeDarkToolTip"] = "Force Dark mode",
                ["General.ColorTheme"] = "Color Theme",
                ["General.ColorThemeDesc"] = "Curated color systems with tailored Light and Dark variants.",
                ["General.Language"] = "Language",
                ["General.LanguageDesc"] = "Choose the display language for NotiGlow.",

                // Appearance View
                ["Appearance.Title"] = "Appearance",
                ["Appearance.Subtitle"] = "Configure global default animation parameters and preview screen glow.",
                ["Appearance.DefaultStyle"] = "Default Animation Style",
                ["Appearance.DefaultStyleDesc"] = "Fallback motion effect for newly added app profiles.",
                ["Appearance.DefaultDuration"] = "Default Duration",
                ["Appearance.DefaultIntensity"] = "Default Glow Intensity",
                ["Appearance.DefaultGlowSize"] = "Default Glow Blur Size",
                ["Appearance.DefaultColor"] = "Default Glow Color",
                ["Appearance.LivePreview"] = "Live Glow Preview",

                // Notifications View
                ["Notifications.Title"] = "Notifications",
                ["Notifications.Subtitle"] = "Control how NotiGlow behaves when rapid multiple notifications arrive within a short time window.",
                ["Notifications.Restart"] = "Restart Animation (Default)",
                ["Notifications.RestartDesc"] = "Immediately resets the animation timer and restarts the glow animation from 0 ms for the new notification.",
                ["Notifications.Extend"] = "Extend Active Animation",
                ["Notifications.ExtendDesc"] = "Extends the ongoing animation duration so the glow remains visible continuously during notification bursts.",
                ["Notifications.Queue"] = "Queue Notifications",
                ["Notifications.QueueDesc"] = "Queues incoming notifications and plays glow animations sequentially one after another.",
                ["Notifications.Ignore"] = "Ignore While Animating",
                ["Notifications.IgnoreDesc"] = "Ignores new incoming notifications while a glow animation is already playing.",

                // Display View
                ["Display.Title"] = "Display",
                ["Display.Subtitle"] = "Choose which displays show the glow border animation when notifications trigger.",
                ["Display.MonitorFormat"] = "Monitor {0}: {1}",
                ["Display.PrimaryBadge"] = "(Primary)",
                ["Display.ResolutionFormat"] = "Resolution: {0} x {1} | BitsPerPixel: {2}",
                ["Display.ActiveMonitor"] = "Active Monitor",
                ["Display.ActiveMonitorDesc"] = "Show glow on the monitor containing the active window / mouse cursor focus.",
                ["Display.PrimaryMonitor"] = "Primary Monitor (Default)",
                ["Display.PrimaryMonitorDesc"] = "Always use the primary Windows display for notification glow animations.",
                ["Display.AllMonitors"] = "All Monitors",
                ["Display.AllMonitorsDesc"] = "Show glow edge animations on every connected display simultaneously.",
                ["Display.DetectedMonitors"] = "Detected Monitors",

                // Gaming View
                ["Gaming.Title"] = "Gaming",
                ["Gaming.Subtitle"] = "Automatically reduce glow intensity or suppress alerts while gaming.",
                ["Gaming.TestGameAnimation"] = "Test Game Animation",
                ["Gaming.TestGameAnimationToolTip"] = "Test tracked game glow animation behavior",
                ["Gaming.Mode"] = "Gaming Mode",
                ["Gaming.ModeDesc"] = "Enables gaming-specific notification and performance behavior.",
                ["Gaming.BehaviorRules"] = "In-Game Behavior Rules",
                ["Gaming.GlowDuringGames"] = "Glow During Games",
                ["Gaming.GlowDuringGamesDesc"] = "Allows notification glow effects to appear while a game is running.",
                ["Gaming.ReduceIntensity"] = "Reduce Intensity in Games",
                ["Gaming.ReduceIntensityDesc"] = "Reduces glow brightness while gaming to minimize distraction.",
                ["Gaming.ReduceDuration"] = "Reduce Duration in Games",
                ["Gaming.ReduceDurationDesc"] = "Shortens glow animation duration while gaming.",
                ["Gaming.OnlyImportant"] = "Only Show Important Notifications",
                ["Gaming.OnlyImportantDesc"] = "Shows only high-priority notifications while gaming.",
                ["Gaming.TrackedGames"] = "Tracked Game Executables",
                ["Gaming.DetectionSupport"] = "Steam & Epic Games automatic detection supported.",
                ["Gaming.ScanLaunchers"] = "Scan Launchers",
                ["Gaming.ScanLaunchersToolTip"] = "Scan for Steam and Epic Games automatically",
                ["Gaming.AddGame"] = "Add a Game",
                ["Gaming.AddGameToolTip"] = "Select game executable via file browser",
                ["Gaming.RemoveGameToolTip"] = "Remove game",
                ["Gaming.ScanningGames"] = "Scanning Steam & Epic Games...",
                ["Gaming.FoundGames"] = "Found and added {0} new games to tracked list.",
                ["Gaming.NoNewGames"] = "No new games found (games list is up to date).",
                ["Gaming.ScanError"] = "Game scan encountered an issue.",
                ["Gaming.SubSettingsDisabledToolTip"] = "This setting cannot be applied while Glow During Games is disabled.",
                ["Gaming.SelectExecutableDialogTitle"] = "Select Game Executable",
                ["Gaming.EmptyTrackedGamesWarning"] = "Add a tracked game executable to test game animation.",
                ["Gaming.ListView"] = "List View",
                ["Gaming.GridView"] = "Grid View",
                ["Gaming.PerGameOverrides"] = "Per-Game Glow Overrides",
                ["Gaming.InheritGlobal"] = "Inherit Global Defaults",
                ["Gaming.CustomOverrides"] = "Custom Overrides",
                ["Gaming.EditGameSettings"] = "Edit Game Settings",
                ["Gaming.GameExecutable"] = "Game Executable",
                ["Gaming.GlowDuration"] = "Glow Duration",
                ["Gaming.GlowIntensity"] = "Glow Intensity",
                ["Gaming.GlowBlurSize"] = "Glow Blur Size",
                ["Gaming.InheritsGlobalDefault"] = "Inherits global default ({0})",
                ["Gaming.NoTrackedGames"] = "No tracked games yet. Scan launchers or add games manually.",
                ["Gaming.InheritsGlobal"] = "Global",
                ["Gaming.ResetToDefault"] = "Reset to Default",
                ["Gaming.CustomBadge"] = "Custom",
                ["Gaming.GamingAppsSectionTitle"] = "Gaming Apps",
                ["Gaming.GamingAppsSectionDesc"] = "Detected and tracked games configured in the Gaming tab",
                ["Gaming.CustomGlow"] = "Custom Glow",
                ["Gaming.CustomGlowToolTip"] = "Toggle between per-game custom glow and global gaming settings",

                // Applications View
                ["Applications.Title"] = "Tracked Applications",
                ["Applications.Subtitle"] = "Manage custom glow colors, durations, priorities, and animation styles per application",
                ["Applications.ListView"] = "List View",
                ["Applications.GridView"] = "Grid View",
                ["Applications.AddApplication"] = "Add Application",
                ["Applications.AddApplicationToolTip"] = "Add a new custom application profile",
                ["Applications.EditProfile"] = "Edit App Profile",
                ["Applications.NewProfile"] = "New App Profile",
                ["Applications.AppName"] = "Application Name",
                ["Applications.AppNamePlaceholder"] = "e.g. Discord",
                ["Applications.AppId"] = "App Identifier / Executable Name",
                ["Applications.AppIdPlaceholder"] = "e.g. discord or discord.exe",
                ["Applications.BrowseToolTip"] = "Select executable file (.exe)",
                ["Applications.AnimationEffect"] = "Animation Style",
                ["Applications.Priority"] = "Notification Priority",
                ["Applications.PriorityLow"] = "Low Priority",
                ["Applications.PriorityNormal"] = "Normal Priority",
                ["Applications.PriorityHigh"] = "High Priority (Bypasses Gaming Mode filter)",
                ["Applications.Duration"] = "Duration (ms)",
                ["Applications.Intensity"] = "Glow Intensity",
                ["Applications.GlowSize"] = "Glow Blur Size",
                ["Applications.Color"] = "Glow Color",
                ["Applications.LivePreview"] = "Live Preview",
                ["Applications.SaveProfile"] = "Save Profile",
                ["Applications.Cancel"] = "Cancel",
                ["Applications.ValidationMsg"] = "Please enter valid Application Name and App Identifier.",
                ["Applications.ValidationTitle"] = "Validation Error",
                ["Applications.ConfirmDeleteMsg"] = "Are you sure you want to remove profile for {0}?",
                ["Applications.ConfirmDeleteTitle"] = "Confirm Delete",
                ["Applications.ProfileCreatedMsg"] = "Profile '{0}' created!",
                ["Applications.ProfileCreatedTitle"] = "Profile Duplicated",
                ["Applications.InvalidFileMsg"] = "Selected executable is invalid.",
                ["Applications.InvalidFileTitle"] = "Invalid File Selection",
                ["Applications.EmptyState"] = "No applications configured yet. Click 'Add Application' to get started.",

                // Advanced View
                ["Advanced.Title"] = "Advanced",
                ["Advanced.Subtitle"] = "Performance tuning, OLED burn-in protection, diagnostic tools, and backup options.",
                ["Advanced.AutoUpdate"] = "Auto Update",
                ["Advanced.AutomaticUpdates"] = "Automatic Updates",
                ["Advanced.CurrentVersionFormat"] = "Current Version: v{0} • Automatic update checking via GitHub Releases",
                ["Advanced.LastCheckFormat"] = "Current: v{0} • Last check: {1}",
                ["Advanced.CheckForUpdates"] = "Check for Updates",
                ["Advanced.AutoCheckUpdatesToolTip"] = "Automatically check for updates",
                ["Advanced.CheckFrequency"] = "Check Frequency",
                ["Advanced.CheckFrequencyDesc"] = "Choose how often NotiGlow checks for newer releases.",
                ["Advanced.FreqStartup"] = "On Startup",
                ["Advanced.FreqDaily"] = "Daily",
                ["Advanced.FreqWeekly"] = "Weekly",
                ["Advanced.FreqMonthly"] = "Monthly",
                ["Advanced.CheckingUpdates"] = "Checking for updates...",
                ["Advanced.UpdateAvailableTitle"] = "Update Available",
                ["Advanced.UpdateAvailableMsg"] = "A new version (v{0}) of NotiGlow is available!\n\nWould you like to download, verify, and automatically install the update now?",
                ["Advanced.UpdateReadyInstalling"] = "Update ready. Installing...",
                ["Advanced.ReadyToInstallTitle"] = "Ready to Install",
                ["Advanced.ReadyToInstallMsg"] = "NotiGlow v{0} downloaded and verified successfully!\n\nThe application will now close, apply the update, and automatically restart. Proceed?",
                ["Advanced.UpToDate"] = "NotiGlow v{0} is up to date.",
                ["Advanced.UpdateCheckFailed"] = "Update check failed. Please check your network connection and try again.",
                ["Advanced.Accessibility"] = "Accessibility",
                ["Advanced.OledMode"] = "OLED Friendly Mode",
                ["Advanced.OledModeDesc"] = "Reduces static bright elements to help minimize OLED image retention.",
                ["Advanced.ReduceMotion"] = "Reduce Motion",
                ["Advanced.ReduceMotionDesc"] = "Reduces animated movement throughout notification effects.",
                ["Advanced.ReduceGlow"] = "Reduce Glow",
                ["Advanced.ReduceGlowDesc"] = "Reduces glow brightness across notification effects.",
                ["Advanced.BackupRestore"] = "Backup & Restore",
                ["Advanced.ConfigManagement"] = "Configuration Management",
                ["Advanced.ConfigManagementDesc"] = "Export profiles to JSON file, import backups, or restore factory defaults.",
                ["Advanced.ExportSettings"] = "Export Settings",
                ["Advanced.ImportSettings"] = "Import Settings",
                ["Advanced.FactoryReset"] = "Factory Reset",
                ["Advanced.Diagnostics"] = "Diagnostics",
                ["Advanced.DebugLogging"] = "Debug Logging",
                ["Advanced.DebugLoggingDesc"] = "Records diagnostic information to help troubleshoot application issues.",
                ["Advanced.IdentityDebug"] = "Notification Identity Debug",
                ["Advanced.IdentityDebugDesc"] = "Shows additional information used to identify notification sources.",
                ["Advanced.ExportSuccessMsg"] = "Settings successfully exported!",
                ["Advanced.ExportSuccessTitle"] = "Export Complete",
                ["Advanced.ExportFailMsg"] = "Failed to export settings.",
                ["Advanced.ExportFailTitle"] = "Export Error",
                ["Advanced.ImportSuccessMsg"] = "Settings successfully imported!",
                ["Advanced.ImportSuccessTitle"] = "Import Complete",
                ["Advanced.ImportFailMsg"] = "Failed to import settings. Invalid file format.",
                ["Advanced.ImportFailTitle"] = "Import Error",
                ["Advanced.ResetConfirmMsg"] = "Reset all NotiGlow settings and application profiles to default?",
                ["Advanced.ResetConfirmTitle"] = "Confirm Reset",
                ["Advanced.ResetCompleteMsg"] = "NotiGlow has been reset to default settings.",
                ["Advanced.ResetCompleteTitle"] = "Reset Complete",

                // Styles
                ["Style.Pulse"] = "Pulse",
                ["Style.Sweep"] = "Sweep",
                ["Style.Ambient"] = "Ambient",
                ["Style.Comet"] = "Comet",
                ["Style.Ripple"] = "Ripple",
                ["Style.PulseDetailed"] = "Pulse (Uniform Opacity Pulse)",
                ["Style.SweepDetailed"] = "Sweep (Moving Light Perimeter)",
                ["Style.AmbientDetailed"] = "Ambient (Soft Breathing Glow)",
                ["Style.CometDetailed"] = "Comet (Travelling Light Head & Trail)",
                ["Style.RippleDetailed"] = "Ripple (Expanding Corner Glow Wave)",

                ["Applications.CardStyle"] = "Style",
                ["Applications.CardPriority"] = "Priority",
                ["Applications.AppGlowColor"] = "App Glow Color",
                ["ColorPicker.Palette"] = "Glow Color Palette",
                ["ColorPicker.RgbRainbowTooltip"] = "RGB Rainbow Spectrum Neon Effect",
                ["ColorPicker.ToggleCustom"] = "Custom Hex Color...",
                ["ColorPicker.Hue"] = "Hue",
                ["ColorPicker.PreviewTooltip"] = "Current Color Preview",
                ["ColorPicker.CopyHexTooltip"] = "Copy Hex Code",
                ["EdgePreview.SimulatedGlow"] = "Simulated Screen Edge Glow",
                // Toast Notifications
                ["Toast.Dismiss"] = "Dismiss",
                ["Toast.Retry"] = "Retry",
                ["Toast.OpenSettings"] = "Open Settings",
                ["Toast.Error"] = "Error",
                ["Toast.Warning"] = "Warning",
                ["Toast.Success"] = "Success",
                ["Toast.Info"] = "Information",

                ["ColorPicker.RgbSpectrum"] = "RGB Spectrum",
                ["ColorPicker.RgbToolTip"] = "RGB Rainbow Spectrum Neon Effect",
                ["ColorPicker.CustomHex"] = "Custom Hex Color...",
                ["ColorPicker.CustomHexToolTip"] = "Enter or edit custom hex color code",

                // In-App Error Dialog
                ["Error.StartupTitle"] = "NotiGlow could not start",
                ["Error.StartupDesc"] = "A critical error occurred while starting the application.",
                ["Error.UnhandledTitle"] = "An unexpected error occurred",
                ["Error.UnhandledDesc"] = "NotiGlow encountered an unexpected issue.",
                ["Error.Details"] = "Details",
                ["Error.CopyLogPath"] = "Copy Log Path",
                ["Error.CopyDetails"] = "Copy Details",
                ["Error.Copied"] = "Copied!",
                ["Error.Close"] = "Close",
                ["Error.Retry"] = "Retry",
                ["Error.LogLocation"] = "Log file location:",

                // Tray
                ["Tray.GlowEnabled"] = "✓ Glow Enabled",
                ["Tray.GamingMode"] = "🎮 Gaming Mode",
                ["Tray.TestAnimation"] = "✨ Test Animation",
                ["Tray.Tooltip"] = "NotiGlow - Ambient Notification Utility",

                // Update Dialog
                ["UpdateDialog.Title"] = "Update Available",
                ["UpdateDialog.AvailableTitle"] = "Update Available",
                ["UpdateDialog.CurrentVersionLabel"] = "CURRENT VERSION",
                ["UpdateDialog.NewVersionLabel"] = "NEW VERSION",
                ["UpdateDialog.Description"] = "A new version of NotiGlow is available. Would you like to download and install this update now?",
                ["UpdateDialog.ReleaseNotes"] = "Release Notes",
                ["UpdateDialog.UpdateNow"] = "Update Now",
                ["UpdateDialog.BtnUpdate"] = "Update Now",
                ["UpdateDialog.Cancel"] = "Cancel",
                ["UpdateDialog.BtnCancel"] = "Cancel",

                // v2.4 Features
                ["Appearance.CornerRounding"] = "Corner Rounding",
                ["Appearance.CornerRoundingDesc"] = "Adjust the corner curvature radius for UI cards, inputs, and controls.",
                ["Applications.ScanApps"] = "Scan Installed Apps",
                ["Applications.ScanAppsToolTip"] = "Scan and detect installed applications on this PC",
                ["Applications.ScanComplete"] = "Application scan complete. Found {0} installed apps.",
                ["Gaming.CommonSettingsTitle"] = "Common Gaming Glow Settings",
                ["Gaming.CommonSettingsDesc"] = "Unified duration, glow intensity, and blur size applied across all tracked games.",
                ["Gaming.CommonDuration"] = "Duration",
                ["Gaming.CommonIntensity"] = "Glow Intensity",
                ["Gaming.CommonBlurSize"] = "Glow Blur Size"
            },

            ["es-ES"] = new(StringComparer.Ordinal)
            {
                // App / Title
                ["App.Title"] = "NotiGlow",
                ["App.TitleBar"] = "NotiGlow {0} — Utilidad de Notificación Ambiental",

                // Navigation
                ["Nav.General"] = "General",
                ["Nav.Applications"] = "Aplicaciones",
                ["Nav.Appearance"] = "Apariencia",
                ["Nav.Display"] = "Pantalla",
                ["Nav.Gaming"] = "Juegos",
                ["Nav.Notifications"] = "Notificaciones",
                ["Nav.Advanced"] = "Avanzado",
                ["Nav.Language"] = "Idioma",

                // Language
                ["Language.Language"] = "Idioma",
                ["Language.English"] = "English",
                ["Language.Spanish"] = "Español",
                ["Language.French"] = "Français",
                ["Language.German"] = "Deutsch",
                ["Language.Turkish"] = "Türkçe",
                ["Language.SelectLanguage"] = "Seleccionar idioma",
                ["Language.ChangeLanguageHelp"] = "Cambiar el idioma de la aplicación",

                // Common Actions
                ["Common.Cancel"] = "Cancelar",
                ["Common.Save"] = "Guardar",
                ["Common.Close"] = "Cerrar",
                ["Common.Browse"] = "Examinar...",
                ["Common.Export"] = "Exportar",
                ["Common.Import"] = "Importar",
                ["Common.Reset"] = "Restablecer",
                ["Common.Delete"] = "Eliminar",
                ["Common.Edit"] = "Editar",
                ["Common.Duplicate"] = "Duplicar",
                ["Common.Preview"] = "Vista previa",
                ["Common.Active"] = "Activo",
                ["Common.Inactive"] = "Inactivo",
                ["Common.Enabled"] = "Habilitado",
                ["Common.Disabled"] = "Deshabilitado",
                ["Common.Yes"] = "Sí",
                ["Common.No"] = "No",
                ["Common.Ok"] = "Aceptar",
                ["Common.Settings"] = "Configuración",
                ["Common.TestAnimation"] = "Probar animación",
                ["Common.TestAnimationToolTip"] = "Probar animación de brillo de notificación",

                // General View
                ["General.Title"] = "Configuración General",
                ["General.Subtitle"] = "Controle el comportamiento de NotiGlow y las animaciones ambientales.",
                ["General.Activation"] = "Activación de NotiGlow",
                ["General.ActivationDesc"] = "Habilita o deshabilita los efectos de notificación de NotiGlow.",
                ["General.Listener"] = "Detector de Notificaciones",
                ["General.ListenerDesc"] = "Estado de acceso al detector de notificaciones de Windows para capturar avisos.",
                ["General.ListenerActive"] = "Activo y escuchando",
                ["General.ListenerPermissionRequired"] = "Permiso requerido",
                ["General.ListenerStopped"] = "Servicio detenido",
                ["General.OpenSettings"] = "Abrir configuración",
                ["General.StartWithWindows"] = "Iniciar con Windows",
                ["General.StartWithWindowsDesc"] = "Inicia NotiGlow automáticamente al iniciar sesión en Windows.",
                ["General.ReduceAnimations"] = "Reducir animaciones",
                ["General.ReduceAnimationsDesc"] = "Reduce los efectos visuales para hacer la interfaz menos dinámica.",
                ["General.ThemeMode"] = "Modo de Tema",
                ["General.ThemeModeDesc"] = "Elija entre la preferencia del sistema Windows, modo Claro u Oscuro.",
                ["General.ThemeSystem"] = "Sistema",
                ["General.ThemeLight"] = "Claro",
                ["General.ThemeDark"] = "Oscuro",
                ["General.ThemeSystemToolTip"] = "Seguir tema del sistema Windows",
                ["General.ThemeLightToolTip"] = "Forzar modo claro",
                ["General.ThemeDarkToolTip"] = "Forzar modo oscuro",
                ["General.ColorTheme"] = "Tema de Color",
                ["General.ColorThemeDesc"] = "Sistemas de color seleccionados con variantes clara y oscura.",
                ["General.Language"] = "Idioma",
                ["General.LanguageDesc"] = "Elija el idioma de visualización para NotiGlow.",

                // Appearance View
                ["Appearance.Title"] = "Apariencia",
                ["Appearance.Subtitle"] = "Configure los parámetros globales de animación y previsualice el brillo.",
                ["Appearance.DefaultStyle"] = "Estilo de Animación Predeterminado",
                ["Appearance.DefaultStyleDesc"] = "Efecto de movimiento para perfiles de aplicación nuevos.",
                ["Appearance.DefaultDuration"] = "Duración Predeterminada",
                ["Appearance.DefaultIntensity"] = "Intensidad de Brillo Predeterminada",
                ["Appearance.DefaultGlowSize"] = "Tamaño de Desenfoque Predeterminado",
                ["Appearance.DefaultColor"] = "Color de Brillo Predeterminado",
                ["Appearance.LivePreview"] = "Vista Previa en Vivo",

                // Notifications View
                ["Notifications.Title"] = "Notificaciones",
                ["Notifications.Subtitle"] = "Controle cómo se comporta NotiGlow ante ráfagas de notificaciones continuas.",
                ["Notifications.Restart"] = "Reiniciar Animación (Predeterminado)",
                ["Notifications.RestartDesc"] = "Reinicia inmediatamente el temporizador y la animación para la nueva notificación.",
                ["Notifications.Extend"] = "Extender Animación Activa",
                ["Notifications.ExtendDesc"] = "Extiende la duración actual para que el brillo continúe durante ráfagas de avisos.",
                ["Notifications.Queue"] = "Poner en Cola Notificaciones",
                ["Notifications.QueueDesc"] = "Encola las notificaciones entrantes y reproduce las animaciones una tras otra.",
                ["Notifications.Ignore"] = "Ignorar Durante Animación",
                ["Notifications.IgnoreDesc"] = "Ignora las nuevas notificaciones entrantes si ya se está reproduciendo una animación.",

                // Display View
                ["Display.Title"] = "Pantalla",
                ["Display.Subtitle"] = "Elija qué monitores muestran la animación de brillo al llegar notificaciones.",
                ["Display.MonitorFormat"] = "Monitor {0}: {1}",
                ["Display.PrimaryBadge"] = "(Principal)",
                ["Display.ResolutionFormat"] = "Resolución: {0} x {1} | BitsPorPíxel: {2}",
                ["Display.ActiveMonitor"] = "Monitor Activo",
                ["Display.ActiveMonitorDesc"] = "Muestra el brillo en el monitor que contiene la ventana activa o el cursor.",
                ["Display.PrimaryMonitor"] = "Monitor Principal (Predeterminado)",
                ["Display.PrimaryMonitorDesc"] = "Usa siempre la pantalla principal de Windows para el brillo de notificaciones.",
                ["Display.AllMonitors"] = "Todos los Monitores",
                ["Display.AllMonitorsDesc"] = "Muestra efectos de brillo en todas las pantallas conectadas simultáneamente.",
                ["Display.DetectedMonitors"] = "Monitores Detectados",

                // Gaming View
                ["Gaming.Title"] = "Juegos",
                ["Gaming.Subtitle"] = "Reduzca automáticamente el brillo o suprima alertas mientras juega.",
                ["Gaming.TestGameAnimation"] = "Probar Animación del Juego",
                ["Gaming.TestGameAnimationToolTip"] = "Probar el comportamiento del brillo en juegos rastreados",
                ["Gaming.Mode"] = "Modo Juego",
                ["Gaming.ModeDesc"] = "Habilita el comportamiento de notificaciones y rendimiento específico para juegos.",
                ["Gaming.BehaviorRules"] = "Reglas de Comportamiento en Juegos",
                ["Gaming.GlowDuringGames"] = "Brillo Durante Juegos",
                ["Gaming.GlowDuringGamesDesc"] = "Permite que los efectos de brillo aparezcan mientras se ejecuta un juego.",
                ["Gaming.ReduceIntensity"] = "Reducir Intensidad en Juegos",
                ["Gaming.ReduceIntensityDesc"] = "Reduce el brillo durante las partidas para minimizar distracciones.",
                ["Gaming.ReduceDuration"] = "Reducir Duración en Juegos",
                ["Gaming.ReduceDurationDesc"] = "Acorta la duración de la animación de brillo en juegos.",
                ["Gaming.OnlyImportant"] = "Solo Notificaciones Importantes",
                ["Gaming.OnlyImportantDesc"] = "Muestra solo notificaciones de alta prioridad mientras juega.",
                ["Gaming.TrackedGames"] = "Ejecutables de Juegos Rastreados",
                ["Gaming.DetectionSupport"] = "Compatible con detección automática de Steam y Epic Games.",
                ["Gaming.ScanLaunchers"] = "Escanear Lanzadores",
                ["Gaming.ScanLaunchersToolTip"] = "Escanear automáticamente juegos de Steam y Epic Games",
                ["Gaming.AddGame"] = "Añadir un Juego",
                ["Gaming.AddGameToolTip"] = "Seleccionar ejecutable de juego mediante el explorador de archivos",
                ["Gaming.RemoveGameToolTip"] = "Eliminar juego",
                ["Gaming.ScanningGames"] = "Escaneando Steam y Epic Games...",
                ["Gaming.FoundGames"] = "Se encontraron y añadieron {0} juegos nuevos.",
                ["Gaming.NoNewGames"] = "No se encontraron juegos nuevos (la lista está actualizada).",
                ["Gaming.ScanError"] = "El escaneo de juegos encontró un problema.",
                ["Gaming.SubSettingsDisabledToolTip"] = "Esta opción no se puede aplicar si Brillo Durante Juegos está deshabilitado.",
                ["Gaming.SelectExecutableDialogTitle"] = "Seleccionar ejecutable de juego",
                ["Gaming.EmptyTrackedGamesWarning"] = "Añada un ejecutable de juego rastreado para probar la animación.",
                ["Gaming.ListView"] = "Vista de lista",
                ["Gaming.GridView"] = "Vista de cuadrícula",
                ["Gaming.PerGameOverrides"] = "Anulaciones de brillo por juego",
                ["Gaming.InheritGlobal"] = "Heredar valores globales",
                ["Gaming.CustomOverrides"] = "Personalizado",
                ["Gaming.EditGameSettings"] = "Editar configuración del juego",
                ["Gaming.GameExecutable"] = "Ejecutable del juego",
                ["Gaming.GlowDuration"] = "Duración del brillo",
                ["Gaming.GlowIntensity"] = "Intensidad de brillo",
                ["Gaming.GlowBlurSize"] = "Tamaño de desenfoque",
                ["Gaming.InheritsGlobalDefault"] = "Hereda el valor global ({0})",
                ["Gaming.NoTrackedGames"] = "No hay juegos rastreados todavía. Escanee lanzadores o añada juegos manualmente.",
                ["Gaming.InheritsGlobal"] = "Global",
                ["Gaming.ResetToDefault"] = "Restablecer a valores predeterminados",
                ["Gaming.CustomBadge"] = "Personalizado",
                ["Gaming.GamingAppsSectionTitle"] = "Aplicaciones de Juegos",
                ["Gaming.GamingAppsSectionDesc"] = "Juegos detectados y rastreados configurados en la pestaña Juegos",
                ["Gaming.CustomGlow"] = "Brillo personalizado",
                ["Gaming.CustomGlowToolTip"] = "Alternar entre brillo personalizado del juego y configuración global",

                // Applications View
                ["Applications.Title"] = "Aplicaciones Rastreadas",
                ["Applications.Subtitle"] = "Administre colores, duraciones, prioridades y estilos de animación por aplicación",
                ["Applications.ListView"] = "Vista de lista",
                ["Applications.GridView"] = "Vista de cuadrícula",
                ["Applications.AddApplication"] = "Añadir Aplicación",
                ["Applications.AddApplicationToolTip"] = "Añadir un nuevo perfil de aplicación personalizada",
                ["Applications.EditProfile"] = "Editar Perfil de Aplicación",
                ["Applications.NewProfile"] = "Nuevo Perfil de Aplicación",
                ["Applications.AppName"] = "Nombre de la Aplicación",
                ["Applications.AppNamePlaceholder"] = "ej. Discord",
                ["Applications.AppId"] = "Identificador / Nombre del Ejecutable",
                ["Applications.AppIdPlaceholder"] = "ej. discord o discord.exe",
                ["Applications.BrowseToolTip"] = "Seleccionar archivo ejecutable (.exe)",
                ["Applications.AnimationEffect"] = "Estilo de Animación",
                ["Applications.Priority"] = "Prioridad de Notificación",
                ["Applications.PriorityLow"] = "Prioridad Baja",
                ["Applications.PriorityNormal"] = "Prioridad Normal",
                ["Applications.PriorityHigh"] = "Prioridad Alta (Omite filtro del Modo Juego)",
                ["Applications.Duration"] = "Duración (ms)",
                ["Applications.Intensity"] = "Intensidad de Brillo",
                ["Applications.GlowSize"] = "Tamaño de Desenfoque",
                ["Applications.Color"] = "Color de Brillo",
                ["Applications.LivePreview"] = "Vista Previa en Vivo",
                ["Applications.SaveProfile"] = "Guardar Perfil",
                ["Applications.Cancel"] = "Cancelar",
                ["Applications.ValidationMsg"] = "Ingrese un nombre de aplicación e identificador válidos.",
                ["Applications.ValidationTitle"] = "Error de Validación",
                ["Applications.ConfirmDeleteMsg"] = "¿Está seguro de eliminar el perfil de {0}?",
                ["Applications.ConfirmDeleteTitle"] = "Confirmar Eliminación",
                ["Applications.ProfileCreatedMsg"] = "¡Perfil '{0}' creado!",
                ["Applications.ProfileCreatedTitle"] = "Perfil Duplicado",
                ["Applications.InvalidFileMsg"] = "El ejecutable seleccionado no es válido.",
                ["Applications.InvalidFileTitle"] = "Selección de Archivo Inválida",
                ["Applications.EmptyState"] = "No hay aplicaciones configuradas aún. Haga clic en 'Añadir Aplicación' para comenzar.",

                // Advanced View
                ["Advanced.Title"] = "Avanzado",
                ["Advanced.Subtitle"] = "Rendimiento, protección OLED, diagnósticos y opciones de copia de seguridad.",
                ["Advanced.AutoUpdate"] = "Actualización Automática",
                ["Advanced.AutomaticUpdates"] = "Actualizaciones Automáticas",
                ["Advanced.CurrentVersionFormat"] = "Versión actual: v{0} • Comprobación automática mediante GitHub Releases",
                ["Advanced.LastCheckFormat"] = "Actual: v{0} • Última comprobación: {1}",
                ["Advanced.CheckForUpdates"] = "Buscar Actualizaciones",
                ["Advanced.AutoCheckUpdatesToolTip"] = "Buscar actualizaciones automáticamente",
                ["Advanced.CheckFrequency"] = "Frecuencia de Comprobación",
                ["Advanced.CheckFrequencyDesc"] = "Elija con qué frecuencia NotiGlow busca nuevas versiones.",
                ["Advanced.FreqStartup"] = "Al Iniciar",
                ["Advanced.FreqDaily"] = "Diariamente",
                ["Advanced.FreqWeekly"] = "Semanalmente",
                ["Advanced.FreqMonthly"] = "Mensualmente",
                ["Advanced.CheckingUpdates"] = "Buscando actualizaciones...",
                ["Advanced.UpdateAvailableTitle"] = "Actualización Disponible",
                ["Advanced.UpdateAvailableMsg"] = "¡Una nueva versión (v{0}) de NotiGlow está disponible!\n\n¿Desea descargar, verificar e instalar la actualización ahora?",
                ["Advanced.UpdateReadyInstalling"] = "Actualización lista. Instalando...",
                ["Advanced.ReadyToInstallTitle"] = "Listo para Instalar",
                ["Advanced.ReadyToInstallMsg"] = "¡NotiGlow v{0} se descargó y verificó con éxito!\n\nLa aplicación se cerrará, aplicará la actualización y se reiniciará automáticamente. ¿Continuar?",
                ["Advanced.UpToDate"] = "NotiGlow v{0} está actualizado.",
                ["Advanced.UpdateCheckFailed"] = "Error al buscar actualizaciones. Compruebe su conexión a internet e inténtelo de nuevo.",
                ["Advanced.Accessibility"] = "Accesibilidad",
                ["Advanced.OledMode"] = "Modo Compatible con OLED",
                ["Advanced.OledModeDesc"] = "Reduce elementos brillantes estáticos para minimizar la retención de imagen en pantallas OLED.",
                ["Advanced.ReduceMotion"] = "Reducir Movimiento",
                ["Advanced.ReduceMotionDesc"] = "Reduce el movimiento animado en los efectos de notificación.",
                ["Advanced.ReduceGlow"] = "Reducir Brillo",
                ["Advanced.ReduceGlowDesc"] = "Reduce el nivel de brillo de los efectos de notificación.",
                ["Advanced.BackupRestore"] = "Copia de Seguridad y Restauración",
                ["Advanced.ConfigManagement"] = "Gestión de Configuración",
                ["Advanced.ConfigManagementDesc"] = "Exporte perfiles a JSON, importe copias o restablezca la configuración de fábrica.",
                ["Advanced.ExportSettings"] = "Exportar Configuración",
                ["Advanced.ImportSettings"] = "Importar Configuración",
                ["Advanced.FactoryReset"] = "Restablecimiento de Fábrica",
                ["Advanced.Diagnostics"] = "Diagnósticos",
                ["Advanced.DebugLogging"] = "Registro de Depuración",
                ["Advanced.DebugLoggingDesc"] = "Registra datos diagnósticos para ayudar a resolver problemas.",
                ["Advanced.IdentityDebug"] = "Depuración de Identidad de Notificaciones",
                ["Advanced.IdentityDebugDesc"] = "Muestra información técnica para identificar el origen de las notificaciones.",
                ["Advanced.ExportSuccessMsg"] = "¡Configuración exportada con éxito!",
                ["Advanced.ExportSuccessTitle"] = "Exportación Completa",
                ["Advanced.ExportFailMsg"] = "Error al exportar la configuración.",
                ["Advanced.ExportFailTitle"] = "Error de Exportación",
                ["Advanced.ImportSuccessMsg"] = "¡Configuración importada con éxito!",
                ["Advanced.ImportSuccessTitle"] = "Importación Completa",
                ["Advanced.ImportFailMsg"] = "Error al importar configuración. Formato de archivo inválido.",
                ["Advanced.ImportFailTitle"] = "Error de Importación",
                ["Advanced.ResetConfirmMsg"] = "¿Restablecer todas las configuraciones y perfiles de NotiGlow a los valores predeterminados?",
                ["Advanced.ResetConfirmTitle"] = "Confirmar Restablecimiento",
                ["Advanced.ResetCompleteMsg"] = "NotiGlow ha sido restablecido a la configuración predeterminada.",
                ["Advanced.ResetCompleteTitle"] = "Restablecimiento Completo",

                // Styles
                ["Style.Pulse"] = "Pulso",
                ["Style.Sweep"] = "Barrido",
                ["Style.Ambient"] = "Ambiental",
                ["Style.Comet"] = "Cometa",
                ["Style.Ripple"] = "Ondulación",
                ["Style.PulseDetailed"] = "Pulso (Pulso de opacidad uniforme)",
                ["Style.SweepDetailed"] = "Barrido (Perímetro de luz en movimiento)",
                ["Style.AmbientDetailed"] = "Ambiental (Brillo suave tipo respiración)",
                ["Style.CometDetailed"] = "Cometa (Cabeza luminosa y estela)",
                ["Style.RippleDetailed"] = "Ondulación (Onda de brillo expansiva)",

                ["Applications.CardStyle"] = "Estilo",
                ["Applications.CardPriority"] = "Prioridad",
                ["Applications.AppGlowColor"] = "Color de brillo de la app",
                ["ColorPicker.Palette"] = "Paleta de colores de brillo",
                ["ColorPicker.RgbRainbowTooltip"] = "Efecto Neón Espectro Arcoíris RGB",
                ["ColorPicker.ToggleCustom"] = "Color hexadecimal personalizado...",
                ["ColorPicker.Hue"] = "Tono",
                ["ColorPicker.PreviewTooltip"] = "Vista previa del color actual",
                ["ColorPicker.CopyHexTooltip"] = "Copiar código hexadecimal",
                ["EdgePreview.SimulatedGlow"] = "Brillo simulado en los bordes de la pantalla",
                // Toast Notifications
                ["Toast.Dismiss"] = "Descartar",
                ["Toast.Retry"] = "Reintentar",
                ["Toast.OpenSettings"] = "Abrir Configuración",
                ["Toast.Error"] = "Error",
                ["Toast.Warning"] = "Advertencia",
                ["Toast.Success"] = "Éxito",
                ["Toast.Info"] = "Información",

                ["ColorPicker.RgbSpectrum"] = "Espectro RGB",
                ["ColorPicker.RgbToolTip"] = "Efecto Neón Espectro Arcoíris RGB",
                ["ColorPicker.CustomHex"] = "Color Hex Personalizado...",
                ["ColorPicker.CustomHexToolTip"] = "Introducir o editar código de color hexadecimal personalizado",

                // In-App Error Dialog
                ["Error.StartupTitle"] = "NotiGlow no pudo iniciarse",
                ["Error.StartupDesc"] = "Ocurrió un error crítico al iniciar la aplicación.",
                ["Error.UnhandledTitle"] = "Ocurrió un error inesperado",
                ["Error.UnhandledDesc"] = "NotiGlow encontró un problema inesperado.",
                ["Error.Details"] = "Detalles",
                ["Error.CopyLogPath"] = "Copiar ruta del registro",
                ["Error.CopyDetails"] = "Copiar detalles",
                ["Error.Copied"] = "¡Copiado!",
                ["Error.Close"] = "Cerrar",
                ["Error.Retry"] = "Reintentar",
                ["Error.LogLocation"] = "Ubicación del archivo de registro:",

                // Tray
                ["Tray.GlowEnabled"] = "✓ Brillo Habilitado",
                ["Tray.GamingMode"] = "🎮 Modo Juego",
                ["Tray.TestAnimation"] = "✨ Probar Animación",
                ["Tray.Tooltip"] = "NotiGlow - Utilidad de Notificación Ambiental",

                // Update Dialog
                ["UpdateDialog.Title"] = "Actualización disponible",
                ["UpdateDialog.AvailableTitle"] = "Actualización disponible",
                ["UpdateDialog.CurrentVersionLabel"] = "VERSIÓN ACTUAL",
                ["UpdateDialog.NewVersionLabel"] = "NUEVA VERSIÓN",
                ["UpdateDialog.Description"] = "Una nueva versión de NotiGlow está disponible. ¿Desea descargar e instalar esta actualización ahora?",
                ["UpdateDialog.ReleaseNotes"] = "Notas de la versión",
                ["UpdateDialog.UpdateNow"] = "Actualizar ahora",
                ["UpdateDialog.BtnUpdate"] = "Actualizar ahora",
                ["UpdateDialog.Cancel"] = "Cancelar",
                ["UpdateDialog.BtnCancel"] = "Cancelar",

                // v2.4 Features
                ["Appearance.CornerRounding"] = "Redondeo de esquinas",
                ["Appearance.CornerRoundingDesc"] = "Ajuste el radio de curvatura de esquinas para tarjetas, entradas y controles.",
                ["Applications.ScanApps"] = "Escanear aplicaciones",
                ["Applications.ScanAppsToolTip"] = "Escanear y detectar aplicaciones instaladas en esta PC",
                ["Applications.ScanComplete"] = "Escaneo completado. Se encontraron {0} aplicaciones instaladas.",
                ["Gaming.CommonSettingsTitle"] = "Configuración global de brillo para juegos",
                ["Gaming.CommonSettingsDesc"] = "Duración unificada, intensidad de brillo y desenfoque aplicados a todos los juegos.",
                ["Gaming.CommonDuration"] = "Duración",
                ["Gaming.CommonIntensity"] = "Intensidad de brillo",
                ["Gaming.CommonBlurSize"] = "Tamaño de desenfoque de brillo"
            },

            ["fr-FR"] = new(StringComparer.Ordinal)
            {
                // App / Title
                ["App.Title"] = "NotiGlow",
                ["App.TitleBar"] = "NotiGlow {0} — Utilitaire de Notification Ambiante",

                // Navigation
                ["Nav.General"] = "Général",
                ["Nav.Applications"] = "Applications",
                ["Nav.Appearance"] = "Apparence",
                ["Nav.Display"] = "Affichage",
                ["Nav.Gaming"] = "Jeux",
                ["Nav.Notifications"] = "Notifications",
                ["Nav.Advanced"] = "Avancé",
                ["Nav.Language"] = "Langue",

                // Language
                ["Language.Language"] = "Langue",
                ["Language.English"] = "English",
                ["Language.Spanish"] = "Español",
                ["Language.French"] = "Français",
                ["Language.German"] = "Deutsch",
                ["Language.Turkish"] = "Türkçe",
                ["Language.SelectLanguage"] = "Sélectionner la langue",
                ["Language.ChangeLanguageHelp"] = "Changer la langue de l'application",

                // Common Actions
                ["Common.Cancel"] = "Annuler",
                ["Common.Save"] = "Enregistrer",
                ["Common.Close"] = "Fermer",
                ["Common.Browse"] = "Parcourir...",
                ["Common.Export"] = "Exporter",
                ["Common.Import"] = "Importer",
                ["Common.Reset"] = "Réinitialiser",
                ["Common.Delete"] = "Supprimer",
                ["Common.Edit"] = "Modifier",
                ["Common.Duplicate"] = "Dupliquer",
                ["Common.Preview"] = "Aperçu",
                ["Common.Active"] = "Actif",
                ["Common.Inactive"] = "Inactif",
                ["Common.Enabled"] = "Activé",
                ["Common.Disabled"] = "Désactivé",
                ["Common.Yes"] = "Oui",
                ["Common.No"] = "Non",
                ["Common.Ok"] = "OK",
                ["Common.Settings"] = "Paramètres",
                ["Common.TestAnimation"] = "Tester l'animation",
                ["Common.TestAnimationToolTip"] = "Tester l'animation d'effet lumineux de notification",

                // General View
                ["General.Title"] = "Paramètres Généraux",
                ["General.Subtitle"] = "Contrôlez le fonctionnement de NotiGlow et les animations ambiantes.",
                ["General.Activation"] = "Activation de NotiGlow",
                ["General.ActivationDesc"] = "Active ou désactive les effets de notification de NotiGlow.",
                ["General.Listener"] = "Écouteur de Notifications",
                ["General.ListenerDesc"] = "État d'accès à l'écouteur de notifications Windows pour intercepter les toasts.",
                ["General.ListenerActive"] = "Actif et à l'écoute",
                ["General.ListenerPermissionRequired"] = "Autorisation requise",
                ["General.ListenerStopped"] = "Service arrêté",
                ["General.OpenSettings"] = "Ouvrir les paramètres",
                ["General.StartWithWindows"] = "Démarrer avec Windows",
                ["General.StartWithWindowsDesc"] = "Lance NotiGlow automatiquement lors de la connexion à Windows.",
                ["General.ReduceAnimations"] = "Réduire les animations",
                ["General.ReduceAnimationsDesc"] = "Atténue les effets pour rendre l'interface et les notifications moins dynamiques.",
                ["General.ThemeMode"] = "Mode de Thème",
                ["General.ThemeModeDesc"] = "Choisissez entre la préférence Windows, le mode Clair ou Sombre.",
                ["General.ThemeSystem"] = "Système",
                ["General.ThemeLight"] = "Clair",
                ["General.ThemeDark"] = "Sombre",
                ["General.ThemeSystemToolTip"] = "Suivre le thème système de Windows",
                ["General.ThemeLightToolTip"] = "Forcer le mode clair",
                ["General.ThemeDarkToolTip"] = "Forcer le mode sombre",
                ["General.ColorTheme"] = "Thème de Couleur",
                ["General.ColorThemeDesc"] = "Palettes de couleurs harmonieuses avec déclinaisons claires et sombres.",
                ["General.Language"] = "Langue",
                ["General.LanguageDesc"] = "Choisissez la langue d'affichage pour NotiGlow.",

                // Appearance View
                ["Appearance.Title"] = "Apparence",
                ["Appearance.Subtitle"] = "Configurez les paramètres par défaut et visualisez l'effet lumineux.",
                ["Appearance.DefaultStyle"] = "Style d'Animation par Défaut",
                ["Appearance.DefaultStyleDesc"] = "Effet de mouvement appliqué aux nouveaux profils d'application.",
                ["Appearance.DefaultDuration"] = "Durée par Défaut",
                ["Appearance.DefaultIntensity"] = "Intensité Lumineuse par Défaut",
                ["Appearance.DefaultGlowSize"] = "Taille du Flou par Défaut",
                ["Appearance.DefaultColor"] = "Couleur Lumineuse par Défaut",
                ["Appearance.LivePreview"] = "Aperçu en Direct",

                // Notifications View
                ["Notifications.Title"] = "Notifications",
                ["Notifications.Subtitle"] = "Définissez le comportement de NotiGlow lors de l'arrivée rapide de plusieurs notifications.",
                ["Notifications.Restart"] = "Redémarrer l'Animation (Par défaut)",
                ["Notifications.RestartDesc"] = "Réinitialise immédiatement le chronomètre et relance l'animation pour la nouvelle alerte.",
                ["Notifications.Extend"] = "Prolonger l'Animation Active",
                ["Notifications.ExtendDesc"] = "Prolonge la durée en cours pour maintenir l'effet lumineux pendant les rafales d'alertes.",
                ["Notifications.Queue"] = "Mettre en File d'Attente",
                ["Notifications.QueueDesc"] = "Met en attente les notifications entrantes et joue les animations successivement.",
                ["Notifications.Ignore"] = "Ignorer Pendant l'Animation",
                ["Notifications.IgnoreDesc"] = "Ignore les nouvelles notifications lorsqu'une animation est déjà en cours.",

                // Display View
                ["Display.Title"] = "Affichage",
                ["Display.Subtitle"] = "Choisissez les écrans qui affichent l'effet lumineux lors des notifications.",
                ["Display.MonitorFormat"] = "Moniteur {0}: {1}",
                ["Display.PrimaryBadge"] = "(Principal)",
                ["Display.ResolutionFormat"] = "Résolution: {0} x {1} | BitsParPixel: {2}",
                ["Display.ActiveMonitor"] = "Écran Actif",
                ["Display.ActiveMonitorDesc"] = "Affiche l'effet sur l'écran contenant la fenêtre active ou le curseur.",
                ["Display.PrimaryMonitor"] = "Écran Principal (Par défaut)",
                ["Display.PrimaryMonitorDesc"] = "Utilise toujours l'écran principal de Windows pour les effets lumineux.",
                ["Display.AllMonitors"] = "Tous les Écrans",
                ["Display.AllMonitorsDesc"] = "Affiche l'effet lumineux sur tous les écrans connectés simultanément.",
                ["Display.DetectedMonitors"] = "Écrans Détectés",

                // Gaming View
                ["Gaming.Title"] = "Jeux",
                ["Gaming.Subtitle"] = "Réduisez l'intensité lumineuse ou masquez les alertes pendant vos sessions de jeu.",
                ["Gaming.TestGameAnimation"] = "Tester l'Animation de Jeu",
                ["Gaming.TestGameAnimationToolTip"] = "Tester le comportement lumineux en jeu configuré",
                ["Gaming.Mode"] = "Mode Jeu",
                ["Gaming.ModeDesc"] = "Active les comportements spécifiques de performance et de notifications en jeu.",
                ["Gaming.BehaviorRules"] = "Règles de Comportement en Jeu",
                ["Gaming.GlowDuringGames"] = "Effet Lumineux Pendant les Jeux",
                ["Gaming.GlowDuringGamesDesc"] = "Autorise l'affichage de l'effet lumineux lorsqu'un jeu est en cours.",
                ["Gaming.ReduceIntensity"] = "Réduire l'Intensité en Jeu",
                ["Gaming.ReduceIntensityDesc"] = "Diminue la luminosité en jeu afin de limiter les distractions visuelles.",
                ["Gaming.ReduceDuration"] = "Réduire la Durée en Jeu",
                ["Gaming.ReduceDurationDesc"] = "Raccourcit la durée de l'animation lumineuse pendant vos jeux.",
                ["Gaming.OnlyImportant"] = "Uniquement les Notifications Importantes",
                ["Gaming.OnlyImportantDesc"] = "Affiche seulement les notifications prioritaires pendant les jeux.",
                ["Gaming.TrackedGames"] = "Exécutables de Jeux Suivis",
                ["Gaming.DetectionSupport"] = "Détection automatique Steam et Epic Games prise en charge.",
                ["Gaming.ScanLaunchers"] = "Analyser les Lanceurs",
                ["Gaming.ScanLaunchersToolTip"] = "Rechercher automatiquement les jeux Steam et Epic Games",
                ["Gaming.AddGame"] = "Ajouter un Jeu",
                ["Gaming.AddGameToolTip"] = "Sélectionner un exécutable de jeu via l'explorateur",
                ["Gaming.RemoveGameToolTip"] = "Supprimer le jeu",
                ["Gaming.ScanningGames"] = "Recherche des jeux Steam et Epic Games en cours...",
                ["Gaming.FoundGames"] = "{0} nouveaux jeux trouvés et ajoutés.",
                ["Gaming.NoNewGames"] = "Aucun nouveau jeu trouvé (la liste est à jour).",
                ["Gaming.ScanError"] = "Une anomalie s'est produite lors de l'analyse.",
                ["Gaming.SubSettingsDisabledToolTip"] = "Ce paramètre ne s'applique pas quand l'effet en jeu est désactivé.",
                ["Gaming.SelectExecutableDialogTitle"] = "Sélectionner l'exécutable du jeu",
                ["Gaming.EmptyTrackedGamesWarning"] = "Ajoutez un exécutable de jeu suivi pour tester l'animation.",
                ["Gaming.ListView"] = "Vue liste",
                ["Gaming.GridView"] = "Vue grille",
                ["Gaming.PerGameOverrides"] = "Remplacements d'effet par jeu",
                ["Gaming.InheritGlobal"] = "Hériter des valeurs globales",
                ["Gaming.CustomOverrides"] = "Personnalisé",
                ["Gaming.EditGameSettings"] = "Modifier les paramètres du jeu",
                ["Gaming.GameExecutable"] = "Exécutable du jeu",
                ["Gaming.GlowDuration"] = "Durée de l'effet",
                ["Gaming.GlowIntensity"] = "Intensité lumineuse",
                ["Gaming.GlowBlurSize"] = "Taille du flou",
                ["Gaming.InheritsGlobalDefault"] = "Hérite de la valeur globale ({0})",
                ["Gaming.NoTrackedGames"] = "Aucun jeu suivi pour le moment. Analysez les lanceurs ou ajoutez des jeux manuellement.",
                ["Gaming.InheritsGlobal"] = "Global",
                ["Gaming.ResetToDefault"] = "Rétablir les valeurs par défaut",
                ["Gaming.CustomBadge"] = "Personnalisé",
                ["Gaming.GamingAppsSectionTitle"] = "Applications de Jeu",
                ["Gaming.GamingAppsSectionDesc"] = "Jeux détectés et suivis configurés dans l'onglet Jeux",
                ["Gaming.CustomGlow"] = "Effet personnalisé",
                ["Gaming.CustomGlowToolTip"] = "Basculer entre effet personnalisé et paramètres globaux",

                // Applications View
                ["Applications.Title"] = "Applications Suivies",
                ["Applications.Subtitle"] = "Personnalisez les couleurs, durées, priorités et styles d'animation par application",
                ["Applications.ListView"] = "Vue liste",
                ["Applications.GridView"] = "Vue grille",
                ["Applications.AddApplication"] = "Ajouter une Application",
                ["Applications.AddApplicationToolTip"] = "Créer un profil d'application personnalisée",
                ["Applications.EditProfile"] = "Modifier le Profil d'Application",
                ["Applications.NewProfile"] = "Nouveau Profil d'Application",
                ["Applications.AppName"] = "Nom de l'Application",
                ["Applications.AppNamePlaceholder"] = "ex. Discord",
                ["Applications.AppId"] = "Identifiant / Nom de l'Exécutable",
                ["Applications.AppIdPlaceholder"] = "ex. discord ou discord.exe",
                ["Applications.BrowseToolTip"] = "Sélectionner un fichier exécutable (.exe)",
                ["Applications.AnimationEffect"] = "Style d'Animation",
                ["Applications.Priority"] = "Priorité de la Notification",
                ["Applications.PriorityLow"] = "Priorité Basse",
                ["Applications.PriorityNormal"] = "Priorité Normale",
                ["Applications.PriorityHigh"] = "Priorité Haute (Ignore le filtre du Mode Jeu)",
                ["Applications.Duration"] = "Durée (ms)",
                ["Applications.Intensity"] = "Intensité Lumineuse",
                ["Applications.GlowSize"] = "Taille du Flou",
                ["Applications.Color"] = "Couleur Lumineuse",
                ["Applications.LivePreview"] = "Aperçu en Direct",
                ["Applications.SaveProfile"] = "Enregistrer le Profil",
                ["Applications.Cancel"] = "Annuler",
                ["Applications.ValidationMsg"] = "Veuillez renseigner un nom et un identifiant d'application valides.",
                ["Applications.ValidationTitle"] = "Erreur de Validation",
                ["Applications.ConfirmDeleteMsg"] = "Voulez-vous vraiment supprimer le profil de {0} ?",
                ["Applications.ConfirmDeleteTitle"] = "Confirmer la Suppression",
                ["Applications.ProfileCreatedMsg"] = "Profil '{0}' créé !",
                ["Applications.ProfileCreatedTitle"] = "Profil Dupliqué",
                ["Applications.InvalidFileMsg"] = "Le fichier exécutable sélectionné est invalide.",
                ["Applications.InvalidFileTitle"] = "Sélection de Fichier Invalide",
                ["Applications.EmptyState"] = "Aucune application configurée. Cliquez sur 'Ajouter une Application' pour commencer.",

                // Advanced View
                ["Advanced.Title"] = "Avancé",
                ["Advanced.Subtitle"] = "Performances, préservation OLED, diagnostics et gestion des sauvegardes.",
                ["Advanced.AutoUpdate"] = "Mise à Jour Automatique",
                ["Advanced.AutomaticUpdates"] = "Mises à Jour Automatiques",
                ["Advanced.CurrentVersionFormat"] = "Version actuelle : v{0} • Recherche automatique via GitHub Releases",
                ["Advanced.LastCheckFormat"] = "Actuelle : v{0} • Dernière vérification : {1}",
                ["Advanced.CheckForUpdates"] = "Vérifier les Mises à Jour",
                ["Advanced.AutoCheckUpdatesToolTip"] = "Vérifier automatiquement les mises à jour",
                ["Advanced.CheckFrequency"] = "Fréquence de Vérification",
                ["Advanced.CheckFrequencyDesc"] = "Définissez la fréquence à laquelle NotiGlow recherche de nouvelles versions.",
                ["Advanced.FreqStartup"] = "Au Démarrage",
                ["Advanced.FreqDaily"] = "Tous les Jours",
                ["Advanced.FreqWeekly"] = "Toutes les Semaines",
                ["Advanced.FreqMonthly"] = "Tous les Mois",
                ["Advanced.CheckingUpdates"] = "Vérification des mises à jour...",
                ["Advanced.UpdateAvailableTitle"] = "Mise à Jour Disponible",
                ["Advanced.UpdateAvailableMsg"] = "Une nouvelle version (v{0}) de NotiGlow est disponible !\n\nSouhaitez-vous la télécharger, la vérifier et l'installer maintenant ?",
                ["Advanced.UpdateReadyInstalling"] = "Mise à jour prête. Installation en cours...",
                ["Advanced.ReadyToInstallTitle"] = "Prêt pour l'Installation",
                ["Advanced.ReadyToInstallMsg"] = "NotiGlow v{0} a été téléchargé et validé avec succès !\n\nL'application va se fermer, appliquer la mise à jour et redémarrer automatiquement. Continuer ?",
                ["Advanced.UpToDate"] = "NotiGlow v{0} est à jour.",
                ["Advanced.UpdateCheckFailed"] = "Échec de la recherche de mises à jour. Veuillez vérifier votre connexion réseau et réessayer.",
                ["Advanced.Accessibility"] = "Accessibilité",
                ["Advanced.OledMode"] = "Mode Éco OLED",
                ["Advanced.OledModeDesc"] = "Atténue les éléments lumineux continus pour prévenir les brûlures d'écran OLED.",
                ["Advanced.ReduceMotion"] = "Réduire les Mouvements",
                ["Advanced.ReduceMotionDesc"] = "Diminue les déplacements animés dans les effets de notification.",
                ["Advanced.ReduceGlow"] = "Réduire la Luminosité",
                ["Advanced.ReduceGlowDesc"] = "Diminue l'intensité globale des reflets lumineux.",
                ["Advanced.BackupRestore"] = "Sauvegarde et Restauration",
                ["Advanced.ConfigManagement"] = "Gestion de la Configuration",
                ["Advanced.ConfigManagementDesc"] = "Exportez vos profils en JSON, restaurez vos sauvegardes ou réinitialisez les paramètres.",
                ["Advanced.ExportSettings"] = "Exporter les Paramètres",
                ["Advanced.ImportSettings"] = "Importer les Paramètres",
                ["Advanced.FactoryReset"] = "Réinitialisation d'Usine",
                ["Advanced.Diagnostics"] = "Diagnostics",
                ["Advanced.DebugLogging"] = "Journal de Débogage",
                ["Advanced.DebugLoggingDesc"] = "Enregistre des informations techniques pour faciliter le dépannage.",
                ["Advanced.IdentityDebug"] = "Débogage d'Identité des Notifications",
                ["Advanced.IdentityDebugDesc"] = "Affiche des détails avancés pour identifier l'origine des notifications.",
                ["Advanced.ExportSuccessMsg"] = "Paramètres exportés avec succès !",
                ["Advanced.ExportSuccessTitle"] = "Exportation Réussie",
                ["Advanced.ExportFailMsg"] = "Échec de l'exportation des paramètres.",
                ["Advanced.ExportFailTitle"] = "Erreur d'Exportation",
                ["Advanced.ImportSuccessMsg"] = "Paramètres importés avec succès !",
                ["Advanced.ImportSuccessTitle"] = "Importation Réussie",
                ["Advanced.ImportFailMsg"] = "Échec de l'importation. Format de fichier invalide.",
                ["Advanced.ImportFailTitle"] = "Erreur d'Importation",
                ["Advanced.ResetConfirmMsg"] = "Réinitialiser tous les paramètres et profils NotiGlow aux valeurs par défaut ?",
                ["Advanced.ResetConfirmTitle"] = "Confirmer la Réinitialisation",
                ["Advanced.ResetCompleteMsg"] = "NotiGlow a été réinitialisé aux paramètres d'usine.",
                ["Advanced.ResetCompleteTitle"] = "Réinitialisation Terminée",

                // Styles
                ["Style.Pulse"] = "Pulsation",
                ["Style.Sweep"] = "Balayage",
                ["Style.Ambient"] = "Ambiant",
                ["Style.Comet"] = "Comète",
                ["Style.Ripple"] = "Ondulation",
                ["Style.PulseDetailed"] = "Pulsation (Variation uniforme d'opacité)",
                ["Style.SweepDetailed"] = "Balayage (Lumière tournante sur le contour)",
                ["Style.AmbientDetailed"] = "Ambiant (Respiration lumineuse douce)",
                ["Style.CometDetailed"] = "Comète (Tête lumineuse avec traînée)",
                ["Style.RippleDetailed"] = "Ondulation (Vague d'expansion depuis les angles)",

                ["Applications.CardStyle"] = "Style",
                ["Applications.CardPriority"] = "Priorité",
                ["Applications.AppGlowColor"] = "Couleur de l'effet d'application",
                ["ColorPicker.Palette"] = "Palette de couleurs de l'effet",
                ["ColorPicker.RgbRainbowTooltip"] = "Effet néon spectre arc-en-ciel RGB",
                ["ColorPicker.ToggleCustom"] = "Couleur hexadécimale personnalisée...",
                ["ColorPicker.Hue"] = "Teinte",
                ["ColorPicker.PreviewTooltip"] = "Aperçu de la couleur actuelle",
                ["ColorPicker.CopyHexTooltip"] = "Copier le code hexadécimal",
                ["EdgePreview.SimulatedGlow"] = "Simulation du halo sur les bords de l'écran",
                // Toast Notifications
                ["Toast.Dismiss"] = "Fermer",
                ["Toast.Retry"] = "Réessayer",
                ["Toast.OpenSettings"] = "Ouvrir les Paramètres",
                ["Toast.Error"] = "Erreur",
                ["Toast.Warning"] = "Avertissement",
                ["Toast.Success"] = "Succès",
                ["Toast.Info"] = "Information",

                ["ColorPicker.RgbSpectrum"] = "Spectre RVB",
                ["ColorPicker.RgbToolTip"] = "Effet néon spectre arc-en-ciel RVB",
                ["ColorPicker.CustomHex"] = "Couleur Hex Personnalisée...",
                ["ColorPicker.CustomHexToolTip"] = "Saisir ou modifier le code de couleur hexadécimal personnalisé",

                // In-App Error Dialog
                ["Error.StartupTitle"] = "NotiGlow n'a pas pu démarrer",
                ["Error.StartupDesc"] = "Une erreur critique s'est produite lors du démarrage de l'application.",
                ["Error.UnhandledTitle"] = "Une erreur inattendue est survenue",
                ["Error.UnhandledDesc"] = "NotiGlow a rencontré un problème inattendu.",
                ["Error.Details"] = "Détails",
                ["Error.CopyLogPath"] = "Copier le chemin du journal",
                ["Error.CopyDetails"] = "Copier les détails",
                ["Error.Copied"] = "Copié !",
                ["Error.Close"] = "Fermer",
                ["Error.Retry"] = "Réessayer",
                ["Error.LogLocation"] = "Emplacement du fichier journal :",

                // Tray
                ["Tray.GlowEnabled"] = "✓ Effet Lumineux Activé",
                ["Tray.GamingMode"] = "🎮 Mode Jeu",
                ["Tray.TestAnimation"] = "✨ Tester l'Animation",
                ["Tray.Tooltip"] = "NotiGlow - Utilitaire de Notification Ambiante",

                // Update Dialog
                ["UpdateDialog.Title"] = "Mise à jour disponible",
                ["UpdateDialog.AvailableTitle"] = "Mise à jour disponible",
                ["UpdateDialog.CurrentVersionLabel"] = "VERSION ACTUELLE",
                ["UpdateDialog.NewVersionLabel"] = "NOUVELLE VERSION",
                ["UpdateDialog.Description"] = "Une nouvelle version de NotiGlow est disponible. Souhaitez-vous télécharger et installer cette mise à jour maintenant ?",
                ["UpdateDialog.ReleaseNotes"] = "Notes de version",
                ["UpdateDialog.UpdateNow"] = "Mettre à jour",
                ["UpdateDialog.BtnUpdate"] = "Mettre à jour",
                ["UpdateDialog.Cancel"] = "Annuler",
                ["UpdateDialog.BtnCancel"] = "Annuler",

                // v2.4 Features
                ["Appearance.CornerRounding"] = "Arrondi des angles",
                ["Appearance.CornerRoundingDesc"] = "Ajustez le rayon de courbure des cartes, champs et contrôles.",
                ["Applications.ScanApps"] = "Scanner les applications",
                ["Applications.ScanAppsToolTip"] = "Scanner et détecter les applications installées sur ce PC",
                ["Applications.ScanComplete"] = "Scan terminé. {0} applications installées trouvées.",
                ["Gaming.CommonSettingsTitle"] = "Paramètres globaux de lueur pour les jeux",
                ["Gaming.CommonSettingsDesc"] = "Durée, intensité de lueur et taille de flou unifiées appliquées à tous les jeux suivis.",
                ["Gaming.CommonDuration"] = "Durée",
                ["Gaming.CommonIntensity"] = "Intensité de lueur",
                ["Gaming.CommonBlurSize"] = "Taille du flou de lueur"
            },

                        ["de-DE"] = new(StringComparer.Ordinal)
            {
                ["App.Title"] = "NotiGlow",
                ["App.TitleBar"] = "NotiGlow {0} — Ambient-Benachrichtigungs-Dienstprogramm",
                ["Nav.General"] = "Allgemein",
                ["Nav.Applications"] = "Anwendungen",
                ["Nav.Appearance"] = "Erscheinungsbild",
                ["Nav.Display"] = "Bildschirm",
                ["Nav.Gaming"] = "Gaming",
                ["Nav.Notifications"] = "Benachrichtigungen",
                ["Nav.Advanced"] = "Erweitert",
                ["Nav.Language"] = "Sprache",
                ["Language.Language"] = "Sprache",
                ["Language.English"] = "English",
                ["Language.Spanish"] = "Español",
                ["Language.French"] = "Français",
                ["Language.German"] = "Deutsch",
                ["Language.Turkish"] = "Türkçe",
                ["Language.SelectLanguage"] = "Sprache auswählen",
                ["Language.ChangeLanguageHelp"] = "Anwendungssprache ändern",
                ["Common.Cancel"] = "Abbrechen",
                ["Common.Save"] = "Speichern",
                ["Common.Close"] = "Schließen",
                ["Common.Browse"] = "Durchsuchen...",
                ["Common.Export"] = "Exportieren",
                ["Common.Import"] = "Importieren",
                ["Common.Reset"] = "Zurücksetzen",
                ["Common.Delete"] = "Löschen",
                ["Common.Edit"] = "Bearbeiten",
                ["Common.Duplicate"] = "Duplizieren",
                ["Common.Preview"] = "Vorschau",
                ["Common.Active"] = "Aktiv",
                ["Common.Inactive"] = "Inaktiv",
                ["Common.Enabled"] = "Aktiviert",
                ["Common.Disabled"] = "Deaktiviert",
                ["Common.Yes"] = "Ja",
                ["Common.No"] = "Nein",
                ["Common.Ok"] = "OK",
                ["Common.Settings"] = "Einstellungen",
                ["Common.TestAnimation"] = "Animation testen",
                ["Common.TestAnimationToolTip"] = "Benachrichtigungs-Leuchteffekt testen",
                ["General.Title"] = "Allgemeine Einstellungen",
                ["General.Subtitle"] = "Steuern Sie, wie NotiGlow funktioniert und Ambient-Animationen ausführt.",
                ["General.Activation"] = "NotiGlow-Aktivierung",
                ["General.ActivationDesc"] = "Aktiviert oder deaktiviert NotiGlow-Benachrichtigungseffekte.",
                ["General.Listener"] = "Benachrichtigungsdienst",
                ["General.ListenerDesc"] = "Zugriffsstatus des Windows-Benachrichtigungsdienstes zum Erfassen von App-Benachrichtigungen.",
                ["General.ListenerActive"] = "Aktiv & Empfangsbereit",
                ["General.ListenerPermissionRequired"] = "Berechtigung erforderlich",
                ["General.ListenerStopped"] = "Dienst angehalten",
                ["General.OpenSettings"] = "Einstellungen öffnen",
                ["General.StartWithWindows"] = "Mit Windows starten",
                ["General.StartWithWindowsDesc"] = "NotiGlow beim Anmelden bei Windows automatisch starten.",
                ["General.ReduceAnimations"] = "Animationen reduzieren",
                ["General.ReduceAnimationsDesc"] = "Reduziert Bewegungseffekte für eine dezentere Benutzeroberfläche.",
                ["General.ThemeMode"] = "Designmodus",
                ["General.ThemeModeDesc"] = "Wählen Sie zwischen Windows-Systemeinstellung, Hell oder Dunkel.",
                ["General.ThemeSystem"] = "System",
                ["General.ThemeLight"] = "Hell",
                ["General.ThemeDark"] = "Dunkel",
                ["General.ThemeSystemToolTip"] = "Windows-Systemdesign verwenden",
                ["General.ThemeLightToolTip"] = "Hellen Modus erzwingen",
                ["General.ThemeDarkToolTip"] = "Dunklen Modus erzwingen",
                ["General.ColorTheme"] = "Farbdesign",
                ["General.ColorThemeDesc"] = "Kuratierte Farbschemata mit abgestimmten hellen und dunklen Varianten.",
                ["General.Language"] = "Sprache",
                ["General.LanguageDesc"] = "Wählen Sie die Anzeigesprache für NotiGlow.",
                ["Appearance.Title"] = "Erscheinungsbild",
                ["Appearance.Subtitle"] = "Globale Standardparameter für Animationen konfigurieren und Leuchteffekt voranzeigen.",
                ["Appearance.DefaultStyle"] = "Standard-Animationsstil",
                ["Appearance.DefaultStyleDesc"] = "Ausweich-Bewegungseffekt für neu hinzugefügte Anwendungsprofile.",
                ["Appearance.DefaultDuration"] = "Standarddauer",
                ["Appearance.DefaultIntensity"] = "Standard-Leuchtintensität",
                ["Appearance.DefaultGlowSize"] = "Standard-Weichzeichnungsgröße",
                ["Appearance.DefaultColor"] = "Standard-Leuchtfarbe",
                ["Appearance.LivePreview"] = "Live-Leuchtvorschau",
                ["Notifications.Title"] = "Benachrichtigungen",
                ["Notifications.Subtitle"] = "Verhalten von NotiGlow steuern, wenn mehrere Benachrichtigungen kurz hintereinander eintreffen.",
                ["Notifications.Restart"] = "Animation neu starten (Standard)",
                ["Notifications.RestartDesc"] = "Setzt den Animationstimer sofort zurück und startet den Leuchteffekt bei 0 ms neu.",
                ["Notifications.Extend"] = "Aktive Animation verlängern",
                ["Notifications.ExtendDesc"] = "Verlängert die laufende Dauer, sodass das Leuchten bei Benachrichtigungswellen durchgehend sichtbar bleibt.",
                ["Notifications.Queue"] = "Benachrichtigungen einreihen",
                ["Notifications.QueueDesc"] = "Reiht eingehende Benachrichtigungen ein und spielt Leuchteffekte nacheinander ab.",
                ["Notifications.Ignore"] = "Während der Animation ignorieren",
                ["Notifications.IgnoreDesc"] = "Ignoriert neue eingehende Benachrichtigungen, während bereits ein Leuchteffekt abgespielt wird.",
                ["Display.Title"] = "Bildschirm",
                ["Display.Subtitle"] = "Wählen Sie aus, welche Bildschirme den Leuchtumrandungseffekt bei Benachrichtigungen anzeigen.",
                ["Display.MonitorFormat"] = "Monitor {0}: {1}",
                ["Display.PrimaryBadge"] = "(Primär)",
                ["Display.ResolutionFormat"] = "Auflösung: {0} x {1} | Farbtiefe: {2}",
                ["Display.ActiveMonitor"] = "Aktiver Bildschirm",
                ["Display.ActiveMonitorDesc"] = "Leuchteffekt auf dem Bildschirm mit dem aktiven Fenster-/Mausfokus anzeigen.",
                ["Display.PrimaryMonitor"] = "Hauptbildschirm (Standard)",
                ["Display.PrimaryMonitorDesc"] = "Benachrichtigungs-Leuchteffekte immer auf dem Windows-Hauptbildschirm anzeigen.",
                ["Display.AllMonitors"] = "Alle Bildschirme",
                ["Display.AllMonitorsDesc"] = "Leuchtumrandung auf allen angeschlossenen Bildschirmen gleichzeitig anzeigen.",
                ["Display.DetectedMonitors"] = "Erkannte Bildschirme",
                ["Gaming.Title"] = "Gaming",
                ["Gaming.Subtitle"] = "Leuchtintensität beim Spielen automatisch reduzieren oder Benachrichtigungen unterdrücken.",
                ["Gaming.TestGameAnimation"] = "Spiel-Animation testen",
                ["Gaming.TestGameAnimationToolTip"] = "Leuchteffekt-Verhalten für erfasste Spiele testen",
                ["Gaming.Mode"] = "Spielmodus",
                ["Gaming.ModeDesc"] = "Aktiviert spiele-spezifisches Benachrichtigungs- und Leistungsverhalten.",
                ["Gaming.BehaviorRules"] = "Verhaltensregeln im Spiel",
                ["Gaming.GlowDuringGames"] = "Leuchten während des Spiels",
                ["Gaming.GlowDuringGamesDesc"] = "Erlaubt Benachrichtigungs-Leuchteffekte, während ein Spiel läuft.",
                ["Gaming.ReduceIntensity"] = "Intensität in Spielen reduzieren",
                ["Gaming.ReduceIntensityDesc"] = "Verringert die Leuchtstärke beim Spielen zur Minimierung von Ablenkungen.",
                ["Gaming.ReduceDuration"] = "Dauer in Spielen reduzieren",
                ["Gaming.ReduceDurationDesc"] = "Verkürzt die Animationsdauer beim Spielen.",
                ["Gaming.OnlyImportant"] = "Nur wichtige Benachrichtigungen",
                ["Gaming.OnlyImportantDesc"] = "Zeigt beim Spielen nur Benachrichtigungen mit hoher Priorität an.",
                ["Gaming.TrackedGames"] = "Erfasste Spiele-Dateien",
                ["Gaming.DetectionSupport"] = "Automatische Erkennung für Steam und Epic Games unterstützt.",
                ["Gaming.ScanLaunchers"] = "Launcher scannen",
                ["Gaming.ScanLaunchersToolTip"] = "Automatisch nach Steam- und Epic Games-Spielen suchen",
                ["Gaming.AddGame"] = "Spiel hinzufügen",
                ["Gaming.AddGameToolTip"] = "Ausführbare Spieldatei über den Dateibrowser auswählen",
                ["Gaming.RemoveGameToolTip"] = "Spiel entfernen",
                ["Gaming.ScanningGames"] = "Steam & Epic Games werden gescannt...",
                ["Gaming.FoundGames"] = "{0} neue Spiele gefunden und zur Liste hinzugefügt.",
                ["Gaming.NoNewGames"] = "Keine neuen Spiele gefunden (Liste ist aktuell).",
                ["Gaming.ScanError"] = "Beim Scannen der Spiele ist ein Fehler aufgetreten.",
                ["Gaming.SubSettingsDisabledToolTip"] = "Diese Option kann nicht angewendet werden, wenn 'Leuchten während des Spiels' deaktiviert ist.",
                ["Gaming.SelectExecutableDialogTitle"] = "Ausführbare Spieldatei auswählen",
                ["Gaming.EmptyTrackedGamesWarning"] = "Fügen Sie ein erfasstes Spiel hinzu, um die Spielanimation zu testen.",
                ["Gaming.ListView"] = "Listenansicht",
                ["Gaming.GridView"] = "Rasteransicht",
                ["Gaming.PerGameOverrides"] = "Leuchteffekt-Anpassungen pro Spiel",
                ["Gaming.InheritGlobal"] = "Globale Standardwerte übernehmen",
                ["Gaming.CustomOverrides"] = "Benutzerdefiniert",
                ["Gaming.EditGameSettings"] = "Spieleinstellungen bearbeiten",
                ["Gaming.GameExecutable"] = "Ausführbare Spieldatei",
                ["Gaming.GlowDuration"] = "Leuchtdauer",
                ["Gaming.GlowIntensity"] = "Leuchtintensität",
                ["Gaming.GlowBlurSize"] = "Weichzeichnungsgröße",
                ["Gaming.InheritsGlobalDefault"] = "Übernimmt globalen Standard ({0})",
                ["Gaming.NoTrackedGames"] = "Noch keine Spiele erfasst. Launcher scannen oder Spiele manuell hinzufügen.",
                ["Gaming.InheritsGlobal"] = "Global",
                ["Gaming.ResetToDefault"] = "Auf Standard zurücksetzen",
                ["Gaming.CustomBadge"] = "Benutzerdefiniert",
                ["Gaming.GamingAppsSectionTitle"] = "Spiele-Apps",
                ["Gaming.GamingAppsSectionDesc"] = "Im Spiele-Tab konfigurierte oder erkannte Spiele",
                ["Gaming.CustomGlow"] = "Benutzerdefinierter Leuchteffekt",
                ["Gaming.CustomGlowToolTip"] = "Zwischen spielspezifischem und globalem Effekt wechseln",
                ["Applications.Title"] = "Verfolgte Anwendungen",
                ["Applications.Subtitle"] = "Benutzerdefinierte Leuchtfarben, Dauern, Prioritäten und Animationsstile pro Anwendung verwalten",
                ["Applications.ListView"] = "Listenansicht",
                ["Applications.GridView"] = "Rasteransicht",
                ["Applications.AddApplication"] = "Anwendung hinzufügen",
                ["Applications.AddApplicationToolTip"] = "Neues benutzerdefiniertes Anwendungsprofil hinzufügen",
                ["Applications.EditProfile"] = "Profil bearbeiten",
                ["Applications.NewProfile"] = "Neues Profil",
                ["Applications.AppName"] = "Anwendungsname",
                ["Applications.AppNamePlaceholder"] = "z. B. Discord",
                ["Applications.AppId"] = "App-Kennung / Dateiname",
                ["Applications.AppIdPlaceholder"] = "z. B. discord oder discord.exe",
                ["Applications.BrowseToolTip"] = "Ausführbare Datei auswählen (.exe)",
                ["Applications.AnimationEffect"] = "Animationsstil",
                ["Applications.Priority"] = "Benachrichtigungspriorität",
                ["Applications.PriorityLow"] = "Niedrige Priorität",
                ["Applications.PriorityNormal"] = "Normale Priorität",
                ["Applications.PriorityHigh"] = "Hohe Priorität (umgeht Spielmodus-Filter)",
                ["Applications.Duration"] = "Dauer (ms)",
                ["Applications.Intensity"] = "Leuchtintensität",
                ["Applications.GlowSize"] = "Weichzeichnungsgröße",
                ["Applications.Color"] = "Leuchtfarbe",
                ["Applications.LivePreview"] = "Live-Vorschau",
                ["Applications.SaveProfile"] = "Profil speichern",
                ["Applications.Cancel"] = "Abbrechen",
                ["Applications.ValidationMsg"] = "Bitte gültigen Anwendungsnamen und Dateikennung eingeben.",
                ["Applications.ValidationTitle"] = "Validierungsfehler",
                ["Applications.ConfirmDeleteMsg"] = "Möchten Sie das Profil für {0} wirklich entfernen?",
                ["Applications.ConfirmDeleteTitle"] = "Löschen bestätigen",
                ["Applications.ProfileCreatedMsg"] = "Profil '{0}' erstellt!",
                ["Applications.ProfileCreatedTitle"] = "Profil dupliziert",
                ["Applications.InvalidFileMsg"] = "Die ausgewählte Datei ist ungültig.",
                ["Applications.InvalidFileTitle"] = "Ungültige Dateiauswahl",
                ["Applications.EmptyState"] = "Noch keine Anwendungen konfiguriert. Klicken Sie auf 'Anwendung hinzufügen'.",
                ["Applications.CardStyle"] = "Stil",
                ["Applications.CardPriority"] = "Priorität",
                ["Applications.AppGlowColor"] = "App-Leuchtfarbe",
                ["Advanced.Title"] = "Erweitert",
                ["Advanced.Subtitle"] = "Leistungsoptimierung, OLED-Einbrennschutz, Diagnosewerkzeuge und Sicherungsoptionen.",
                ["Advanced.AutoUpdate"] = "Automatisches Update",
                ["Advanced.AutomaticUpdates"] = "Automatische Updates",
                ["Advanced.CurrentVersionFormat"] = "Aktuelle Version: v{0} • Automatische Update-Prüfung über GitHub Releases",
                ["Advanced.LastCheckFormat"] = "Aktuell: v{0} • Zuletzt geprüft: {1}",
                ["Advanced.CheckForUpdates"] = "Nach Updates suchen",
                ["Advanced.AutoCheckUpdatesToolTip"] = "Automatisch nach Updates suchen",
                ["Advanced.CheckFrequency"] = "Prüfhäufigkeit",
                ["Advanced.CheckFrequencyDesc"] = "Legen Sie fest, wie oft NotiGlow nach neuen Versionen sucht.",
                ["Advanced.FreqStartup"] = "Beim Start",
                ["Advanced.FreqDaily"] = "Täglich",
                ["Advanced.FreqWeekly"] = "Wöchentlich",
                ["Advanced.FreqMonthly"] = "Monatlich",
                ["Advanced.CheckingUpdates"] = "Suche nach Updates...",
                ["Advanced.UpdateAvailableTitle"] = "Update verfügbar",
                ["Advanced.UpdateAvailableMsg"] = "Eine neue Version (v{0}) von NotiGlow ist verfügbar!\n\nMöchten Sie das Update jetzt herunterladen und installieren?",
                ["Advanced.UpdateReadyInstalling"] = "Update bereit. Installation wird gestartet...",
                ["Advanced.ReadyToInstallTitle"] = "Bereit zur Installation",
                ["Advanced.ReadyToInstallMsg"] = "NotiGlow v{0} wurde erfolgreich heruntergeladen!\n\nDie Anwendung wird nun geschlossen und aktualisiert. Fortfahren?",
                ["Advanced.UpToDate"] = "NotiGlow v{0} ist auf dem neuesten Stand.",
                ["Advanced.UpdateCheckFailed"] = "Update-Prüfung fehlgeschlagen. Bitte überprüfen Sie Ihre Netzwerkverbindung und versuchen Sie es erneut.",
                ["Advanced.Accessibility"] = "Barrierefreiheit",
                ["Advanced.OledMode"] = "OLED-Schonmodus",
                ["Advanced.OledModeDesc"] = "Reduziert statische helle Elemente zur Verringerung von OLED-Einbrenneffekten.",
                ["Advanced.ReduceMotion"] = "Bewegung reduzieren",
                ["Advanced.ReduceMotionDesc"] = "Reduziert Bewegungsanimationen bei allen Leuchteffekten.",
                ["Advanced.ReduceGlow"] = "Leuchten reduzieren",
                ["Advanced.ReduceGlowDesc"] = "Verringert die Gesamthelligkeit der Leuchteffekte.",
                ["Advanced.BackupRestore"] = "Sichern & Wiederherstellen",
                ["Advanced.ConfigManagement"] = "Konfigurationsverwaltung",
                ["Advanced.ConfigManagementDesc"] = "Profile als JSON exportieren, Sicherungen importieren oder Werkseinstellungen wiederherstellen.",
                ["Advanced.ExportSettings"] = "Einstellungen exportieren",
                ["Advanced.ImportSettings"] = "Einstellungen importieren",
                ["Advanced.FactoryReset"] = "Auf Werkseinstellungen zurücksetzen",
                ["Advanced.Diagnostics"] = "Diagnose",
                ["Advanced.DebugLogging"] = "Debug-Protokollierung",
                ["Advanced.DebugLoggingDesc"] = "Zeichnet Diagnoseinformationen zur Fehlerbehebung auf.",
                ["Advanced.IdentityDebug"] = "Benachrichtigungs-Identitätsprüfung",
                ["Advanced.IdentityDebugDesc"] = "Zeigt zusätzliche Details zur Identifizierung von Benachrichtigungsquellen an.",
                ["Advanced.ExportSuccessMsg"] = "Einstellungen erfolgreich exportiert!",
                ["Advanced.ExportSuccessTitle"] = "Export abgeschlossen",
                ["Advanced.ExportFailMsg"] = "Einstellungen konnten nicht exportiert werden.",
                ["Advanced.ExportFailTitle"] = "Exportfehler",
                ["Advanced.ImportSuccessMsg"] = "Einstellungen erfolgreich importiert!",
                ["Advanced.ImportSuccessTitle"] = "Import abgeschlossen",
                ["Advanced.ImportFailMsg"] = "Import fehlgeschlagen. Ungültiges Dateiformat.",
                ["Advanced.ImportFailTitle"] = "Importfehler",
                ["Advanced.ResetConfirmMsg"] = "Alle NotiGlow-Einstellungen und Anwendungsprofile auf Standardwerte zurücksetzen?",
                ["Advanced.ResetConfirmTitle"] = "Zurücksetzen bestätigen",
                ["Advanced.ResetCompleteMsg"] = "NotiGlow wurde auf die Standardeinstellungen zurückgesetzt.",
                ["Advanced.ResetCompleteTitle"] = "Zurücksetzen abgeschlossen",
                ["Style.Pulse"] = "Pulsieren",
                ["Style.Sweep"] = "Abtasten",
                ["Style.Ambient"] = "Sanftes Atmen",
                ["Style.Comet"] = "Komet",
                ["Style.Ripple"] = "Welle",
                ["Style.PulseDetailed"] = "Pulsieren (Gleichmäßiger Deckkraft-Puls)",
                ["Style.SweepDetailed"] = "Abtasten (Kreisendes Randlicht)",
                ["Style.AmbientDetailed"] = "Sanftes Atmen (Weicher Atemschein)",
                ["Style.CometDetailed"] = "Komet (Lichtkopf mit Schweif)",
                ["Style.RippleDetailed"] = "Welle (Expandierende Eckwelle)",
                ["Toast.Dismiss"] = "Schließen",
                ["Toast.Retry"] = "Wiederholen",
                ["Toast.OpenSettings"] = "Einstellungen öffnen",
                ["Toast.Error"] = "Fehler",
                ["Toast.Warning"] = "Warnung",
                ["Toast.Success"] = "Erfolg",
                ["Toast.Info"] = "Information",
                ["ColorPicker.Palette"] = "Leuchtfarbpalette",
                ["ColorPicker.RgbRainbowTooltip"] = "RGB-Regenbogenspektrum-Neoneffekt",
                ["ColorPicker.ToggleCustom"] = "Benutzerdefinierte Hex-Farbe...",
                ["ColorPicker.Hue"] = "Farbton",
                ["ColorPicker.PreviewTooltip"] = "Aktuelle Farbvorschau",
                ["ColorPicker.CopyHexTooltip"] = "Hex-Code kopieren",
                ["EdgePreview.SimulatedGlow"] = "Simulierter Bildschirmrand-Leuchteffekt",
                ["ColorPicker.RgbSpectrum"] = "RGB-Spektrum",
                ["ColorPicker.RgbToolTip"] = "RGB-Regenbogenspektrum-Neoneffekt",
                ["ColorPicker.CustomHex"] = "Benutzerdefinierte Hex-Farbe...",
                ["ColorPicker.CustomHexToolTip"] = "Benutzerdefinierten Hex-Farbcode eingeben oder bearbeiten",

                // In-App Error Dialog
                ["Error.StartupTitle"] = "NotiGlow konnte nicht gestartet werden",
                ["Error.StartupDesc"] = "Beim Starten der Anwendung ist ein kritischer Fehler aufgetreten.",
                ["Error.UnhandledTitle"] = "Ein unerwarteter Fehler ist aufgetreten",
                ["Error.UnhandledDesc"] = "NotiGlow ist auf ein unerwartetes Problem gestoßen.",
                ["Error.Details"] = "Details",
                ["Error.CopyLogPath"] = "Protokollpfad kopieren",
                ["Error.CopyDetails"] = "Details kopieren",
                ["Error.Copied"] = "Kopiert!",
                ["Error.Close"] = "Schließen",
                ["Error.Retry"] = "Wiederholen",
                ["Error.LogLocation"] = "Speicherort der Protokolldatei:",

                // Tray
                ["Tray.GlowEnabled"] = "✓ Leuchten aktiviert",
                ["Tray.GamingMode"] = "🎮 Spielmodus",
                ["Tray.TestAnimation"] = "✨ Animation testen",
                ["Tray.Tooltip"] = "NotiGlow - Ambient-Benachrichtigungs-Dienstprogramm",

                // Update Dialog
                ["UpdateDialog.Title"] = "Update verfügbar",
                ["UpdateDialog.AvailableTitle"] = "Update verfügbar",
                ["UpdateDialog.CurrentVersionLabel"] = "AKTUELLE VERSION",
                ["UpdateDialog.NewVersionLabel"] = "NEUE VERSION",
                ["UpdateDialog.Description"] = "Eine neue Version von NotiGlow ist verfügbar. Möchten Sie dieses Update jetzt herunterladen und installieren?",
                ["UpdateDialog.ReleaseNotes"] = "Versionshinweise",
                ["UpdateDialog.UpdateNow"] = "Jetzt aktualisieren",
                ["UpdateDialog.BtnUpdate"] = "Jetzt aktualisieren",
                ["UpdateDialog.Cancel"] = "Abbrechen",
                ["UpdateDialog.BtnCancel"] = "Abbrechen",

                // v2.4 Features
                ["Appearance.CornerRounding"] = "Eckenabrundung",
                ["Appearance.CornerRoundingDesc"] = "Passen Sie den Eckenradius für Karten, Eingabefelder und Steuerelemente an.",
                ["Applications.ScanApps"] = "Apps scannen",
                ["Applications.ScanAppsToolTip"] = "Installierte Anwendungen auf diesem PC scannen und erkennen",
                ["Applications.ScanComplete"] = "Scan abgeschlossen. {0} installierte Apps gefunden.",
                ["Gaming.CommonSettingsTitle"] = "Gemeinsame Leuchteinstellungen für Spiele",
                ["Gaming.CommonSettingsDesc"] = "Einheitliche Dauer, Leuchtintensität und Weichzeichnung für alle erfassten Spiele.",
                ["Gaming.CommonDuration"] = "Dauer",
                ["Gaming.CommonIntensity"] = "Leuchtintensität",
                ["Gaming.CommonBlurSize"] = "Leucht-Weichzeichnungsgröße"
            },

["tr-TR"] = new(StringComparer.Ordinal)
            {
                // App / Title
                ["App.Title"] = "NotiGlow",
                ["App.TitleBar"] = "NotiGlow {0} — Ortam Bildirim Yardımcısı",

                // Navigation
                ["Nav.General"] = "Genel",
                ["Nav.Applications"] = "Uygulamalar",
                ["Nav.Appearance"] = "Görünüm",
                ["Nav.Display"] = "Ekran",
                ["Nav.Gaming"] = "Oyun",
                ["Nav.Notifications"] = "Bildirimler",
                ["Nav.Advanced"] = "Gelişmiş",
                ["Nav.Language"] = "Dil",

                // Language
                ["Language.Language"] = "Dil",
                ["Language.English"] = "English",
                ["Language.Spanish"] = "Español",
                ["Language.French"] = "Français",
                ["Language.German"] = "Deutsch",
                ["Language.Turkish"] = "Türkçe",
                ["Language.SelectLanguage"] = "Dil Seçin",
                ["Language.ChangeLanguageHelp"] = "Uygulama dilini değiştirin",

                // Common Actions
                ["Common.Cancel"] = "İptal",
                ["Common.Save"] = "Kaydet",
                ["Common.Close"] = "Kapat",
                ["Common.Browse"] = "Gözat...",
                ["Common.Export"] = "Dışa Aktar",
                ["Common.Import"] = "İçe Aktar",
                ["Common.Reset"] = "Sıfırla",
                ["Common.Delete"] = "Sil",
                ["Common.Edit"] = "Düzenle",
                ["Common.Duplicate"] = "Çoğalt",
                ["Common.Preview"] = "Önizleme",
                ["Common.Active"] = "Aktif",
                ["Common.Inactive"] = "Devre Dışı",
                ["Common.Enabled"] = "Etkin",
                ["Common.Disabled"] = "Kapalı",
                ["Common.Yes"] = "Evet",
                ["Common.No"] = "Hayır",
                ["Common.Ok"] = "Tamam",
                ["Common.Settings"] = "Ayarlar",
                ["Common.TestAnimation"] = "Animasyonu Test Et",
                ["Common.TestAnimationToolTip"] = "Bildirim kenar ışığı animasyonunu test edin",

                // General View
                ["General.Title"] = "Genel Ayarlar",
                ["General.Subtitle"] = "NotiGlow'un çalışma davranışını ve ortam animasyonlarını yönetin.",
                ["General.Activation"] = "NotiGlow Etkinliği",
                ["General.ActivationDesc"] = "NotiGlow bildirim efektlerini açar veya kapatır.",
                ["General.Listener"] = "Bildirim Dinleyici",
                ["General.ListenerDesc"] = "Uygulama bildirimlerini yakalamak için Windows Bildirim Dinleyici erişim durumu.",
                ["General.ListenerActive"] = "Aktif ve Dinliyor",
                ["General.ListenerPermissionRequired"] = "İzin Gerekli",
                ["General.ListenerStopped"] = "Hizmet Durduruldu",
                ["General.OpenSettings"] = "Ayarları Aç",
                ["General.StartWithWindows"] = "Windows ile Başlat",
                ["General.StartWithWindowsDesc"] = "Windows oturumu açıldığında NotiGlow'u otomatik olarak başlatır.",
                ["General.ReduceAnimations"] = "Animasyonları Azalt",
                ["General.ReduceAnimationsDesc"] = "Arayüzü ve bildirimleri daha sade hale getirmek için animasyon efektlerini azaltır.",
                ["General.ThemeMode"] = "Tema Modu",
                ["General.ThemeModeDesc"] = "Windows sistem tercihi, Açık veya Koyu mod arasında seçim yapın.",
                ["General.ThemeSystem"] = "Sistem",
                ["General.ThemeLight"] = "Aydınlık",
                ["General.ThemeDark"] = "Karanlık",
                ["General.ThemeSystemToolTip"] = "Windows sistem temasını takip et",
                ["General.ThemeLightToolTip"] = "Açık temayı zorla",
                ["General.ThemeDarkToolTip"] = "Koyu temayı zorla",
                ["General.ColorTheme"] = "Renk Teması",
                ["General.ColorThemeDesc"] = "Özel Açık ve Koyu varyantlara sahip seçkin renk sistemleri.",
                ["General.Language"] = "Uygulama Dili",
                ["General.LanguageDesc"] = "NotiGlow için görüntüleme dilini seçin.",

                // Appearance View
                ["Appearance.Title"] = "Görünüm",
                ["Appearance.Subtitle"] = "Evrensel varsayılan animasyon parametrelerini yapılandırın ve ekran ışığını önizleyin.",
                ["Appearance.DefaultStyle"] = "Varsayılan Animasyon Stili",
                ["Appearance.DefaultStyleDesc"] = "Yeni eklenen uygulama profilleri için varsayılan hareket efekti.",
                ["Appearance.DefaultDuration"] = "Varsayılan Süre",
                ["Appearance.DefaultIntensity"] = "Varsayılan Işık Yoğunluğu",
                ["Appearance.DefaultGlowSize"] = "Varsayılan Bulanıklık Boyutu",
                ["Appearance.DefaultColor"] = "Varsayılan Işık Rengi",
                ["Appearance.LivePreview"] = "Canlı Işık Önizleme",

                // Notifications View
                ["Notifications.Title"] = "Bildirimler",
                ["Notifications.Subtitle"] = "Kısa bir zaman aralığında art arda birden çok bildirim geldiğinde NotiGlow'un davranışını belirleyin.",
                ["Notifications.Restart"] = "Animasyonu Yeniden Başlat (Varsayılan)",
                ["Notifications.RestartDesc"] = "Zamanlayıcıyı anında sıfırlar ve yeni bildirim için kenar ışığı animasyonunu 0 ms'den başlatır.",
                ["Notifications.Extend"] = "Aktif Animasyonu Uzat",
                ["Notifications.ExtendDesc"] = "Mevcut animasyon süresini uzatarak yoğun bildirim anlarında ışığın kesintisiz görünmesini sağlar.",
                ["Notifications.Queue"] = "Bildirimleri Sıraya Al",
                ["Notifications.QueueDesc"] = "Gelen bildirimleri sıraya koyar ve animasyonları sırayla tek tek oynatır.",
                ["Notifications.Ignore"] = "Animasyon Sırasında Yoksay",
                ["Notifications.IgnoreDesc"] = "Zaten bir animasyon oynatılırken gelen yeni bildirimleri yok sayar.",

                // Display View
                ["Display.Title"] = "Ekran",
                ["Display.Subtitle"] = "Bildirimler tetiklendiğinde kenar ışığı animasyonunun hangi ekranlarda gösterileceğini seçin.",
                ["Display.MonitorFormat"] = "Monitör {0}: {1}",
                ["Display.PrimaryBadge"] = "(Birincil)",
                ["Display.ResolutionFormat"] = "Çözünürlük: {0} x {1} | Renk Derinliği: {2}",
                ["Display.ActiveMonitor"] = "Aktif Monitör",
                ["Display.ActiveMonitorDesc"] = "Işığı, aktif pencereyi veya fare imlecini barındıran monitörde gösterir.",
                ["Display.PrimaryMonitor"] = "Birincil Monitör (Varsayılan)",
                ["Display.PrimaryMonitorDesc"] = "Bildirim ışığı animasyonları için her zaman Windows birincil ekranını kullanır.",
                ["Display.AllMonitors"] = "Tüm Monitörler",
                ["Display.AllMonitorsDesc"] = "Bağlı tüm monitörlerde eşzamanlı olarak kenar ışığı efektlerini gösterir.",
                ["Display.DetectedMonitors"] = "Algılanan Monitörler",

                // Gaming View
                ["Gaming.Title"] = "Oyun",
                ["Gaming.Subtitle"] = "Oyun oynarken ışık yoğunluğunu otomatik azaltın veya bildirimleri susturun.",
                ["Gaming.TestGameAnimation"] = "Oyun Animasyonunu Test Et",
                ["Gaming.TestGameAnimationToolTip"] = "Takip edilen oyun kenar ışığı davranışını test edin",
                ["Gaming.Mode"] = "Oyun Modu",
                ["Gaming.ModeDesc"] = "Oyuna özel bildirim ve performans davranışını etkinleştirir.",
                ["Gaming.BehaviorRules"] = "Oyun İçi Davranış Kuralları",
                ["Gaming.GlowDuringGames"] = "Oyun Sırasında Kenar Işığı",
                ["Gaming.GlowDuringGamesDesc"] = "Bir oyun çalışırken bildirim ışığı efektlerinin görünmesine izin verir.",
                ["Gaming.ReduceIntensity"] = "Oyunlarda Yoğunluğu Azalt",
                ["Gaming.ReduceIntensityDesc"] = "Dikkatin dağılmasını önlemek için oyun sırasında ışık parlaklığını azaltır.",
                ["Gaming.ReduceDuration"] = "Oyunlarda Süreyi Kısalt",
                ["Gaming.ReduceDurationDesc"] = "Oyun oynarken animasyon süresini kısaltır.",
                ["Gaming.OnlyImportant"] = "Yalnızca Önemli Bildirimleri Göster",
                ["Gaming.OnlyImportantDesc"] = "Oyun sırasında yalnızca yüksek öncelikli bildirimleri gösterir.",
                ["Gaming.TrackedGames"] = "Takip Edilen Oyun Dosyaları",
                ["Gaming.DetectionSupport"] = "Steam ve Epic Games otomatik algılama desteklenir.",
                ["Gaming.ScanLaunchers"] = "Başlatıcıları Tara",
                ["Gaming.ScanLaunchersToolTip"] = "Steam ve Epic Games kütüphanelerini otomatik olarak tara",
                ["Gaming.AddGame"] = "Oyun Ekle",
                ["Gaming.AddGameToolTip"] = "Dosya gezgini ile oyun yürütülebilir dosyasını (.exe) seçin",
                ["Gaming.RemoveGameToolTip"] = "Oyunu kaldır",
                ["Gaming.ScanningGames"] = "Steam ve Epic Games kütüphaneleri taranıyor...",
                ["Gaming.FoundGames"] = "{0} yeni oyun bulundu ve takip listesine eklendi.",
                ["Gaming.NoNewGames"] = "Yeni oyun bulunamadı (oyun listesi güncel).",
                ["Gaming.ScanError"] = "Oyun taraması sırasında bir sorunla karşılaşıldı.",
                ["Gaming.SubSettingsDisabledToolTip"] = "Oyun Sırasında Kenar Işığı kapalıyken bu ayar uygulanamaz.",
                ["Gaming.SelectExecutableDialogTitle"] = "Oyun Yürütülebilir Dosyasını Seçin",
                ["Gaming.EmptyTrackedGamesWarning"] = "Oyun animasyonunu test etmek için takip edilen bir oyun ekleyin.",
                ["Gaming.ListView"] = "Liste görünümü",
                ["Gaming.GridView"] = "Izgara görünümü",
                ["Gaming.PerGameOverrides"] = "Oyun Başına Işık Ayarları",
                ["Gaming.InheritGlobal"] = "Genel Varsayılanları Kullan",
                ["Gaming.CustomOverrides"] = "Özel Ayarlar",
                ["Gaming.EditGameSettings"] = "Oyun Ayarlarını Düzenle",
                ["Gaming.GameExecutable"] = "Oyun Dosyası",
                ["Gaming.GlowDuration"] = "Işık Süresi",
                ["Gaming.GlowIntensity"] = "Işık Yoğunluğu",
                ["Gaming.GlowBlurSize"] = "Işık Bulanıklığı",
                ["Gaming.InheritsGlobalDefault"] = "Genel varsayılanı kullanır ({0})",
                ["Gaming.NoTrackedGames"] = "Henüz takip edilen oyun yok. Başlatıcıları tarayın veya manuel oyun ekleyin.",
                ["Gaming.InheritsGlobal"] = "Genel",
                ["Gaming.ResetToDefault"] = "Varsayılana Sıfırla",
                ["Gaming.CustomBadge"] = "Özel",
                ["Gaming.GamingAppsSectionTitle"] = "Oyun Uygulamaları",
                ["Gaming.GamingAppsSectionDesc"] = "Oyun sekmesinde algılanan veya eklenen oyunlar",
                ["Gaming.CustomGlow"] = "Özel Glow",
                ["Gaming.CustomGlowToolTip"] = "Özel glow ve genel oyun ayarları arasında geçiş yap",

                // Applications View
                ["Applications.Title"] = "Takip Edilen Uygulamalar",
                ["Applications.Subtitle"] = "Uygulama bazında özel ışık renklerini, süreleri, öncelikleri ve animasyon stillerini yönetin",
                ["Applications.ListView"] = "Liste görünümü",
                ["Applications.GridView"] = "Izgara görünümü",
                ["Applications.AddApplication"] = "Uygulama Ekle",
                ["Applications.AddApplicationToolTip"] = "Yeni bir özel uygulama profili ekleyin",
                ["Applications.EditProfile"] = "Uygulama Profilini Düzenle",
                ["Applications.NewProfile"] = "Yeni Uygulama Profili",
                ["Applications.AppName"] = "Uygulama Adı",
                ["Applications.AppNamePlaceholder"] = "örn. Discord",
                ["Applications.AppId"] = "Uygulama Kimliği / Yürütülebilir Dosya Adı",
                ["Applications.AppIdPlaceholder"] = "örn. discord veya discord.exe",
                ["Applications.BrowseToolTip"] = "Yürütülebilir dosyayı (.exe) seçin",
                ["Applications.AnimationEffect"] = "Animasyon Stili",
                ["Applications.Priority"] = "Bildirim Önceliği",
                ["Applications.PriorityLow"] = "Düşük Öncelik",
                ["Applications.PriorityNormal"] = "Normal Öncelik",
                ["Applications.PriorityHigh"] = "Yüksek Öncelik (Oyun Modu filtresini atlar)",
                ["Applications.Duration"] = "Süre (ms)",
                ["Applications.Intensity"] = "Işık Yoğunluğu",
                ["Applications.GlowSize"] = "Işık Bulanıklık Boyutu",
                ["Applications.Color"] = "Işık Rengi",
                ["Applications.LivePreview"] = "Canlı Önizleme",
                ["Applications.SaveProfile"] = "Profili Kaydet",
                ["Applications.Cancel"] = "İptal",
                ["Applications.ValidationMsg"] = "Lütfen geçerli bir Uygulama Adı ve Kimliği girin.",
                ["Applications.ValidationTitle"] = "Doğrulama Hatası",
                ["Applications.ConfirmDeleteMsg"] = "{0} için profili silmek istediğinize emin misiniz?",
                ["Applications.ConfirmDeleteTitle"] = "Silmeyi Onayla",
                ["Applications.ProfileCreatedMsg"] = "'{0}' profili oluşturuldu!",
                ["Applications.ProfileCreatedTitle"] = "Profil Çoğaltıldı",
                ["Applications.InvalidFileMsg"] = "Seçilen yürütülebilir dosya geçersiz.",
                ["Applications.InvalidFileTitle"] = "Geçersiz Dosya Seçimi",
                ["Applications.EmptyState"] = "Henüz yapılandırılmış uygulama yok. Başlamak için 'Uygulama Ekle'ye tıklayın.",

                // Advanced View
                ["Advanced.Title"] = "Gelişmiş",
                ["Advanced.Subtitle"] = "Performans optimizasyonu, OLED yanma koruması, tanılama araçları ve yedekleme seçenekleri.",
                ["Advanced.AutoUpdate"] = "Otomatik Güncelleme",
                ["Advanced.AutomaticUpdates"] = "Otomatik Güncellemeler",
                ["Advanced.CurrentVersionFormat"] = "Mevcut Sürüm: v{0} • GitHub Releases üzerinden otomatik güncelleme denetimi",
                ["Advanced.LastCheckFormat"] = "Mevcut: v{0} • Son denetim: {1}",
                ["Advanced.CheckForUpdates"] = "Güncellemeleri Denetle",
                ["Advanced.AutoCheckUpdatesToolTip"] = "Güncellemeleri otomatik olarak denetle",
                ["Advanced.CheckFrequency"] = "Denetim Sıklığı",
                ["Advanced.CheckFrequencyDesc"] = "NotiGlow'un yeni sürümleri ne sıklıkla denetleyeceğini seçin.",
                ["Advanced.FreqStartup"] = "Başlangıçta",
                ["Advanced.FreqDaily"] = "Günlük",
                ["Advanced.FreqWeekly"] = "Haftalık",
                ["Advanced.FreqMonthly"] = "Aylık",
                ["Advanced.CheckingUpdates"] = "Güncellemeler denetleniyor...",
                ["Advanced.UpdateAvailableTitle"] = "Güncelleme Mevcut",
                ["Advanced.UpdateAvailableMsg"] = "NotiGlow'un yeni bir sürümü (v{0}) mevcut!\n\nGüncellemeyi şimdi indirmek, doğrulamak ve otomatik olarak kurmak ister misiniz?",
                ["Advanced.UpdateReadyInstalling"] = "Güncelleme hazır. Yükleniyor...",
                ["Advanced.ReadyToInstallTitle"] = "Kuruluma Hazır",
                ["Advanced.ReadyToInstallMsg"] = "NotiGlow v{0} başarıyla indirildi ve doğrulandı!\n\nUygulama şimdi kapanacak, güncellemeyi uygulayacak ve otomatik olarak yeniden başlayacak. Devam edilsin mi?",
                ["Advanced.UpToDate"] = "NotiGlow v{0} güncel.",
                ["Advanced.UpdateCheckFailed"] = "Güncelleme denetimi başarısız oldu. Lütfen ağ bağlantınızı kontrol edin ve tekrar deneyin.",
                ["Advanced.Accessibility"] = "Erişilebilirlik",
                ["Advanced.OledMode"] = "OLED Dostu Mod",
                ["Advanced.OledModeDesc"] = "OLED ekranlarda görüntü izi kalmasını önlemek için statik parlak öğeleri azaltır.",
                ["Advanced.ReduceMotion"] = "Hareketi Azalt",
                ["Advanced.ReduceMotionDesc"] = "Bildirim efektlerindeki animasyonlu hareketleri azaltır.",
                ["Advanced.ReduceGlow"] = "Işığı Azalt",
                ["Advanced.ReduceGlowDesc"] = "Bildirim efektlerindeki kenar ışığı parlaklığını azaltır.",
                ["Advanced.BackupRestore"] = "Yedekle ve Geri Yükle",
                ["Advanced.ConfigManagement"] = "Yapılandırma Yönetimi",
                ["Advanced.ConfigManagementDesc"] = "Profilleri JSON dosyasına aktarın, yedekleri içe aktarın veya fabrika ayarlarına sıfırlayın.",
                ["Advanced.ExportSettings"] = "Ayarları Dışa Aktar",
                ["Advanced.ImportSettings"] = "Ayarları İçe Aktar",
                ["Advanced.FactoryReset"] = "Fabrika Ayarlarına Sıfırla",
                ["Advanced.Diagnostics"] = "Tanılama",
                ["Advanced.DebugLogging"] = "Hata Ayıklama Günlüğü",
                ["Advanced.DebugLoggingDesc"] = "Uygulama sorunlarını gidermeye yardımcı olmak için tanılama bilgilerini kaydeder.",
                ["Advanced.IdentityDebug"] = "Bildirim Kimliği Hata Ayıklama",
                ["Advanced.IdentityDebugDesc"] = "Bildirim kaynaklarını tanımlamak için kullanılan ek bilgileri gösterir.",
                ["Advanced.ExportSuccessMsg"] = "Ayarlar başarıyla dışa aktarıldı!",
                ["Advanced.ExportSuccessTitle"] = "Dışa Aktarma Tamamlandı",
                ["Advanced.ExportFailMsg"] = "Ayarlar dışa aktarılamadı.",
                ["Advanced.ExportFailTitle"] = "Dışa Aktarma Hatası",
                ["Advanced.ImportSuccessMsg"] = "Ayarlar başarıyla içe aktarıldı!",
                ["Advanced.ImportSuccessTitle"] = "İçe Aktarma Tamamlandı",
                ["Advanced.ImportFailMsg"] = "Ayarlar içe aktarılamadı. Geçersiz dosya biçimi.",
                ["Advanced.ImportFailTitle"] = "İçe Aktarma Hatası",
                ["Advanced.ResetConfirmMsg"] = "Tüm NotiGlow ayarlarını ve uygulama profillerini varsayılana sıfırlamak istiyor musunuz?",
                ["Advanced.ResetConfirmTitle"] = "Sıfırlamayı Onayla",
                ["Advanced.ResetCompleteMsg"] = "NotiGlow varsayılan ayarlara sıfırlandı.",
                ["Advanced.ResetCompleteTitle"] = "Sıfırlama Tamamlandı",

                // Styles
                ["Style.Pulse"] = "Nabız",
                ["Style.Sweep"] = "Süpürme",
                ["Style.Ambient"] = "Ortam",
                ["Style.Comet"] = "Kuyruklu Yıldız",
                ["Style.Ripple"] = "Dalga",
                ["Style.PulseDetailed"] = "Nabız (Tekdüze Parlaklık Nabzı)",
                ["Style.SweepDetailed"] = "Süpürme (Dönen Çevre Işığı)",
                ["Style.AmbientDetailed"] = "Ortam (Yumuşak Nefes Işığı)",
                ["Style.CometDetailed"] = "Kuyruklu Yıldız (Hareket Eden Işık Başı ve İzi)",
                ["Style.RippleDetailed"] = "Dalga (Köşelerden Genişleyen Işık Dalgası)",

                ["Applications.CardStyle"] = "Stil",
                ["Applications.CardPriority"] = "Öncelik",
                ["Applications.AppGlowColor"] = "Uygulama Işık Rengi",
                ["ColorPicker.Palette"] = "Işık Renk Paleti",
                ["ColorPicker.RgbRainbowTooltip"] = "RGB Gökkuşağı Spektrumu Neon Efekti",
                ["ColorPicker.ToggleCustom"] = "Özel Onaltılık Renk...",
                ["ColorPicker.Hue"] = "Ton",
                ["ColorPicker.PreviewTooltip"] = "Geçerli Renk Önizlemesi",
                ["ColorPicker.CopyHexTooltip"] = "Hex Kodunu Kopyala",
                ["EdgePreview.SimulatedGlow"] = "Simüle Edilmiş Ekran Kenar Işığı",
                ["Toast.Dismiss"] = "Kapat",
                ["Toast.Retry"] = "Yeniden Dene",
                ["Toast.OpenSettings"] = "Ayarları Aç",
                ["Toast.Error"] = "Hata",
                ["Toast.Warning"] = "Uyarı",
                ["Toast.Success"] = "Başarılı",
                ["Toast.Info"] = "Bilgi",
                ["ColorPicker.RgbSpectrum"] = "RGB Renk Spektrumu",
                ["ColorPicker.RgbToolTip"] = "RGB Gökkuşağı Spektrumu Neon Efekti",
                ["ColorPicker.CustomHex"] = "Özel Onaltılık Renk...",
                ["ColorPicker.CustomHexToolTip"] = "Özel onaltılık (Hex) renk kodunu girin veya düzenleyin",

                // In-App Error Dialog
                ["Error.StartupTitle"] = "NotiGlow başlatılamadı",
                ["Error.StartupDesc"] = "Uygulama başlatılırken kritik bir hata oluştu.",
                ["Error.UnhandledTitle"] = "Beklenmeyen bir hata oluştu",
                ["Error.UnhandledDesc"] = "NotiGlow beklenmeyen bir sorunla karşılaştı.",
                ["Error.Details"] = "Ayrıntılar",
                ["Error.CopyLogPath"] = "Log konumunu kopyala",
                ["Error.CopyDetails"] = "Ayrıntıları kopyala",
                ["Error.Copied"] = "Kopyalandı!",
                ["Error.Close"] = "Kapat",
                ["Error.Retry"] = "Tekrar Dene",
                ["Error.LogLocation"] = "Log dosyası konumu:",

                // Tray
                ["Tray.GlowEnabled"] = "✓ Kenar Işığı Açık",
                ["Tray.GamingMode"] = "🎮 Oyun Modu",
                ["Tray.TestAnimation"] = "✨ Animasyonu Test Et",
                ["Tray.Tooltip"] = "NotiGlow - Ortam Bildirim Yardımcısı",

                // Update Dialog
                ["UpdateDialog.Title"] = "Güncelleme Mevcut",
                ["UpdateDialog.AvailableTitle"] = "Güncelleme Mevcut",
                ["UpdateDialog.CurrentVersionLabel"] = "MEVCUT SÜRÜM",
                ["UpdateDialog.NewVersionLabel"] = "YENİ SÜRÜM",
                ["UpdateDialog.Description"] = "NotiGlow'un yeni bir sürümü mevcut. Bu güncellemeyi şimdi indirip kurmak istiyor musunuz?",
                ["UpdateDialog.ReleaseNotes"] = "Sürüm Notları",
                ["UpdateDialog.UpdateNow"] = "Şimdi Güncelle",
                ["UpdateDialog.BtnUpdate"] = "Şimdi Güncelle",
                ["UpdateDialog.Cancel"] = "İptal",
                ["UpdateDialog.BtnCancel"] = "İptal",

                // v2.4 Features
                ["Appearance.CornerRounding"] = "Köşe Yuvarlaklığı",
                ["Appearance.CornerRoundingDesc"] = "Kartlar, giriş alanları ve kontroller için köşe yuvarlaklığı yarıçapını ayarlayın.",
                ["Applications.ScanApps"] = "Yüklü Uygulamaları Tara",
                ["Applications.ScanAppsToolTip"] = "Bu bilgisayardaki kurulu uygulamaları otomatik olarak tespit et",
                ["Applications.ScanComplete"] = "Uygulama taraması tamamlandı. {0} kurulu uygulama tespit edildi.",
                ["Gaming.CommonSettingsTitle"] = "Ortak Oyun Parlama Ayarları",
                ["Gaming.CommonSettingsDesc"] = "İzlenen tüm oyunlarda ortak geçerli olan süre, parıltı yoğunluğu ve bulanıklık boyutu.",
                ["Gaming.CommonDuration"] = "Süre",
                ["Gaming.CommonIntensity"] = "Parıltı Yoğunluğu",
                ["Gaming.CommonBlurSize"] = "Bulanıklık Boyutu"
            }
        };

        #endregion
    }
}
