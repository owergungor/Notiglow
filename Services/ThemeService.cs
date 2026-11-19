using System;
using System.Collections.Generic;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using ThemeMode = NotiGlow.Models.ThemeMode;

namespace NotiGlow.Services
{
    public class ThemePalette
    {
        public string WindowBackground { get; init; } = "#0A0A0A";
        public string SidebarBackground { get; init; } = "#121212";
        public string CardBackground { get; init; } = "#2B2B2B";
        public string CardBackgroundSecondary { get; init; } = "#323232";
        public string TextPrimary { get; init; } = "#FFFFFF";
        public string TextSecondary { get; init; } = "#A0A0A0";
        public string TextMuted { get; init; } = "#707070";
        public string TextDisabled { get; init; } = "#4D4D4D";
        public string BorderColor { get; init; } = "#3D3D3D";
        public string AccentColor { get; init; } = "#3B82F6";
        public string ControlBackground { get; init; } = "#242424";
        public string ControlHoverBackground { get; init; } = "#2E2E2E";
        public string ControlPressedBackground { get; init; } = "#383838";
        public string ControlDisabledBackground { get; init; } = "#1A1A1A";
        public string InputBackground { get; init; } = "#242424";
        public string InputBorder { get; init; } = "#3D3D3D";
        public string DividerColor { get; init; } = "#333333";
        public string NavActiveBackground { get; init; } = "#2C2C2C";
        public string NavActiveHoverBackground { get; init; } = "#353535";
        public string NavIndicatorColor { get; init; } = "#3B82F6";
        public string SliderTrackBackground { get; init; } = "#404040";
        public string SliderTrackHoverBackground { get; init; } = "#505050";
        public string SliderThumbBackground { get; init; } = "#FFFFFF";
        public string SliderThumbBorder { get; init; } = "#3B82F6";
        public string SliderActiveTrackBackground { get; init; } = "#3B82F6";
        public string DropdownHoverBackground { get; init; } = "#353535";
        public string DropdownSelectedBackground { get; init; } = "#3B82F6";
        public string SkeletonShimmerBase { get; init; } = "#242424";
        public string SkeletonShimmerHighlight { get; init; } = "#333333";
    }

    public static class ThemeService
    {
        public static readonly IReadOnlyList<ColorTheme> CanonicalThemes = new[]
        {
            ColorTheme.Windows,
            ColorTheme.Blue,
            ColorTheme.Purple,
            ColorTheme.Pink,
            ColorTheme.Red,
            ColorTheme.Orange,
            ColorTheme.Yellow,
            ColorTheme.Green,
            ColorTheme.Teal
        };

        public static event EventHandler? ThemeChanged;

        private static readonly Dictionary<(ColorTheme, bool), ThemePalette> Palettes = new();
        private static readonly Dictionary<string, SolidColorBrush> BrushCache = new();
        private static Windows.UI.ViewManagement.UISettings? _uiSettings;
        private static bool _accentWatcherInitialized = false;

        static ThemeService()
        {
            RegisterPalettes();
            InitializeAccentWatcher();
        }

