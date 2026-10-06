using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class EpicGameDetector
    {
        private readonly string _manifestsDirectory;
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string> _readAllText;
        private readonly Func<string, string, string[]> _getFiles;

        public EpicGameDetector(
            string? manifestsDirectory = null,
            Func<string, bool>? directoryExists = null,
            Func<string, bool>? fileExists = null,
            Func<string, string>? readAllText = null,
            Func<string, string, string[]>? getFiles = null)
        {
            _manifestsDirectory = manifestsDirectory ?? DefaultManifestsDirectory;
            _directoryExists = directoryExists ?? (d => !string.IsNullOrEmpty(d) && Directory.Exists(d));
            _fileExists = fileExists ?? (f => !string.IsNullOrEmpty(f) && File.Exists(f));
            _readAllText = readAllText ?? File.ReadAllText;
            _getFiles = getFiles ?? ((d, p) => Directory.Exists(d) ? Directory.GetFiles(d, p) : Array.Empty<string>());
        }

        public static string DefaultManifestsDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");

        public IReadOnlyList<DetectedGameInfo> DetectGames()
        {
            var detectedGames = new List<DetectedGameInfo>();

            try
            {
                if (!_directoryExists(_manifestsDirectory))
                {
                    return detectedGames;
                }

                var itemFiles = _getFiles(_manifestsDirectory, "*.item");
                foreach (var itemPath in itemFiles)
                {
                    try
                    {
                        var game = ParseManifestItem(itemPath);
                        if (game != null && !string.IsNullOrEmpty(game.ExecutablePath))
                        {
                            detectedGames.Add(game);
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogWarning($"Failed to parse Epic manifest {itemPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogError("Error during Epic Games detection", ex);
            }

            return detectedGames;
        }

        public DetectedGameInfo? ParseManifestItem(string manifestPath)
        {
            string json = _readAllText(manifestPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? displayName = GetStringProp(root, "DisplayName");
            string? installLocation = GetStringProp(root, "InstallLocation");
            string? launchExecutable = GetStringProp(root, "LaunchExecutable");
            string? appName = GetStringProp(root, "AppName");

            if (string.IsNullOrWhiteSpace(displayName) ||
                string.IsNullOrWhiteSpace(installLocation) ||
                string.IsNullOrWhiteSpace(launchExecutable))
            {
                return null;
            }

            // Exclude Epic Games Launcher or Unreal Engine Prerequisites
            if (displayName.IndexOf("Epic Games", StringComparison.OrdinalIgnoreCase) >= 0 ||
                displayName.IndexOf("Unreal Engine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return null;
            }

            string fullExePath = Path.Combine(installLocation, launchExecutable);
            if (!_fileExists(fullExePath))
            {
                return null;
            }

            return new DetectedGameInfo
            {
                Name = displayName.Trim(),
                ExecutablePath = Path.GetFullPath(fullExePath),
                Launcher = "Epic",
                AppId = appName
            };
        }

        private static string? GetStringProp(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            return null;
        }
    }
}
