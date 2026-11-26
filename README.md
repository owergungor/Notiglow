<div align="center">
  <img src="https://github.com/user-attachments/assets/733963e8-6969-4c1d-86f2-8afd2f2c52fd" alt="NotiGlow Logo" width="96" height="96" />
  <h1>NotiGlow v5.0</h1>
  <p><strong>Ambient fullscreen edge glow notifications for Windows — powered by WinUI 3 and a native Rust background worker.</strong></p>
  <p>
    <img src="https://img.shields.io/badge/Version-5.0-9B7BFF?style=flat" alt="Version 5.0" />
    <img src="https://img.shields.io/badge/Worker-Rust-38BDF8?style=flat&logo=rust&logoColor=white" alt="Rust Worker" />
    <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0067C0?style=flat&logo=windows11&logoColor=white" alt="Windows 10 / 11" />
    <img src="https://img.shields.io/badge/License-MIT-6D28D9?style=flat" alt="MIT License" />
  </p>
  <p>
    <a href="https://github.com/owergungor/Notiglow/releases/tag/v5.0" target="_blank">
      <img src="https://github.com/user-attachments/assets/91f67983-4f91-45b5-affe-af8e7b66e0f2" alt="Download from GitHub Releases" height="44" />
    </a>
  </p>
  <p>
    <a href="#preview">Preview</a> ·
    <a href="#key-features">Features</a> ·
    <a href="#download--installation">Download</a> ·
    <a href="#building-from-source">Build</a>
  </p>
</div>

---

## Overview

**NotiGlow** listens to incoming Windows notifications locally and illuminates your display perimeter with customizable edge glow animations — keeping you informed during games, videos, or focused work without intrusive popups.

Built on a **WinUI 3 + Rust hybrid architecture**, NotiGlow pairs a native Windows 11 Fluent Design settings app (`NotiGlow.exe`) with a lightweight Rust background worker (`NotiGlow_Background.exe`) that stays active after you close the window.

---

## Preview

<div align="center">
  <img src="https://github.com/user-attachments/assets/7b3f4394-7ec9-4b12-892b-8de67129cac8" alt="NotiGlow Settings Overview" width="820" />
  <p><em>WinUI 3 Fluent Design settings interface with per-app profiles and live preview.</em></p>
  <br />
</div>

---

## Key Features

- **Smart Notifications** — Listens locally via Windows `UserNotificationListener` and matches apps by AUMID, executable, or display name. Supports Burst Handling (Restart, Extend, Queue, Ignore), OLED Mode, Subtle Glow, and Reduce Motion.
- **Six Glow Modes** — Choose from **Pulse**, **Sweep** (two-layer bloom), **Comet** (two-layer bloom), **Ripple**, **Ambient**, and **RGB Spectrum** with configurable duration (`500 ms`–`15 s`), intensity (`10%`–`100%`), thickness, glow size (`0 px`–`256 px`), corner radius (`0 px`–`48 px`), multi-monitor targeting, and per-monitor DPI scaling.
- **Rust Background Worker** — Runs `NotiGlow_Background.exe` by default (~3.1–3.3 MB Private Working Set when the UI is closed) to manage the system tray, notifications, game detection, and glow rendering with automatic crash and Explorer-restart recovery.
- **Gaming Detection** — Detects running games and applies Gaming Mode rules (Glow During Games, Dim Intensity, Shorten Duration, High Priority Only) plus optional per-game glow overrides.
- **Custom Profiles** — Unified profile editor for Applications and Gaming with preset, HSV, and Hex color pickers, live edge preview, one-click app scanning, and automatic category routing.
- **Fluent Design** — Native WinUI 3 interface with Acrylic, Mica, and Mica Alt backdrops, System/Light/Dark themes, Windows System Accent, and 5 languages (`en-US`, `es-ES`, `fr-FR`, `de-DE`, `tr-TR`).

---

## Supported Platforms

- **Steam** — Discovers installed titles from local `libraryfolders.vdf` and `appmanifest_*.acf` manifests.
- **Epic Games** — Reads local Epic Games Launcher manifests (`%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests`).
- **Xbox App & Microsoft Store** — Scans `XboxGames`, `ModifiableWindowsApps`, `.GamingRoot`, `MicrosoftGame.config`, and packaged Store titles.
- **Windows Desktop Apps & Manual Addition** — Detects installed apps via Registry Uninstall keys, App Paths, and Start Menu shortcuts, or add any `.exe` / App ID manually.

