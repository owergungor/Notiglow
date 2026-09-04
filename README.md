# Curry

Curry is a modern Windows desktop notification companion that captures Windows notifications and provides a configurable visual glow experience around the screen edges.

[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20%7C%20Windows%2010-0078D4?style=flat-square&logo=windows)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/Framework-Tauri%202.x-24C8D8?style=flat-square&logo=tauri)](https://tauri.app/)
[![Frontend](https://img.shields.io/badge/Frontend-Svelte%205%20%2B%20TypeScript-FF3E00?style=flat-square&logo=svelte)](https://svelte.dev/)
[![Backend](https://img.shields.io/badge/Backend-Rust-black?style=flat-square&logo=rust)](https://www.rust-lang.org/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

---

## Overview

Curry sits quietly in your Windows system tray, actively monitoring incoming toast notifications via Windows native APIs. When notifications arrive, Curry illuminates the edges of your screen with an ambient, configurable glow animation tailored to your preference, while indexing notification history into a local feed.

---

## Key Features

- **Windows Notification Capture**: Directly integrates with Windows `UserNotificationListener` WinRT APIs to capture incoming system and app toast notifications.
- **Real-Time Notification Feed**: Clean, reactive notification feed with timestamps, application source titles, message bodies, and dismissal controls.
- **Notification History & State**: Tracks read and unread states across notifications with quick action to mark all as read or clear history.
- **Priority & Urgency Inference**: Classifies incoming notifications by urgency levels (Urgent, High, Normal, Low) using privacy-safe local heuristics.
- **Configurable Screen-Edge Glow**: Custom transparent overlay illuminates screen perimeters across single or multi-monitor configurations.
- **Distinct Glow Animations**:
  - **Pulse**: Fast, rhythmic illumination effect for immediate visual feedback.
  - **Breathing**: Slow, graceful ambient illumination cycle.
  - **Solid**: Continuous, static illumination during notification hold periods.
- **Fine-Grained Glow Adjustments**:
  - **Glow Duration**: Configurable active hold durations.
  - **Glow Opacity**: Adjustable transparency from subtle backlight to vivid highlights.
  - **Glow Thickness**: Border width control from slim outlines to prominent bands.
  - **Corner Curvature**: Adjustable corner rounding from sharp corners to matched Windows 11 rounded displays.
  - **Monitor Targeting**: Direct glow to Primary display, Active monitor, or All displays.
  - **Theme-Integrated Color Selection**: Dynamic color swatches synchronized with active theme palettes.
- **Windows System Tray Lifecycle**:
  - **Close-to-Tray**: Window `X` minimizes Curry to the notification area without interrupting background listening.
  - **Tray Restore**: Single-click or context menu restore to immediately foreground the application.
  - **Clean Quit**: Gracefully halts listener threads, unregisters hooks, and terminates without leaving orphan processes.
- **Single-Instance Protection**: Native Win32 named system mutex prevents duplicate instances and foregrounds the existing window if launched again.
- **Crash-Resilient Persistence**: Atomic multi-step writes ensure settings and notification history cannot be corrupted during unexpected shutdowns.
- **Curated 7-Theme System**: Built-in visual themes with **Perpetuity** configured as the canonical default.
- **Windows Startup Integration**: Optional autostart with Windows via Run registry keys, starting minimized in the tray.
- **Local-First & Offline Architecture**: Zero telemetry, no external network requests, no cloud dependencies.

---

## 📸 Screenshots

<!-- Screenshots will be showcased here upon release -->
> *UI screenshots and ambient glow demonstrations will be published here with the upcoming release.*

---

## Theme System

Curry includes seven themes inspired by modern minimalist and glassmorphic aesthetics. **Perpetuity** is the canonical default theme.

| Theme | Description |
|---|---|
| **Perpetuity** *(Default)* | Clean, balanced slate-dark aesthetic with high contrast and emerald accents. |
| **Catppuccin** | Soothing pastel-inspired dark palette with warm lavender accents and muted surfaces. |
| **Vintage Paper** | Warm, nostalgic cream and sepia tones reminiscent of classic editorial print. |
| **Amethyst Haze** | Atmospheric deep purple backdrop with luminous violet and neon glow highlights. |
| **Sage Mist** | Calming, organic botanical green surfaces with soft sage accents. |
| **Bubblegum** | Playful, vibrant candy-pink and magenta styling with high-contrast surfaces. |
| **Amberstate** | Deep warm charcoal foundation accented by radiant golden amber glow tones. |

---

## Technology Stack

- **Frontend**: [Svelte 5](https://svelte.dev/) with runes, [TypeScript](https://www.typescriptlang.org/), [Vite](https://vitejs.dev/)
- **Styling**: Modern CSS design tokens, CSS custom properties, responsive glassmorphism
- **Desktop Framework**: [Tauri 2.x](https://tauri.app/)
- **Backend Core**: [Rust](https://www.rust-lang.org/) (`2021` edition)
- **Windows Integration**:
  - `windows` crate (v0.62) with `UI_Notifications_Management` and `UI_Notifications`
  - WinRT `UserNotificationListener`
  - Win32 Native Tray APIs & Named Mutex (`Global\Curry`)
  - Windows Registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`)

---

## Architecture

Curry separates user interface rendering from native Windows notification monitoring via an asynchronous Tauri IPC bridge:

```text
┌──────────────────────────────────────────────────────────┐
│                Svelte 5 / TypeScript UI                  │
│   (Dashboard, Notification Feed, Glow Controls, Themes)  │
└────────────────────────────┬─────────────────────────────┘
                             │
                             ▼ Tauri IPC (Commands & Events)
┌──────────────────────────────────────────────────────────┐
│                     Rust Application                     │
│         (State Management, Mutex, Tray Lifecycle)         │
└──────────────┬─────────────────────────────┬─────────────┘
               │                             │
               ▼                             ▼
┌──────────────────────────────┐ ┌─────────────────────────┐
│     Notification Engine      │ │   Glow / Storage Engine │
│  - UserNotificationListener  │ │  - Transparent Overlay  │
│  - Urgency Classifier        │ │  - Atomic JSON Storage  │
│  - Deduplication Engine      │ │  - Registry Autostart   │
└──────────────────────────────┘ └─────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────┐
│                    Windows Subsystems                    │
│      (WinRT Notification API, Win32 Windowing, Tray)     │
└──────────────────────────────────────────────────────────┘
```

---

## Installation

Release binaries and standalone installers are published directly on GitHub Releases:

1. Navigate to the [Releases](https://github.com) section of this repository.
2. Download the preferred installer:
   - **Setup Installer**: `Curry_0.1.0_x64-setup.exe` (Recommended, Nullsoft NSIS)
   - **Enterprise MSI**: `Curry_0.1.0_x64_en-US.msi` (Windows Installer package)
3. Run the installer and launch Curry from your Start Menu or system tray.

---

## Development

### Prerequisites

- **Node.js**: v20.x or higher
- **npm**: v10.x or higher
- **Rust Toolchain**: v1.85.0 or later (Windows MSVC or GNU target)

### Building from Source

1. Clone this repository:
   ```bash
   git clone <REPOSITORY_URL>
   cd <REPOSITORY_NAME>
   ```

2. Install frontend dependencies:
   ```bash
   cd tauri
   npm install
   ```

3. Type-check and validate frontend:
   ```bash
   npm run check
   npm run build
   ```

4. Check and test the Rust backend:
   ```bash
   cd src-tauri
   cargo check
   cargo test
   cargo build --release
   cd ..
   ```

5. Build the complete production application:
   ```bash
   npm run tauri build
   ```

---

## Build Output

When building Curry for production, artifacts are generated in the following locations:

- **Standalone Executable**:  
  `tauri/src-tauri/target/release/curry.exe`
- **NSIS Setup Installer**:  
  `tauri/src-tauri/target/release/bundle/nsis/Curry_0.1.0_x64-setup.exe`
- **WiX MSI Installer**:  
  `tauri/src-tauri/target/release/bundle/msi/Curry_0.1.0_x64_en-US.msi`

---

## Privacy & Security

Curry is built on a strict **local-first** privacy foundation:

- **Zero Telemetry**: No analytics, telemetry trackers, or crash reporting beacons.
- **Offline Operation**: No external network requests or remote server communication.
- **Local Data Only**: All notification records and user preferences remain on your machine.
- **Sanitized Logging**: Notification contents, titles, and message bodies are never written to disk logs; only non-identifying operational IDs are recorded for troubleshooting.

---

## Data Model & Migration

Curry persists its state atomically to user application storage:
- `settings.json`: User appearance and behavior options.
- `notifications.json`: Local notification history feed.
- `glow_settings.json`: Edge animation, color, and monitor configurations.

Curry includes backwards-compatible migration logic so existing settings and notification history can be preserved when upgrading from previous application versions. The migration runs once on initial launch, verifying payload validity before importing.

---

## Historical Note

This repository contains the Tauri 2 + Rust + Svelte desktop implementation of Curry. A legacy C#/WPF reference implementation used during initial prototyping is preserved locally as an architectural reference specification.

---

## License

This project is licensed under the [MIT License](LICENSE) — see the LICENSE file for details.
