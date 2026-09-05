<div align="center">

# 🔔 NotiGlow

**A modern Windows notification companion with ambient screen-edge glow.**

Transform your standard Windows desktop toast notifications into smooth, beautiful, and customizable fullscreen edge lighting animations.

[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20%7C%20Windows%2010%20(x64)-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET-9.0--windows10.0.19041-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Language](https://img.shields.io/badge/Language-C%23%2013-239120?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![UI](https://img.shields.io/badge/UI-WPF%20%2B%20WPF--UI%204.3.0-0078D4?style=for-the-badge)](https://github.com/lepoco/wpfui)
[![Tests](https://img.shields.io/badge/Tests-86%20Passed-brightgreen?style=for-the-badge&logo=checkmarx&logoColor=white)]()
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

</div>

---

## 📖 Overview

**NotiGlow** is a lightweight, ambient notification utility built from the ground up for Windows 10 and Windows 11. It sits quietly in the Windows system tray and actively monitors incoming toast notifications using native Windows Runtime APIs (`UserNotificationListener`).

When a notification is received from any application, NotiGlow illuminates the perimeter of your screen with an ambient border glow animation. You receive immediate, non-intrusive peripheral awareness of incoming messages, emails, and alerts without breaking your focus or covering your workspace with intrusive popups.

---

## ✨ Features

### 🔔 Notification Monitoring & Management
- **Native Windows Integration**: Directly connects to the Windows `UserNotificationListener` WinRT API to capture toasts in real-time.
- **Intelligent Deduplication**: Employs a 2.5-second sliding window SHA256 hash filter to suppress repetitive toast triggers without retaining message text.
- **Priority Filtering**: Categorize notifications by priority level to highlight critical alerts while muting background noise.

### ✨ Ambient Screen Glow Engine
- **4-Edge Continuous Glow**: Fullscreen perimeter lighting featuring smooth, seamless corner gradient blends.
- **5 Distinct Motion Engines**:
  - `Pulse`: Smooth, uniform opacity breathing rhythm.
  - `Sweep`: Dynamic beam of light circulating around the display perimeter.
  - `Ambient`: Gentle, calm breathing cycle designed for minimal distraction.
  - `Comet`: High-intensity leading head accompanied by an elegant trailing decay.
  - `Ripple`: Corner-expanding light waves cascading along the screen boundaries.
- **Granular Visual Controls**: Independently adjust glow hold duration (seconds), edge thickness, blur radius, color, and peak opacity.

### 🎛️ Per-Application Profiles
- **Custom App Rules**: Assign dedicated colors (HEX/RGB), animation styles, durations, and intensity per executable (e.g., Discord, Slack, Steam, Outlook).
- **One-Click Duplication**: Rapidly duplicate and adapt existing application profiles.
- **Automatic Matching**: Dynamically matches incoming notification AppIds and process names to tailored profiles with a clean default fallback.

### 🛡️ OLED Friendly & Display Protection
- **Luminance Dampening**: Restricts peak luminance on OLED screens to protect panel health and minimize power draw.
- **True Black Preservation**: Ensures deep black areas remain untouched to prevent panel wear and burn-in.

### 🎮 Gaming & Fullscreen Detection
- **Foreground Process Monitoring**: Automatically detects active Steam titles, DirectX, and Vulkan games.
- **Smart Distraction Suppression**: Automatically dims glow intensity (configurable up to 60%) or curtails animation duration during gameplay.

### 🖥️ Multi-Monitor & DPI Scaling
- **Flexible Targeting**: Direct glow animations to the **Active Monitor** (where your cursor or focused window is), the **Primary Display**, or **All Displays**.
- **Per-Monitor V2 DPI Awareness**: Perfectly scales across mixed-DPI multi-monitor environments from 100% to 200%+ scaling factors, including ultrawide resolutions.

### 🪟 Fluent Desktop Experience
- **Modern Fluent Design System**: Clean, responsive UI powered by [WPF-UI 4.3.0](https://github.com/lepoco/wpfui) with full Dark and Light mode support.
- **Single-Instance Protection**: Enforced via a native Win32 named system mutex (`NotiGlow_SingleInstance_Mutex`), restoring the existing window if launched again.
- **System Tray Lifecycle**: Runs unobtrusively in the notification area with quick context menu controls (Open Dashboard, Toggle Monitoring, Exit).
- **Zero Idle Resource Consumption**: The transparent click-through overlay windows hide entirely between notification triggers, consuming **~0% CPU and GPU** when idle.
- **Settings Backup & Migration**: Built-in JSON settings export, import, factory reset, and automatic migration for legacy configurations.

---

## 📸 Screenshots

<!-- Screenshots will be published with the public release -->
> *UI dashboard screenshots and ambient glow demonstrations will be showcased here upon public release.*

---

## 💻 System Requirements

| Component | Requirement |
|---|---|
| **Operating System** | Windows 11 or Windows 10 (64-bit, Version 2004 / Build 19041.0 or higher) |
| **Architecture** | x64 |
| **Runtime** | [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (x64) |
| **Permissions** | Windows Notification Access enabled in Windows Settings |

---

## 📦 Installation

### Portable Release (Recommended)
1. Download the latest `NotiGlow_1.0.0_win-x64.zip` from the [Releases](../../releases) section.
2. Extract the archive into a preferred folder (e.g., `C:\Tools\NotiGlow`).
3. Run `NotiGlow.exe`.

### Windows Installer
1. Download `NotiGlow-Setup.exe` from the [Releases](../../releases) section.
2. Launch the installer and follow the setup wizard to configure Start Menu shortcuts and optional Windows startup integration.

---

## 🔔 Windows Notification Permission

Windows requires explicit user approval for desktop tools to inspect toast notifications:

1. Launch **NotiGlow**.
2. If the dashboard status reads **Action Required**, click **Open Windows Settings** (or navigate to `Windows Settings > System > Notifications`).
3. Turn **Notification Access** to **On** so NotiGlow's listener can capture notifications.

---

## 🏛️ Architecture Overview

NotiGlow separates UI management, system background monitoring, and overlay rendering into modular layers:

```text
┌─────────────────────────────────────────────────────────────┐
│                      NotiGlow UI (WPF)                      │
│   (MainWindow, Appearance, Applications, Gaming, General)   │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│                    NotiGlow Application                     │
│  - TrayService (NotifyIcon, Shell Tray Lifecycle)           │
│  - SettingsService (%APPDATA%\NotiGlow\settings.json)       │
│  - ProfileService (Per-App Rule Matching & Duplication)     │
│  - GameDetectionService (Foreground Process Watcher)        │
│  - GlowManager (Multi-Screen Overlay Coordinator)           │
└──────────────┬──────────────────────────────┬───────────────┘
               │                              │
               ▼                              ▼
┌───────────────────────────────┐ ┌───────────────────────────┐
│   Windows Notification Hook   │ │   OverlayWindow (Win32)   │
│ - WinRT UserNotificationReader│ │ - WS_EX_TRANSPARENT       │
│ - Toast Notification Stream   │ │ - WS_EX_LAYERED           │
│ - SHA256 Deduplication Filter │ │ - GlowBorderControl (WPF) │
└───────────────────────────────┘ └───────────────────────────┘
```

- **`OverlayWindow`**: Transparent, click-through (`WS_EX_TRANSPARENT`, `WS_EX_LAYERED`, `WS_EX_TOOLWINDOW`, `WS_EX_NOACTIVATE`) topmost border window positioned dynamically across targeted screens.
- **`WindowsNotificationReader`**: Utilizes Windows Runtime `Windows.UI.Notifications.Management.UserNotificationListener` APIs to capture toast triggers without third-party drivers.
- **`NotificationDeduplicator`**: Generates a transient SHA256 hash of app identifiers to filter duplicate rapid-fire notifications within a sliding 2.5-second window.
- **`SettingsService`**: Manages configuration stored cleanly at `%APPDATA%\NotiGlow\settings.json`.

---

## 🛠️ Project Structure

```text
NotiGlow/
├── Assets/                      # Icons, logo artwork, application resources
│   ├── NotiGlow.ico
│   └── NotiGlowLogo.png
├── Core/                        # Win32 P/Invoke declarations and system helpers
│   ├── Helpers/                 # Monitor, process, and color utilities
│   └── Win32/                   # Windows User32 / Shell32 native interop
├── Models/                      # Application data models and settings definitions
│   ├── AppProfile.cs
│   ├── AppSettings.cs
│   ├── Enums.cs
│   └── NotificationItem.cs
├── Overlay/                     # Transparent border overlay rendering engine
│   ├── GlowBorderControl.xaml   # Continuous gradient border drawing
│   └── OverlayWindow.xaml       # Click-through layered topmost window
├── Services/                    # Core background application services
│   ├── GameDetectionService.cs  # Foreground game process watcher
│   ├── GlowManager.cs           # Screen overlay animation dispatcher
│   ├── NotificationService.cs   # Toast notification coordinator
│   ├── ProfileService.cs        # App profile matcher & storage
│   ├── SettingsService.cs       # JSON configuration manager
│   ├── TrayService.cs           # System tray icon and context menu
│   └── WindowsNotificationReader.cs # WinRT notification listener
├── Tools/                       # Utility helpers and icon toolchain
├── UI/                          # WPF user interface and views
│   ├── MainWindow.xaml          # Main application dashboard
│   ├── Controls/                # Custom color pickers and preview controls
│   └── Views/                   # Appearance, Applications, Gaming, General tabs
├── NotiGlow.Tests/              # Automated unit test suite (xUnit)
├── NotiGlow.csproj              # .NET project configuration
├── NotiGlow-Setup.iss           # Inno Setup installer script
├── LICENSE                      # MIT License
└── README.md                    # Project documentation
```

---

## 🏗️ Development & Build

### Prerequisites
- [Windows 10/11 x64](https://microsoft.com/windows) (Build 19041+)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (v9.0.100 or higher)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (v17.12+) or Visual Studio Code with the C# Dev Kit extension
- *(Optional)* [Inno Setup 6](https://jrsoftware.org/isdl.php) (for building the installer)

### 1. Clone the Repository
```powershell
git clone https://github.com/owergungor/notiglow.git
cd notiglow
```

### 2. Restore Dependencies
```powershell
dotnet restore NotiGlow.csproj
```

### 3. Build the Solution
```powershell
# Build in Debug configuration
dotnet build NotiGlow.csproj -c Debug

# Build in Release configuration
dotnet build NotiGlow.csproj -c Release
```

### 4. Run Unit Tests
```powershell
dotnet test NotiGlow.Tests\NotiGlow.Tests.csproj
```
> **Test Status**: All **86 tests passing** (0 failed, 0 skipped).

### 5. Publish Release Binaries
```powershell
dotnet publish NotiGlow.csproj -c Release -r win-x64 --self-contained false
```
The published application files will be generated in:
`bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\`

### 6. Compile the Windows Installer (Optional)
If Inno Setup 6 is installed on your workstation:
```powershell
iscc NotiGlow-Setup.iss
```
The compiled installer will be output as `Output\NotiGlow-Setup.exe`.

---

## 🔒 Privacy & Local Processing

- **100% Offline**: NotiGlow performs **zero** network requests and contains **no** analytics, telemetry, or third-party tracking.
- **Ephemeral Toast Inspection**: Notification source titles and body contents are inspected strictly in-memory for profile matching and hash deduplication. Notification text is **never** saved to disk or written to log files.
- **Transparent Local Storage**: User settings and custom profiles are stored exclusively in `%APPDATA%\NotiGlow\` as readable, user-editable JSON files.

---

## ⚠️ Known Limitations

1. **User Notification Permission**: Windows requires manual user authorization in Windows Settings before any utility can receive toast events.
2. **DirectX / Vulkan Exclusive Fullscreen**: Games running in legacy Exclusive Fullscreen mode bypass the Windows Desktop Window Manager (DWM) composition pipeline. Borderless Fullscreen (Windowed Fullscreen) is recommended for full ambient overlay visibility.
3. **HDR Tone Mapping Variations**: Depending on display hardware and Windows Advanced Color settings, HDR tone mapping can subtly influence perceived edge glow brightness.

---

## 📄 License

This project is licensed under the terms of the [MIT License](LICENSE).

Copyright &copy; 2026 owergungor
