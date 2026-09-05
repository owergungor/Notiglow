using System;
using Microsoft.Win32;
using NotiGlow.Services;

namespace NotiGlow.Core.Helpers
{
    public static class AutoStartHelper
    {
        private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "NotiGlow";
        private const string LegacyAppName = "GlowBorder";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
                var value = key?.GetValue(AppName) ?? key?.GetValue(LegacyAppName);
                return value != null;
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to read autostart registry key", ex);
                return false;
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                if (enable)
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        // Remove legacy entry if present
                        try { key.DeleteValue(LegacyAppName, false); } catch { }

                        key.SetValue(AppName, $"\"{exePath}\" --autostart");
                        LoggerService.LogInfo($"AutoStart enabled: {exePath}");
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                    try { key.DeleteValue(LegacyAppName, false); } catch { }
                    LoggerService.LogInfo("AutoStart disabled");
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Failed to update autostart registry key", ex);
            }
        }
    }
}
