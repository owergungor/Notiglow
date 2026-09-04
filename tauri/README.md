# Curry (Tauri 2 + TypeScript Implementation)

This directory contains the modern, cross-platform Tauri 2.x implementation of **Curry** using TypeScript, Svelte 5, and Rust.

---

## Why This Implementation is Separate from the Existing C# Implementation

1. **Reference Implementation Preservation**: The existing C# / WPF codebase (`NotiGlow/`, `NotiGlow.Core/`, etc.) serves as the functional and behavioural reference specification for notification detection and glow effects. Keeping it untouched prevents any regressions or disruption during development.
2. **True Cross-Platform Capability**: The original C# implementation targets Windows and WPF (`net9.0-windows10.0.19041.0`). Tauri 2 allows Curry to compile natively for Windows, macOS, and Linux from a single unified codebase.
3. **Decoupled Architecture**: All platform-agnostic UI and state management resides in Svelte 5 / TypeScript, while native platform hooks (notification listeners, window management, tray icons) are implemented cleanly in Rust behind cross-platform traits.
4. **Lightweight Footprint & Modern Web Aesthetics**: A Tauri + Svelte stack provides hardware-accelerated, flexible styling with lower memory overhead and native binary distribution.

---

## System Requirements & Prerequisites

- **Node.js**: `v20.x` or later (`v24.x` recommended)
- **Package Manager**: `npm` (`v10.x` or later)
- **Rust Toolchain**: `v1.85.0` or later (`v1.98.0` recommended)
  - Target: `x86_64-pc-windows-msvc` (with MSVC Build Tools) or `x86_64-pc-windows-gnu` (with MinGW-w64 / WinLibs)
  - macOS: `aarch64-apple-darwin` or `x86_64-apple-darwin`
  - Linux: `x86_64-unknown-linux-gnu` with `libwebkit2gtk-4.1-dev` and build-essential

---

## Project Structure

```
tauri/
├── src/                      # Frontend source code (Svelte 5 + TypeScript)
│   ├── routes/
│   │   ├── +layout.ts        # SPA prerendering configuration
│   │   └── +page.svelte      # Main Curry application window UI
│   └── app.html              # HTML template
├── src-tauri/                # Rust backend
│   ├── src/
│   │   ├── lib.rs            # Application builder, plugins & IPC handlers
│   │   └── main.rs           # Native binary entry point
│   ├── capabilities/         # Tauri 2 permission capabilities
│   │   └── default.json      # Core & plugin capabilities
│   ├── icons/                # Multi-platform app icons (.ico, .icns, .png)
│   ├── Cargo.toml            # Rust dependencies & metadata
│   ├── build.rs              # Tauri build script
│   └── tauri.conf.json       # Window geometry, branding & bundle settings
├── static/                   # Static public assets (icons, SVGs)
├── svelte.config.js          # SvelteKit static adapter configuration
├── vite.config.js            # Vite bundler & Tauri HMR settings
├── tsconfig.json             # TypeScript compiler settings
├── package.json              # NPM dependencies & scripts
├── .gitignore                # Build artifacts, dependencies & secrets ignore
└── README.md                 # Project documentation
```

---

## Installation & Setup

1. Navigate to the `tauri/` directory:
   ```bash
   cd tauri
   ```

2. Install Node.js dependencies:
   ```bash
   npm install
   ```

3. Ensure Rust is installed and available in your environment:
   ```bash
   rustc --version
   cargo --version
   ```

---

## Development Mode

To start the Vite development server with Tauri hot-module reloading:

```bash
npm run tauri dev
```

During development:
- The Svelte frontend runs at `http://localhost:1420`.
- Rust backend compiles into `src-tauri/target/debug/`.
- Changes to Svelte components reload instantly via HMR.
- Changes to Rust files automatically trigger a native recompile.

---

## Building for Production

To create an optimized, standalone release bundle for your platform:

```bash
npm run tauri build
```

The output bundle and installer will be located in:
- Windows: `src-tauri/target/release/bundle/nsis/` or `msi/`
- macOS: `src-tauri/target/release/bundle/dmg/` or `macos/`
- Linux: `src-tauri/target/release/bundle/deb/` or `appimage/`

