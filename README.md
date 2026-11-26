<div align="center">

  <img alt="NotiGlow Logo" src="https://github.com/user-attachments/assets/733963e8-6969-4c1d-86f2-8afd2f2c52fd" width="96" height="96" />

  <h1>NotiGlow v5.0</h1>

  <p><strong>Transform your Windows notifications into sleek, ambient fullscreen edge glow animations — combining a WinUI 3 Fluent Design settings app with a lightweight native Rust background worker.</strong></p>

  <p>
    <a href="https://github.com/owergungor/Notiglow/releases/tag/v5.0" target="_blank">
      <img src="https://github.com/user-attachments/assets/91f67983-4f91-45b5-affe-af8e7b66e0f2" alt="Download from GitHub Releases" height="44" />
    </a>
  </p>

  <p>
    <img src="https://img.shields.io/badge/Version-5.0-BB9EFF?style=flat" alt="Version 5.0" />
    <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-08a1f7?style=flat&logo=windows11&logoColor=white" alt="Windows 10 / 11" />
    <img src="https://img.shields.io/badge/UI-WinUI%203%20%2F%20Fluent-0067C0?style=flat" alt="WinUI 3 Fluent Design" />
    <img src="https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 9.0" />
    <img src="https://img.shields.io/badge/Worker-Rust-DEA584?style=flat&logo=rust&logoColor=black" alt="Rust Background Worker" />
    <img src="https://img.shields.io/badge/Privacy-Local--First-40E40B?style=flat" alt="Local-First Privacy" />
    <img src="https://img.shields.io/badge/License-MIT-750014?style=flat" alt="MIT License" />
  </p>

</div>

---

## What Is NotiGlow?

Standard Windows toast notifications are easy to miss during immersive gameplay, full-screen video playback, or focused work — or too intrusive when they pop over active content.

**NotiGlow** listens to incoming Windows notifications locally and illuminates the perimeter of your display with smooth ambient edge glow animations tailored to each application's color and style.

### v5.0 Hybrid Architecture (WinUI 3 + Native Rust Background Worker)
Starting with **v5.0**, NotiGlow runs its **Native Rust Background Worker (`NotiGlow_Background.exe` v5.0.0)** by default (`UseRustBackgroundWorker`, `UseRustGameDetection`, and `UseRustGlowEngine` are all enabled out of the box):
- **C# WinUI 3 + Windows App SDK 1.6 (`NotiGlow.exe`)**: Settings UI, Windows 11 Fluent Design controls, themes, localization, and single-writer management of `%AppData%\NotiGlow\settings.json` and `profiles.json`.
- **Native Rust Background Worker (`NotiGlow_Background.exe`)**: Runs continuously in the background (~3.1–3.3 MB Private Working Set when the settings window is closed) to manage the system tray, Windows notification listener, game detection, and fullscreen edge glow rendering.
- **Named Pipe IPC & Self-Healing Lifecycle**: Closing the settings window keeps the Rust worker active; reopening the UI reconnects to the running worker without spawning duplicates. Includes automatic crash recovery and Explorer restart tray recovery.

---

## Preview

<div align="center">
  <img width="3840" height="2160" alt="NotiGlow Overview" src="https://github.com/user-attachments/assets/7b3f4394-7ec9-4b12-892b-8de67129cac8" />
  <br/><br/>
  <img src="Package/Assets/SplashScreen.png" alt="NotiGlow Ambient Edge Glow" width="640" />
  <p><em>Customizable ambient screen-edge glow with real-time live preview and per-app color profiles.</em></p>
</div>

---

## Core Features

### Ambient Edge Glow & Animation Styles (Native Rust Engine)
- **Six Animation Styles**:
  - **Pulse** — Gentle breathing illumination with smooth easing.
  - **Sweep** — Fluid light beam with two-layer bloom traveling around your screen edges.
  - **Comet** — Travelling bright head with two-layer bloom and a fading trail.
  - **Ripple** — Expanding wave propagation around the screen perimeter.
  - **Ambient** — Steady, soft border glow for minimal distraction.
  - **RGB Spectrum** — Smooth, continuous rainbow spectrum illumination around the display edges.
- **Fine-Grained Geometry & Timing**:
  - Adjust **Duration** (`500 ms` – `15 s`), **Intensity** (`10%` – `100%`), **Border Thickness**, **Glow Size** (`0 px` – `256 px`), and **Corner Radius** (`0 px` – `48 px`).
- **Multi-Monitor & DPI Awareness**: Display edge glows on the **Active Monitor** (following cursor/focus), **Primary Monitor**, or **All Monitors**, with per-monitor DPI scaling support.
- **Notification Burst Handling**: Choose how rapid notifications behave — **Restart**, **Extend**, **Queue**, or **Ignore**.
- **Accessibility & Display Safety**: Supports **OLED Screen Saver Mode**, **Subtle Glow Mode**, and respects **Reduce Motion / Reduce Animations** preferences.

