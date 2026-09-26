# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