To test only the frontend production build without compiling Rust:
```bash
npm run build
```

---

## Architecture & Communication (IPC)

Frontend and backend communicate over Tauri 2's secure IPC bridge:
- **Frontend**: Calls `invoke("check_backend_connection")` via `@tauri-apps/api/core` when the window opens and when **Test IPC** is selected.
- **Rust Backend**: Exposes `#[tauri::command]` handlers registered in `tauri::Builder` in `src-tauri/src/lib.rs`.
- **Platform Separation**: No Windows-specific or OS-specific code resides in the frontend. All OS interactions are mediated through Rust backend services.
- **Least privilege**: Exposes only the core capabilities required for its own window and tray interactions.

---

## Windows Notification Capture

### Architecture
Curry uses Microsoft's official Windows Runtime API (`Windows.UI.Notifications.Management.UserNotificationListener`) via the `windows = "0.62"` crate under conditional compilation `#[cfg(target_os = "windows")]`.

```
Windows Desktop Toast
          ↓
WinRT UserNotificationListener (windows = "0.62")
          ↓
WindowsNotificationProvider (dedicated thread + 250ms polling + startup snapshot)
          ↓
Deduplicator (bounded HashMap<String, Instant> with 15-minute TTL & fingerprinting)
          ↓
Normalized Notification Model (model.rs / models/notification.rs)
          ↓
NotificationManager / Engine (engine.rs)
   ├── AppState check (dropped if disabled)
   ├── NotificationStorage (bounded in-memory ring buffer with atomic persistence)
   ├── Tauri Event: "notification-received" & "notification-created"
   └── GlowManager (ambient screen-edge glow overlay)
          ↓
Svelte UI (routes/+page.svelte) & Dedicated Overlay (routes/glow/+page.svelte)
```

### Required Windows Permissions & Setup
1. **Notification Listener Access**: Modern Windows 10 (Build 1607+) and Windows 11 require user consent for desktop applications to read notifications. Curry invokes `listener.RequestAccessAsync()?.join()` on startup.
2. **Access States**:
   - `Allowed`: Active monitoring proceeds.
   - `Denied`: Status is reported as `Permission Denied` in the UI.
   - `Unspecified`: Status is reported as `Permission Required`. Users can click the **Windows Settings** button in Curry to open `ms-settings:notifications` directly.

### Duplicate Prevention Strategy
- **Stable Notification IDs**: Uses the native Windows `UserNotification.Id()`.
- **Fallback Fingerprinting**: If an ID is missing, computes a fingerprint string from `source:title:body:timestamp`.
- **TTL Expiration**: Tracks arrival `Instant` with a 15-minute expiration period.
- **Bounded Capacity**: Caps tracked IDs at 500 items, automatically pruning the oldest entries.
- **Startup Snapshot**: Performs an initial scan on startup to seed the deduplicator, preventing historical Action Center toasts from generating alert bursts upon application launch.

### Known Windows Limitations
- **Unpackaged App NotificationChanged Events**: In unpackaged desktop applications, WinRT's event-based `NotificationChanged` delegate subscription can fail or require package identity. Curry uses a 250ms interruptible worker polling loop with 10ms responsive sleep slices, ensuring instantaneous event processing and 0% CPU idle footprint.
- **Storage & Privacy**: User data (settings, notifications) is persisted safely in local application storage (`%APPDATA%\com.curry.desktop\`) with atomic writes (`.tmp` → replace) and automatic migration from legacy storage. Zero cloud telemetry.

### How to Test Windows Notification Capture
1. Start development mode:
   ```bash
   cd tauri
   npm run tauri dev
   ```
2. Verify status badge displays `Status: Running` with `Provider: Windows Notification Provider`.
3. Trigger a desktop notification from any Windows app (e.g. WhatsApp, Slack, Outlook, or Windows Clock/Timer).
4. Observe that:
   - The notification is captured and logged.
   - It appears in the "Notifications" tab in the Svelte UI.
   - The screen-edge glow overlay animates around the display.
   - No duplicate notifications are created.
5. Toggle Curry to **Disabled** via the system tray or header toggle:
   - Subsequent notifications are suppressed and will not appear in the feed or trigger a glow.
