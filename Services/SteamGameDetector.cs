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
            "unins", "setup", "install", "crashpad", "crashreport", "crashhandler", "unitycrashhandler",
            "dxsetup", "vcredist", "easyanticheat", "eaanticheat", "epicgameslauncher", "battleye",
            "bugreport", "steamerrorreporter", "bspzip", "vbsp", "vrad", "vvis", "vpk", "vtex",
            "studiomdl", "shadercompile", "hlmv", "hammer"
        };

        private static readonly Regex LibraryPathRegex = new("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LegacyLibraryPathRegex = new("\"\\d+\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NonAlphaNumRegex = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);
        private static readonly Regex AppIdVdfRegex = new("\"appid\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NameVdfRegex = new("\"name\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InstallDirVdfRegex = new("\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
                    var matches = LibraryPathRegex.Matches(content);
                    foreach (Match m in matches)
                    {
                        string path = NormalizePath(m.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(path) && _directoryExists(path))
                        {
                            libraries.Add(path);
                        }
                    }

                    // Legacy format: "1" "D:\\SteamLibrary"
                    var legacyMatches = LegacyLibraryPathRegex.Matches(content);
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
            if (string.IsNullOrEmpty(mainExe) || !_fileExists(mainExe))
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
            string cleanInstall = NonAlphaNumRegex.Replace(installDir, "").ToLowerInvariant();
            string cleanGameName = NonAlphaNumRegex.Replace(gameName, "").ToLowerInvariant();

            foreach (var cand in validCandidates)
            {
                string fn = Path.GetFileNameWithoutExtension(cand);
                string cleanFn = NonAlphaNumRegex.Replace(fn, "").ToLowerInvariant();

                if ((!string.IsNullOrEmpty(cleanInstall) && cleanFn.Equals(cleanInstall, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(cleanGameName) && cleanFn.Equals(cleanGameName, StringComparison.OrdinalIgnoreCase)))
                {
                    return cand;
                }
            }

            // Priority 2: Exe contains installDir or gameName (guard against "portal" matching "portal2")
            foreach (var cand in validCandidates)
            {
                string fn = Path.GetFileNameWithoutExtension(cand).ToLowerInvariant();
                string cleanFn = NonAlphaNumRegex.Replace(fn, "").ToLowerInvariant();

                if (!string.IsNullOrEmpty(cleanInstall) && cleanFn.Contains(cleanInstall))
                {
                    if (!HasMismatchingTrailingNumber(cleanFn, cleanInstall))
                    {
                        return cand;
                    }
                }
                if (!string.IsNullOrEmpty(cleanGameName) && cleanFn.Contains(cleanGameName))
                {
                    if (!HasMismatchingTrailingNumber(cleanFn, cleanGameName))
                    {
                        return cand;
                    }
                }
            }

            // Priority 3: Root candidates (prefer known engine main executables like hl2.exe for Source Engine games, then largest file size)
            var rootCandidates = validCandidates
                .Where(c => Path.GetDirectoryName(c)?.Equals(gameDir, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            if (rootCandidates.Count > 0)
            {
                var sourceEngineExe = rootCandidates.FirstOrDefault(c =>
                    Path.GetFileName(c).Equals("hl2.exe", StringComparison.OrdinalIgnoreCase));
                if (sourceEngineExe != null && rootCandidates.Count == 1)
                {
                    return sourceEngineExe;
                }

                return rootCandidates.OrderByDescending(c =>
                {
                    try { return new FileInfo(c).Length; }
                    catch { return 0; }
                }).First();
            }

            return validCandidates.OrderByDescending(c =>
            {
                try { return new FileInfo(c).Length; }
                catch { return 0; }
            }).First();
        }

        private static bool HasMismatchingTrailingNumber(string candidateClean, string targetClean)
        {
            if (candidateClean.Length <= targetClean.Length) return false;
            int idx = candidateClean.IndexOf(targetClean, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            int afterIdx = idx + targetClean.Length;
            if (afterIdx < candidateClean.Length && char.IsDigit(candidateClean[afterIdx]))
            {
                // E.g. target is "portal" and candidate is "portal2" -> mismatching numbered sequel
                return true;
            }
            return false;
        }

        private static string? ExtractVdfValue(string content, string key)
        {
            Regex regex = key switch
            {
                "appid" => AppIdVdfRegex,
                "name" => NameVdfRegex,
                "installdir" => InstallDirVdfRegex,
                _ => new Regex($"\"{Regex.Escape(key)}\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)
            };
            var match = regex.Match(content);
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
