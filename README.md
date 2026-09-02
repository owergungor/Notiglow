# NotiGlow (Glow Border 2.0) ✨

> **Modern, High-Performance 4-Edge Screen Glow Notification Utility for Windows 11 & Windows 10**

![Windows 11](https://img.shields.io/badge/OS-Windows%2011%20%7C%2010-0078D4?style=for-the-badge&logo=windows)
![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF%20%2B%20WPF--UI-0078D4?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

**NotiGlow** transforms standard Windows toast notifications into stunning, ambient 4-edge screen glow animations. Built with WPF, WPF-UI, and modern Windows API hooks, it offers seamless notification monitoring with zero idle resource footprint.

---

## 🌟 Key Features

- **🌈 4-Edge Continuous Ambient Glow**: Edge-to-edge screen glow with smooth corner gradient joins.
- **⚡ 5 Dynamic Motion Engines**:
  - **Pulse**: Uniform opacity breathing effect.
  - **Sweep**: Dynamic traveling light beam around the perimeter.
  - **Ambient**: Soft, relaxed breathing glow.
  - **Comet**: High-velocity light head with trailing glow decay.
  - **Ripple**: Corner-expanding light waves.
- **🛡️ OLED Saver Mode**: Caps peak luminance and avoids static element burn-in to preserve OLED screens.
- **🎮 Smart Gaming Mode**: Auto-detects active games (Steam, DirectX, Vulkan) to dim glow intensity, adjust animation speed, or filter non-critical notifications.
- **🧠 Intelligent Deduplication**: Suppresses spammy and repetitive notification toasts automatically.
- **🖥️ Multi-Monitor & DPI Aware**: Full support for Active Monitor, Primary Display, and All Monitors across 100%–200%+ DPI scaling factors and Ultrawide displays.
- **🎯 Per-App Custom Profiles**: Customize colors (HEX/RGB), durations, glow blur radiuses, border thicknesses, and priorities per application. Includes 1-click profile duplication.
- **⚡ Zero Idle Overhead**: Uses ~0% CPU/GPU when idle with transparent overlay windows that hide automatically.
- **💾 Settings Import & Export**: Easily back up, restore, or reset configurations to factory defaults.

---

## 📋 System Requirements

- **Operating System**: Windows 11 / Windows 10 (x64, Version 19041.0 or higher)
- **Runtime**: [.NET 9.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0)

---

## 🚀 Getting Started

### Option 1: Portable Zip
1. Download `GlowBorder-Portable.zip` from the latest release.
2. Extract to your desired directory.
3. Run `NotiGlow.exe` (or `GlowBorder.exe`).

### Option 2: Installer
1. Run `GlowBorder-Setup.exe`.
2. Follow the setup wizard to install Start Menu shortcuts and optional Windows startup integration.

---

## 🔔 Windows Notification Permission Setup

Windows requires explicit user authorization for apps monitoring system toasts:
1. Launch **NotiGlow**.
2. If `Notification Access` indicates **Action Required**, click **Open Windows Settings**.
3. In Windows Settings (under **Notifications & actions**), enable Notification Listener access for NotiGlow.

---

## 🛠️ Building & Running locally

### Prerequisites
- Visual Studio 2022 / JetBrains Rider / VS Code
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build Debug / Release
```powershell
# Build project
dotnet build GlowBorder.csproj

# Publish Release
dotnet publish GlowBorder.csproj -c Release -r win-x64 --self-contained false
```

### Run Unit Tests
```powershell
dotnet test GlowBorder.Tests/GlowBorder.Tests.csproj
```

---

## 🔒 Privacy & Security

- **100% Local**: Works completely offline.
- **Zero Telemetry**: No tracking, analytics, or background internet calls.
- **In-Memory Processing**: Toast titles and text are parsed in-memory strictly for profile matching and deduplication; content is never stored or logged.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
