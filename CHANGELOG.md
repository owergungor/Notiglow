# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6] - 2026-09-27

### Added
- **True Circular Shockwave Ripple**:
  - Anchored Ripple animation to exact screen center `(ActualWidth / 2, ActualHeight / 2)`.
  - Geometric true circular shockwave with `BrushMappingMode.Absolute` (`RadiusX == RadiusY`), preventing elliptical distortion on 16:9, 21:9 ultrawide, and custom aspect ratios.
  - Annular hollow shockwave expanding cleanly beyond screen corners (`maxRadius = diagonal * 1.10`) with zero clipping.
  - Full Reduce Motion accessibility compliance.
- **Unified RGB Spectrum Architecture**:
  - Built `GlowSpectrumBrushFactory` providing screen-space absolute coordinate mapping for RGB across all four edges, core border, and ambient spill.
  - Eliminated the right-edge color jump/seam bug in Pulse, Sweep, Ambient, and Comet engines.
  - Synchronized rainbow stops and uniform bloom directional masks.
- **Applications Grid View & Modern Cards**:
  - Hero UI-inspired segmented view switcher `[List] [Grid]` in Tracked Applications header.
  - Modern responsive Grid Cards with profile color badge, category, active status toggle, and action buttons.
  - AppSettings persistence for selected `ApplicationsViewMode`.
- **Automatic Updates via GitHub Releases**:
  - Added non-blocking `UpdateService` checking latest releases via GitHub API.
  - SHA256 integrity verification against `SHA256.txt`.
  - Downgrade protection and offline fail-safe behavior.
  - Settings UI card with update toggle, last-check timestamp, and manual check button.
- **Hero UI Design Language Enhancements**:
  - Modernized ToolTips, Close Buttons, and Segmented controls following 21st.dev/Hero UI guidelines with native WPF styles and dynamic theme tokens.

### Changed
- **Color Theme System Refinement**:
  - Removed legacy Amethyst palette.
  - Migrated "Nubank" royal purple palette to become the user-facing "Amethyst" theme.
  - Backward compatibility migration converting legacy "Nubank" and "Amethyst" configs seamlessly.
  - Canonical chromatic ordering based on HSL color proximity (Standard, Zen, Amber, Mocha, Burgundy, Sakura, Bubblegum, Amethyst, Violet, Indigo, Sapphire, Nature).

## [1.5] - 2026-09-27

### Added
- **AI Desktop Applications Integration**:
  - Expanded ready application profiles with built-in configurations for Claude, ChatGPT, Microsoft Copilot, and Google Gemini.
  - Native Windows identity resolver integration mapping official AUMIDs, MSIX package identities, and process identifiers without fragile title substrings.
  - Added application `Category` classification ("AI Assistants", "Messaging", "Gaming", "Media").
  - RGB spectrum visualization for color badges in application profile cards.

### Fixed
- **Button Click / Press Animation Target Isolation**:
  - Confined press/click animations strictly to genuine clickable `Button` targets.
  - Eliminated unwanted scaling and click feedback on parent containers, cards, grids, stack panels, and sidebar navigation blocks (`NavigationViewItem`).
- **RGB Spectrum Full Glow Rendering**:
  - Fixed issue where only a thin outer border showed RGB while the glow area fell back to static red.
  - Applied the continuous animated RGB spectrum brush across all glow bloom layers (top, bottom, left, right edge blooms, ambient spill, and inner border).
- **Ripple Radial Shockwave Animation**:
  - Re-architected Ripple animation into an expanding annular shockwave expanding from screen center to the edges.
  - Dynamic aspect-ratio compensation ensuring perfectly circular waves across ultrawide, square, and vertical monitors without clipping or edge distortions.
- **Enhanced Comet Animation**:
  - Boosted Comet visibility with a brilliant nucleus head, glowing bloom layer, and rich trailing tail.
  - Full RGB spectrum and theme color compatibility with smooth perimeter motion.
- **Enhanced Sweep Beam Animation**:
  - Replaced thin 1px stripe with a wide, luminous light beam featuring feathered glow transitions (dark → soft glow → vibrant core → bright sweep peak → dark) and ambient bloom.
- **Collapsed Sidebar Icon Centering**:
  - Resolved sidebar icon misalignment when collapsed; icons now align perfectly along horizontal and vertical centers in compact mode.

## [1.4] - 2026-09-26

### Added
- **13 Curated Color Themes & Light/Dark Variants**:
  - Standard, Zen, Indigo, Sapphire, Burgundy, Nature, Amethyst, Mocha, Sakura, Amber, Nubank, Violet, and Bubblegum.
  - Independent Theme and Mode (System / Light / Dark) architecture with automatic Windows theme synchronization.
- **Spectrum RGB Glow Mode**:
  - Rainbow spectrum neon border glow supporting continuous color cycling and static reduced-motion presentation.
  - Full compatibility across Pulse, Ambient, Sweep, Comet, and Ripple styles.