### Supported Game Platforms & App Detection
- **Game Platform Discovery**:
  - **Steam** — Scans local Steam library folders (`libraryfolders.vdf`, `appmanifest_*.acf`, and library cache metadata) to discover installed titles (including Valve Source Engine titles like *Portal* and *Portal 2* and Xbox Game Studios titles like *Halo Infinite*).
  - **Epic Games** — Reads local Epic Games Launcher manifests (`%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests`) to identify installed games.
  - **Xbox App & Microsoft Store** — Scans Xbox PC library directories (`XboxGames`, `ModifiableWindowsApps`, `.GamingRoot`), GDK `MicrosoftGame.config` manifests, and packaged Store titles.
  - **Manual Addition** — Add any game or desktop executable directly via file selection (`.exe`) or App ID.
- **Installed Application Detection**:
  - Scans standard Windows installed application entries (Registry Uninstall keys, App Paths, Start Menu shortcuts, and common desktop apps such as Discord, WhatsApp, Telegram, Teams, Spotify, Slack, and Steam) to populate profiles automatically.
  - Matches incoming Windows notifications by AppUserModelId (AUMID), executable name, or display name using word-boundary matching.

### Applications & Gaming Management
- **Applications (Streamlined List View)**:
  - Clean, Windows 11 Settings-style horizontal list cards displaying each app's icon, name, duration, intensity, animation style, priority, color swatch, quick preview, edit, delete, and native enable/disable toggle.
  - Alphabetical and Recently Added sorting, one-click **Scan Installed Apps**, and **Add App**.
- **Gaming Mode**:
  - Detects when tracked games are running and applies configurable in-game behavior rules: **Glow During Games**, **Dim Intensity in Games**, **Shorten Duration in Games**, and **High Priority Only**.
  - Compact game cards with iconized **Customize** and **Remove** buttons, plus a native Windows 11 modal `ContentDialog` confirmation before removing a game.
- **Automatic Category Routing**:
  - Every profile belongs strictly to either **General App** (`Applications` tab) or **Gaming** (`Gaming` tab).
  - Changing a profile's category immediately moves it to the target tab without losing any color, animation, or timing settings, navigates to that tab, scrolls the item into view, and highlights it.

### Unified Profile & Game Customization
- Both **Applications** and **Gaming** share the same reusable WinUI 3 `ProfileEditorControl`:
  - **Color Picker**: Curated preset palette, custom HSV/Hex color selector, and dynamic **RGB Spectrum** mode.
  - **Live Preview**: Real-time `EdgePreviewControl` showing your exact color, corner radius, intensity, and animation style before saving.
  - **Per-Game Custom Glow Toggle**: Enable or disable custom per-game overrides to either use game-specific glow settings or inherit global Gaming Mode rules.

### Windows 11 Fluent Design (WinUI 3) & Themes
- **Native WinUI 3 Architecture**: Built with `NavigationView`, native `ToggleSwitch`, `ContentDialog`, `ComboBox`, `Slider`, and `EntranceThemeTransition`.
- **Window Backdrop Materials**: Switch seamlessly between **Acrylic**, **Mica**, and **Mica Alt**.
- **Themes & Accent Colors**: Supports **System**, **Light**, and **Dark** theme modes, defaulting to the **Windows System Accent** with curated accent color presets.
- **5 Languages**: Instant in-app switching across **English (`en-US`)**, **Español (`es-ES`)**, **Français (`fr-FR`)**, **Deutsch (`de-DE`)**, and **Türkçe (`tr-TR`)**.

---

## System Requirements

- **Operating System**: Windows 10 (version 1809 / build `17763` or newer) or Windows 11
- **Architecture**: 64-bit (`x64`)
- **Permissions**: Windows Notification Listener access (`UserNotificationListener`) to detect incoming toast notification headers
- **Runtime Dependencies**: **None required for end users** — NotiGlow v5.0 is distributed as a **self-contained** package bundling **.NET 9.0**, **Windows App SDK 1.6** (`WindowsAppSDKSelfContained=true`), and the native Rust background worker (`NotiGlow_Background.exe`).

---

## Download, Installation & Updating

