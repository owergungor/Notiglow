using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class XboxAndStoreGameDetector
    {
        private readonly Func<IEnumerable<string>> _getXboxLibraryDrives;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string> _readAllText;
        private readonly Func<string, string, string[]> _getFiles;
        private readonly Func<string, string[]> _getDirectories;
        private readonly Func<IReadOnlyList<DetectedGameInfo>>? _winRtPackageScanner;
        private readonly Func<IReadOnlyList<DetectedGameInfo>>? _registryScanner;

        private static readonly HashSet<string> ExcludedPackagePrefixes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft.WindowsStore",
            "Microsoft.GamingApp",
            "Microsoft.XboxGamingOverlay",
            "Microsoft.XboxIdentityProvider",
            "Microsoft.XboxSpeechToTextOverlay",
            "Microsoft.XboxTCUI",
            "Microsoft.XboxGameOverlay",
            "Microsoft.WindowsTerminal",
            "Microsoft.GetHelp",
            "Microsoft.Getstarted",
            "Microsoft.ScreenSketch",
            "Microsoft.Paint",
            "Microsoft.WindowsCalculator",
            "Microsoft.WindowsCamera",
            "Microsoft.WindowsAlarms",
            "Microsoft.YourPhone",
            "Microsoft.ZuneVideo",
            "Microsoft.ZuneMusic",
            "Microsoft.People",
            "Microsoft.MicrosoftOfficeHub",
            "Microsoft.MicrosoftSolitaireCollection",
            "crius.Notiglow"
        };

        private static readonly HashSet<string> IgnoredExeNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "unins000.exe", "uninstall.exe", "setup.exe", "installer.exe", "crashpad_handler.exe",
            "crashreport.exe", "crashhandler.exe", "unitycrashhandler.exe", "unitycrashhandler64.exe",
            "dxsetup.exe", "vcredist_x64.exe", "vcredist_x86.exe", "easyanticheat.exe", "battleye.exe",
            "gamelaunchhelper.exe", "gamelauncher.exe"
        };

        public XboxAndStoreGameDetector(
            Func<IEnumerable<string>>? getXboxLibraryDrives = null,
            Func<string, bool>? directoryExists = null,
            Func<string, bool>? fileExists = null,
            Func<string, string>? readAllText = null,
            Func<string, string, string[]>? getFiles = null,
            Func<string, string[]>? getDirectories = null,
            Func<IReadOnlyList<DetectedGameInfo>>? winRtPackageScanner = null,
            Func<IReadOnlyList<DetectedGameInfo>>? registryScanner = null)
        {
            _getXboxLibraryDrives = getXboxLibraryDrives ?? DefaultGetXboxLibraryDrives;
            _directoryExists = directoryExists ?? (d => !string.IsNullOrEmpty(d) && Directory.Exists(d));
            _fileExists = fileExists ?? (f => !string.IsNullOrEmpty(f) && File.Exists(f));
            _readAllText = readAllText ?? File.ReadAllText;
            _getFiles = getFiles ?? ((d, p) => Directory.Exists(d) ? Directory.GetFiles(d, p) : Array.Empty<string>());
            _getDirectories = getDirectories ?? (d => Directory.Exists(d) ? Directory.GetDirectories(d) : Array.Empty<string>());
            _winRtPackageScanner = winRtPackageScanner;
            _registryScanner = registryScanner;
        }

        public static XboxAndStoreGameDetector CreateEmpty() =>
            new XboxAndStoreGameDetector(
                getXboxLibraryDrives: () => Array.Empty<string>(),
                directoryExists: _ => false,
                fileExists: _ => false,
                winRtPackageScanner: () => Array.Empty<DetectedGameInfo>(),
                registryScanner: () => Array.Empty<DetectedGameInfo>());

        public IReadOnlyList<DetectedGameInfo> DetectGames()
        {
            var detected = new List<DetectedGameInfo>();
            var seenExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(DetectedGameInfo game)
            {
                if (game == null || string.IsNullOrWhiteSpace(game.ExecutablePath)) return;

                string exeName = Path.GetFileName(game.ExecutablePath).ToLowerInvariant();
                string fullPath = game.ExecutablePath.Trim();
                try
                {
                    if (Path.IsPathRooted(fullPath))
                    {
                        fullPath = Path.GetFullPath(fullPath);
                    }
                }
                catch { }

                string pathKey = fullPath.ToLowerInvariant();
                string nameKey = (game.Name ?? string.Empty).Trim().ToLowerInvariant();

                if (IgnoredExeNames.Contains(exeName)) return;

                if (!seenExes.Contains(exeName) && !seenPaths.Contains(pathKey) && (string.IsNullOrEmpty(nameKey) || !seenNames.Contains(nameKey)))
                {
                    seenExes.Add(exeName);
                    seenPaths.Add(pathKey);
                    if (!string.IsNullOrEmpty(nameKey)) seenNames.Add(nameKey);
                    detected.Add(game);
                }
            }

            // 1. Scan Xbox PC Game Install Folders (e.g. C:\XboxGames\...)
            try
            {
                var xboxGames = ScanXboxLibraryFolders();
                foreach (var g in xboxGames)
                {
                    TryAdd(g);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Xbox folder game detection warning: {ex.Message}");
            }

            // 2. Scan Windows Native Gaming Registry (HKCU\System\GameConfigStore\Children)
            try
            {
                var registryGames = _registryScanner != null ? _registryScanner() : ScanGameConfigStoreRegistry();
                foreach (var g in registryGames)
                {
                    TryAdd(g);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"GameConfigStore registry scan warning: {ex.Message}");
            }

            // 3. Scan WinRT / AppX Package Manager (Packaged Store & Game Pass Games)
            try
            {
                var packageGames = ScanPackageStoreGames();
                foreach (var g in packageGames)
                {
                    TryAdd(g);
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Package store game scan warning: {ex.Message}");
            }

            return detected;
        }

        public IReadOnlyList<DetectedGameInfo> ScanXboxLibraryFolders()
        {
            var results = new List<DetectedGameInfo>();
            var drives = _getXboxLibraryDrives();

            foreach (var drive in drives)
            {
                try
                {
                    // Typical Xbox app locations: <drive>:\XboxGames, <drive>:\ModifiableWindowsApps, <drive>:\.GamingRoot
                    var candidateRoots = new List<string>
                    {
                        Path.Combine(drive, "XboxGames"),
                        Path.Combine(drive, "ModifiableWindowsApps")
                    };

                    // Check for .GamingRoot file which Xbox App drops at root of game library drives
                    string gamingRootFile = Path.Combine(drive, ".GamingRoot");
                    if (_fileExists(gamingRootFile))
                    {
                        try
                        {
                            string content = _readAllText(gamingRootFile);
                            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                string trimmed = line.Trim();
                                if (!string.IsNullOrEmpty(trimmed) && trimmed.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                                {
                                    string customRoot = Path.IsPathRooted(trimmed) ? trimmed : Path.Combine(drive, trimmed);
                                    if (!candidateRoots.Contains(customRoot, StringComparer.OrdinalIgnoreCase))
                                    {
                                        candidateRoots.Add(customRoot);
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    foreach (var root in candidateRoots)
                    {
                        if (!_directoryExists(root)) continue;

                        string[] subDirs = _getDirectories(root);
                        foreach (var gameFolder in subDirs)
                        {
                            try
                            {
                                var game = ParseXboxGameFolder(gameFolder);
                                if (game != null)
                                {
                                    results.Add(game);
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggerService.LogWarning($"Failed to parse Xbox game folder {gameFolder}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"Error scanning Xbox games on drive {drive}: {ex.Message}");
                }
            }

            return results;
        }

        public DetectedGameInfo? ParseXboxGameFolder(string gameFolder)
        {
            if (!_directoryExists(gameFolder)) return null;

            string folderName = Path.GetFileName(gameFolder);
            string contentDir = Path.Combine(gameFolder, "Content");
            string searchDir = _directoryExists(contentDir) ? contentDir : gameFolder;

            string? gameName = null;
            string? exeName = null;
            string? logoPath = null;
            string? appId = null;

            bool hasMicrosoftGameConfig = false;
            bool hasAppxManifest = false;

            // 1. Try MicrosoftGame.config
            string configPath = Path.Combine(searchDir, "MicrosoftGame.config");
            if (!_fileExists(configPath))
            {
                configPath = Path.Combine(gameFolder, "MicrosoftGame.config");
            }

            if (_fileExists(configPath))
            {
                hasMicrosoftGameConfig = true;
                try
                {
                    string xml = _readAllText(configPath);
                    var doc = XDocument.Parse(xml);
                    var root = doc.Root;

                    var identity = root?.Element("Identity");
                    appId = identity?.Attribute("Name")?.Value;

                    var shellVisuals = root?.Element("ShellVisuals");
                    gameName = shellVisuals?.Attribute("DisplayName")?.Value;
                    string? logoRel = shellVisuals?.Attribute("Square150x150Logo")?.Value
                                   ?? shellVisuals?.Attribute("Square44x44Logo")?.Value
                                   ?? shellVisuals?.Attribute("DefaultDisplayName")?.Value;

                    if (!string.IsNullOrEmpty(logoRel))
                    {
                        string candidateLogo = Path.Combine(searchDir, logoRel);
                        if (_fileExists(candidateLogo))
                        {
                            logoPath = candidateLogo;
                        }
                    }

                    var execList = root?.Element("ExecutableList")?.Elements("Executable");
                    if (execList != null)
                    {
                        foreach (var exec in execList)
                        {
                            string? targetDevice = exec.Attribute("TargetDeviceFamily")?.Value;
                            if (string.IsNullOrEmpty(targetDevice) || targetDevice.Equals("PC", StringComparison.OrdinalIgnoreCase))
                            {
                                string? candidateExe = exec.Attribute("Name")?.Value;
                                if (!string.IsNullOrEmpty(candidateExe) && !IgnoredExeNames.Contains(candidateExe))
                                {
                                    exeName = candidateExe;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Try appxmanifest.xml if still missing info
            string manifestPath = Path.Combine(searchDir, "appxmanifest.xml");
            if (!_fileExists(manifestPath))
            {
                manifestPath = Path.Combine(gameFolder, "appxmanifest.xml");
            }

            if (_fileExists(manifestPath))
            {
                hasAppxManifest = true;
                try
                {
                    string xml = _readAllText(manifestPath);
                    var doc = XDocument.Parse(xml);
                    var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                    var appElem = doc.Descendants(ns + "Application").FirstOrDefault();
                    if (appElem != null)
                    {
                        string? candidateExe = appElem.Attribute("Executable")?.Value;
                        if (!string.IsNullOrEmpty(candidateExe) && !IgnoredExeNames.Contains(candidateExe))
                        {
                            exeName = candidateExe;
                        }
                        if (string.IsNullOrEmpty(appId))
                        {
                            appId = appElem.Attribute("Id")?.Value;
                        }
                    }

                    if (string.IsNullOrEmpty(gameName))
                    {
                        var visualElements = doc.Descendants().FirstOrDefault(d => d.Name.LocalName == "VisualElements");
                        gameName = visualElements?.Attribute("DisplayName")?.Value;
                    }
                }
                catch { }
            }

            // If neither MicrosoftGame.config nor appxmanifest.xml exists, ensure this is an authentic Xbox directory
            bool isExplicitXboxDir = gameFolder.Contains("XboxGames", StringComparison.OrdinalIgnoreCase) ||
                                     gameFolder.Contains("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase);

            if (!hasMicrosoftGameConfig && !hasAppxManifest && !isExplicitXboxDir)
            {
                return null;
            }

            // 3. Fallback to locating main executable in directory
            string? resolvedExePath = null;
            if (!string.IsNullOrEmpty(exeName))
            {
                string candidate = Path.Combine(searchDir, exeName);
                if (_fileExists(candidate))
                {
                    resolvedExePath = candidate;
                }
                else
                {
                    candidate = Path.Combine(gameFolder, exeName);
                    if (_fileExists(candidate))
                    {
                        resolvedExePath = candidate;
                    }
                }
            }

            if (string.IsNullOrEmpty(resolvedExePath) && (hasMicrosoftGameConfig || hasAppxManifest || isExplicitXboxDir))
            {
                try
                {
                    string[] exes = _getFiles(searchDir, "*.exe");
                    resolvedExePath = exes.FirstOrDefault(f =>
                    {
                        string fn = Path.GetFileName(f);
                        return !IgnoredExeNames.Contains(fn);
                    });
                }
                catch { }
            }

            if (string.IsNullOrEmpty(resolvedExePath) || !_fileExists(resolvedExePath))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(gameName) || gameName.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
            {
                gameName = CleanGameDisplayName(folderName);
            }

            return new DetectedGameInfo
            {
                Name = gameName,
                ExecutablePath = resolvedExePath,
                Launcher = "Xbox",
                AppId = appId ?? Path.GetFileName(resolvedExePath),
                IconPath = logoPath
            };
        }

        public IReadOnlyList<DetectedGameInfo> ScanGameConfigStoreRegistry()
        {
            var results = new List<DetectedGameInfo>();

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children");
                if (key == null) return results;

                string[] subKeys = key.GetSubKeyNames();
                foreach (var subKeyName in subKeys)
                {
                    try
                    {
                        using var child = key.OpenSubKey(subKeyName);
                        if (child == null) continue;

                        string? matchedExe = child.GetValue("MatchedExeFullPath") as string;
                        if (string.IsNullOrWhiteSpace(matchedExe)) continue;

                        string trimmedPath = matchedExe.Trim('\"').Trim();
                        if (!_fileExists(trimmedPath)) continue;

                        string exeName = Path.GetFileName(trimmedPath).ToLowerInvariant();
                        if (IgnoredExeNames.Contains(exeName)) continue;

                        // Filter out non-games or Steam/Epic paths (they are handled by their own detectors)
                        string pathLower = trimmedPath.ToLowerInvariant();
                        if (pathLower.Contains(@"\steamapps\") || pathLower.Contains(@"\epic games\"))
                        {
                            continue;
                        }

                        // Ignore typical non-gaming applications or system processes registered in Game Bar
                        if (pathLower.Contains(@"\windows\system32\") ||
                            pathLower.Contains(@"\windows\syswow64\") ||
                            pathLower.Contains(@"\windows\systemapps\") ||
                            pathLower.Contains(@"\program files\windowsapps\microsoft.windows.") ||
                            pathLower.Contains(@"\google\chrome\") ||
                            pathLower.Contains(@"\mozilla firefox\") ||
                            pathLower.Contains(@"\discord\") ||
                            pathLower.Contains(@"\notiglow\") ||
                            pathLower.Contains(@"\microsoft\onedrive\"))
                        {
                            continue;
                        }

                        string? title = child.GetValue("Title") as string;
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            title = Path.GetFileNameWithoutExtension(trimmedPath);
                        }

                        string launcher = (pathLower.Contains("xbox") || pathLower.Contains("gamingroot")) ? "Xbox" : "Microsoft Store";

                        results.Add(new DetectedGameInfo
                        {
                            Name = CleanGameDisplayName(title),
                            ExecutablePath = trimmedPath,
                            Launcher = launcher,
                            AppId = subKeyName
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"GameConfigStore registry read failed: {ex.Message}");
            }

            return results;
        }

        public IReadOnlyList<DetectedGameInfo> ScanPackageStoreGames()
        {
            if (_winRtPackageScanner != null)
            {
                return _winRtPackageScanner();
            }

            var results = new List<DetectedGameInfo>();

            try
            {
                // WinRT PackageManager API
                var pm = new Windows.Management.Deployment.PackageManager();
                var packages = pm.FindPackagesForUser(string.Empty);

                foreach (var pkg in packages)
                {
                    try
                    {
                        if (pkg.IsFramework || pkg.IsResourcePackage || pkg.IsBundle) continue;

                        string pkgName = pkg.Id.Name;
                        if (ExcludedPackagePrefixes.Any(p => pkgName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        string? installedPath = null;
                        try
                        {
                            installedPath = pkg.InstalledLocation?.Path;
                        }
                        catch { }

                        // Check if package has game classification before enumerating app entries
                        bool isGame = IsPackageGame(pkg, installedPath);
                        if (!isGame) continue;

                        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041, 0)) continue;
                        var appList = pkg.GetAppListEntries();
                        if (appList == null) continue;

                        foreach (var appEntry in appList)
                        {
                            try
                            {
                                string aumid = appEntry.AppUserModelId;
                                string dispName = appEntry.DisplayInfo?.DisplayName ?? string.Empty;
                                if (string.IsNullOrWhiteSpace(dispName) || dispName.StartsWith("@{") || dispName.StartsWith("ms-resource:"))
                                {
                                    dispName = pkgName;
                                }

                                string exePath = string.Empty;
                                if (!string.IsNullOrEmpty(installedPath) && _directoryExists(installedPath))
                                {
                                    try
                                    {
                                        string[] exes = _getFiles(installedPath, "*.exe");
                                        string? found = exes.FirstOrDefault(f => !IgnoredExeNames.Contains(Path.GetFileName(f)));
                                        if (!string.IsNullOrEmpty(found))
                                        {
                                            exePath = found;
                                        }
                                    }
                                    catch { }
                                }

                                if (string.IsNullOrEmpty(exePath))
                                {
                                    exePath = $"{pkgName}.exe";
                                }

                                results.Add(new DetectedGameInfo
                                {
                                    Name = CleanGameDisplayName(dispName),
                                    ExecutablePath = exePath,
                                    Launcher = (pkgName.Contains("Xbox", StringComparison.OrdinalIgnoreCase) || pkgName.Contains("Gaming", StringComparison.OrdinalIgnoreCase)) ? "Xbox" : "Microsoft Store",
                                    AppId = aumid,
                                    AumId = aumid
                                });
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"WinRT package store scan fallback: {ex.Message}");
            }

            return results;
        }

        private bool IsPackageGame(Windows.ApplicationModel.Package pkg, string? installedPath)
        {
            try
            {
                // Check appxmanifest.xml Category="game"
                if (!string.IsNullOrEmpty(installedPath))
                {
                    string manifestPath = Path.Combine(installedPath, "appxmanifest.xml");
                    if (_fileExists(manifestPath))
                    {
                        string xml = _readAllText(manifestPath);
                        if (xml.Contains("Category=\"game\"", StringComparison.OrdinalIgnoreCase) ||
                            xml.Contains("Category=\"Game\"", StringComparison.OrdinalIgnoreCase) ||
                            xml.Contains("xboxLive", StringComparison.OrdinalIgnoreCase) ||
                            xml.Contains("gaming", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                // Check AppModel repository registry for category
                string pkgFamily = pkg.Id.FamilyName;
                using var repoKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
                if (repoKey != null)
                {
                    string? matchKey = repoKey.GetSubKeyNames().FirstOrDefault(k => k.StartsWith(pkg.Id.Name, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(matchKey))
                    {
                        using var targetKey = repoKey.OpenSubKey($@"{matchKey}\Applications");
                        if (targetKey != null)
                        {
                            foreach (var appSub in targetKey.GetSubKeyNames())
                            {
                                using var appDetails = targetKey.OpenSubKey(appSub);
                                string? cat = appDetails?.GetValue("ApplicationCategory") as string ?? appDetails?.GetValue("Category") as string;
                                if (!string.IsNullOrEmpty(cat) && cat.Contains("game", StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        private static IEnumerable<string> DefaultGetXboxLibraryDrives()
        {
            var drives = new List<string>();
            try
            {
                foreach (var di in DriveInfo.GetDrives())
                {
                    if (di.IsReady && di.DriveType == DriveType.Fixed)
                    {
                        drives.Add(di.RootDirectory.FullName);
                    }
                }
            }
            catch
            {
                drives.Add(@"C:\");
            }
            return drives;
        }

        public static string CleanGameDisplayName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "Unknown Game";
            string clean = rawName.Trim();

            // Strip resource strings e.g. "ms-resource:..."
            if (clean.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase) || clean.StartsWith("@{", StringComparison.OrdinalIgnoreCase))
            {
                return "PC Game";
            }

            clean = Regex.Replace(clean, @"_+", " ");
            return clean.Trim();
        }
    }
}
