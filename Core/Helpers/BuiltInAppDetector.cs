using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NotiGlow.Models;

namespace NotiGlow.Core.Helpers
{
    public record BuiltInAppDefinition(
        string AppId,
        string Name,
        string Category,
        string ColorHex,
        int DurationMs,
        double Intensity,
        GlowStyle Style,
        IReadOnlyList<string> CandidateRelativePaths,
        IReadOnlyList<string> RegistryAppPathKeys,
        Func<Func<string, bool>, string?>? CustomResolver = null
    );

    public class BuiltInAppDetector
    {
        private static BuiltInAppDetector? _defaultInstance;
        public static BuiltInAppDetector Default => _defaultInstance ??= new BuiltInAppDetector();

        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, string, string?> _wildcardLookup;
        private readonly Func<string, string?> _registryAppPathLookup;

        public static readonly IReadOnlyList<BuiltInAppDefinition> Definitions = new List<BuiltInAppDefinition>
        {
            // 1. Claude
            new BuiltInAppDefinition(
                AppId: "Claude",
                Name: "Claude",
                Category: "AI Assistants",
                ColorHex: "#D97757",
                DurationMs: 4000,
                Intensity: 0.80,
                Style: GlowStyle.Pulse,
                CandidateRelativePaths: new[]
                {
                    @"%LOCALAPPDATA%\Programs\Claude\Claude.exe",
                    @"%APPDATA%\Claude\Claude.exe",
                    @"%ProgramFiles%\Claude\Claude.exe"
                },
                RegistryAppPathKeys: new[] { "Claude.exe" }
            ),

            // 2. ChatGPT
            new BuiltInAppDefinition(
                AppId: "OpenAI.ChatGPT",
                Name: "ChatGPT",
                Category: "AI Assistants",
                ColorHex: "#10A37F",
                DurationMs: 4000,
                Intensity: 0.80,
                Style: GlowStyle.Sweep,
                CandidateRelativePaths: new[]
                {
                    @"%LOCALAPPDATA%\Programs\ChatGPT\ChatGPT.exe",
                    @"%LOCALAPPDATA%\OpenAI\ChatGPT\ChatGPT.exe",
                    @"%LOCALAPPDATA%\Microsoft\WindowsApps\ChatGPT.exe",
                    @"%ProgramFiles%\ChatGPT\ChatGPT.exe"
                },
                RegistryAppPathKeys: new[] { "ChatGPT.exe" }
            ),

            // 3. Microsoft Copilot
            new BuiltInAppDefinition(
                AppId: "Microsoft.Copilot",
                Name: "Microsoft Copilot",
                Category: "AI Assistants",
                ColorHex: "#0F6CBD",
                DurationMs: 4000,
                Intensity: 0.80,
                Style: GlowStyle.Comet,
                CandidateRelativePaths: new[]
                {
                    @"%LOCALAPPDATA%\Microsoft\Copilot\Copilot.exe",
                    @"%LOCALAPPDATA%\Programs\Microsoft\Copilot\Copilot.exe",
                    @"%LOCALAPPDATA%\Microsoft\WindowsApps\Copilot.exe"
                },
                RegistryAppPathKeys: new[] { "Copilot.exe" }
            ),

            // 4. Google Gemini
            new BuiltInAppDefinition(
                AppId: "Google.Gemini",
                Name: "Google Gemini",
                Category: "AI Assistants",
                ColorHex: "#4E88F5",
                DurationMs: 4000,
                Intensity: 0.80,
                Style: GlowStyle.Ambient,
                CandidateRelativePaths: new[]
                {
                    @"%LOCALAPPDATA%\Programs\Gemini\Gemini.exe",
                    @"%APPDATA%\Gemini\Gemini.exe"
                },
                RegistryAppPathKeys: new[] { "Gemini.exe" }
            ),

            // 5. Discord
            new BuiltInAppDefinition(
                AppId: "Discord",
                Name: "Discord",
                Category: "Messaging",
                ColorHex: "#5865F2",
                DurationMs: 4000,
                Intensity: 0.80,
                Style: GlowStyle.Pulse,
                CandidateRelativePaths: new[]
                {
                    @"%LOCALAPPDATA%\Discord\app-*\Discord.exe",
                    @"%LOCALAPPDATA%\Discord\Discord.exe",
                    @"%ProgramFiles%\Discord\Discord.exe"
                },
                RegistryAppPathKeys: new[] { "Discord.exe" },
                CustomResolver: fileExists =>
                {
                    try
                    {
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string discordDir = Path.Combine(localAppData, "Discord");
                        if (Directory.Exists(discordDir))
                        {
                            var appDirs = Directory.GetDirectories(discordDir, "app-*")
                                .OrderByDescending(d => d);
                            foreach (var dir in appDirs)
                            {
                                string exe = Path.Combine(dir, "Discord.exe");
                                if (fileExists(exe)) return exe;
                            }
                        }
                    }
                    catch { }
                    return null;
                }
            ),

            // 6. WhatsApp
            new BuiltInAppDefinition(
                AppId: "WhatsApp",
                Name: "WhatsApp",
                Category: "Messaging",
                ColorHex: "#25D366",
                DurationMs: 3000,
                Intensity: 0.75,
                Style: GlowStyle.Ambient,
                CandidateRelativePaths: new[]
                {
                    @"%LOCALAPPDATA%\WhatsApp\WhatsApp.exe",
                    @"%LOCALAPPDATA%\Microsoft\WindowsApps\WhatsApp.exe",
                    @"%ProgramFiles%\WindowsApps\5319275A.WhatsAppDesktop_*\WhatsApp.exe"
                },
                RegistryAppPathKeys: new[] { "WhatsApp.exe" }
            ),

            // 7. Telegram
            new BuiltInAppDefinition(
                AppId: "Telegram",
                Name: "Telegram",
                Category: "Messaging",
                ColorHex: "#24A1DE",
                DurationMs: 4000,
                Intensity: 0.75,
                Style: GlowStyle.Sweep,
                CandidateRelativePaths: new[]
                {
                    @"%APPDATA%\Telegram Desktop\Telegram.exe",
                    @"%LOCALAPPDATA%\Programs\Telegram Desktop\Telegram.exe",
                    @"%ProgramFiles%\Telegram Desktop\Telegram.exe",
                    @"%ProgramFiles(x86)%\Telegram Desktop\Telegram.exe"
                },
                RegistryAppPathKeys: new[] { "Telegram.exe" }
            ),

            // 8. Microsoft Teams
            new BuiltInAppDefinition(
                AppId: "MSTeams",
                Name: "Microsoft Teams",
                Category: "Messaging",
                ColorHex: "#6264A7",
                DurationMs: 4000,
                Intensity: 0.70,
                Style: GlowStyle.Pulse,
                CandidateRelativePaths: new[]
                {
                    @"%LOCALAPPDATA%\Microsoft\Teams\current\Teams.exe",
                    @"%LOCALAPPDATA%\Microsoft\WindowsApps\ms-teams.exe",
                    @"%ProgramFiles%\WindowsApps\MSTeams_*\ms-teams.exe"
                },
                RegistryAppPathKeys: new[] { "ms-teams.exe", "Teams.exe" }
            ),

            // 9. Steam
            new BuiltInAppDefinition(
                AppId: "Steam",
                Name: "Steam",
                Category: "Gaming",
                ColorHex: "#66C0F4",
                DurationMs: 5000,
                Intensity: 0.70,
                Style: GlowStyle.Pulse,
                CandidateRelativePaths: new[]
                {
                    @"%ProgramFiles(x86)%\Steam\steam.exe",
                    @"%ProgramFiles%\Steam\steam.exe"
                },
                RegistryAppPathKeys: new[] { "steam.exe" },
                CustomResolver: fileExists =>
                {
                    // Check HKCU\Software\Valve\Steam or HKLM\Software\Valve\Steam
                    try
                    {
                        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                        if (key != null)
                        {
                            var steamExe = key.GetValue("SteamExe") as string;
                            if (!string.IsNullOrWhiteSpace(steamExe) && fileExists(steamExe))
                                return steamExe;

                            var steamPath = key.GetValue("SteamPath") as string;
                            if (!string.IsNullOrWhiteSpace(steamPath))
                            {
                                string candidate = Path.Combine(steamPath, "steam.exe");
                                if (fileExists(candidate)) return candidate;
                            }
                        }
                    }
                    catch { }
                    return null;
                }
            ),

            // 10. Spotify
            new BuiltInAppDefinition(
                AppId: "Spotify",
                Name: "Spotify",
                Category: "Media",
                ColorHex: "#1DB954",
                DurationMs: 3000,
                Intensity: 0.70,
                Style: GlowStyle.Pulse,
                CandidateRelativePaths: new[]
                {
                    @"%APPDATA%\Spotify\Spotify.exe",
                    @"%LOCALAPPDATA%\Microsoft\WindowsApps\Spotify.exe"
                },
                RegistryAppPathKeys: new[] { "Spotify.exe" }
            )
        };

