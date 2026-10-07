using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public record LanguageItem(string Code, string DisplayName, string ShortCode);

    public class LocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public const string DefaultLanguage = "en-US";

        public static readonly IReadOnlyList<LanguageItem> SupportedLanguages = new[]
        {
            new LanguageItem("en-US", "English", "EN"),
            new LanguageItem("es-ES", "Español", "ES"),
            new LanguageItem("fr-FR", "Français", "FR"),
            new LanguageItem("tr-TR", "Türkçe", "TR")
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

        public void ApplyToWpfResources(ResourceDictionary? target = null)
        {
            try
            {
                if (target == null)
                {
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
                                app.Dispatcher.BeginInvoke(() => ApplyToWpfResources(target));
                            }
                            catch { }
                            return;
                        }
                    }

                    target = app.Resources;
                }

                if (target == null) return;

                var currentDict = GetRawDictionary(_currentLanguage);
                var fallbackDict = _translations[DefaultLanguage];

                foreach (var kvp in fallbackDict)
                {
                    string key = kvp.Key;
                    string val = currentDict.TryGetValue(key, out var localizedVal) && !string.IsNullOrEmpty(localizedVal)
                        ? localizedVal
                        : kvp.Value;

                    target[key] = val;
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

                // Tray
                ["Tray.GlowEnabled"] = "✓ Glow Enabled",
                ["Tray.GamingMode"] = "🎮 Gaming Mode",
                ["Tray.TestAnimation"] = "✨ Test Animation",
                ["Tray.OpenSettings"] = "⚙️ Open Settings",
                ["Tray.Exit"] = "❌ Exit",
                ["Tray.Tooltip"] = "NotiGlow - Ambient Notification Utility"
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

                // Tray
                ["Tray.GlowEnabled"] = "✓ Brillo Habilitado",
                ["Tray.GamingMode"] = "🎮 Modo Juego",
                ["Tray.TestAnimation"] = "✨ Probar Animación",
                ["Tray.OpenSettings"] = "⚙️ Abrir Configuración",
                ["Tray.Exit"] = "❌ Salir",
                ["Tray.Tooltip"] = "NotiGlow - Utilidad de Notificación Ambiental"
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

                // Tray
                ["Tray.GlowEnabled"] = "✓ Effet Lumineux Activé",
                ["Tray.GamingMode"] = "🎮 Mode Jeu",
                ["Tray.TestAnimation"] = "✨ Tester l'Animation",
                ["Tray.OpenSettings"] = "⚙️ Ouvrir les Paramètres",
                ["Tray.Exit"] = "❌ Quitter",
                ["Tray.Tooltip"] = "NotiGlow - Utilitaire de Notification Ambiante"
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

                // Tray
                ["Tray.GlowEnabled"] = "✓ Kenar Işığı Açık",
                ["Tray.GamingMode"] = "🎮 Oyun Modu",
                ["Tray.TestAnimation"] = "✨ Animasyonu Test Et",
                ["Tray.OpenSettings"] = "⚙️ Ayarları Aç",
                ["Tray.Exit"] = "❌ Çıkış",
                ["Tray.Tooltip"] = "NotiGlow - Ortam Bildirim Yardımcısı"
            }
        };

        #endregion
    }
}
