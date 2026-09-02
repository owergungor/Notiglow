# NotiGlow (Glow Border 2.0) ✨

> **Modern, High-Performance 4-Edge Ambient Screen Glow Notification Utility for Windows 11 & Windows 10**

[![Windows 11](https://img.shields.io/badge/OS-Windows%2011%20%7C%2010-0078D4?style=for-the-badge&logo=windows)](https://github.com/owergungor/Notiglow)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![WPF](https://img.shields.io/badge/UI-WPF%20%2B%20WPF--UI-0078D4?style=for-the-badge)](https://github.com/lepoco/wpfui)
[![License: MIT](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

**NotiGlow** transforms standard Windows toast notifications into stunning, ambient 4-edge screen glow animations. Built with C#, .NET 9.0, WPF, and WPF-UI, it offers seamless notification monitoring with zero idle resource footprint and native Windows 11 Fluent design aesthetics.

---

## 📸 Screenshots

*(Screenshots will be showcased here upon release)*

---

## 🌟 Key Features

- **🌈 4-Edge Continuous Ambient Glow**: Edge-to-edge screen glow with smooth corner gradient joins.
- **⚡ 5 Dynamic Motion Engines**:
  - **Pulse**: Uniform opacity breathing effect.
  - **Sweep**: Dynamic traveling light beam around the screen perimeter.
  - **Ambient**: Soft, relaxed breathing glow.
  - **Comet**: High-velocity light head with trailing glow decay.
  - **Ripple**: Corner-expanding light waves.
- **🎨 Windows 11 Fluent UI & Physical Press Feedback**:
  - Tactile physical press micro-animations (`1.0 → 0.97` scale with snappy easing).
  - Synchronized sidebar selection box with Accent color indicators.
  - 4 Curated Themes: **Dark**, **Light**, **System**, and **Night Blue**.
- **🛡️ OLED Saver Mode**: Caps peak luminance and avoids static element burn-in to preserve OLED screens.
- **🎮 Smart Gaming Mode**: Auto-detects active games (Steam, DirectX, Vulkan) to dim glow intensity, adjust animation speed, or filter non-critical notifications.
- **🧠 Intelligent Deduplication**: Suppresses spammy and repetitive notification toasts automatically within configurable burst windows.
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

### Option 1: Portable Zip (No Installation Required)
1. Download `GlowBorder-Portable.zip` from the [Latest Release](https://github.com/owergungor/Notiglow/releases).
2. Extract the archive to any folder.
3. Run `NotiGlow.exe`.

### Option 2: Windows Installer
1. Download and run `GlowBorder-Setup.exe`.
2. Follow the setup wizard to install Start Menu shortcuts and optional Windows startup integration.

---

## 🔔 Windows Notification Permission Setup

Windows requires explicit user authorization for apps monitoring system toasts:
1. Launch **NotiGlow**.
2. If `Notification Access` indicates **Action Required**, click **Open Windows Settings**.
3. In Windows Settings (under **Notifications & actions**), enable Notification Listener access for **NotiGlow**.

---

## 🛠️ Tech Stack & Architecture

| Component | Technology | Description |
| :--- | :--- | :--- |
| **Framework** | .NET 9.0 (C# 13) | Modern, high-performance .NET runtime |
| **UI Framework** | WPF (Windows Presentation Foundation) | Hardware-accelerated desktop rendering |
| **Design System** | [WPF-UI](https://github.com/lepoco/wpfui) (v4.3.0) | Windows 11 Fluent Design controls & Mica/Acrylic styling |
| **Notification Hook** | Windows.UI.Notifications API | Low-overhead Toast notification listener |
| **Testing** | MSTest + Coverlet | Comprehensive unit test suite with STA thread validation |

---

## 💻 Building & Running Locally

### Prerequisites
- Visual Studio 2022 (v17.10+) / JetBrains Rider / VS Code
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build Debug / Release
```powershell
# Restore dependencies
dotnet restore

# Build project
dotnet build GlowBorder.csproj

# Publish Release executable (win-x64)
dotnet publish GlowBorder.csproj -c Release -r win-x64 --self-contained false
```

### Run Unit Tests
```powershell
dotnet test GlowBorder.Tests/GlowBorder.Tests.csproj
```

---

## ⚠️ Known Limitations

- **Full-Screen Exclusive Games**: Some legacy games running in Exclusive Fullscreen mode may render directly over the desktop window layer; running games in Borderless Windowed mode ensures 100% overlay visibility.
- **Windows N / Server Editions**: Windows Media / Notification features may require the Windows Media Feature Pack.

---

## 🔒 Privacy & Security

- **100% Local**: Works completely offline.
- **Zero Telemetry**: No tracking, analytics, or background internet calls.
- **In-Memory Processing**: Toast titles and app identifiers are parsed in-memory strictly for profile matching and deduplication; notification bodies are never stored, logged, or transmitted.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE) - see the [LICENSE](LICENSE) file for details.
