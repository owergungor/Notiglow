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

**NotiGlow** is a lightweight, ambient notification utility designed specifically for Windows 10 and Windows 11. It resides quietly in your system tray and monitors incoming notifications via native Windows Runtime APIs (`UserNotificationListener`).

When a notification arrives from any application (e.g., Slack, Discord, Outlook, Steam), NotiGlow illuminates the perimeter of your screen with an ambient border glow animation. You gain immediate peripheral awareness of incoming messages and alerts without breaking focus or covering your workspace with intrusive popups.

---

## 💡 Why NotiGlow?

- **Stay Focused**: Standard toast popups cover your active windows, break concentration, and pull your gaze away from your work. NotiGlow gives you subtle peripheral awareness through screen-edge lighting.
- **Know Who's Calling**: Assign signature colors and motion engines to specific apps (e.g., purple for Discord, blue for Outlook, orange for Slack). A glance tells you what just happened.
- **Gamer & Media Friendly**: Automatically detects when you are in a game or fullscreen app to dim or suppress lighting so your immersion remains uninterrupted.
- **100% Private & Local**: No cloud accounts, no network calls, no telemetry, and notification text is never saved to disk.

---

## ✨ Features

- **Windows Notification Monitoring**: Integrates directly with native Windows `UserNotificationListener` APIs to capture toast events in real-time without third-party drivers or polling hacks.
- **Ambient Screen-Edge Glow**: Illuminates display borders with continuous four-edge lighting and smooth corner gradient blending.
- **5 Distinct Animation Engines**:
  - `Pulse`: Smooth, rhythmic opacity breathing.
  - `Sweep`: A focused beam of light circulating around the display perimeter.
  - `Ambient`: Calm, subtle breathing designed for zero distraction.
  - `Comet`: Dynamic high-intensity leading head with an elegant trailing decay.
  - `Ripple`: Corner-expanding light waves cascading along display boundaries.
