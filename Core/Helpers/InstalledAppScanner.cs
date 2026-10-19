using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NotiGlow.Models;

namespace NotiGlow.Core.Helpers
{
    public class InstalledAppScanner
    {
        private static InstalledAppScanner? _defaultInstance;
        public static InstalledAppScanner Default => _defaultInstance ??= new InstalledAppScanner();

        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, string?> _shortcutResolver;

        public InstalledAppScanner(
            Func<string, bool>? fileExists = null,
            Func<string, bool>? directoryExists = null,
            Func<string, string?>? shortcutResolver = null)
        {
            _fileExists = fileExists ?? (p => !string.IsNullOrEmpty(p) && File.Exists(p));
            _directoryExists = directoryExists ?? (p => !string.IsNullOrEmpty(p) && Directory.Exists(p));
            _shortcutResolver = shortcutResolver ?? DefaultShortcutResolver;
        }

        public IReadOnlyList<AppProfile> DetectAllInstalledApps()
        {
            var discovered = new List<AppProfile>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAddProfile(AppProfile profile)
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.ExecutablePath)) return;

                string rawPath = profile.ExecutablePath.Trim().Trim('\"');
                if (!_fileExists(rawPath)) return;

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(rawPath);
                }
                catch
                {
                    return;
                }

                if (!_fileExists(fullPath)) return;

                string exeName = Path.GetFileName(fullPath).ToLowerInvariant();
                string normPath = fullPath.ToLowerInvariant();
                string cleanId = CleanIdentityString(profile.AppId);
                string cleanName = CleanIdentityString(profile.Name);

                if (IsDisallowedExecutable(exeName, fullPath)) return;

                // Deduplication check: if canonical path, exe name, or clean identity/name was already seen, do not add duplicate!
                if (seenPaths.Contains(normPath) ||
                    (!string.IsNullOrEmpty(exeName) && seenExes.Contains(exeName)) ||
                    (!string.IsNullOrEmpty(cleanId) && seenIdentities.Contains(cleanId)) ||
                    (!string.IsNullOrEmpty(cleanName) && seenIdentities.Contains(cleanName)))
                {
                    // If existing entry has less specific details (e.g. placeholder color), update it if this is a built-in match
                    var existing = discovered.FirstOrDefault(p =>
                        (p.ExecutablePath != null && Path.GetFullPath(p.ExecutablePath).Equals(fullPath, StringComparison.OrdinalIgnoreCase)) ||
                        CleanIdentityString(p.AppId).Equals(cleanId, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(p.Name) && CleanIdentityString(p.Name).Equals(cleanName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(p.ExecutablePath) && Path.GetFileName(p.ExecutablePath).Equals(exeName, StringComparison.OrdinalIgnoreCase)));

                    if (existing != null)
                    {
                        if (existing.ColorHex == "#5865F2" && profile.ColorHex != "#5865F2")
                        {
                            existing.ColorHex = profile.ColorHex;
                            existing.Style = profile.Style;
                            existing.DurationMs = profile.DurationMs;
                            existing.Intensity = profile.Intensity;
                        }
                    }
                    return;
                }

                profile.ExecutablePath = fullPath;
                discovered.Add(profile);

                seenPaths.Add(normPath);
                seenExes.Add(exeName);
                if (!string.IsNullOrEmpty(cleanId)) seenIdentities.Add(cleanId);
                if (!string.IsNullOrEmpty(cleanName)) seenIdentities.Add(cleanName);
            }

            // 1. Built-In Canonical Apps (Highest fidelity metadata)
            try
            {
                var builtIn = BuiltInAppDetector.Default.GetInstalledProfiles();
                foreach (var p in builtIn)
                {
                    TryAddProfile(p);
                }
            }
            catch { }

            // 2. Registry Uninstall Entries
            try
            {
                ScanRegistryUninstall(TryAddProfile);
            }
            catch { }

            // 3. Registry App Paths
            try
            {
                ScanRegistryAppPaths(TryAddProfile);
            }
            catch { }

            // 4. Start Menu Shortcuts (.lnk)
            try
            {
                ScanStartMenuShortcuts(TryAddProfile);
            }
            catch { }

            return discovered;
        }

        private void ScanRegistryUninstall(Action<AppProfile> onAppFound)
        {
            string[] uninstallSubKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                foreach (var subKeyPath in uninstallSubKeys)
                {
                    try
                    {
                        using var key = root.OpenSubKey(subKeyPath);
                        if (key == null) continue;

                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using var appKey = key.OpenSubKey(subKeyName);
                                if (appKey == null) continue;

                                // Ignore system components or sub-updates
                                var sysComp = appKey.GetValue("SystemComponent");
                                if (sysComp is int sysInt && sysInt == 1) continue;
                                if (appKey.GetValue("ParentKeyName") != null) continue;

                                string? displayName = appKey.GetValue("DisplayName") as string;
                                if (string.IsNullOrWhiteSpace(displayName)) continue;

                                string? candidateExe = null;

                                // Try DisplayIcon
                                string? displayIcon = appKey.GetValue("DisplayIcon") as string;
                                if (!string.IsNullOrWhiteSpace(displayIcon))
                                {
                                    string cleanIcon = displayIcon.Split(',')[0].Trim('\"').Trim();
                                    if (cleanIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && _fileExists(cleanIcon))
                                    {
                                        candidateExe = cleanIcon;
                                    }
                                }

                                // Try InstallLocation
                                if (string.IsNullOrEmpty(candidateExe))
                                {
                                    string? installLocation = appKey.GetValue("InstallLocation") as string;
                                    if (!string.IsNullOrWhiteSpace(installLocation))
                                    {
                                        installLocation = installLocation.Trim('\"').Trim();
                                        if (_directoryExists(installLocation))
                                        {
                                            candidateExe = FindMainExecutableInFolder(installLocation, displayName);
                                        }
                                    }
                                }

                                if (!string.IsNullOrEmpty(candidateExe) && _fileExists(candidateExe))
                                {
                                    string appId = Path.GetFileName(candidateExe);
                                    var profile = CreateDefaultProfile(appId, displayName, candidateExe, "Applications");
                                    onAppFound(profile);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
        }

        private void ScanRegistryAppPaths(Action<AppProfile> onAppFound)
        {
            string[] subKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
            };

            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                foreach (var subKey in subKeys)
                {
                    try
                    {
                        using var key = root.OpenSubKey(subKey);
                        if (key == null) continue;

                        foreach (var appName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using var appKey = key.OpenSubKey(appName);
                                if (appKey == null) continue;

                                var rawVal = appKey.GetValue(null) as string;
                                if (string.IsNullOrWhiteSpace(rawVal)) continue;

                                string cleanPath = rawVal.Trim('\"').Trim();
                                if (cleanPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && _fileExists(cleanPath))
                                {
                                    string name = Path.GetFileNameWithoutExtension(cleanPath);
                                    if (!string.IsNullOrWhiteSpace(name))
                                    {
                                        name = char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "");
                                    }
                                    var profile = CreateDefaultProfile(appName, name, cleanPath, "Applications");
                                    onAppFound(profile);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
        }

        private void ScanStartMenuShortcuts(Action<AppProfile> onAppFound)
        {
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string prog = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                if (_directoryExists(prog)) directories.Add(prog);
                string userStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                if (_directoryExists(userStart)) directories.Add(userStart);
            }
            catch { }

            try
            {
                string commonProg = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                if (_directoryExists(commonProg)) directories.Add(commonProg);
                string commonStart = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
                if (_directoryExists(commonStart)) directories.Add(commonStart);
            }
            catch { }

            foreach (var dir in directories)
            {
                try
                {
                    var lnkFiles = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                    foreach (var lnk in lnkFiles)
                    {
                        try
                        {
                            string? target = _shortcutResolver(lnk);
                            if (!string.IsNullOrWhiteSpace(target) &&
                                target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                _fileExists(target))
                            {
                                string friendlyName = Path.GetFileNameWithoutExtension(lnk);
                                string appId = Path.GetFileName(target);
                                var profile = CreateDefaultProfile(appId, friendlyName, target, "Applications");
                                onAppFound(profile);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private string? FindMainExecutableInFolder(string folder, string appName)
        {
            try
            {
                var exeFiles = Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly);
                if (exeFiles.Length == 0) return null;

                // Priority 1: Exact or clean match with app name
                string cleanApp = CleanIdentityString(appName);
                foreach (var exe in exeFiles)
                {
                    string nameNoExt = Path.GetFileNameWithoutExtension(exe);
                    if (nameNoExt.Equals(cleanApp, StringComparison.OrdinalIgnoreCase) ||
                        cleanApp.IndexOf(nameNoExt, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!IsDisallowedExecutable(Path.GetFileName(exe), exe))
                            return exe;
                    }
                }

                // Priority 2: First non-installer exe
                foreach (var exe in exeFiles)
                {
                    if (!IsDisallowedExecutable(Path.GetFileName(exe), exe))
                        return exe;
                }
            }
            catch { }
            return null;
        }

        public static bool IsDisallowedExecutable(string exeName, string fullPath)
        {
            string lower = exeName.ToLowerInvariant();
            if (lower.Contains("unins") ||
                lower.Contains("uninstall") ||
                lower.EndsWith("setup.exe") ||
                lower.Contains("installer") ||
                lower.Contains("crashpad") ||
                lower.Contains("elevate") ||
                lower.Contains("helper") ||
                lower.Contains("updater") ||
                lower.Contains("reporter") ||
                lower == "cmd.exe" ||
                lower == "powershell.exe" ||
                lower == "pwsh.exe" ||
                lower == "conhost.exe" ||
                lower == "rundll32.exe" ||
                lower == "svchost.exe" ||
                lower == "dllhost.exe")
            {
                return true;
            }

            string pathLower = fullPath.ToLowerInvariant();
            if (pathLower.Contains(@"\windows\system32\") ||
                pathLower.Contains(@"\windows\syswow64\") ||
                pathLower.Contains(@"\windows\winsxs\"))
            {
                // Allow user-facing utilities like notepad, calc, paint, etc.
                if (lower != "notepad.exe" &&
                    lower != "calc.exe" &&
                    lower != "mspaint.exe" &&
                    lower != "write.exe" &&
                    lower != "snippingtool.exe" &&
                    lower != "regedit.exe")
                {
                    return true;
                }
            }

            return false;
        }

        private static AppProfile CreateDefaultProfile(string appId, string name, string path, string category)
        {
            // Pick a stylish brand-like accent color based on app name hash
            string[] palette = new[] { "#3B82F6", "#10B981", "#8B5CF6", "#F59E0B", "#EC4899", "#06B6D4", "#6366F1" };
            int hash = Math.Abs(name.GetHashCode());
            string color = palette[hash % palette.Length];

            return new AppProfile
            {
                AppId = appId,
                Name = name,
                ExecutablePath = path,
                Enabled = true,
                ColorHex = color,
                DurationMs = 4000,
                Intensity = 0.80,
                Thickness = 4.0,
                GlowSize = 30.0,
                Style = GlowStyle.Pulse,
                Category = category,
                IsCustom = false
            };
        }

        public static string CleanIdentityString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string s = input.Trim();
            if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 4);
            }
            return s.Replace(" ", "").Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
        }

        private static string? DefaultShortcutResolver(string lnkPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    string target = shortcut.TargetPath;
                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        return Environment.ExpandEnvironmentVariables(target.Trim('\"'));
                    }
                }
            }
            catch { }

            // Binary fallback: scan shortcut bytes for Drive:\...exe path
            try
            {
                byte[] bytes = File.ReadAllBytes(lnkPath);
                string text = System.Text.Encoding.Default.GetString(bytes);
                int exeIdx = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeIdx > 3)
                {
                    int colonIdx = text.LastIndexOf(":\\", exeIdx, StringComparison.OrdinalIgnoreCase);
                    if (colonIdx > 0)
                    {
                        int driveIdx = colonIdx - 1;
                        char drive = text[driveIdx];
                        if (char.IsLetter(drive))
                        {
                            string candidate = text.Substring(driveIdx, (exeIdx + 4) - driveIdx);
                            if (File.Exists(candidate))
                            {
                                return candidate;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