---

## Download & Installation

| Package | Architecture | Notes |
| --- | --- | --- |
| [**NotiGlow-Setup-v5.0.exe**](https://github.com/owergungor/Notiglow/releases/download/v5.0/NotiGlow-Setup-v5.0.exe) | `x64` | **Recommended** Windows installer (self-contained) |
| [**NotiGlow_5.0_win-x64.zip**](https://github.com/owergungor/Notiglow/releases/download/v5.0/NotiGlow_5.0_win-x64.zip) | `x64` | Portable archive (keep `NotiGlow.exe` and `NotiGlow_Background.exe` together) |
| [**NotiGlow-v5.0-x64.msix**](https://github.com/owergungor/Notiglow/releases/download/v5.0/NotiGlow-v5.0-x64.msix) | `x64` | Self-signed MSIX package |
| [**SHA256.txt**](https://github.com/owergungor/Notiglow/releases/download/v5.0/SHA256.txt) | — | Checksum manifest on [v5.0 Release](https://github.com/owergungor/Notiglow/releases/tag/v5.0) |

- **Clean Install:** Run `NotiGlow-Setup-v5.0.exe` (or extract the portable ZIP and launch `NotiGlow.exe`), then grant Windows Notification access when prompted.
- **Upgrading from v4.x:** Existing `%AppData%\NotiGlow\settings.json` files migrate automatically to `SettingsSchemaVersion = 5` with the Rust worker enabled by default while preserving all settings, tracked games, and `profiles.json`. Uninstalling also preserves `%AppData%\NotiGlow`.
- **Optional Fallback:** To switch back to legacy C# background services, set `"UseRustBackgroundWorker": false` in `%AppData%\NotiGlow\settings.json`.

---

## System Requirements

- **OS:** Windows 10 (version 1809 / build `17763` or newer) or Windows 11
- **Architecture:** 64-bit (`x64`)
- **Permissions:** Windows Notification Listener access (`UserNotificationListener`)
- **Runtimes:** None required — self-contained distribution bundles `.NET 9.0`, `Windows App SDK 1.6`, and `NotiGlow_Background.exe`

---

## Building from Source

**Prerequisites:** Windows 10/11 (`x64`), [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0), [Rust (`rustup` / `cargo`)](https://rustup.rs/), Windows 10/11 SDK (`10.0.19041.0+`, `Microsoft.WindowsAppSDK 1.6` via NuGet), and optionally [Inno Setup 6](https://jrsoftware.org/isinfo.php).

```powershell
git clone https://github.com/owergungor/Notiglow.git
cd Notiglow

# Test and build the native Rust background worker
cargo test --manifest-path native/NotiGlow.Background/Cargo.toml
cargo build --release --manifest-path native/NotiGlow.Background/Cargo.toml

# Build WinUI 3 app (Release) and run C# tests
dotnet build NotiGlow.csproj -c Release
dotnet test NotiGlow.Tests/NotiGlow.Tests.csproj -c Release

# Optional: build self-contained x64 MSIX package
powershell -ExecutionPolicy Bypass -File Tools\build-msix.ps1
```

---

## Known Limitations

- **Self-Signed MSIX Sideloading:** `NotiGlow-v5.0-x64.msix` is signed with a self-signed publisher certificate (`CN=2F9A5694-FADC-405A-AF51-BB9269D3FFF1`). Direct sideloading fails with `0x800B0109` unless the certificate is trusted locally; use `NotiGlow-Setup-v5.0.exe` or `NotiGlow_5.0_win-x64.zip` for standard installation.
- **Distribution:** NotiGlow v5.0 is currently distributed exclusively via GitHub Releases and has not been submitted to the Microsoft Store.

---

## Privacy, License & Contributing

- **Privacy:** All notification headers, settings, and profiles stay strictly local on your device — zero telemetry, analytics, ads, or accounts. Read the [Privacy Policy](https://owergungor.github.io/Notiglow/privacy-policy.html).
- **License:** Distributed under the [MIT License](LICENSE).
- **Contributing:** Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and open an issue or pull request on [GitHub Issues](https://github.com/owergungor/Notiglow/issues).

<div align="center">
  <sub>NotiGlow v5.0 · <a href="https://github.com/owergungor/Notiglow/releases/tag/v5.0">Releases</a> · <a href="https://owergungor.github.io/Notiglow/privacy-policy.html">Privacy Policy</a> · <a href="LICENSE">MIT License</a></sub>
</div>