- **Per-Application Profiles**: Assign dedicated colors, animation motions, durations, and intensity levels to individual apps (e.g., Discord, Slack, Steam, Outlook).
- **Profile Duplication**: Duplicate and customize existing application profiles with a single click.
- **OLED-Friendly Behavior**: Protects display panels with peak luminance limiting and pure black preservation to prevent panel burn-in.
- **Fullscreen & Gaming Suppression**: Detects active DirectX, Vulkan, and Steam games to automatically dim glow intensity (up to 60%) or shorten durations.
- **Notification Deduplication**: Uses a sliding 2.5-second SHA-256 hash window to suppress duplicate rapid-fire notifications without storing message content.
- **Multi-Monitor Support**: Target the **Active Monitor** (where your cursor/focus is), the **Primary Display**, or **All Displays** simultaneously.
- **Per-Monitor V2 DPI Scaling**: Sharp, pixel-perfect rendering across mixed-DPI monitors (100% to 200%+) and ultrawide aspect ratios.
- **Fluent UI Experience**: Built with [WPF-UI 4.3.0](https://github.com/lepoco/wpfui) featuring Windows 11 Fluent styling and seamless Dark/Light theme switching.
- **Single-Instance Enforcement**: Protected via a named Win32 system mutex (`NotiGlow_SingleInstance_Mutex`), restoring the existing window when relaunched.
- **Offline & Privacy-First Architecture**: 100% local execution with zero network requests, zero telemetry, and zero notification text persistence.

---

## 📸 Screenshots / Visuals

> [!NOTE]
> *High-resolution screenshots and animated demonstration GIFs of the settings dashboard and ambient edge lighting effects will be published here in the upcoming v1.1.0 media showcase.*

---

## 📦 Installation & Distribution

### 1. Windows Installer (`NotiGlow-Setup-x64.exe`)

The easiest way to install and use NotiGlow on any 64-bit Windows PC:
1. Download **`NotiGlow-Setup-x64.exe`** from the latest GitHub Release.
2. Run the installer. It configures the application in `Program Files\NotiGlow`, creates Start Menu and optional Desktop shortcuts, and provides clean Windows Settings uninstallation.
3. The installer includes all self-contained .NET dependencies—**no manual .NET runtime installation required**.

### 2. Portable Archive (`NotiGlow-win-x64.zip`)

For users who prefer running NotiGlow without an installer:
1. Download **`NotiGlow-win-x64.zip`** (or versioned `NotiGlow_1.0.0_win-x64.zip` for v1.0.0).
2. Extract the archive into any preferred directory (e.g., `C:\Tools\NotiGlow`).
3. Launch **`NotiGlow.exe`**.

> [!NOTE]
> **Windows SmartScreen Notice**: Because NotiGlow is an independent open-source project without a paid corporate EV Code Signing Certificate, Windows SmartScreen may present an informational message stating *"Windows protected your PC"* on initial launch. Click **More info** ➔ **Run anyway** to start the application. NotiGlow contains zero telemetry, zero trackers, and 100% locally open-source code. Procuring an official code-signing certificate is part of the project's long-term roadmap.

---

## 💻 System Requirements

| Component | Requirement |
|---|---|
| **Operating System** | Windows 11 or Windows 10 64-bit (Version 2004 / Build 19041.0+) |
| **Architecture** | x64 |
| **Runtime** | **Self-Contained** (Runtime is built-in; no separate .NET install needed for self-contained packages) |
| **Framework-Dependent Builds** | Requires [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (x64) |
| **Permissions** | Windows Notification Access enabled in Windows Settings |

---

## 🚀 First Run & Usage

### 1. First-Time Setup
1. Launch **`NotiGlow.exe`**.
2. If the dashboard status banner displays **Action Required**, click **Open Windows Settings** (or navigate to `Windows Settings > System > Notifications`).
3. Ensure **Notification Access** is toggled to **On** so NotiGlow's listener can detect incoming toast notifications.

### 2. Everyday Operation
- **System Tray**: When minimized, NotiGlow runs quietly in the system tray. Right-click the tray icon to quickly open settings, toggle monitoring on/off, or exit.
- **Customizing Glow**: Under the **Appearance** tab, adjust default color, animation engine, duration, blur radius, and edge thickness.
- **Application Profiles**: Under the **Applications** tab, create custom rules for your favorite apps, or duplicate an existing profile to tweak settings.
- **Gaming & Fullscreen**: Under the **Gaming** tab, configure whether glow is dimmed or suppressed when playing games.

---

## ⚙️ Configuration

NotiGlow stores all user configuration locally as readable JSON:
- **Location**: `%APPDATA%\NotiGlow\settings.json`
- **Backup & Reset**: You can export, import, or factory-reset your settings directly from the **General** settings tab in the dashboard.
- **Startup Diagnostics**: If NotiGlow encounters any startup issue, detailed initialization logs are written to `%LOCALAPPDATA%\NotiGlow\startup.log`.

---

## 🔒 Privacy & Local Processing

- **100% Offline**: NotiGlow performs **zero** network requests and contains **no** telemetry, analytics, or third-party tracking code.
- **In-Memory Toast Processing**: Notification metadata is inspected strictly in-memory to match profiles and compute deduplication hashes. Notification message text is **never** saved to disk, logged, or transmitted anywhere.
- **Local Storage Only**: User preferences and custom profiles are stored exclusively on your machine in `%APPDATA%\NotiGlow\settings.json`.

---

## 🔧 Troubleshooting

| Issue | Likely Cause | Resolution |
|---|---|---|
| **Notifications not detected** | Windows Notification Access is disabled | Open `Windows Settings > System > Notifications` and grant permission. Ensure Focus Assist / Do Not Disturb isn't blocking notifications. |
| **Glow not appearing** | Overlay window minimized or monitoring paused | Right-click the tray icon and ensure monitoring is active. Go to **Appearance** and click **Test Glow** to verify screen rendering. |
| **Glow suppressed during games** | Game suppression mode active | Under the **Gaming** tab, check the dimming level or disable gaming suppression if you prefer full-brightness alerts during gameplay. |
| **DirectX exclusive fullscreen** | Legacy fullscreen bypasses Desktop Composition (DWM) | Run your game in **Borderless Fullscreen** (Windowed Fullscreen) so the Windows DWM compositing layer can render overlays above the game. |
| **Multi-monitor placement issue** | Monitor mode setting | Under **Appearance**, select your desired targeting mode: `Active Monitor`, `Primary Display`, or `All Displays`. |
| **App fails to start** | Missing .NET 9 Desktop Runtime | Install the [.NET 9.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0). Check `%LOCALAPPDATA%\NotiGlow\startup.log` for details. |
| **Duplicate rapid-fire alerts** | App emitting multiple distinct toasts | NotiGlow automatically filters identical toasts within a 2.5-second sliding window. Rapid notifications with different contents will trigger individually. |

---

## ❓ Frequently Asked Questions (FAQ)

<details>
<summary><b>Does NotiGlow work on Windows 10?</b></summary>
Yes! NotiGlow supports Windows 10 64-bit (Version 2004 / Build 19041.0 or newer) as well as Windows 11.
</details>

<details>
<summary><b>Does NotiGlow read my private messages?</b></summary>
No. NotiGlow only inspects toast metadata in-memory to match application IDs (like Slack or Discord) and compute a temporary hash to avoid duplicate triggers. Message bodies are never stored, logged, or sent anywhere.
</details>

<details>
<summary><b>Does NotiGlow impact gaming performance or battery life?</b></summary>
No. When idle between notifications, the transparent overlay windows hide entirely and consume ~0% CPU and GPU. During animations, lightweight hardware-accelerated WPF shaders are used.
</details>

<details>
<summary><b>Can I run multiple copies of NotiGlow?</b></summary>
NotiGlow enforces a single-instance lock. If you attempt to launch it again, it brings the existing dashboard window to the foreground.
</details>

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
├── NotiGlow.Tests/              # Automated unit test suite (MSTest)
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

NotiGlow includes an automated unit test suite built with **MSTest**:
- **86 tests currently passing** (0 failed, 0 skipped).
- Tests validate notification deduplication windows, color conversion models, profile matching logic, game detection rules, and settings migration.

---

## 🗺️ Roadmap

- [ ] High-resolution visual showcase assets and GIFs for repository landing page.
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
