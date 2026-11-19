using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class AppIconService
    {
        private static AppIconService? _instance;
        public static AppIconService Instance => _instance ??= new AppIconService();

        private readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _cacheDirectory;

        public AppIconService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(localAppData, "NotiGlow", "icon_cache");
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
            catch { }
        }

        public async Task<ImageSource?> GetAppIconAsync(AppProfile? profile)
        {
            if (profile == null) return null;
            return await GetAppIconAsync(profile.ExecutablePath, profile.AppId);
        }

        public async Task<ImageSource?> GetAppIconAsync(string? executablePath, string? appId = null)
        {
            string cacheKey = (!string.IsNullOrWhiteSpace(executablePath) ? executablePath : (appId ?? string.Empty)).Trim();
            if (string.IsNullOrEmpty(cacheKey)) return null;

            if (_iconCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            string? iconFilePath = await Task.Run(() => ResolveIconFilePath(executablePath, appId));
            if (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(iconFilePath, UriKind.Absolute));
                    _iconCache[cacheKey] = bitmap;
                    return bitmap;
                }
                catch (Exception ex)
                {
                    LoggerService.LogWarning($"BitmapImage creation failed for '{iconFilePath}': {ex.Message}");
                }
            }

            return null;
        }

        public ImageSource? GetCachedIcon(string? executablePath, string? appId = null)
        {
            string cacheKey = (!string.IsNullOrWhiteSpace(executablePath) ? executablePath : (appId ?? string.Empty)).Trim();
            if (string.IsNullOrEmpty(cacheKey)) return null;

            _iconCache.TryGetValue(cacheKey, out var cached);
            return cached;
        }

        private string? ResolveIconFilePath(string? executablePath, string? appId)
        {
            try
            {
                string cleanPath = CleanExecutablePath(executablePath);

                // If not an absolute existing path, try resolving via Windows App Paths or Steam registry
                if (!string.IsNullOrWhiteSpace(cleanPath) && !File.Exists(cleanPath))
                {
                    try
                    {
                        string fileName = Path.GetFileName(cleanPath);
                        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + fileName)
                                     ?? Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + fileName);
                        string? registeredPath = key?.GetValue("") as string;
                        if (!string.IsNullOrEmpty(registeredPath))
                        {
                            registeredPath = registeredPath.Trim('\"');
                            if (File.Exists(registeredPath))
                            {
                                cleanPath = registeredPath;
                            }
                        }
                    }
                    catch { }

                    // Also check Steam games icon folder if appId or game name is present
                    if (!File.Exists(cleanPath))
                    {
                        try
                        {
                            string steamIcons = @"C:\Program Files (x86)\Steam\steam\games";
                            if (Directory.Exists(steamIcons))
                            {
                                string targetName = Path.GetFileNameWithoutExtension(cleanPath);
                                var matchedIco = Directory.GetFiles(steamIcons, "*.ico")
                                    .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                                                         (!string.IsNullOrEmpty(appId) && Path.GetFileNameWithoutExtension(f).Equals(appId, StringComparison.OrdinalIgnoreCase)));
                                if (!string.IsNullOrEmpty(matchedIco) && File.Exists(matchedIco))
                                {
                                    cleanPath = matchedIco;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // 1. Try Win32 Executable Icon
                if (!string.IsNullOrWhiteSpace(cleanPath) && File.Exists(cleanPath))
                {
                    string ext = Path.GetExtension(cleanPath);
                    if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string hash = ComputeHash(cleanPath);
                        string cachedPng = Path.Combine(_cacheDirectory, $"{hash}.png");
                        if (File.Exists(cachedPng))
                        {
                            return cachedPng;
                        }

                        using var ico = System.Drawing.Icon.ExtractAssociatedIcon(cleanPath);
                        if (ico != null)
                        {
                            using var bmp = ico.ToBitmap();
                            bmp.Save(cachedPng, System.Drawing.Imaging.ImageFormat.Png);
                            return cachedPng;
                        }
                    }
                    else if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(ext, ".ico", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        return cleanPath;
                    }
                }

                // 2. Try AppX / MSIX / Xbox game directory logos
                if (!string.IsNullOrWhiteSpace(cleanPath))
                {
                    string? dir = Path.GetDirectoryName(cleanPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        string[] candidateDirs = new[]
                        {
                            Path.Combine(dir, "Assets"),
                            Path.Combine(dir, "Content", "Assets"),
                            dir
                        };

                        foreach (var cDir in candidateDirs)
                        {
                            if (Directory.Exists(cDir))
                            {
                                try
                                {
                                    var files = Directory.GetFiles(cDir, "*logo*.png");
                                    var bestLogo = files.FirstOrDefault(f => f.Contains("44x44", StringComparison.OrdinalIgnoreCase))
                                                ?? files.FirstOrDefault(f => f.Contains("150x150", StringComparison.OrdinalIgnoreCase))
                                                ?? files.FirstOrDefault();

                                    if (!string.IsNullOrEmpty(bestLogo) && File.Exists(bestLogo))
                                    {
                                        return bestLogo;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Icon resolution fallback for '{executablePath}': {ex.Message}");
            }

            return null;
        }

        private static string ComputeHash(string input)
        {
            using var md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static string CleanExecutablePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            string cleaned = rawPath.Trim().Trim('\"');

            int exeIdx = cleaned.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0 && exeIdx + 4 < cleaned.Length)
            {
                string candidate = cleaned.Substring(0, exeIdx + 4).Trim('\"');
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return cleaned;
        }

        public void ClearCache()
        {
            _iconCache.Clear();
        }
    }
}
