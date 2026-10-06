using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class SteamGameDetector
    {
        private readonly Func<string?> _getSteamRoot;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string> _readAllText;
        private readonly Func<string, string, string[]> _getFiles;
        private readonly Func<string, string[]> _getDirectories;

        private static readonly HashSet<string> IgnoredAppIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "228980",  // Steamworks Common Redistributables
            "250820",  // SteamVR
            "1070560", // Steam Linux Runtime
            "1391110", // Steam Linux Runtime - Soldier
            "1628350"  // Steam Linux Runtime - Sniper
        };

        private static readonly HashSet<string> IgnoredExePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "unins", "setup", "install", "crashpad", "unitycrashhandler", "dxsetup", "vcredist", "easyanticheat", "epicgameslauncher", "battleye"
        };

        public SteamGameDetector(
            Func<string?>? getSteamRoot = null,
            Func<string, bool>? directoryExists = null,
            Func<string, bool>? fileExists = null,
            Func<string, string>? readAllText = null,
            Func<string, string, string[]>? getFiles = null,
            Func<string, string[]>? getDirectories = null)
        {
            _getSteamRoot = getSteamRoot ?? DefaultGetSteamRoot;
            _directoryExists = directoryExists ?? (d => !string.IsNullOrEmpty(d) && Directory.Exists(d));
            _fileExists = fileExists ?? (f => !string.IsNullOrEmpty(f) && File.Exists(f));
            _readAllText = readAllText ?? File.ReadAllText;
            _getFiles = getFiles ?? ((d, p) => Directory.Exists(d) ? Directory.GetFiles(d, p) : Array.Empty<string>());
            _getDirectories = getDirectories ?? (d => Directory.Exists(d) ? Directory.GetDirectories(d) : Array.Empty<string>());
        }

        public IReadOnlyList<DetectedGameInfo> DetectGames()
        {
            var detectedGames = new List<DetectedGameInfo>();

            try
            {
                string? steamRoot = _getSteamRoot();
                if (string.IsNullOrWhiteSpace(steamRoot) || !_directoryExists(steamRoot))
                {
                    return detectedGames;
                }

                var libraryFolders = FindLibraryFolders(steamRoot);

                foreach (var library in libraryFolders)
                {
                    try
                    {
                        string steamappsDir = Path.Combine(library, "steamapps");
                        if (!_directoryExists(steamappsDir)) continue;

                        var manifestFiles = _getFiles(steamappsDir, "appmanifest_*.acf");
                        foreach (var manifestPath in manifestFiles)
                        {
                            try
                            {
                                var game = ParseAppManifest(manifestPath, steamappsDir);
                                if (game != null && !string.IsNullOrEmpty(game.ExecutablePath))
                                {
                                    detectedGames.Add(game);
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggerService.LogWarning($"Failed to parse Steam manifest {manifestPath}: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogWarning($"Failed scanning Steam library {library}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error during Steam game detection", ex);
            }

            return detectedGames;
        }

        public List<string> FindLibraryFolders(string steamRoot)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };

            string vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (_fileExists(vdfPath))
            {
                try
                {
                    string content = _readAllText(vdfPath);

                    // Parse "path" "D:\\SteamLibrary"
                    var matches = Regex.Matches(content, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        string path = NormalizePath(m.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(path) && _directoryExists(path))
                        {
                            libraries.Add(path);
                        }
                    }

                    // Legacy format: "1" "D:\\SteamLibrary"
                    var legacyMatches = Regex.Matches(content, "\"\\d+\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    foreach (Match m in legacyMatches)
                    {
                        string path = NormalizePath(m.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(path) && _directoryExists(path))
                        {
                            libraries.Add(path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"Failed reading libraryfolders.vdf: {ex.Message}");
                }
            }

            return libraries.ToList();
        }

        public DetectedGameInfo? ParseAppManifest(string manifestPath, string steamappsDir)
        {
            string content = _readAllText(manifestPath);

            string? appId = ExtractVdfValue(content, "appid");
            string? name = ExtractVdfValue(content, "name");
            string? installDir = ExtractVdfValue(content, "installdir");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(installDir))
                return null;

            if (!string.IsNullOrEmpty(appId) && IgnoredAppIds.Contains(appId))
                return null;

            if (name.IndexOf("Steamworks", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Proton", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Redistributable", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            string gameCommonDir = Path.Combine(steamappsDir, "common", installDir);
            if (!_directoryExists(gameCommonDir))
                return null;

            string? mainExe = FindBestGameExecutable(gameCommonDir, installDir, name);
            if (string.IsNullOrEmpty(mainExe))
                return null;

            return new DetectedGameInfo
            {
                Name = name.Trim(),
                ExecutablePath = Path.GetFullPath(mainExe),
                Launcher = "Steam",
                AppId = appId
            };
        }

        public string? FindBestGameExecutable(string gameDir, string installDir, string gameName)
        {
            var candidates = new List<string>();

            // Root directory search
            try
            {
                candidates.AddRange(_getFiles(gameDir, "*.exe"));
            }
            catch { }

            // Subdirectories (depth 1 and 2, e.g. bin/win64 or game/bin/win64)
            try
            {
                foreach (var sub in _getDirectories(gameDir))
                {
                    candidates.AddRange(_getFiles(sub, "*.exe"));
                    foreach (var sub2 in _getDirectories(sub))
                    {
                        candidates.AddRange(_getFiles(sub2, "*.exe"));
                    }
                }
            }
            catch { }

            // Filter out installers, uninstalls, crash handlers
            var validCandidates = candidates.Where(c =>
            {
                string fn = Path.GetFileNameWithoutExtension(c).ToLowerInvariant();
                return !IgnoredExePrefixes.Any(prefix => fn.StartsWith(prefix));
            }).ToList();

            if (validCandidates.Count == 0) return null;

            // Priority 1: Exe name matches installDir or sanitized gameName
            string cleanInstall = Regex.Replace(installDir, @"[^a-zA-Z0-9]", "").ToLowerInvariant();
            string cleanGameName = Regex.Replace(gameName, @"[^a-zA-Z0-9]", "").ToLowerInvariant();

            foreach (var cand in validCandidates)
            {
                string fn = Path.GetFileNameWithoutExtension(cand);
                string cleanFn = Regex.Replace(fn, @"[^a-zA-Z0-9]", "").ToLowerInvariant();

                if (cleanFn.Equals(cleanInstall, StringComparison.OrdinalIgnoreCase) ||
                    cleanFn.Equals(cleanGameName, StringComparison.OrdinalIgnoreCase))
                {
                    return cand;
                }
            }

            // Priority 2: Exe contains installDir or gameName
            foreach (var cand in validCandidates)
            {
                string fn = Path.GetFileNameWithoutExtension(cand).ToLowerInvariant();
                if (fn.Contains(cleanInstall) || (!string.IsNullOrEmpty(cleanGameName) && fn.Contains(cleanGameName)))
                {
                    return cand;
                }
            }

            // Priority 3: Largest file or first root candidate
            var rootCandidate = validCandidates.FirstOrDefault(c => Path.GetDirectoryName(c)?.Equals(gameDir, StringComparison.OrdinalIgnoreCase) == true);
            if (rootCandidate != null) return rootCandidate;

            return validCandidates.First();
        }

        private static string? ExtractVdfValue(string content, string key)
        {
            var match = Regex.Match(content, $"\"{Regex.Escape(key)}\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string NormalizePath(string raw)
        {
            return raw.Replace(@"\\", @"\").Replace('/', '\\').Trim();
        }

        private static string? DefaultGetSteamRoot()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var steamPath = key.GetValue("SteamPath") as string;
                    if (!string.IsNullOrWhiteSpace(steamPath))
                    {
                        string normalized = NormalizePath(steamPath);
                        if (Directory.Exists(normalized)) return normalized;
                    }
                }
            }
            catch { }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Valve\Steam");
                if (key != null)
                {
                    var installPath = key.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(installPath))
                    {
                        string normalized = NormalizePath(installPath);
                        if (Directory.Exists(normalized)) return normalized;
                    }
                }
            }
            catch { }

            string p86 = @"C:\Program Files (x86)\Steam";
            if (Directory.Exists(p86)) return p86;

            string p64 = @"C:\Program Files\Steam";
            if (Directory.Exists(p64)) return p64;

            return null;
        }
    }
}