        public static Windows.UI.Color GetWindowsAccentColor()
        {
            try
            {
                var ui = new Windows.UI.ViewManagement.UISettings();
                return ui.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
            }
            catch
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                    if (key?.GetValue("AccentColor") is int val)
                    {
                        byte a = (byte)((val >> 24) & 0xFF);
                        byte b = (byte)((val >> 16) & 0xFF);
                        byte g = (byte)((val >> 8) & 0xFF);
                        byte r = (byte)(val & 0xFF);
                        if (a == 0) a = 255;
                        return Windows.UI.Color.FromArgb(a, r, g, b);
                    }
                }
                catch { }
                return Windows.UI.Color.FromArgb(255, 0, 120, 215); // Fallback Windows Blue #0078D7
            }
        }

        public static void InitializeAccentWatcher()
        {
            if (_accentWatcherInitialized) return;
            _accentWatcherInitialized = true;
            try
            {
                _uiSettings = new Windows.UI.ViewManagement.UISettings();
                _uiSettings.ColorValuesChanged += (sender, args) =>
                {
                    App.CurrentMainWindow?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        (Microsoft.UI.Xaml.Application.Current as App)?.ApplyCurrentSettingsTheme();
                    });
                };
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Failed to initialize UISettings accent watcher: {ex.Message}");
            }
        }

        private static ThemePalette CreateDarkPalette(string accent, string windowBg = "#202020", string sidebarBg = "#1C1C1C")
        {
            return new ThemePalette
            {
                WindowBackground = windowBg,
                SidebarBackground = sidebarBg,
                CardBackground = "#2B2B2B",
                CardBackgroundSecondary = "#323232",
                TextPrimary = "#FFFFFF",
                TextSecondary = "#A0A0A0",
                TextMuted = "#707070",
                TextDisabled = "#4D4D4D",
                BorderColor = "#3D3D3D",
                AccentColor = accent,
                ControlBackground = "#2E2E2E",
                ControlHoverBackground = "#353535",
                ControlPressedBackground = "#282828",
                ControlDisabledBackground = "#1F1F1F",
                InputBackground = "#262626",
                InputBorder = "#3D3D3D",
                DividerColor = "#333333",
                NavActiveBackground = "#2E2E2E",
                NavActiveHoverBackground = "#3A3A3A",
                NavIndicatorColor = accent,
                SliderTrackBackground = "#454545",
                SliderTrackHoverBackground = "#555555",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = accent,
                SliderActiveTrackBackground = accent,
                DropdownHoverBackground = "#353535",
                DropdownSelectedBackground = accent,
                SkeletonShimmerBase = "#242424",
                SkeletonShimmerHighlight = "#333333"
            };
        }

        private static ThemePalette CreateLightPalette(string accent, string windowBg = "#F3F3F3", string sidebarBg = "#EAEAEA")
        {
            return new ThemePalette
            {
                WindowBackground = windowBg,
                SidebarBackground = sidebarBg,
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#F9F9F9",
                TextPrimary = "#1A1A1A",
                TextSecondary = "#5E5E5E",
                TextMuted = "#8A8A8A",
                TextDisabled = "#CCCCCC",
                BorderColor = "#E5E5E5",
                AccentColor = accent,
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#F5F5F5",
                ControlPressedBackground = "#ECECEC",
                ControlDisabledBackground = "#FAFAFA",
                InputBackground = "#FFFFFF",
                InputBorder = "#E5E5E5",
                DividerColor = "#EBEBEB",
                NavActiveBackground = "#E2E2E2",
                NavActiveHoverBackground = "#D6D6D6",
                NavIndicatorColor = accent,
                SliderTrackBackground = "#CCCCCC",
                SliderTrackHoverBackground = "#BDBDBD",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = accent,
                SliderActiveTrackBackground = accent,
                DropdownHoverBackground = "#F5F5F5",
                DropdownSelectedBackground = accent,
                SkeletonShimmerBase = "#EAEAEA",
                SkeletonShimmerHighlight = "#F5F5F5"
            };
        }

        private static void RegisterPalettes()
        {
            // Standard Windows 11 Personalization accent colors on clean neutral surfaces
            Palettes[(ColorTheme.Blue, true)] = CreateDarkPalette("#3B82F6");
            Palettes[(ColorTheme.Blue, false)] = CreateLightPalette("#2563EB");

            Palettes[(ColorTheme.Purple, true)] = CreateDarkPalette("#820AD1");
            Palettes[(ColorTheme.Purple, false)] = CreateLightPalette("#7B0ABA");

            Palettes[(ColorTheme.Pink, true)] = CreateDarkPalette("#EC4899");
            Palettes[(ColorTheme.Pink, false)] = CreateLightPalette("#DB2777");

            Palettes[(ColorTheme.Red, true)] = CreateDarkPalette("#E11D48");
            Palettes[(ColorTheme.Red, false)] = CreateLightPalette("#BE123C");

            Palettes[(ColorTheme.Orange, true)] = CreateDarkPalette("#F59E0B");
            Palettes[(ColorTheme.Orange, false)] = CreateLightPalette("#D97706");

            Palettes[(ColorTheme.Yellow, true)] = CreateDarkPalette("#FFB900");
            Palettes[(ColorTheme.Yellow, false)] = CreateLightPalette("#FFB900");

            Palettes[(ColorTheme.Green, true)] = CreateDarkPalette("#10B981");
            Palettes[(ColorTheme.Green, false)] = CreateLightPalette("#059669");

            Palettes[(ColorTheme.Teal, true)] = CreateDarkPalette("#008272");
            Palettes[(ColorTheme.Teal, false)] = CreateLightPalette("#006B5E");
        }

        public static bool GetIsDark(ThemeMode mode)
        {
            return mode switch
            {
                ThemeMode.Light => false,
                ThemeMode.Dark => true,
                ThemeMode.System or _ => GetWindowsSystemThemeIsDark()
            };
        }

        public static ColorTheme NormalizeTheme(ColorTheme theme) => theme switch
        {
            ColorTheme.Standard or ColorTheme.Vercel or ColorTheme.Sapphire => ColorTheme.Blue,
            ColorTheme.Amethyst or ColorTheme.Violet => ColorTheme.Purple,
            ColorTheme.Sakura or ColorTheme.Bubblegum => ColorTheme.Pink,
            ColorTheme.Burgundy => ColorTheme.Red,
            ColorTheme.Amber or ColorTheme.Mocha => ColorTheme.Orange,
            ColorTheme.Zen => ColorTheme.Yellow,
            ColorTheme.Nature => ColorTheme.Green,
            ColorTheme.System => ColorTheme.Windows,
            _ => theme
        };

        public static ThemePalette GetPalette(ColorTheme theme, ThemeMode mode)
        {
            return GetPalette(theme, GetIsDark(mode));
        }

        public static ThemePalette GetPalette(ColorTheme theme, bool isDark)
        {
            theme = NormalizeTheme(theme);
            if (theme == ColorTheme.Windows)
            {
                var winAccent = GetWindowsAccentColor();
                string hex = $"#{winAccent.R:X2}{winAccent.G:X2}{winAccent.B:X2}";
                return isDark ? CreateDarkPalette(hex) : CreateLightPalette(hex);
            }

            if (Palettes.TryGetValue((theme, isDark), out var palette))
            {
                return palette;
            }
            return Palettes[(ColorTheme.Blue, isDark)];
        }

        private static bool? _lastAppliedIsDark = null;
        private static ColorTheme? _lastAppliedColorTheme = null;
        private static ThemeMode? _lastAppliedThemeMode = null;

        public static void ApplyTheme(ColorTheme colorTheme, ThemeMode mode)
        {
            try
            {
                bool isDark = GetIsDark(mode);

                if (_lastAppliedColorTheme == colorTheme && _lastAppliedThemeMode == mode && _lastAppliedIsDark == isDark)
                {
                    return;
                }

                _lastAppliedColorTheme = colorTheme;
                _lastAppliedIsDark = isDark;
                _lastAppliedThemeMode = mode;

                var palette = GetPalette(colorTheme, mode);
                var res = Microsoft.UI.Xaml.Application.Current?.Resources;
                if (res != null)
                {
                    SetBrush(res, "WindowBackground", palette.WindowBackground);
                    SetBrush(res, "SidebarBackground", palette.SidebarBackground);
                    SetBrush(res, "CardBackground", palette.CardBackground);
                    SetBrush(res, "CardBackgroundSecondary", palette.CardBackgroundSecondary);
                    SetBrush(res, "TextPrimary", palette.TextPrimary);
                    SetBrush(res, "TextSecondary", palette.TextSecondary);
                    SetBrush(res, "TextMuted", palette.TextMuted);
                    SetBrush(res, "TextDisabled", palette.TextDisabled);
                    SetBrush(res, "BorderColor", palette.BorderColor);
                    SetBrush(res, "AccentColor", palette.AccentColor);
                    SetBrush(res, "ControlBackground", palette.ControlBackground);
                    SetBrush(res, "ControlHoverBackground", palette.ControlHoverBackground);
                    SetBrush(res, "ControlPressedBackground", palette.ControlPressedBackground);
                    SetBrush(res, "ControlDisabledBackground", palette.ControlDisabledBackground);
                    SetBrush(res, "InputBackground", palette.InputBackground);
                    SetBrush(res, "InputBorder", palette.InputBorder);
                    SetBrush(res, "DividerColor", palette.DividerColor);
                    SetBrush(res, "NavActiveBackground", palette.NavActiveBackground);
                    SetBrush(res, "NavActiveHoverBackground", palette.NavActiveHoverBackground);
                    SetBrush(res, "NavIndicatorColor", palette.NavIndicatorColor);
                    SetBrush(res, "SliderTrackBackground", palette.SliderTrackBackground);
                    SetBrush(res, "SliderTrackHoverBackground", palette.SliderTrackHoverBackground);
                    SetBrush(res, "SliderThumbBackground", palette.SliderThumbBackground);
                    SetBrush(res, "SliderThumbBorder", palette.SliderThumbBorder);
                    SetBrush(res, "SliderActiveTrackBackground", palette.SliderActiveTrackBackground);
                    SetBrush(res, "DropdownHoverBackground", palette.DropdownHoverBackground);
                    SetBrush(res, "DropdownSelectedBackground", palette.DropdownSelectedBackground);
                    SetBrush(res, "SkeletonShimmerBase", palette.SkeletonShimmerBase);
                    SetBrush(res, "SkeletonShimmerHighlight", palette.SkeletonShimmerHighlight);

                    SetBrush(res, "CardControlBorderBrush", palette.BorderColor);
                    SetBrush(res, "CardControlHeaderBorderBrush", palette.DividerColor);
                    SetBrush(res, "CardControlSeparatorBrush", palette.DividerColor);
                    SetBrush(res, "ComboBoxBorderBrush", palette.BorderColor);
                    SetBrush(res, "ComboBoxDropDownBackground", palette.CardBackground);
                    SetBrush(res, "ComboBoxDropDownBorderBrush", palette.BorderColor);

                    SetBrush(res, "NavigationViewSelectionIndicatorForeground", palette.NavIndicatorColor);
                    SetBrush(res, "NavigationViewItemSelectionIndicatorForeground", palette.NavIndicatorColor);
                    SetBrush(res, "NavigationViewItemSelectionIndicatorForegroundPointerOver", palette.NavIndicatorColor);
                    SetBrush(res, "NavigationViewItemSelectionIndicatorForegroundPressed", palette.NavIndicatorColor);
                    SetBrush(res, "NavigationViewItemBackgroundSelected", palette.NavActiveBackground);
                    SetBrush(res, "NavigationViewItemBackgroundSelectedPointerOver", palette.NavActiveHoverBackground);
                    SetBrush(res, "NavigationViewItemBackgroundSelectedPressed", palette.NavActiveHoverBackground);
                    SetBrush(res, "NavigationViewItemBackgroundPointerOver", palette.ControlHoverBackground);
                    SetBrush(res, "NavigationViewItemForegroundSelected", isDark ? "#FFFFFF" : "#111111");
                    SetBrush(res, "NavigationViewItemForegroundSelectedPointerOver", isDark ? "#FFFFFF" : "#111111");

                    // WinUI 3 Native Accent Controls Sync
                    var accentColor = ColorHelper.ParseColor(palette.AccentColor);
                    res["SystemAccentColor"] = accentColor;
                    SetBrush(res, "SystemControlHighlightAccentBrush", palette.AccentColor);
                    SetBrush(res, "AccentButtonBackground", palette.AccentColor);
                    SetBrush(res, "AccentButtonBackgroundPointerOver", palette.AccentColor);
                    SetBrush(res, "AccentButtonBackgroundPressed", palette.AccentColor);
                    SetBrush(res, "ToggleSwitchFillOn", palette.AccentColor);
                    SetBrush(res, "ToggleSwitchFillOnPointerOver", palette.AccentColor);
                    SetBrush(res, "ToggleSwitchFillOnPressed", palette.AccentColor);
                    SetBrush(res, "SliderTrackValueFill", palette.AccentColor);
                    SetBrush(res, "SliderTrackValueFillPointerOver", palette.AccentColor);
                    SetBrush(res, "SliderTrackValueFillPressed", palette.AccentColor);
                    SetBrush(res, "ProgressBarPrimaryBrush", palette.AccentColor);

                    // ToggleSwitch Native Fluent brushes
                    SetBrush(res, "ToggleSwitchFillOff", "#00000000");
                    SetBrush(res, "ToggleSwitchFillOffPointerOver", isDark ? "#14FFFFFF" : "#0A000000");
                    SetBrush(res, "ToggleSwitchFillOffPressed", isDark ? "#24FFFFFF" : "#14000000");
                    SetBrush(res, "ToggleSwitchStrokeOff", isDark ? "#888888" : "#8A8A8A");
                    SetBrush(res, "ToggleSwitchStrokeOffPointerOver", isDark ? "#B0B0B0" : "#606060");
                    SetBrush(res, "ToggleSwitchStrokeOffPressed", isDark ? "#6E6E6E" : "#454545");
                    SetBrush(res, "ToggleSwitchKnobFillOff", isDark ? "#CECECE" : "#5D5D5D");
                    SetBrush(res, "ToggleSwitchKnobFillOn", "#FFFFFF");
                    SetBrush(res, "ToggleSwitchKnobFillOnPointerOver", "#FFFFFF");
                    SetBrush(res, "ToggleSwitchKnobFillOnPressed", "#FFFFFF");
                }

                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed applying theme in ThemeService", ex);
            }
        }

        public static void ApplyCornerRadius(double radius)
        {
            try
            {
                double clamped = Math.Clamp(radius, 0.0, 48.0);
                var cornerRadius = new Microsoft.UI.Xaml.CornerRadius(clamped);
                var res = Microsoft.UI.Xaml.Application.Current?.Resources;
                if (res != null)
                {
                    res["GlowCornerRadius"] = cornerRadius;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to apply CornerRadius in ThemeService", ex);
            }
        }

        private static void SetBrush(ResourceDictionary res, string key, string hexColor)
        {
            var color = ColorHelper.ParseColor(hexColor);
            if (res.TryGetValue(key, out var existing) && existing is SolidColorBrush sb)
            {
                sb.Color = color;
            }
            else
            {
                res[key] = new SolidColorBrush(color);
            }
        }

        public static bool GetWindowsSystemThemeIsDark()
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
    }
}
