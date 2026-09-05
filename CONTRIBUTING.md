# Contributing to NotiGlow

Thank you for your interest in contributing to **NotiGlow**! We welcome bug fixes, documentation improvements, and feature proposals that adhere to our design principles.

---

## 📋 Table of Contents
- [Code of Conduct](#code-of-conduct)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Building the Project](#building-the-project)
- [Running Unit Tests](#running-unit-tests)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Commit Message Guidelines](#commit-message-guidelines)
- [Submitting a Pull Request](#submitting-a-pull-request)
- [Reporting Issues](#reporting-issues)

---

## 🤝 Code of Conduct

All contributors and maintainers are expected to follow our [Code of Conduct](CODE_OF_CONDUCT.md) to ensure an inclusive, respectful, and collaborative environment.

---

## 🛠️ Prerequisites

Before you start, make sure you have the following tools installed:

- **Operating System**: Windows 10 (Build 19041+) or Windows 11 (64-bit).
- **.NET SDK**: [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (v9.0.100 or newer).
- **IDE**: [Visual Studio 2022](https://visualstudio.microsoft.com/) (v17.12+ with *.NET Desktop Development* workload) or [Visual Studio Code](https://code.visualstudio.com/) with the *C# Dev Kit* extension.
- **Git**: [Git for Windows](https://git-scm.com/download/win).

---

## 🚀 Getting Started

1. **Fork** the repository on GitHub: [https://github.com/owergungor/NotiGlow](https://github.com/owergungor/NotiGlow)
2. **Clone** your fork locally:
   ```powershell
   git clone https://github.com/<your-username>/NotiGlow.git
   cd NotiGlow
   ```
3. **Add upstream remote**:
   ```powershell
   git remote add upstream https://github.com/owergungor/NotiGlow.git
   git fetch upstream
   ```

---

## 🔨 Building the Project

Restore NuGet packages:
```powershell
dotnet restore NotiGlow.csproj
dotnet restore NotiGlow.Tests/NotiGlow.Tests.csproj
```

Build in Release configuration:
```powershell
dotnet build NotiGlow.csproj -c Release --no-restore
```

---

## 🧪 Running Unit Tests

NotiGlow maintains automated unit tests covering core services, models, deduplication, and game detection logic. Always ensure all tests pass before proposing changes:

```powershell
dotnet test NotiGlow.Tests/NotiGlow.Tests.csproj -c Release --no-restore
```

If you introduce new logic or bug fixes, please write corresponding unit tests in `NotiGlow.Tests/`.

---

## 🔄 Development Workflow

1. Create a dedicated feature or bugfix branch from `main`:
   ```powershell
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/your-bugfix-name
   ```
2. Make targeted, atomic changes.
3. Verify your build and test runs pass locally with zero warnings and zero errors.

---

## 🎨 Coding Standards

- Follow standard [C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions).
- Target **.NET 9** and **C# 13** language idioms.
- Keep UI markup in XAML cleanly formatted with clear naming conventions.
- Maintain existing non-blocking async patterns (`async`/`await`) for background monitoring.
- Do not introduce external third-party dependencies unless strictly required and agreed upon via an issue discussion.

---

## 📝 Commit Message Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` A new user-facing feature or enhancement.
- `fix:` A bug fix.
- `docs:` Documentation improvements or changes.
- `test:` Adding or updating tests.
- `refactor:` Code restructuring without changing observable behavior.
- `ci:` Changes to GitHub Actions or CI scripts.
- `chore:` Miscellaneous housekeeping or maintenance tasks.

Example:
```text
feat(overlay): add configurable corner radius option
fix(dedup): prevent race condition on concurrent toast arrival
```

---

## 📤 Submitting a Pull Request

1. Ensure your branch is rebased onto the latest `upstream/main`:
   ```powershell
   git fetch upstream
   git rebase upstream/main
   ```
2. Push your branch to your fork:
   ```powershell
   git push origin feature/your-feature-name
   ```
3. Open a Pull Request against `owergungor/NotiGlow:main`.
4. Fill out the [Pull Request Template](.github/pull_request_template.md) completely, describing the what, why, and test verification.

---

## 🐛 Reporting Issues

If you encounter bugs, regressions, or have feature suggestions:

- **Check existing issues** first to avoid duplicates.
- Use our structured [Issue Templates](https://github.com/owergungor/NotiGlow/issues/new/choose) (`Bug Report` or `Feature Request`).
- Include your Windows OS build, NotiGlow version, and reproduction steps.
