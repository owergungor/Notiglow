using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class AppIconService
    {
        private static AppIconService? _instance;
        public static AppIconService Instance => _instance ??= new AppIconService();

        private readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

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

            return await Task.Run(() =>
            {
                ImageSource? icon = ExtractIcon(executablePath, appId);
                _iconCache[cacheKey] = icon;
                return icon;
            });
        }

        public ImageSource? GetCachedIcon(string? executablePath, string? appId = null)
        {
            string cacheKey = (!string.IsNullOrWhiteSpace(executablePath) ? executablePath : (appId ?? string.Empty)).Trim();
            if (string.IsNullOrEmpty(cacheKey)) return null;

            _iconCache.TryGetValue(cacheKey, out var cached);
            return cached;
        }

        private ImageSource? ExtractIcon(string? executablePath, string? appId)
        {
            try
            {
                string cleanPath = CleanExecutablePath(executablePath);

                // 1. Try Win32 Executable Icon
                if (!string.IsNullOrWhiteSpace(cleanPath) && File.Exists(cleanPath))
                {
                    string ext = Path.GetExtension(cleanPath);
                    if (string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        using var ico = System.Drawing.Icon.ExtractAssociatedIcon(cleanPath);
                        if (ico != null)
                        {
                            using var bmp = ico.ToBitmap();
                            IntPtr hBitmap = bmp.GetHbitmap();
                            try
                            {
                                var wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                wpfBitmap.Freeze();
                                return wpfBitmap;
                            }
                            finally
                            {
                                DeleteObject(hBitmap);
                            }
                        }
                    }
                    else if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(ext, ".ico", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase))
                    {
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.UriSource = new Uri(cleanPath, UriKind.Absolute);
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        return bi;
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
                                        var bi = new BitmapImage();
                                        bi.BeginInit();
                                        bi.UriSource = new Uri(bestLogo, UriKind.Absolute);
                                        bi.CacheOption = BitmapCacheOption.OnLoad;
                                        bi.EndInit();
                                        bi.Freeze();
                                        return bi;
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
                LoggerService.LogWarning($"Icon extraction fallback for '{executablePath}': {ex.Message}");
            }

            return null;
        }

        private static string CleanExecutablePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return string.Empty;

            string cleaned = rawPath.Trim().Trim('\"');

            // Strip trailing command-line arguments if appended to an .exe path
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
