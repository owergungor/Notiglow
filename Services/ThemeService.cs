using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using NotiGlow.Models;
using Wpf.Ui.Appearance;
using ThemeMode = NotiGlow.Models.ThemeMode;

namespace NotiGlow.Services
{
    public class ThemePalette
    {
        public string WindowBackground { get; init; } = "#141414";
        public string SidebarBackground { get; init; } = "#181818";
        public string CardBackground { get; init; } = "#202020";
        public string CardBackgroundSecondary { get; init; } = "#1B1B1B";
        public string TextPrimary { get; init; } = "#FFFFFF";
        public string TextSecondary { get; init; } = "#B8B8B8";
        public string TextMuted { get; init; } = "#808080";
        public string TextDisabled { get; init; } = "#555555";
        public string BorderColor { get; init; } = "#383838";
        public string AccentColor { get; init; } = "#5865F2";
        public string ControlBackground { get; init; } = "#262626";
        public string ControlHoverBackground { get; init; } = "#303030";
        public string ControlPressedBackground { get; init; } = "#383838";
        public string ControlDisabledBackground { get; init; } = "#181818";
        public string InputBackground { get; init; } = "#1C1C1C";
        public string InputBorder { get; init; } = "#383838";
        public string DividerColor { get; init; } = "#282828";
        public string NavActiveBackground { get; init; } = "#2E2E2E";
        public string NavActiveHoverBackground { get; init; } = "#363636";
        public string NavIndicatorColor { get; init; } = "#5865F2";
        public string SliderTrackBackground { get; init; } = "#404552";
        public string SliderTrackHoverBackground { get; init; } = "#505668";
        public string SliderThumbBackground { get; init; } = "#FFFFFF";
        public string SliderThumbBorder { get; init; } = "#5865F2";
        public string SliderActiveTrackBackground { get; init; } = "#5865F2";
        public string DropdownHoverBackground { get; init; } = "#303030";
        public string DropdownSelectedBackground { get; init; } = "#5865F2";
        public string SkeletonShimmerBase { get; init; } = "#2A2A2A";
        public string SkeletonShimmerHighlight { get; init; } = "#3D3D3D";
        public string GlassOverlay { get; init; } = "#00000000";
        public string GlassBorder { get; init; } = "#383838";
    }

    public static class ThemeService
    {
        public static readonly IReadOnlyList<ColorTheme> CanonicalThemes = new[]
        {
            ColorTheme.Standard,
            ColorTheme.Zen,
            ColorTheme.Amber,
            ColorTheme.Mocha,
            ColorTheme.Burgundy,
            ColorTheme.Sakura,
            ColorTheme.Bubblegum,
            ColorTheme.Amethyst,
            ColorTheme.Violet,
            ColorTheme.Indigo,
            ColorTheme.Sapphire,
            ColorTheme.Nature
        };

        private static readonly Dictionary<(ColorTheme, bool), ThemePalette> Palettes = new();
        private static readonly Dictionary<string, SolidColorBrush> BrushCache = new();

        static ThemeService()
        {
            RegisterPalettes();
        }

