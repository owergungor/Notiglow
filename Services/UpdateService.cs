using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public enum UpdatePackageType
    {
        InnoSetupInstaller,
        PortableZip
    }

    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = UpdateService.CurrentVersionString;
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string? InstallerDownloadUrl { get; set; }
        public string? ZipDownloadUrl { get; set; }
        public string? Sha256DownloadUrl { get; set; }
        public string? ExpectedSha256 { get; set; }
        public UpdatePackageType PackageType { get; set; } = UpdatePackageType.InnoSetupInstaller;
        public string? SelectedDownloadUrl => PackageType == UpdatePackageType.InnoSetupInstaller ? InstallerDownloadUrl : ZipDownloadUrl;
    }

    public enum UpdateStatus
    {
        Idle,
        Checking,
        UpdateAvailable,
        UpToDate,
        Downloading,
        Verifying,
        ReadyToInstall,
        Installing,
        Failed
    }

    public class UpdateService
    {
        public static string CurrentVersionString => ResolveCurrentVersion();
        private static string? _cachedVersion;

        private static string ResolveCurrentVersion()
        {
            if (_cachedVersion != null)
                return _cachedVersion;

            try
            {
                var assembly = typeof(UpdateService).Assembly;
                var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (!string.IsNullOrWhiteSpace(infoAttr?.InformationalVersion))
                {
                    string infoVer = infoAttr.InformationalVersion.Split('+')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(infoVer))
                    {
                        _cachedVersion = infoVer;
                        return _cachedVersion;
                    }
                }

                var asmVer = assembly.GetName().Version;
                if (asmVer != null)
                {
                    _cachedVersion = asmVer.Build > 0
                        ? $"{asmVer.Major}.{asmVer.Minor}.{asmVer.Build}"
                        : $"{asmVer.Major}.{asmVer.Minor}";
                    return _cachedVersion;
                }
            }
            catch
            {
                // Fallback
            }

            _cachedVersion = "2.0";
            return _cachedVersion;
        }
        private const string GitHubOwner = "owergungor";
        private const string GitHubRepo = "NotiGlow";

        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly SettingsService _settingsService;

        public event EventHandler<UpdateInfo>? UpdateAvailable;
        public event EventHandler<(UpdateStatus Status, string Message, double Progress)>? StatusChanged;

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"NotiGlow-Updater/{CurrentVersionString} (Windows; x64)");
            _httpClient.Timeout = TimeSpan.FromSeconds(20);
        }

        public UpdateService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public static string GetUpdatesDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string updatesDir = Path.Combine(appData, "NotiGlow", "updates");
            Directory.CreateDirectory(updatesDir);
            return updatesDir;
        }

        public static bool IsNewerVersion(string latestTagOrVersion, string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(latestTagOrVersion)) return false;
            string cleanLatest = latestTagOrVersion.Trim().TrimStart('v', 'V');
            string cleanCurrent = currentVersion.Trim().TrimStart('v', 'V');

            if (Version.TryParse(cleanLatest, out var vLatest) && Version.TryParse(cleanCurrent, out var vCurrent))
            {
                return vLatest > vCurrent;
            }

            return false;
        }

        public static bool ShouldCheckForUpdates(AppSettings settings, DateTime utcNow)
        {
            if (!settings.AutoCheckUpdates)
                return false;

            if (settings.UpdateFrequency == UpdateCheckFrequency.OnStartup)
                return true;

            if (!settings.LastUpdateCheck.HasValue)
                return true;

            var elapsed = utcNow - settings.LastUpdateCheck.Value;
            if (elapsed < TimeSpan.Zero)
            {
                // Skewed clock / system time moved backwards -> allow check
                return true;
            }

            return settings.UpdateFrequency switch
            {
                UpdateCheckFrequency.Daily => elapsed >= TimeSpan.FromDays(1),
                UpdateCheckFrequency.Weekly => elapsed >= TimeSpan.FromDays(7),
                UpdateCheckFrequency.Monthly => elapsed >= TimeSpan.FromDays(30),
                _ => true
            };
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            StatusChanged?.Invoke(this, (UpdateStatus.Checking, "Checking for updates...", 0));

            var info = new UpdateInfo
            {
                CurrentVersion = CurrentVersionString,
                IsUpdateAvailable = false
            };

            try
            {
                string apiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                using var response = await _httpClient.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    LoggerService.LogWarning($"Update check returned status code: {response.StatusCode}");
                    StatusChanged?.Invoke(this, (UpdateStatus.Idle, $"Update check returned {response.StatusCode}", 0));
                    return info;
                }

                string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                info.LatestVersion = tagName.TrimStart('v', 'V');
                info.ReleaseNotes = body;

                if (IsNewerVersion(info.LatestVersion, info.CurrentVersion))
                {
                    info.IsUpdateAvailable = true;

                    if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                            string url = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() ?? "" : "";

                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                            {
                                info.InstallerDownloadUrl = url;
                            }
                            else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                info.ZipDownloadUrl = url;
                            }
                            else if (name.Equals("SHA256.txt", StringComparison.OrdinalIgnoreCase))
                            {
                                info.Sha256DownloadUrl = url;
                            }
                        }
                    }

                    // Choose package type based on running context (installed vs portable)
                    info.PackageType = IsRunningFromInstalledLocation() && !string.IsNullOrEmpty(info.InstallerDownloadUrl)
                        ? UpdatePackageType.InnoSetupInstaller
                        : (!string.IsNullOrEmpty(info.InstallerDownloadUrl) ? UpdatePackageType.InnoSetupInstaller : UpdatePackageType.PortableZip);

                    _settingsService.Current.LastUpdateCheck = DateTime.UtcNow;
                    _settingsService.Current.LastUpdateVersion = info.LatestVersion;
                    _settingsService.Save(_settingsService.Current);

                    StatusChanged?.Invoke(this, (UpdateStatus.UpdateAvailable, $"v{info.LatestVersion} available", 0));
                    UpdateAvailable?.Invoke(this, info);
                }
                else
                {
                    _settingsService.Current.LastUpdateCheck = DateTime.UtcNow;
                    _settingsService.Save(_settingsService.Current);
                    StatusChanged?.Invoke(this, (UpdateStatus.UpToDate, $"NotiGlow v{CurrentVersionString} is up to date.", 100));
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Update check failed gracefully: {ex.Message}");
                StatusChanged?.Invoke(this, (UpdateStatus.Idle, "Update check failed (offline or server unreachable).", 0));
            }

            return info;
        }

        public static bool IsRunningFromInstalledLocation()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                return (!string.IsNullOrEmpty(programFiles) && exePath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase)) ||
                       (!string.IsNullOrEmpty(programFilesX86) && exePath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase)) ||
                       (!string.IsNullOrEmpty(localAppData) && exePath.StartsWith(Path.Combine(localAppData, "Programs"), StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> DownloadAndVerifyPackageAsync(
            UpdateInfo info,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string? downloadUrl = info.SelectedDownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                LoggerService.LogError("No valid download URL available in UpdateInfo.");
                StatusChanged?.Invoke(this, (UpdateStatus.Failed, "No download asset found.", 0));
                return null;
            }

            string fileName = Path.GetFileName(new Uri(downloadUrl).AbsolutePath);
            string destinationPath = Path.Combine(GetUpdatesDirectory(), fileName);
            string partPath = destinationPath + ".part";

            StatusChanged?.Invoke(this, (UpdateStatus.Downloading, $"Downloading {fileName}...", 0));

            try
            {
                if (File.Exists(partPath))
                {
                    try { File.Delete(partPath); } catch { }
                }

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;

                    using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var fileStream = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 16384, true);

                    var buffer = new byte[16384];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        totalRead += bytesRead;
                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            double pct = (double)totalRead / totalBytes.Value * 100.0;
                            progress?.Report(pct);
                            StatusChanged?.Invoke(this, (UpdateStatus.Downloading, $"Downloading... {pct:0}%", pct));
                        }
                    }
                }

                // Download completed without interruption -> Move .part to final destinationPath
                if (File.Exists(destinationPath))
                {
                    try { File.Delete(destinationPath); } catch { }
                }
                File.Move(partPath, destinationPath);

                // Step: SHA256 Verification
                StatusChanged?.Invoke(this, (UpdateStatus.Verifying, "Verifying integrity (SHA-256)...", 100));

                string? expectedHash = info.ExpectedSha256;
                if (string.IsNullOrEmpty(expectedHash) && !string.IsNullOrEmpty(info.Sha256DownloadUrl))
                {
                    try
                    {
                        string shaContent = await _httpClient.GetStringAsync(info.Sha256DownloadUrl, cancellationToken).ConfigureAwait(false);
                        expectedHash = ExtractHashForFile(shaContent, fileName);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogWarning($"Could not download SHA256.txt: {ex.Message}");
                    }
                }

                if (!string.IsNullOrEmpty(expectedHash))
                {
                    bool hashValid = VerifySha256(destinationPath, expectedHash);
                    if (!hashValid)
                    {
                        LoggerService.LogError($"SHA256 mismatch for {fileName}. Aborting install.");
                        StatusChanged?.Invoke(this, (UpdateStatus.Failed, "SHA-256 verification failed! Corrupted package removed.", 0));
                        try { File.Delete(destinationPath); } catch { }
                        return null;
                    }
                }

                StatusChanged?.Invoke(this, (UpdateStatus.ReadyToInstall, "Update downloaded and verified.", 100));
                return destinationPath;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to download or verify update package", ex);
                StatusChanged?.Invoke(this, (UpdateStatus.Failed, $"Download failed: {ex.Message}", 0));
                try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
                return null;
            }
        }

        public static bool VerifySha256(string filePath, string expectedHash)
        {
            if (!File.Exists(filePath) || string.IsNullOrWhiteSpace(expectedHash)) return false;

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(stream);
            string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            return computedHash.Equals(expectedHash.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }

        public static string ExtractHashForFile(string sha256Content, string fileName)
        {
            if (string.IsNullOrWhiteSpace(sha256Content)) return string.Empty;

            using var reader = new StringReader(sha256Content);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string hash = parts[0];
                    string target = parts[1].TrimStart('*');
                    if (string.Equals(target, fileName, StringComparison.OrdinalIgnoreCase) ||
                        fileName.Contains(target, StringComparison.OrdinalIgnoreCase) ||
                        target.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return hash;
                    }
                }
                else if (parts.Length == 1 && parts[0].Length == 64)
                {
                    return parts[0];
                }
            }

            return string.Empty;
        }

        public static bool ExecuteUpdateAndRestart(string packagePath, Action onAppCloseRequested)
        {
            try
            {
                if (!File.Exists(packagePath))
                {
                    LoggerService.LogError($"Package path does not exist: {packagePath}");
                    return false;
                }

                int currentPid = Process.GetCurrentProcess().Id;
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string targetDir = Path.GetDirectoryName(currentExe) ?? "";

                if (packagePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // Inno Setup installer workflow:
                    // Runs external installer process which waits for current PID to exit,
                    // silently installs to targetDir, and relaunches NotiGlow.exe
                    var psi = new ProcessStartInfo
                    {
                        FileName = packagePath,
                        Arguments = $"/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /DIR=\"{targetDir}\"",
                        UseShellExecute = true
                    };

                    Process.Start(psi);
                    LoggerService.LogInfo($"Launched Inno Setup updater: {packagePath}. Requesting app close.");
                    onAppCloseRequested();
                    return true;
                }
                else if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // Portable ZIP workflow:
                    // Extract to a staged directory, deploy standalone staging updater
                    string stagedDir = Path.Combine(GetUpdatesDirectory(), "staged_" + DateTime.UtcNow.Ticks);
                    Directory.CreateDirectory(stagedDir);
                    ZipFile.ExtractToDirectory(packagePath, stagedDir, true);

                    string updaterScriptPath = Path.Combine(GetUpdatesDirectory(), "apply_update.vbs");
                    string backupDir = Path.Combine(GetUpdatesDirectory(), "backup_" + CurrentVersionString);

                    // Write a fail-safe VBScript updater:
                    // 1. Waits for PID to terminate
                    // 2. Copies current directory to backupDir
                    // 3. Atomically replaces files from stagedDir to targetDir
                    // 4. If copy errors occur, rolls back from backupDir
                    // 5. Launches currentExe
                    // 6. Cleans up stagedDir and updater script
                    string vbsCode = GenerateVbsUpdaterScript(currentPid, currentExe, targetDir, stagedDir, backupDir);
                    File.WriteAllText(updaterScriptPath, vbsCode);

                    var psi = new ProcessStartInfo
                    {
                        FileName = "wscript.exe",
                        Arguments = $"\"{updaterScriptPath}\"",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process.Start(psi);
                    LoggerService.LogInfo($"Launched portable atomic updater: {updaterScriptPath}. Requesting app close.");
                    onAppCloseRequested();
                    return true;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to launch updater", ex);
            }

            return false;
        }

        public static string GenerateVbsUpdaterScript(int targetPid, string targetExe, string targetDir, string stagedDir, string backupDir)
        {
            return $@"
Option Explicit
Dim fso, shell, pid, targetExe, targetDir, stagedDir, backupDir
Set fso = CreateObject(""Scripting.FileSystemObject"")
Set shell = CreateObject(""WScript.Shell"")

pid = {targetPid}
targetExe = ""{targetExe.Replace("\\", "\\\\")}""
targetDir = ""{targetDir.Replace("\\", "\\\\")}""
stagedDir = ""{stagedDir.Replace("\\", "\\\\")}""
backupDir = ""{backupDir.Replace("\\", "\\\\")}""

' Step 1: Wait up to 15 seconds for running NotiGlow process to exit
Dim i, running
For i = 1 To 30
    running = False
    Dim procs, p
    Set procs = GetObject(""winmgmts:"").ExecQuery(""Select * from Win32_Process Where ProcessId = "" & pid)
    For Each p in procs
        running = True
    Next
    If Not running Then Exit For
    WScript.Sleep 500
Next

' Step 2: Backup current targetDir
On Error Resume Next
If fso.FolderExists(backupDir) Then fso.DeleteFolder backupDir, True
fso.CreateFolder backupDir
fso.CopyFolder targetDir & ""\*"", backupDir, True

' Step 3: Atomic copy from stagedDir to targetDir
Err.Clear
fso.CopyFolder stagedDir & ""\*"", targetDir, True
fso.CopyFile stagedDir & ""\*"", targetDir & ""\"", True

If Err.Number <> 0 Then
    ' Rollback on failure
    fso.CopyFolder backupDir & ""\*"", targetDir, True
    fso.CopyFile backupDir & ""\*"", targetDir & ""\"", True
End If

' Step 4: Relaunch application
If fso.FileExists(targetExe) Then
    shell.Run """" & targetExe & """", 1, False
End If

' Step 5: Clean up staging folder
If fso.FolderExists(stagedDir) Then fso.DeleteFolder stagedDir, True
";
        }
    }
}
