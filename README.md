<div align="center">

# 🔔 NotiGlow

**Modern Windows notification companion with an ambient screen-edge glow.**

Transform standard Windows desktop toast notifications into smooth, elegant, and non-intrusive screen perimeter lighting animations.

[![CI](https://github.com/owergungor/NotiGlow/actions/workflows/notiglow.yml/badge.svg)](https://github.com/owergungor/NotiGlow/actions/workflows/notiglow.yml)
[![Release](https://img.shields.io/github/v/release/owergungor/NotiGlow?style=flat&label=Release)](https://github.com/owergungor/NotiGlow/releases/tag/v1.0.0)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4?style=flat&logo=windows&logoColor=white)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?style=flat&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Tests](https://img.shields.io/badge/Tests-86%20Passed-brightgreen?style=flat)](NotiGlow.Tests/)
[![License](https://img.shields.io/badge/License-MIT-blue?style=flat)](LICENSE)

</div>

---

## 📖 Overview

**NotiGlow** is a lightweight, ambient notification utility designed specifically for Windows 10 and Windows 11. It resides unobtrusively in your system tray and monitors incoming system notifications via native Windows Runtime APIs (`UserNotificationListener`).

When a notification arrives from any application (e.g., Slack, Discord, Outlook, Steam), NotiGlow illuminates the boundaries of your screen with an ambient glow animation. You gain immediate peripheral awareness of messages and alerts without breaking focus or obstructing your workspace with intrusive popups.

---

## ✨ Features

- **Windows Notification Monitoring**: Integrates directly with native Windows `UserNotificationListener` APIs to capture toast events without third-party drivers or polling hacks.
- **Ambient Screen-Edge Glow**: Illuminates the screen perimeter with continuous four-edge lighting and smooth corner gradient blending.
- **5 Distinct Animation Engines**:
  - `Pulse`: Smooth, rhythmic opacity breathing.
  - `Sweep`: A focused beam of light traveling around the screen perimeter.
  - `Ambient`: Calm, subtle breathing designed for zero distraction.
  - `Comet`: Dynamic high-intensity leading head with an elegant trailing decay.
  - `Ripple`: Corner-expanding light waves cascading along display boundaries.
- **Per-Application Profiles**: Assign dedicated colors, animation motions, durations, and intensity levels to individual apps (e.g., Discord, Slack, Steam, Outlook).
- **Profile Duplication**: Duplicate and customize existing application profiles with a single click.
- **OLED-Friendly Behavior**: Protects display panels with peak luminance limiting and pure black preservation to prevent burn-in.
- **Fullscreen & Gaming Suppression**: Detects active DirectX, Vulkan, and Steam games to automatically dim glow intensity (up to 60%) or shorten durations.
- **Notification Deduplication**: Uses a sliding 2.5-second SHA-256 hash window to suppress duplicate rapid-fire notifications without storing message content.
- **Multi-Monitor Support**: Target the **Active Monitor** (where your cursor/focus is), the **Primary Display**, or **All Displays** simultaneously.
- **Per-Monitor V2 DPI Scaling**: Sharp, pixel-perfect rendering across mixed-DPI monitors (100% to 200%+) and ultrawide aspect ratios.
- **Fluent UI Experience**: Built with [WPF-UI 4.3.0](https://github.com/lepoco/wpfui) featuring Windows 11 Fluent styling and seamless Dark/Light theme switching.
- **Single-Instance Enforcement**: Protected via a named Win32 system mutex (`NotiGlow_SingleInstance_Mutex`), restoring the existing window when relaunched.
- **Offline & Privacy-First Architecture**: 100% local execution with zero network requests, zero telemetry, and zero notification text persistence.

---

## 📸 Screenshots / Demo

> [!NOTE]
> *High-resolution screenshots and animated demonstration GIFs of the settings dashboard and edge lighting effects will be published in the media showcase section.*

---

## 📦 Installation

### Portable Release (Recommended)

1. Navigate to the [v1.0.0 GitHub Release](https://github.com/owergungor/NotiGlow/releases/tag/v1.0.0).
2. Download **`NotiGlow_1.0.0_win-x64.zip`** ([Direct Download](https://github.com/owergungor/NotiGlow/releases/download/v1.0.0/NotiGlow_1.0.0_win-x64.zip)).
3. Verify the file integrity:
   - **SHA-256**: `5A152E0F3D5C8323710C878ADCF85EEC6FFB8517A2077825A0BD08772C3B3610`
4. Extract the ZIP archive into a preferred directory (e.g., `C:\Tools\NotiGlow`).
5. Run `NotiGlow.exe`.

### Windows Installer

An Inno Setup script is provided in the repository root (`NotiGlow-Setup.iss`). To compile a standalone installer on your workstation:
1. Install [Inno Setup 6](https://jrsoftware.org/isdl.php).
2. Run the compiler:
   ```powershell
   iscc NotiGlow-Setup.iss
   ```
3. The installer binary will be generated in `Output\NotiGlow-Setup.exe`.

---

## 💻 System Requirements

| Component | Minimum Requirement |
|---|---|
| **Operating System** | Windows 11 or Windows 10 64-bit (Version 2004 / Build 19041.0+) |
| **Architecture** | x64 |
| **Runtime** | [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (x64) |
| **Permissions** | Windows Notification Access enabled in Windows Settings |

---

## 🚀 Usage

### First-Time Setup
1. Launch **NotiGlow**.
2. If the dashboard status displays **Action Required**, click **Open Windows Settings** (or navigate to `Windows Settings > System > Notifications`).
3. Ensure **Notification Access** is toggled to **On** so NotiGlow's listener can detect incoming toast notifications.

### Everyday Operation
- **System Tray**: When minimized, NotiGlow runs quietly in the system tray. Right-click the tray icon to quickly open settings, toggle monitoring, or exit.
- **Customizing Glow**: Under the **Appearance** tab, adjust default color, animation engine, duration, blur radius, and edge thickness.
- **Application Profiles**: Under the **Applications** tab, create custom rules for your favorite apps, or duplicate an existing profile to tweak settings.

---

## 🏛️ Architecture

NotiGlow separates notification ingestion, business logic, and transparent overlay rendering into distinct modular components:

```text
Windows Notifications (Toast Events)
                │
                ▼
Notification Reader (WinRT UserNotificationListener)
                │
                ▼
       Application Services
  ┌───────────────────────────────┐
  │  • NotificationDeduplicator   │ (2.5-second SHA-256 hash window)
  │  • ProfileService             │ (Process / AppId rule matching)
  │  • GameDetectionService       │ (DirectX / Vulkan game suppression)
  │  • SettingsService            │ (Local JSON persistence)
  └──────────────┬────────────────┘
                 │
                 ▼
        Overlay / Rendering
  ┌───────────────────────────────┐
  │  • GlowManager                │ (Multi-screen dispatcher)
  │  • OverlayWindow (Win32)      │ (WS_EX_TRANSPARENT, WS_EX_LAYERED)
  │  • GlowBorderControl (WPF)    │ (Continuous gradient border)
  └──────────────┬────────────────┘
                 │
                 ▼
Screen Edge Glow (Perimeter Lighting)
```

---

## 📁 Project Structure

```text
NotiGlow/
├── .github/
│   ├── ISSUE_TEMPLATE/          # Structured issue forms (bug report, feature request)
│   ├── workflows/               # GitHub Actions CI & release automation
│   ├── dependabot.yml           # Dependabot dependency updates
│   └── pull_request_template.md # Standard PR checklist and template
├── Assets/                      # Application icons, artwork, and branding
├── Core/                        # Win32 P/Invoke declarations and system helpers
│   ├── Helpers/                 # Monitor, process, and color utilities
│   └── Win32/                   # Windows User32, Shell32 native interop
├── Models/                      # Application data models and settings definitions
├── Overlay/                     # Transparent border overlay rendering engine
├── Services/                    # Core background application services
│   ├── GameDetectionService.cs  # Foreground game detection
│   ├── GlowManager.cs           # Screen overlay coordinator
│   ├── NotificationService.cs   # Toast notification dispatcher
│   ├── ProfileService.cs        # App profile management
│   ├── SettingsService.cs       # JSON configuration manager
│   ├── TrayService.cs           # System tray icon & context menu
│   └── WindowsNotificationReader.cs # WinRT notification listener
├── UI/                          # WPF user interface and dashboard views
├── NotiGlow.Tests/              # Automated unit test suite (xUnit)
├── CHANGELOG.md                 # Keep a Changelog version history
├── CONTRIBUTING.md              # Contributor guidelines and workflow
├── CODE_OF_CONDUCT.md           # Contributor Covenant Code of Conduct
├── SECURITY.md                  # Vulnerability reporting policy
├── NotiGlow.csproj              # .NET project configuration
├── NotiGlow-Setup.iss           # Inno Setup installer script
├── LICENSE                      # MIT License
└── README.md                    # Project documentation
```

---

## 🏗️ Development & Build

### 1. Clone the Repository
```powershell
git clone https://github.com/owergungor/NotiGlow.git
cd NotiGlow
```

### 2. Restore Dependencies
```powershell
dotnet restore NotiGlow.csproj
dotnet restore NotiGlow.Tests/NotiGlow.Tests.csproj
```

### 3. Build the Solution
```powershell
# Build in Release configuration
dotnet build NotiGlow.csproj -c Release --no-restore
```

### 4. Run Unit Tests
```powershell
dotnet test NotiGlow.Tests/NotiGlow.Tests.csproj -c Release --no-restore
```

### 5. Publish Release Binaries
```powershell
dotnet publish NotiGlow.csproj -c Release -r win-x64 --self-contained false
```
The published binary files will be generated in:
`bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\`

---

## 🧪 Testing

NotiGlow includes a comprehensive automated test suite built with xUnit:
- **86 tests currently passing** (0 failed, 0 skipped).
- Tests validate notification deduplication windows, color conversion models, profile matching logic, game detection rules, and settings migration.

---

## 🔒 Privacy

- **100% Offline**: NotiGlow performs **zero** network requests and contains **no** telemetry, analytics, or tracking libraries.
- **In-Memory Toast Processing**: Notification metadata is inspected strictly in-memory to match profiles and compute deduplication hashes. Notification content is **never** saved to disk, logged, or transmitted.
- **Local Configuration Only**: User preferences and profiles are stored cleanly in `%APPDATA%\NotiGlow\settings.json`.

---

## 🗺️ Roadmap

- [ ] Visual demonstration GIF assets and screenshots for repository landing page.
- [ ] Additional animation customization parameters (corner radius tuning, dual-gradient sweeps).
- [ ] Automated installer builds in the CI release pipeline.
- [ ] Expanded notification priority rules and time-based Do Not Disturb scheduling.

---

## 🤝 Contributing

We welcome contributions! Please review our [Contributing Guidelines](CONTRIBUTING.md) and [Code of Conduct](CODE_OF_CONDUCT.md) before submitting pull requests.

---

## 🛡️ Security

For vulnerability reporting instructions and policy details, please refer to [SECURITY.md](SECURITY.md).

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

Copyright &copy; 2026 [owergungor](https://github.com/owergungor).

---

## 🚀 Releases

The latest production release is available on the [Releases](https://github.com/owergungor/NotiGlow/releases/tag/v1.0.0) page:
- **v1.0.0**: [Release Notes & Downloads](https://github.com/owergungor/NotiGlow/releases/tag/v1.0.0)