### Direct Downloads (v5.0)
- **Windows Installer EXE (x64, Recommended):** [**NotiGlow-Setup-v5.0.exe**](https://github.com/owergungor/Notiglow/releases/download/v5.0/NotiGlow-Setup-v5.0.exe)
- **Portable ZIP Archive (x64, Self-Contained):** [**NotiGlow_5.0_win-x64.zip**](https://github.com/owergungor/Notiglow/releases/download/v5.0/NotiGlow_5.0_win-x64.zip)
- **MSIX Package (x64, Self-Signed):** [**NotiGlow-v5.0-x64.msix**](https://github.com/owergungor/Notiglow/releases/download/v5.0/NotiGlow-v5.0-x64.msix)
- **SHA-256 Checksums:** [**SHA256.txt**](https://github.com/owergungor/Notiglow/releases/download/v5.0/SHA256.txt)
- **All Release Assets:** [GitHub Releases — v5.0](https://github.com/owergungor/Notiglow/releases/tag/v5.0)

### Installing or Updating via EXE Installer (`NotiGlow-Setup-v5.0.exe`)
1. Download `NotiGlow-Setup-v5.0.exe` from the [v5.0 Release page](https://github.com/owergungor/Notiglow/releases/tag/v5.0).
2. Run the installer — it bundles the complete self-contained **.NET 9.0**, **Windows App SDK 1.6**, and **`NotiGlow_Background.exe`** runtime files (no separate downloads required) and supports optional desktop shortcut and Windows startup registration.
3. Launch **NotiGlow** from the Start Menu. Uninstalling via Windows Settings or `unins000.exe` stops background processes cleanly while preserving `%AppData%\NotiGlow`.

### Upgrading from v4.x to v5.0
- **Automatic One-Time Migration**: Existing `v4.x` `%AppData%\NotiGlow\settings.json` files are automatically migrated to `SettingsSchemaVersion = 5`, enabling the Rust Background Worker (`UseRustBackgroundWorker`, `UseRustGameDetection`, and `UseRustGlowEngine`) by default.
- **User Data Preserved**: All existing settings, tracked games, game overrides, and `%AppData%\NotiGlow\profiles.json` are preserved across upgrades and uninstalls.
- **Safe Fallback**: If you prefer the legacy C# background services, set `"UseRustBackgroundWorker": false` in `%AppData%\NotiGlow\settings.json`; your explicit preference is preserved across restarts.

### Portable ZIP (`NotiGlow_5.0_win-x64.zip`)
1. Extract the complete ZIP archive to a folder of your choice (keep `NotiGlow.exe` and `NotiGlow_Background.exe` in the same directory).
2. Run `NotiGlow.exe`.

### Installing or Updating via MSIX (`NotiGlow-v5.0-x64.msix`)
1. Download `NotiGlow-v5.0-x64.msix` from the [v5.0 Release page](https://github.com/owergungor/Notiglow/releases/tag/v5.0).
2. Double-click the `.msix` file to open Windows App Installer and click **Install** (or **Update** if a previous `crius.Notiglow` MSIX version is already installed).
3. Launch **NotiGlow** from the Start Menu and grant Windows Notification access when prompted.

### Known Limitations
- **Self-Signed MSIX Sideloading**: `NotiGlow-v5.0-x64.msix` is signed with the project's self-signed publisher certificate (`CN=2F9A5694-FADC-405A-AF51-BB9269D3FFF1`). Direct sideloading without trusting this certificate fails with `0x800B0109`; for standard GitHub installation, use `NotiGlow-Setup-v5.0.exe` or `NotiGlow_5.0_win-x64.zip`.
- **Distribution Channel**: NotiGlow v5.0 is currently distributed exclusively via GitHub Releases (EXE installer, Portable ZIP, and self-signed MSIX) and has not been submitted to the Microsoft Store.

---

## Building from Source

### Prerequisites
- Windows 10/11 (x64)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Rust Toolchain (`rustup` / `cargo`)](https://rustup.rs/)
- Windows 10/11 SDK (`10.0.19041.0` or newer; `Microsoft.WindowsAppSDK 1.6` is restored automatically via NuGet)
- *(Optional, for Windows installer)* [Inno Setup 6](https://jrsoftware.org/isinfo.php)

### Commands
```powershell
# Clone the repository
git clone https://github.com/owergungor/Notiglow.git
cd Notiglow

# Test and build the native Rust background worker
cargo test --manifest-path native/NotiGlow.Background/Cargo.toml
cargo build --release --manifest-path native/NotiGlow.Background/Cargo.toml

# Build WinUI 3 app (Release) and run the full C# test suite
dotnet build NotiGlow.csproj -c Release
dotnet test NotiGlow.Tests/NotiGlow.Tests.csproj -c Release

# Build self-contained x64 MSIX package
powershell -ExecutionPolicy Bypass -File Tools\build-msix.ps1
```

---

## Privacy Policy

NotiGlow processes incoming notification headers strictly in local memory and stores all settings and profiles locally on your device. There is no telemetry, no analytics, no advertising, and no user account system.

Read the full Privacy Policy here:  
**[https://owergungor.github.io/Notiglow/privacy-policy.html](https://owergungor.github.io/Notiglow/privacy-policy.html)**

---

## License & Contributing

- **License**: Distributed under the [MIT License](LICENSE).
- **Contributing**: Bug reports, feature suggestions, and pull requests are welcome. Please review [CONTRIBUTING.md](CONTRIBUTING.md) and open an issue on [GitHub Issues](https://github.com/owergungor/Notiglow/issues).

<div align="center">
  <sub>NotiGlow v5.0 &bull; <a href="https://github.com/owergungor/Notiglow/releases/tag/v5.0">Releases</a> &bull; <a href="https://owergungor.github.io/Notiglow/privacy-policy.html">Privacy Policy</a> &bull; <a href="LICENSE">MIT License</a></sub>
</div>