- **Modernized Visual Design System**:
  - Origin UI inspired solid-thumb sliders with crisp active/inactive track definition.
  - Refined floating dropdown / ComboBox controls with fluid popups.
  - Modernized sidebar with active item indicators, subtle hover states, and smooth transitions.
  - Non-blocking skeleton shimmer loading for deferred tabs.
- **Comprehensive Reduce Motion Policy**:
  - Global animation suppression turning off all non-essential UI transitions, hover states, ripples, and shimmers.

### Fixed
- **Corner Ripple Artifacts**:
  - Eliminated corner clipping and boundary clipping on rounded screen edges.
- **Comet & Sweep Edge Lines**:
  - Removed artificial solid edge stripes along non-active borders during dynamic sweep and comet animations.

## [1.3] - 2026-09-20

### Added
- **Performance & Lazy Loading**:
  - View caching optimizations and memory footprint reduction.

## [1.2] - 2026-09-08

### Added
- **UI/UX Polish**:
  - Settings dashboard layout refinements and single-file publish tuning.

## [1.1] - 2026-09-05

### Added
- **Self-Contained Windows Distribution**:
  - Fully self-contained .NET 9 single-file packaging (`NotiGlow.exe`, ~187 MB) embedding the complete runtime and managed assemblies.
  - Native DirectX and WPF rendering libraries (`wpfgfx_cor3.dll`, `D3DCompiler_47_cor3.dll` etc.) bundled alongside the executable to prevent startup extraction lag and `%TEMP%` file locks.
  - Target Windows 10/11 x64 systems run immediately without needing .NET Desktop Runtime, SDKs, or developer tools.
- **Windows Installer (`NotiGlow-Setup-x64.exe`)**:
  - Modern Inno Setup 6 dual-mode installer supporting both standard user (`PrivilegesRequired=lowest`) and administrative installations.
  - Start Menu program group integration and optional Desktop shortcut.
  - Clean uninstaller (`unins000.exe`) with full Windows Settings / Control Panel uninstallation support.
  - Application settings isolated in `%APPDATA%\NotiGlow\settings.json` to prevent Program Files write-permission issues.
- **Portable ZIP Package (`NotiGlow_1.1_win-x64.zip`)**:
  - Clean zero-install standalone archive containing only essential end-user runtime files.
- **CI/CD Release Automation Hardening**:
  - Updated GitHub Actions workflow for automated self-contained building, Inno Setup compilation, and SHA-256 generation.

## [1.0] - 2026-09-05

### Added
- **Ambient Screen-Edge Glow Engine**:
  - Continuous 4-edge screen lighting with corner gradient blending.
  - 5 distinct animation styles: `Pulse`, `Sweep`, `Ambient`, `Comet`, and `Ripple`.
  - Configurable edge thickness, blur radius, glow duration, peak opacity, and custom hex/RGB colors.
  - Transparent, click-through, non-activating Win32 topmost overlay (`WS_EX_TRANSPARENT`, `WS_EX_LAYERED`, `WS_EX_NOACTIVATE`).
- **Windows Notification Integration**:
  - Real-time toast event detection using native Windows Runtime `UserNotificationListener` APIs.
  - In-memory SHA-256 sliding-window (2.5 seconds) deduplication to prevent repetitive triggers.
  - Granular notification priority filtering.
- **Per-Application Profiles**:
  - Custom color, animation motion, duration, and intensity matching per application process/AppId.
  - One-click profile duplication and editing in the settings dashboard.
  - Clean default fallback configuration when no custom profile matches.
- **Display & Hardware Protections**:
  - Multi-monitor support with targeted rendering modes (Active Monitor, Primary Display, All Displays).
  - Per-Monitor V2 DPI awareness supporting high-DPI and ultrawide resolutions.
  - OLED-friendly display mode with peak luminance dampening and true black preservation.
  - Foreground game detection with automatic distraction reduction (dimming glow intensity up to 60%).
- **Fluent Desktop Experience**:
  - Modern Windows 11 Fluent UI dashboard powered by WPF-UI 4.3.0 with Dark and Light mode support.
  - System tray icon with quick actions (dashboard toggle, monitoring pause/resume, exit).
  - Single-instance enforcement via Win32 named mutex (`NotiGlow_SingleInstance_Mutex`).
  - Zero idle CPU/GPU consumption when overlay windows are hidden.
  - JSON settings export, import, and factory reset capabilities.
- **Packaging & CI**:
  - Portable x64 distribution package (`NotiGlow_1.0_win-x64.zip`).
  - Inno Setup installer script (`NotiGlow-Setup.iss`).
  - GitHub Actions CI pipeline for Windows x64 .NET 9 build and unit test verification.

[1.4]: https://github.com/owergungor/NotiGlow/releases/tag/v1.4
[1.3]: https://github.com/owergungor/NotiGlow/releases/tag/v1.3
[1.2]: https://github.com/owergungor/NotiGlow/releases/tag/v1.2
[1.1]: https://github.com/owergungor/NotiGlow/releases/tag/v1.1
[1.0]: https://github.com/owergungor/NotiGlow/releases/tag/v1.0