        public BuiltInAppDetector(
            Func<string, bool>? fileExists = null,
            Func<string, bool>? directoryExists = null,
            Func<string, string, string?>? wildcardLookup = null,
            Func<string, string?>? registryAppPathLookup = null)
        {
            _fileExists = fileExists ?? (p => !string.IsNullOrEmpty(p) && File.Exists(p));
            _directoryExists = directoryExists ?? (p => !string.IsNullOrEmpty(p) && Directory.Exists(p));
            _wildcardLookup = wildcardLookup ?? DefaultWildcardLookup;
            _registryAppPathLookup = registryAppPathLookup ?? DefaultRegistryAppPathLookup;
        }

        public bool TryDetectApp(BuiltInAppDefinition definition, out string detectedPath)
        {
            detectedPath = string.Empty;

            // 1. Custom Resolver
            if (definition.CustomResolver != null)
            {
                try
                {
                    string? resolved = definition.CustomResolver(_fileExists);
                    if (!string.IsNullOrEmpty(resolved) && _fileExists(resolved))
                    {
                        detectedPath = Path.GetFullPath(resolved);
                        return true;
                    }
                }
                catch { }
            }

            // 2. Registry App Paths
            foreach (var appPathKey in definition.RegistryAppPathKeys)
            {
                try
                {
                    string? regPath = _registryAppPathLookup(appPathKey);
                    if (!string.IsNullOrEmpty(regPath) && _fileExists(regPath))
                    {
                        detectedPath = Path.GetFullPath(regPath);
                        return true;
                    }
                }
                catch { }
            }

            // 3. Candidate Paths
            foreach (var rawPath in definition.CandidateRelativePaths)
            {
                try
                {
                    string expanded = Environment.ExpandEnvironmentVariables(rawPath);
                    if (expanded.Contains('*'))
                    {
                        string? match = ResolveWildcardPath(expanded);
                        if (!string.IsNullOrEmpty(match) && _fileExists(match))
                        {
                            detectedPath = Path.GetFullPath(match);
                            return true;
                        }
                    }
                    else if (_fileExists(expanded))
                    {
                        detectedPath = Path.GetFullPath(expanded);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        public IReadOnlyList<AppProfile> GetInstalledProfiles()
        {
            var results = new List<AppProfile>();

            foreach (var def in Definitions)
            {
                if (TryDetectApp(def, out string path))
                {
                    results.Add(new AppProfile
                    {
                        AppId = def.AppId,
                        Name = def.Name,
                        ExecutablePath = path,
                        Enabled = true,
                        ColorHex = def.ColorHex,
                        DurationMs = def.DurationMs,
                        Intensity = def.Intensity,
                        Thickness = 4.0,
                        GlowSize = 30.0,
                        Style = def.Style,
                        Category = def.Category
                    });
                }
            }

            return results;
        }

        private string? ResolveWildcardPath(string patternPath)
        {
            int starIndex = patternPath.IndexOf('*');
            if (starIndex < 0) return null;

            int lastSlashBeforeStar = patternPath.LastIndexOfAny(new[] { '\\', '/' }, starIndex);
            if (lastSlashBeforeStar < 0) return null;

            string parentDir = patternPath.Substring(0, lastSlashBeforeStar);
            string remainder = patternPath.Substring(lastSlashBeforeStar + 1);

            return _wildcardLookup(parentDir, remainder);
        }

        private static string? DefaultWildcardLookup(string parentDir, string remainder)
        {
            try
            {
                if (!Directory.Exists(parentDir)) return null;

                int slashIndex = remainder.IndexOfAny(new[] { '\\', '/' });
                if (slashIndex > 0)
                {
                    string dirPattern = remainder.Substring(0, slashIndex);
                    string fileSubpath = remainder.Substring(slashIndex + 1);

                    var matchingDirs = Directory.GetDirectories(parentDir, dirPattern)
                        .OrderByDescending(d => d);

                    foreach (var dir in matchingDirs)
                    {
                        string candidate = Path.Combine(dir, fileSubpath);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }
            return null;
        }

        private static string? DefaultRegistryAppPathLookup(string exeName)
        {
            string[] subKeys = new[]
            {
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\{exeName}"
            };

            foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                foreach (var subKey in subKeys)
                {
                    try
                    {
                        using var key = root.OpenSubKey(subKey);
                        if (key != null)
                        {
                            var val = key.GetValue(null) as string;
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                val = val.Trim('\"');
                                if (File.Exists(val)) return val;
                            }
                        }
                    }
                    catch { }
                }
            }

            return null;
        }
    }
}