        private static void RegisterPalettes()
        {
            // 1. Standard (Vercel)
            Palettes[(ColorTheme.Standard, true)] = new ThemePalette
            {
                WindowBackground = "#0A0A0A",
                SidebarBackground = "#121212",
                CardBackground = "#171717",
                CardBackgroundSecondary = "#1C1C1C",
                TextPrimary = "#EDEDED",
                TextSecondary = "#A1A1A1",
                TextMuted = "#666666",
                TextDisabled = "#444444",
                BorderColor = "#262626",
                AccentColor = "#3B82F6",
                ControlBackground = "#1F1F1F",
                ControlHoverBackground = "#292929",
                ControlPressedBackground = "#333333",
                ControlDisabledBackground = "#141414",
                InputBackground = "#141414",
                InputBorder = "#262626",
                DividerColor = "#202020",
                NavActiveBackground = "#222222",
                NavActiveHoverBackground = "#2C2C2C",
                NavIndicatorColor = "#3B82F6",
                SliderTrackBackground = "#333333",
                SliderTrackHoverBackground = "#444444",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#3B82F6",
                SliderActiveTrackBackground = "#3B82F6",
                DropdownHoverBackground = "#262626",
                DropdownSelectedBackground = "#3B82F6",
                SkeletonShimmerBase = "#1F1F1F",
                SkeletonShimmerHighlight = "#2F2F2F"
            };
            Palettes[(ColorTheme.Standard, false)] = new ThemePalette
            {
                WindowBackground = "#FAFAFA",
                SidebarBackground = "#F4F4F5",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#F9F9FB",
                TextPrimary = "#09090B",
                TextSecondary = "#71717A",
                TextMuted = "#A1A1AA",
                TextDisabled = "#D4D4D8",
                BorderColor = "#E4E4E7",
                AccentColor = "#2563EB",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#F4F4F5",
                ControlPressedBackground = "#E4E4E7",
                ControlDisabledBackground = "#FAFAFA",
                InputBackground = "#FFFFFF",
                InputBorder = "#E4E4E7",
                DividerColor = "#ECECEF",
                NavActiveBackground = "#E4E4E7",
                NavActiveHoverBackground = "#D4D4D8",
                NavIndicatorColor = "#2563EB",
                SliderTrackBackground = "#D4D4D8",
                SliderTrackHoverBackground = "#A1A1AA",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#2563EB",
                SliderActiveTrackBackground = "#2563EB",
                DropdownHoverBackground = "#F4F4F5",
                DropdownSelectedBackground = "#2563EB",
                SkeletonShimmerBase = "#E8E8EC",
                SkeletonShimmerHighlight = "#F4F4F7"
            };

            // 2. Zen (Zen Linen)
            Palettes[(ColorTheme.Zen, true)] = new ThemePalette
            {
                WindowBackground = "#181614",
                SidebarBackground = "#1E1B18",
                CardBackground = "#25221E",
                CardBackgroundSecondary = "#211E1A",
                TextPrimary = "#F2ECE4",
                TextSecondary = "#B8AEA2",
                TextMuted = "#827768",
                TextDisabled = "#574E43",
                BorderColor = "#3B352E",
                AccentColor = "#C7A97B",
                ControlBackground = "#2A2621",
                ControlHoverBackground = "#35302A",
                ControlPressedBackground = "#3F3A33",
                ControlDisabledBackground = "#1E1B18",
                InputBackground = "#1E1B18",
                InputBorder = "#3B352E",
                DividerColor = "#302B24",
                NavActiveBackground = "#312C25",
                NavActiveHoverBackground = "#3D372F",
                NavIndicatorColor = "#C7A97B",
                SliderTrackBackground = "#473F35",
                SliderTrackHoverBackground = "#5C5245",
                SliderThumbBackground = "#F2ECE4",
                SliderThumbBorder = "#C7A97B",
                SliderActiveTrackBackground = "#C7A97B",
                DropdownHoverBackground = "#35302A",
                DropdownSelectedBackground = "#C7A97B",
                SkeletonShimmerBase = "#2E2A24",
                SkeletonShimmerHighlight = "#3E3830"
            };
            Palettes[(ColorTheme.Zen, false)] = new ThemePalette
            {
                WindowBackground = "#F9F6F0",
                SidebarBackground = "#F2ECE2",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#F5EFE5",
                TextPrimary = "#2C261E",
                TextSecondary = "#706555",
                TextMuted = "#9C8F7C",
                TextDisabled = "#C4B8A6",
                BorderColor = "#E0D5C3",
                AccentColor = "#9E7B4F",
                ControlBackground = "#FDFBF7",
                ControlHoverBackground = "#F0E7D8",
                ControlPressedBackground = "#E5D9C7",
                ControlDisabledBackground = "#F2ECE2",
                InputBackground = "#FFFFFF",
                InputBorder = "#E0D5C3",
                DividerColor = "#E8DEC8",
                NavActiveBackground = "#E8DEC8",
                NavActiveHoverBackground = "#DDD1B9",
                NavIndicatorColor = "#9E7B4F",
                SliderTrackBackground = "#D1C3AC",
                SliderTrackHoverBackground = "#B8A88E",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#9E7B4F",
                SliderActiveTrackBackground = "#9E7B4F",
                DropdownHoverBackground = "#F0E7D8",
                DropdownSelectedBackground = "#9E7B4F",
                SkeletonShimmerBase = "#E8DFC9",
                SkeletonShimmerHighlight = "#F5EEDC"
            };

            // 3. Indigo (Indigo Frost)
            Palettes[(ColorTheme.Indigo, true)] = new ThemePalette
            {
                WindowBackground = "#0B0F19",
                SidebarBackground = "#111827",
                CardBackground = "#162035",
                CardBackgroundSecondary = "#131C2E",
                TextPrimary = "#F0F4FF",
                TextSecondary = "#94A3B8",
                TextMuted = "#64748B",
                TextDisabled = "#475569",
                BorderColor = "#22324F",
                AccentColor = "#6366F1",
                ControlBackground = "#1E293B",
                ControlHoverBackground = "#2A3A54",
                ControlPressedBackground = "#344866",
                ControlDisabledBackground = "#111827",
                InputBackground = "#0F172A",
                InputBorder = "#22324F",
                DividerColor = "#1C2942",
                NavActiveBackground = "#22314E",
                NavActiveHoverBackground = "#2B3E62",
                NavIndicatorColor = "#6366F1",
                SliderTrackBackground = "#334155",
                SliderTrackHoverBackground = "#475569",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#6366F1",
                SliderActiveTrackBackground = "#6366F1",
                DropdownHoverBackground = "#2A3A54",
                DropdownSelectedBackground = "#6366F1",
                SkeletonShimmerBase = "#1E293B",
                SkeletonShimmerHighlight = "#303F57"
            };
            Palettes[(ColorTheme.Indigo, false)] = new ThemePalette
            {
                WindowBackground = "#F4F7FC",
                SidebarBackground = "#EBF0F9",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#F1F5FB",
                TextPrimary = "#0F172A",
                TextSecondary = "#475569",
                TextMuted = "#94A3B8",
                TextDisabled = "#CBD5E1",
                BorderColor = "#D4E0F2",
                AccentColor = "#4F46E5",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#E2EAF8",
                ControlPressedBackground = "#D3E0F4",
                ControlDisabledBackground = "#EBF0F9",
                InputBackground = "#FFFFFF",
                InputBorder = "#D4E0F2",
                DividerColor = "#E1EBF7",
                NavActiveBackground = "#D9E5F7",
                NavActiveHoverBackground = "#CADCF4",
                NavIndicatorColor = "#4F46E5",
                SliderTrackBackground = "#CBD5E1",
                SliderTrackHoverBackground = "#94A3B8",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#4F46E5",
                SliderActiveTrackBackground = "#4F46E5",
                DropdownHoverBackground = "#E2EAF8",
                DropdownSelectedBackground = "#4F46E5",
                SkeletonShimmerBase = "#E0E9F7",
                SkeletonShimmerHighlight = "#F0F5FD"
            };

            // 4. Sapphire (Promotor WOW)
            Palettes[(ColorTheme.Sapphire, true)] = new ThemePalette
            {
                WindowBackground = "#07111E",
                SidebarBackground = "#0B1B2F",
                CardBackground = "#102642",
                CardBackgroundSecondary = "#0D2038",
                TextPrimary = "#EDF6FF",
                TextSecondary = "#8CB6DE",
                TextMuted = "#5883AB",
                TextDisabled = "#375775",
                BorderColor = "#1B3E6B",
                AccentColor = "#0070F3",
                ControlBackground = "#122C4D",
                ControlHoverBackground = "#1C4170",
                ControlPressedBackground = "#25538D",
                ControlDisabledBackground = "#0B1B2F",
                InputBackground = "#09182B",
                InputBorder = "#1B3E6B",
                DividerColor = "#163359",
                NavActiveBackground = "#173761",
                NavActiveHoverBackground = "#1F487F",
                NavIndicatorColor = "#0070F3",
                SliderTrackBackground = "#254E80",
                SliderTrackHoverBackground = "#3369AC",
                SliderThumbBackground = "#EDF6FF",
                SliderThumbBorder = "#0070F3",
                SliderActiveTrackBackground = "#0070F3",
                DropdownHoverBackground = "#1C4170",
                DropdownSelectedBackground = "#0070F3",
                SkeletonShimmerBase = "#163359",
                SkeletonShimmerHighlight = "#234C82"
            };
            Palettes[(ColorTheme.Sapphire, false)] = new ThemePalette
            {
                WindowBackground = "#F0F6FC",
                SidebarBackground = "#E3EFFB",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#EAF3FC",
                TextPrimary = "#0A2540",
                TextSecondary = "#425466",
                TextMuted = "#7B91A8",
                TextDisabled = "#B8C9DC",
                BorderColor = "#CCE0F5",
                AccentColor = "#0066DB",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#E1EFFD",
                ControlPressedBackground = "#D1E5FC",
                ControlDisabledBackground = "#E3EFFB",
                InputBackground = "#FFFFFF",
                InputBorder = "#CCE0F5",
                DividerColor = "#D8E9FB",
                NavActiveBackground = "#D5E7FA",
                NavActiveHoverBackground = "#C4DCF7",
                NavIndicatorColor = "#0066DB",
                SliderTrackBackground = "#B8D7F7",
                SliderTrackHoverBackground = "#8FBAE6",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#0066DB",
                SliderActiveTrackBackground = "#0066DB",
                DropdownHoverBackground = "#E1EFFD",
                DropdownSelectedBackground = "#0066DB",
                SkeletonShimmerBase = "#D4E6FA",
                SkeletonShimmerHighlight = "#ECF4FD"
            };

            // 5. Burgundy (Burgundy Wine)
            Palettes[(ColorTheme.Burgundy, true)] = new ThemePalette
            {
                WindowBackground = "#15080C",
                SidebarBackground = "#1C0D12",
                CardBackground = "#26121B",
                CardBackgroundSecondary = "#210F17",
                TextPrimary = "#FDE8EF",
                TextSecondary = "#C997A8",
                TextMuted = "#8E5B6E",
                TextDisabled = "#5C3644",
                BorderColor = "#421D2C",
                AccentColor = "#E11D48",
                ControlBackground = "#2E1621",
                ControlHoverBackground = "#3C1C2B",
                ControlPressedBackground = "#4D2437",
                ControlDisabledBackground = "#1C0D12",
                InputBackground = "#1A0C13",
                InputBorder = "#421D2C",
                DividerColor = "#361624",
                NavActiveBackground = "#3B1A2A",
                NavActiveHoverBackground = "#492135",
                NavIndicatorColor = "#E11D48",
                SliderTrackBackground = "#572439",
                SliderTrackHoverBackground = "#73304B",
                SliderThumbBackground = "#FDE8EF",
                SliderThumbBorder = "#E11D48",
                SliderActiveTrackBackground = "#E11D48",
                DropdownHoverBackground = "#3C1C2B",
                DropdownSelectedBackground = "#E11D48",
                SkeletonShimmerBase = "#301723",
                SkeletonShimmerHighlight = "#4A2437"
            };
            Palettes[(ColorTheme.Burgundy, false)] = new ThemePalette
            {
                WindowBackground = "#FDF2F5",
                SidebarBackground = "#F8E4EB",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#FAECF1",
                TextPrimary = "#3B0A1A",
                TextSecondary = "#7E354D",
                TextMuted = "#A6637B",
                TextDisabled = "#CCA3B2",
                BorderColor = "#F0CAD7",
                AccentColor = "#BE123C",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#F5D6E1",
                ControlPressedBackground = "#ECC3D2",
                ControlDisabledBackground = "#F8E4EB",
                InputBackground = "#FFFFFF",
                InputBorder = "#F0CAD7",
                DividerColor = "#ECC0CF",
                NavActiveBackground = "#F2D0DE",
                NavActiveHoverBackground = "#E9BFD0",
                NavIndicatorColor = "#BE123C",
                SliderTrackBackground = "#DF9EB5",
                SliderTrackHoverBackground = "#C77995",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#BE123C",
                SliderActiveTrackBackground = "#BE123C",
                DropdownHoverBackground = "#F5D6E1",
                DropdownSelectedBackground = "#BE123C",
                SkeletonShimmerBase = "#E8C2D0",
                SkeletonShimmerHighlight = "#F8DEE7"
            };

            // 6. Nature
            Palettes[(ColorTheme.Nature, true)] = new ThemePalette
            {
                WindowBackground = "#0A130D",
                SidebarBackground = "#101E15",
                CardBackground = "#16281D",
                CardBackgroundSecondary = "#122218",
                TextPrimary = "#E8F8EE",
                TextSecondary = "#96C2A4",
                TextMuted = "#5E8F6E",
                TextDisabled = "#3C5E47",
                BorderColor = "#24412E",
                AccentColor = "#10B981",
                ControlBackground = "#1C3225",
                ControlHoverBackground = "#274432",
                ControlPressedBackground = "#325740",
                ControlDisabledBackground = "#101E15",
                InputBackground = "#0E1B13",
                InputBorder = "#24412E",
                DividerColor = "#1F382A",
                NavActiveBackground = "#203B2A",
                NavActiveHoverBackground = "#294C37",
                NavIndicatorColor = "#10B981",
                SliderTrackBackground = "#32573F",
                SliderTrackHoverBackground = "#437354",
                SliderThumbBackground = "#E8F8EE",
                SliderThumbBorder = "#10B981",
                SliderActiveTrackBackground = "#10B981",
                DropdownHoverBackground = "#274432",
                DropdownSelectedBackground = "#10B981",
                SkeletonShimmerBase = "#1B3324",
                SkeletonShimmerHighlight = "#294D38"
            };
            Palettes[(ColorTheme.Nature, false)] = new ThemePalette
            {
                WindowBackground = "#F1F9F4",
                SidebarBackground = "#E5F3EA",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#ECF6EF",
                TextPrimary = "#0D2B17",
                TextSecondary = "#3E694D",
                TextMuted = "#76A184",
                TextDisabled = "#B0CEBC",
                BorderColor = "#C9E6D3",
                AccentColor = "#059669",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#D9F0E1",
                ControlPressedBackground = "#C8E8D3",
                ControlDisabledBackground = "#E5F3EA",
                InputBackground = "#FFFFFF",
                InputBorder = "#C9E6D3",
                DividerColor = "#D1EBDA",
                NavActiveBackground = "#CEEADB",
                NavActiveHoverBackground = "#BBDDC9",
                NavIndicatorColor = "#059669",
                SliderTrackBackground = "#AEDBBF",
                SliderTrackHoverBackground = "#8AC5A0",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#059669",
                SliderActiveTrackBackground = "#059669",
                DropdownHoverBackground = "#D9F0E1",
                DropdownSelectedBackground = "#059669",
                SkeletonShimmerBase = "#CCE5D5",
                SkeletonShimmerHighlight = "#E7F4EB"
            };

            // Amethyst (formerly Nubank palette: Royal Purple)
            Palettes[(ColorTheme.Amethyst, true)] = new ThemePalette
            {
                WindowBackground = "#11091A",
                SidebarBackground = "#1A0E28",
                CardBackground = "#241437",
                CardBackgroundSecondary = "#1F1130",
                TextPrimary = "#FAF5FF",
                TextSecondary = "#C4A6E8",
                TextMuted = "#8865B3",
                TextDisabled = "#563A7A",
                BorderColor = "#3E2360",
                AccentColor = "#820AD1",
                ControlBackground = "#2D1945",
                ControlHoverBackground = "#3C225B",
                ControlPressedBackground = "#4C2B74",
                ControlDisabledBackground = "#1A0E28",
                InputBackground = "#170D23",
                InputBorder = "#3E2360",
                DividerColor = "#341C52",
                NavActiveBackground = "#361D53",
                NavActiveHoverBackground = "#45256A",
                NavIndicatorColor = "#820AD1",
                SliderTrackBackground = "#572F85",
                SliderTrackHoverBackground = "#703DAC",
                SliderThumbBackground = "#FAF5FF",
                SliderThumbBorder = "#820AD1",
                SliderActiveTrackBackground = "#820AD1",
                DropdownHoverBackground = "#3C225B",
                DropdownSelectedBackground = "#820AD1",
                SkeletonShimmerBase = "#301B4B",
                SkeletonShimmerHighlight = "#4A2A73"
            };
            Palettes[(ColorTheme.Amethyst, false)] = new ThemePalette
            {
                WindowBackground = "#F8F3FD",
                SidebarBackground = "#F1E3FC",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#F5EBFC",
                TextPrimary = "#2B0847",
                TextSecondary = "#6D2E9C",
                TextMuted = "#9E67C9",
                TextDisabled = "#C8A3E7",
                BorderColor = "#E2C4FA",
                AccentColor = "#7B0ABA",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#ECCFFB",
                ControlPressedBackground = "#E0B7F8",
                ControlDisabledBackground = "#F1E3FC",
                InputBackground = "#FFFFFF",
                InputBorder = "#E2C4FA",
                DividerColor = "#E5C2F9",
                NavActiveBackground = "#E8CEFA",
                NavActiveHoverBackground = "#D7B5F3",
                NavIndicatorColor = "#7B0ABA",
                SliderTrackBackground = "#CEA2F4",
                SliderTrackHoverBackground = "#B27FE0",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#7B0ABA",
                SliderActiveTrackBackground = "#7B0ABA",
                DropdownHoverBackground = "#ECCFFB",
                DropdownSelectedBackground = "#7B0ABA",
                SkeletonShimmerBase = "#E0C4F7",
                SkeletonShimmerHighlight = "#F5E8FD"
            };

            // 8. Mocha (Mocha Mousse)
            Palettes[(ColorTheme.Mocha, true)] = new ThemePalette
            {
                WindowBackground = "#16110D",
                SidebarBackground = "#1E1712",
                CardBackground = "#281F19",
                CardBackgroundSecondary = "#231B15",
                TextPrimary = "#F9F0E8",
                TextSecondary = "#C4AEA0",
                TextMuted = "#887060",
                TextDisabled = "#5C493B",
                BorderColor = "#423227",
                AccentColor = "#D97706",
                ControlBackground = "#31261E",
                ControlHoverBackground = "#3F3127",
                ControlPressedBackground = "#4F3E32",
                ControlDisabledBackground = "#1E1712",
                InputBackground = "#1C1510",
                InputBorder = "#423227",
                DividerColor = "#362A20",
                NavActiveBackground = "#3B2D23",
                NavActiveHoverBackground = "#4A392D",
                NavIndicatorColor = "#D97706",
                SliderTrackBackground = "#584435",
                SliderTrackHoverBackground = "#735946",
                SliderThumbBackground = "#F9F0E8",
                SliderThumbBorder = "#D97706",
                SliderActiveTrackBackground = "#D97706",
                DropdownHoverBackground = "#3F3127",
                DropdownSelectedBackground = "#D97706",
                SkeletonShimmerBase = "#32271E",
                SkeletonShimmerHighlight = "#4E3C30"
            };
            Palettes[(ColorTheme.Mocha, false)] = new ThemePalette
            {
                WindowBackground = "#FBF6F1",
                SidebarBackground = "#F4EAE1",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#F7EFE7",
                TextPrimary = "#332014",
                TextSecondary = "#735643",
                TextMuted = "#A1826D",
                TextDisabled = "#CAB3A1",
                BorderColor = "#E7D6C7",
                AccentColor = "#B45309",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#EFE0D3",
                ControlPressedBackground = "#E3CEBE",
                ControlDisabledBackground = "#F4EAE1",
                InputBackground = "#FFFFFF",
                InputBorder = "#E7D6C7",
                DividerColor = "#E5D2C0",
                NavActiveBackground = "#ECDDD0",
                NavActiveHoverBackground = "#DFCBBB",
                NavIndicatorColor = "#B45309",
                SliderTrackBackground = "#CFB49F",
                SliderTrackHoverBackground = "#B29177",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#B45309",
                SliderActiveTrackBackground = "#B45309",
                DropdownHoverBackground = "#EFE0D3",
                DropdownSelectedBackground = "#B45309",
                SkeletonShimmerBase = "#E8D5C4",
                SkeletonShimmerHighlight = "#F7ECE2"
            };

            // 9. Sakura (Sakura Dawn)
            Palettes[(ColorTheme.Sakura, true)] = new ThemePalette
            {
                WindowBackground = "#180D13",
                SidebarBackground = "#22131C",
                CardBackground = "#2E1A26",
                CardBackgroundSecondary = "#271620",
                TextPrimary = "#FFF0F6",
                TextSecondary = "#DEABC2",
                TextMuted = "#A06A83",
                TextDisabled = "#6B3E54",
                BorderColor = "#4A2A3E",
                AccentColor = "#F43F5E",
                ControlBackground = "#361F2D",
                ControlHoverBackground = "#46293B",
                ControlPressedBackground = "#573349",
                ControlDisabledBackground = "#22131C",
                InputBackground = "#1E1018",
                InputBorder = "#4A2A3E",
                DividerColor = "#3E2333",
                NavActiveBackground = "#412436",
                NavActiveHoverBackground = "#502D43",
                NavIndicatorColor = "#F43F5E",
                SliderTrackBackground = "#633952",
                SliderTrackHoverBackground = "#804A6B",
                SliderThumbBackground = "#FFF0F6",
                SliderThumbBorder = "#F43F5E",
                SliderActiveTrackBackground = "#F43F5E",
                DropdownHoverBackground = "#46293B",
                DropdownSelectedBackground = "#F43F5E",
                SkeletonShimmerBase = "#38202F",
                SkeletonShimmerHighlight = "#543046"
            };
            Palettes[(ColorTheme.Sakura, false)] = new ThemePalette
            {
                WindowBackground = "#FFF5F8",
                SidebarBackground = "#FFEAF1",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#FFF0F4",
                TextPrimary = "#3B1024",
                TextSecondary = "#8E4467",
                TextMuted = "#B87B99",
                TextDisabled = "#DCABC3",
                BorderColor = "#FFCCE0",
                AccentColor = "#E11D48",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#FFDDE9",
                ControlPressedBackground = "#FFCCD9",
                ControlDisabledBackground = "#FFEAF1",
                InputBackground = "#FFFFFF",
                InputBorder = "#FFCCE0",
                DividerColor = "#FFD4E4",
                NavActiveBackground = "#FFDCE8",
                NavActiveHoverBackground = "#F7C9D8",
                NavIndicatorColor = "#E11D48",
                SliderTrackBackground = "#FFB8D2",
                SliderTrackHoverBackground = "#E88DAF",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#E11D48",
                SliderActiveTrackBackground = "#E11D48",
                DropdownHoverBackground = "#FFDDE9",
                DropdownSelectedBackground = "#E11D48",
                SkeletonShimmerBase = "#F7CEDB",
                SkeletonShimmerHighlight = "#FFEBF1"
            };

            // 10. Amber (Amber Hearth)
            Palettes[(ColorTheme.Amber, true)] = new ThemePalette
            {
                WindowBackground = "#171105",
                SidebarBackground = "#211808",
                CardBackground = "#2E220D",
                CardBackgroundSecondary = "#271C0A",
                TextPrimary = "#FEF9EE",
                TextSecondary = "#D4B87C",
                TextMuted = "#94783E",
                TextDisabled = "#614C23",
                BorderColor = "#4A3816",
                AccentColor = "#F59E0B",
                ControlBackground = "#382A10",
                ControlHoverBackground = "#483616",
                ControlPressedBackground = "#59431D",
                ControlDisabledBackground = "#211808",
                InputBackground = "#1D1507",
                InputBorder = "#4A3816",
                DividerColor = "#3E2E11",
                NavActiveBackground = "#403013",
                NavActiveHoverBackground = "#4F3C18",
                NavIndicatorColor = "#F59E0B",
                SliderTrackBackground = "#634C1E",
                SliderTrackHoverBackground = "#806328",
                SliderThumbBackground = "#FEF9EE",
                SliderThumbBorder = "#F59E0B",
                SliderActiveTrackBackground = "#F59E0B",
                DropdownHoverBackground = "#483616",
                DropdownSelectedBackground = "#F59E0B",
                SkeletonShimmerBase = "#382B13",
                SkeletonShimmerHighlight = "#54411C"
            };
            Palettes[(ColorTheme.Amber, false)] = new ThemePalette
            {
                WindowBackground = "#FFFBEB",
                SidebarBackground = "#FEF3C7",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#FFFDF5",
                TextPrimary = "#382003",
                TextSecondary = "#85530D",
                TextMuted = "#B38031",
                TextDisabled = "#D6B06E",
                BorderColor = "#FDE68A",
                AccentColor = "#D97706",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#FEE89D",
                ControlPressedBackground = "#FCDA74",
                ControlDisabledBackground = "#FEF3C7",
                InputBackground = "#FFFFFF",
                InputBorder = "#FDE68A",
                DividerColor = "#FCD34D",
                NavActiveBackground = "#FDEAB0",
                NavActiveHoverBackground = "#F7D88B",
                NavIndicatorColor = "#D97706",
                SliderTrackBackground = "#FBBF24",
                SliderTrackHoverBackground = "#D99B16",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#D97706",
                SliderActiveTrackBackground = "#D97706",
                DropdownHoverBackground = "#FEE89D",
                DropdownSelectedBackground = "#D97706",
                SkeletonShimmerBase = "#F7DE9B",
                SkeletonShimmerHighlight = "#FFF4CE"
            };


            // 12. Violet (Violet Dusk)
            Palettes[(ColorTheme.Violet, true)] = new ThemePalette
            {
                WindowBackground = "#0E0C1B",
                SidebarBackground = "#151229",
                CardBackground = "#1F1B3C",
                CardBackgroundSecondary = "#1A1633",
                TextPrimary = "#F3F1FF",
                TextSecondary = "#A8A1D9",
                TextMuted = "#6F67A4",
                TextDisabled = "#4A4375",
                BorderColor = "#342E5E",
                AccentColor = "#6366F1",
                ControlBackground = "#27224B",
                ControlHoverBackground = "#332D62",
                ControlPressedBackground = "#40397B",
                ControlDisabledBackground = "#151229",
                InputBackground = "#131026",
                InputBorder = "#342E5E",
                DividerColor = "#2D2754",
                NavActiveBackground = "#2E2758",
                NavActiveHoverBackground = "#3B3270",
                NavIndicatorColor = "#6366F1",
                SliderTrackBackground = "#4B4286",
                SliderTrackHoverBackground = "#6156AC",
                SliderThumbBackground = "#F3F1FF",
                SliderThumbBorder = "#6366F1",
                SliderActiveTrackBackground = "#6366F1",
                DropdownHoverBackground = "#332D62",
                DropdownSelectedBackground = "#6366F1",
                SkeletonShimmerBase = "#292451",
                SkeletonShimmerHighlight = "#3E3778"
            };
            Palettes[(ColorTheme.Violet, false)] = new ThemePalette
            {
                WindowBackground = "#F5F4FF",
                SidebarBackground = "#ECE9FE",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#F2F0FF",
                TextPrimary = "#17133E",
                TextSecondary = "#4A4287",
                TextMuted = "#867EC0",
                TextDisabled = "#BCB5E4",
                BorderColor = "#D6D1FD",
                AccentColor = "#4F46E5",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#E2DEFE",
                ControlPressedBackground = "#D1CBFD",
                ControlDisabledBackground = "#ECE9FE",
                InputBackground = "#FFFFFF",
                InputBorder = "#D6D1FD",
                DividerColor = "#D9D4FD",
                NavActiveBackground = "#DFDAFD",
                NavActiveHoverBackground = "#CDC5FB",
                NavIndicatorColor = "#4F46E5",
                SliderTrackBackground = "#C0B7FA",
                SliderTrackHoverBackground = "#9F92EC",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#4F46E5",
                SliderActiveTrackBackground = "#4F46E5",
                DropdownHoverBackground = "#E2DEFE",
                DropdownSelectedBackground = "#4F46E5",
                SkeletonShimmerBase = "#D9D3F8",
                SkeletonShimmerHighlight = "#F1EEFE"
            };

            // 13. Bubblegum (Bubblegum Remix)
            Palettes[(ColorTheme.Bubblegum, true)] = new ThemePalette
            {
                WindowBackground = "#180B18",
                SidebarBackground = "#221023",
                CardBackground = "#2E172F",
                CardBackgroundSecondary = "#271328",
                TextPrimary = "#FFF0FE",
                TextSecondary = "#E09EDC",
                TextMuted = "#A15D9C",
                TextDisabled = "#6C3869",
                BorderColor = "#4C244D",
                AccentColor = "#EC4899",
                ControlBackground = "#381B39",
                ControlHoverBackground = "#49244A",
                ControlPressedBackground = "#5B2D5D",
                ControlDisabledBackground = "#221023",
                InputBackground = "#1D0D1E",
                InputBorder = "#4C244D",
                DividerColor = "#3F1F40",
                NavActiveBackground = "#432044",
                NavActiveHoverBackground = "#542856",
                NavIndicatorColor = "#EC4899",
                SliderTrackBackground = "#653067",
                SliderTrackHoverBackground = "#833E85",
                SliderThumbBackground = "#FFF0FE",
                SliderThumbBorder = "#EC4899",
                SliderActiveTrackBackground = "#EC4899",
                DropdownHoverBackground = "#49244A",
                DropdownSelectedBackground = "#EC4899",
                SkeletonShimmerBase = "#3A1C3C",
                SkeletonShimmerHighlight = "#592A5C"
            };
            Palettes[(ColorTheme.Bubblegum, false)] = new ThemePalette
            {
                WindowBackground = "#FDF4FB",
                SidebarBackground = "#FCE5F7",
                CardBackground = "#FFFFFF",
                CardBackgroundSecondary = "#FDEFF9",
                TextPrimary = "#3B0A38",
                TextSecondary = "#8C2982",
                TextMuted = "#B964B0",
                TextDisabled = "#DC9FD5",
                BorderColor = "#F8BFEF",
                AccentColor = "#DB2777",
                ControlBackground = "#FFFFFF",
                ControlHoverBackground = "#FAD4F3",
                ControlPressedBackground = "#F5BDE9",
                ControlDisabledBackground = "#FCE5F7",
                InputBackground = "#FFFFFF",
                InputBorder = "#F8BFEF",
                DividerColor = "#F7C3EF",
                NavActiveBackground = "#F8D3F3",
                NavActiveHoverBackground = "#F2B8EA",
                NavIndicatorColor = "#DB2777",
                SliderTrackBackground = "#F3A4E6",
                SliderTrackHoverBackground = "#DD76CD",
                SliderThumbBackground = "#FFFFFF",
                SliderThumbBorder = "#DB2777",
                SliderActiveTrackBackground = "#DB2777",
                DropdownHoverBackground = "#FAD4F3",
                DropdownSelectedBackground = "#DB2777",
                SkeletonShimmerBase = "#F5C7EE",
                SkeletonShimmerHighlight = "#FDE8F9"
            };
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

        public static ThemePalette GetPalette(ColorTheme theme, bool isDark)
        {
            if (Palettes.TryGetValue((theme, isDark), out var palette))
            {
                return palette;
            }
            return Palettes[(ColorTheme.Standard, isDark)];
        }

        public static void ApplyTheme(ColorTheme colorTheme, ThemeMode mode)
        {
            try
            {
                bool isDark = GetIsDark(mode);

                try
                {
                    ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
                }
                catch { }

                var palette = GetPalette(colorTheme, isDark);
                var res = System.Windows.Application.Current.Resources;

                SetFrozenBrush(res, "WindowBackground", palette.WindowBackground);
                SetFrozenBrush(res, "SidebarBackground", palette.SidebarBackground);
                SetFrozenBrush(res, "CardBackground", palette.CardBackground);
                SetFrozenBrush(res, "CardBackgroundSecondary", palette.CardBackgroundSecondary);
                SetFrozenBrush(res, "TextPrimary", palette.TextPrimary);
                SetFrozenBrush(res, "TextSecondary", palette.TextSecondary);
                SetFrozenBrush(res, "TextMuted", palette.TextMuted);
                SetFrozenBrush(res, "TextDisabled", palette.TextDisabled);
                SetFrozenBrush(res, "BorderColor", palette.BorderColor);
                SetFrozenBrush(res, "AccentColor", palette.AccentColor);
                SetFrozenBrush(res, "ControlBackground", palette.ControlBackground);
                SetFrozenBrush(res, "ControlHoverBackground", palette.ControlHoverBackground);
                SetFrozenBrush(res, "ControlPressedBackground", palette.ControlPressedBackground);
                SetFrozenBrush(res, "ControlDisabledBackground", palette.ControlDisabledBackground);
                SetFrozenBrush(res, "InputBackground", palette.InputBackground);
                SetFrozenBrush(res, "InputBorder", palette.InputBorder);
                SetFrozenBrush(res, "DividerColor", palette.DividerColor);
                SetFrozenBrush(res, "NavActiveBackground", palette.NavActiveBackground);
                SetFrozenBrush(res, "NavActiveHoverBackground", palette.NavActiveHoverBackground);
                SetFrozenBrush(res, "NavIndicatorColor", palette.NavIndicatorColor);
                SetFrozenBrush(res, "SliderTrackBackground", palette.SliderTrackBackground);
                SetFrozenBrush(res, "SliderTrackHoverBackground", palette.SliderTrackHoverBackground);
                SetFrozenBrush(res, "SliderThumbBackground", palette.SliderThumbBackground);
                SetFrozenBrush(res, "SliderThumbBorder", palette.SliderThumbBorder);
                SetFrozenBrush(res, "SliderActiveTrackBackground", palette.SliderActiveTrackBackground);
                SetFrozenBrush(res, "DropdownHoverBackground", palette.DropdownHoverBackground);
                SetFrozenBrush(res, "DropdownSelectedBackground", palette.DropdownSelectedBackground);
                SetFrozenBrush(res, "SkeletonShimmerBase", palette.SkeletonShimmerBase);
                SetFrozenBrush(res, "SkeletonShimmerHighlight", palette.SkeletonShimmerHighlight);
                SetFrozenBrush(res, "GlassOverlay", palette.GlassOverlay);
                SetFrozenBrush(res, "GlassBorder", palette.GlassBorder);

                SetFrozenBrush(res, "CardControlBorderBrush", palette.BorderColor);
                SetFrozenBrush(res, "CardControlHeaderBorderBrush", palette.DividerColor);
                SetFrozenBrush(res, "CardControlSeparatorBrush", palette.DividerColor);
                SetFrozenBrush(res, "ComboBoxBorderBrush", palette.BorderColor);
                SetFrozenBrush(res, "ComboBoxDropDownBackground", palette.CardBackground);
                SetFrozenBrush(res, "ComboBoxDropDownBorderBrush", palette.BorderColor);
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed applying theme in ThemeService", ex);
            }
        }

        private static void SetFrozenBrush(ResourceDictionary res, string key, string hexColor)
        {
            if (!BrushCache.TryGetValue(hexColor, out var brush))
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                brush = new SolidColorBrush(color);
                brush.Freeze(); // Critical for RAM optimization & WPFMil rendering speed
                BrushCache[hexColor] = brush;
            }
            res[key] = brush;
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
