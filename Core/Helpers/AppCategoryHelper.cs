using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.Core.Helpers
{
    public class CategoryNavigationEventArgs : EventArgs
    {
        public string TargetCategory { get; }
        public string TargetIdentifier { get; }

        public CategoryNavigationEventArgs(string targetCategory, string targetIdentifier)
        {
            TargetCategory = targetCategory;
            TargetIdentifier = targetIdentifier;
        }
    }

    /// <summary>
    /// Centralizes category classification, deduplication, and cross-tab synchronization
    /// between General Applications (ProfileService) and Gaming (TrackedGames + GameOverrides + ProfileService).
    /// </summary>
    public static class AppCategoryHelper
    {
        public const string CategoryGeneral = "General";
        public const string CategoryGaming = "Gaming";

        private static readonly HashSet<string> SharedEngineExecutables = new(StringComparer.OrdinalIgnoreCase)
        {
            "hl2.exe",
            "gdklauncher.exe",
            "gamelaunchhelper.exe",
            "shipping.exe",
            "unrealgame.exe"
        };

        private static readonly HashSet<string> GenericSubfolderNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "binaries", "win64", "win32", "x64", "x86", "content", "engine", "extras",
            "redist", "launcher", "game", "retail", "shipping", "client", "support", "common",
            "steamapps", "xboxgames", "epic games", "games", "program files", "program files (x86)"
        };

        private static readonly Dictionary<string, string> KnownExecutableDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["haloinfinite"] = "Halo Infinite",
            ["haloinfinite.exe"] = "Halo Infinite",
            ["microsoft.254428597cfe2"] = "Halo Infinite",
            ["portal"] = "Portal",
            ["portal.exe"] = "Portal",
            ["portal2"] = "Portal 2",
            ["portal2.exe"] = "Portal 2",
            ["f1_24"] = "F1 24",
            ["f1_24.exe"] = "F1 24",
            ["forzahorizon4"] = "Forza Horizon 4",
            ["forzahorizon5"] = "Forza Horizon 5",
            ["forzahorizon6"] = "Forza Horizon 6",
            ["assettocorsa"] = "Assetto Corsa",
            ["stickfight"] = "Stick Fight: The Game",
            ["stickfightthegame"] = "Stick Fight: The Game"
        };

        public static bool IsSharedEngineExecutable(string? pathOrExe)
        {
            if (string.IsNullOrWhiteSpace(pathOrExe)) return false;
            string exeName = Path.GetFileName(pathOrExe.Trim());
            return !string.IsNullOrEmpty(exeName) && SharedEngineExecutables.Contains(exeName);
        }

        public static string ResolveFriendlyGameName(string? gamePathOrEntry, AppProfile? matchingProfile = null)
        {
            if (matchingProfile != null && !string.IsNullOrWhiteSpace(matchingProfile.Name))
            {
                string profName = matchingProfile.Name.Trim();
                string profLower = profName.ToLowerInvariant();
                if (!SharedEngineExecutables.Contains(profLower) &&
                    !SharedEngineExecutables.Contains(profLower + ".exe") &&
                    !KnownExecutableDisplayNames.ContainsKey(profLower))
                {
                    return profName;
                }
            }

            if (string.IsNullOrWhiteSpace(gamePathOrEntry))
            {
                return matchingProfile?.Name ?? "Unknown Game";
            }

            string trimmed = gamePathOrEntry.Trim().Trim('\"');
            string fileName = Path.GetFileName(trimmed);
            string fileNameNoExt = Path.GetFileNameWithoutExtension(trimmed);

            if (KnownExecutableDisplayNames.TryGetValue(fileName, out var knownByFile))
            {
                return knownByFile;
            }
            if (KnownExecutableDisplayNames.TryGetValue(fileNameNoExt, out var knownByStem))
            {
                return knownByStem;
            }

            // Extract meaningful game folder from path when using shared engine exe (e.g. Portal\hl2.exe or Fortnite\...\GDKLauncher.exe)
            string? folderCandidate = ExtractGameFolderDisplayName(trimmed);
            if (!string.IsNullOrWhiteSpace(folderCandidate))
            {
                if (KnownExecutableDisplayNames.TryGetValue(folderCandidate, out var mappedFolder))
                {
                    return mappedFolder;
                }

                if (IsSharedEngineExecutable(fileName) ||
                    folderCandidate.Contains(' ') ||
                    !string.Equals(folderCandidate, fileNameNoExt, StringComparison.OrdinalIgnoreCase))
                {
                    return folderCandidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(matchingProfile?.Name))
            {
                string rawProf = matchingProfile!.Name.Trim();
                if (KnownExecutableDisplayNames.TryGetValue(rawProf, out var mappedProf))
                {
                    return mappedProf;
                }
                return rawProf;
            }

            if (string.IsNullOrEmpty(fileNameNoExt))
            {
                return trimmed;
            }

            return char.ToUpperInvariant(fileNameNoExt[0]) + (fileNameNoExt.Length > 1 ? fileNameNoExt.Substring(1) : string.Empty);
        }

        private static string? ExtractGameFolderDisplayName(string path)
        {
            if (!path.Contains('\\') && !path.Contains('/')) return null;

            string normalized = path.Replace('/', '\\');
            string[] segments = normalized.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) return null;

            // 1. Look specifically for folder right after 'common', 'XboxGames', 'ModifiableWindowsApps', or 'Epic Games'
            for (int i = 0; i < segments.Length - 2; i++)
            {
                string seg = segments[i];
                if (seg.Equals("common", StringComparison.OrdinalIgnoreCase) ||
                    seg.Equals("XboxGames", StringComparison.OrdinalIgnoreCase) ||
                    seg.Equals("ModifiableWindowsApps", StringComparison.OrdinalIgnoreCase) ||
                    seg.Equals("Epic Games", StringComparison.OrdinalIgnoreCase))
                {
                    string gameFolder = segments[i + 1].Trim();
                    if (!string.IsNullOrEmpty(gameFolder))
                    {
                        return gameFolder;
                    }
                }
            }

            // 2. Walk backwards from parent folder and pick the first non-generic folder name
            for (int i = segments.Length - 2; i >= 1; i--)
            {
                string seg = segments[i].Trim();
                if (seg.EndsWith(":", StringComparison.OrdinalIgnoreCase)) break;
                if (!GenericSubfolderNames.Contains(seg))
                {
                    return seg;
                }
            }

            return null;
        }

        public static bool IsGamingCategory(string? category)
        {
            return string.Equals(category?.Trim(), CategoryGaming, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsGamingProfile(AppProfile profile, IEnumerable<string>? trackedGames)
        {
            if (profile == null) return false;
            if (IsGamingCategory(profile.Category)) return true;

            if (trackedGames == null) return false;

            string profId = AppSettings.NormalizeGameIdentifier(profile.AppId);
            string profExe = !string.IsNullOrEmpty(profile.ExecutablePath)
                ? AppSettings.NormalizeGameIdentifier(profile.ExecutablePath)
                : string.Empty;

            foreach (var g in trackedGames)
            {
                if (string.IsNullOrWhiteSpace(g)) continue;
                if (MatchesIdentifier(profile, g))
                    return true;

                string norm = AppSettings.NormalizeGameIdentifier(g);
                if (!string.IsNullOrEmpty(norm) && !IsSharedEngineExecutable(norm))
                {
                    if (!string.IsNullOrEmpty(profId) && string.Equals(profId, norm, StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (!string.IsNullOrEmpty(profExe) && string.Equals(profExe, norm, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public static string GetPrimaryIdentifier(AppProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.ExecutablePath))
                return profile.ExecutablePath;
            return profile.AppId ?? string.Empty;
        }

        public static bool MatchesIdentifier(AppProfile profile, string identifier)
        {
            if (profile == null || string.IsNullOrWhiteSpace(identifier)) return false;

            string trimmedId = identifier.Trim();
            if (string.Equals(profile.AppId, trimmedId, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(profile.ExecutablePath) &&
                string.Equals(ProfileService.NormalizePath(profile.ExecutablePath), ProfileService.NormalizePath(trimmedId), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string targetNorm = AppSettings.NormalizeGameIdentifier(trimmedId);
            if (IsSharedEngineExecutable(targetNorm))
            {
                // For shared engine executables (e.g. hl2.exe), require matching folder/name if both are paths
                string? targetFolder = ExtractGameFolderDisplayName(trimmedId);
                string? profFolder = !string.IsNullOrEmpty(profile.ExecutablePath)
                    ? ExtractGameFolderDisplayName(profile.ExecutablePath)
                    : null;

                if (!string.IsNullOrEmpty(targetFolder) && !string.IsNullOrEmpty(profFolder))
                {
                    return string.Equals(targetFolder, profFolder, StringComparison.OrdinalIgnoreCase);
                }

                if (!string.IsNullOrEmpty(targetFolder) && !string.IsNullOrEmpty(profile.Name))
                {
                    return string.Equals(targetFolder, profile.Name.Trim(), StringComparison.OrdinalIgnoreCase);
                }
            }

            string profIdNorm = AppSettings.NormalizeGameIdentifier(profile.AppId);
            string profExeNorm = !string.IsNullOrEmpty(profile.ExecutablePath)
                ? AppSettings.NormalizeGameIdentifier(profile.ExecutablePath)
                : string.Empty;

            if (!string.IsNullOrEmpty(targetNorm) && string.Equals(profIdNorm, targetNorm, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(targetNorm) && !string.IsNullOrEmpty(profExeNorm) &&
                string.Equals(profExeNorm, targetNorm, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        /// <summary>
        /// Saves an AppProfile and synchronizes Gaming TrackedGames + GameOverrides atomically.
        /// Guarantees that an app belongs to strictly one category (Gaming OR General App),
        /// preserves all customization settings across category changes, and avoids duplicates.
        /// </summary>
        public static void ApplyCategorySync(
            AppProfile profile,
            ProfileService? profileService,
            SettingsService? settingsService,
            bool customGlowEnabled = true,
            string? originalIdentifier = null)
        {
            if (profile == null) return;

            bool isGaming = IsGamingCategory(profile.Category);
            profile.Category = isGaming ? CategoryGaming : CategoryGeneral;

            string primaryId = GetPrimaryIdentifier(profile);
            string normPrimary = AppSettings.NormalizeGameIdentifier(primaryId);
            string normAppId = AppSettings.NormalizeGameIdentifier(profile.AppId);
            string normOrig = !string.IsNullOrWhiteSpace(originalIdentifier)
                ? AppSettings.NormalizeGameIdentifier(originalIdentifier)
                : string.Empty;

            // 1. Update ProfileService (removing old entry if identifier was renamed)
            if (profileService != null)
            {
                if (!string.IsNullOrWhiteSpace(originalIdentifier))
                {
                    var oldMatch = profileService.Profiles.FirstOrDefault(p =>
                        MatchesIdentifier(p, originalIdentifier) && !MatchesIdentifier(p, primaryId));
                    if (oldMatch != null)
                    {
                        profileService.RemoveProfile(oldMatch.AppId);
                    }
                }
                profileService.AddOrUpdateProfile(profile);
            }

            // 2. Synchronize SettingsService (TrackedGames & GameOverrides)
            if (settingsService != null)
            {
                var settings = settingsService.Current;
                settings.TrackedGames ??= new List<string>();
                settings.GameOverrides ??= new Dictionary<string, GameGlowOverride>(StringComparer.OrdinalIgnoreCase);

                if (isGaming)
                {
                    // Remove stale renamed entry if identifier changed
                    if (!string.IsNullOrEmpty(normOrig) && !string.Equals(normOrig, normPrimary, StringComparison.OrdinalIgnoreCase))
                    {
                        settings.TrackedGames.RemoveAll(g =>
                            string.Equals(AppSettings.NormalizeGameIdentifier(g), normOrig, StringComparison.OrdinalIgnoreCase));
                        settings.GameOverrides.Remove(normOrig);
                    }

                    // Ensure single unique entry in TrackedGames
                    int existingIdx = settings.TrackedGames.FindIndex(g =>
                    {
                        string gNorm = AppSettings.NormalizeGameIdentifier(g);
                        return (!string.IsNullOrEmpty(normPrimary) && string.Equals(gNorm, normPrimary, StringComparison.OrdinalIgnoreCase)) ||
                               (!string.IsNullOrEmpty(normAppId) && string.Equals(gNorm, normAppId, StringComparison.OrdinalIgnoreCase));
                    });

                    if (existingIdx >= 0)
                    {
                        // Upgrade filename-only entry to full executable path if available
                        if (!string.IsNullOrWhiteSpace(profile.ExecutablePath))
                        {
                            settings.TrackedGames[existingIdx] = profile.ExecutablePath;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(primaryId))
                    {
                        settings.TrackedGames.Add(primaryId);
                    }

                    // Sync GameGlowOverride so in-game glow uses all customized parameters
                    if (!string.IsNullOrEmpty(normPrimary))
                    {
                        var ov = new GameGlowOverride
                        {
                            GameIdentifier = normPrimary,
                            CustomGlowEnabled = customGlowEnabled,
                            ColorHex = profile.ColorHex,
                            DurationMs = GameGlowOverride.ClampDuration(profile.DurationMs),
                            Intensity = GameGlowOverride.ClampIntensity(profile.Intensity),
                            GlowSize = GameGlowOverride.ClampGlowSize(profile.GlowSize),
                            CornerRadius = GameGlowOverride.ClampCornerRadius(profile.CornerRadius),
                            Style = profile.Style
                        };
                        settings.GameOverrides[normPrimary] = ov;
                    }
                }
                else
                {
                    // Moved to General App: remove from TrackedGames and GameOverrides
                    var normsToRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(normPrimary)) normsToRemove.Add(normPrimary);
                    if (!string.IsNullOrEmpty(normAppId)) normsToRemove.Add(normAppId);
                    if (!string.IsNullOrEmpty(normOrig)) normsToRemove.Add(normOrig);

                    settings.TrackedGames.RemoveAll(g =>
                        normsToRemove.Contains(AppSettings.NormalizeGameIdentifier(g)));

                    foreach (var n in normsToRemove)
                    {
                        settings.GameOverrides.Remove(n);
                    }
                }

                settingsService.Save(settings);
            }
        }

        /// <summary>
        /// Safely removes a game from TrackedGames, GameOverrides, and any Gaming profile in ProfileService.
        /// </summary>
        public static void RemoveGameCompletely(string gameEntry, ProfileService? profileService, SettingsService? settingsService)
        {
            if (string.IsNullOrWhiteSpace(gameEntry)) return;
            string normId = AppSettings.NormalizeGameIdentifier(gameEntry);

            if (settingsService != null)
            {
                var settings = settingsService.Current;
                settings.TrackedGames?.RemoveAll(g =>
                    string.Equals(g, gameEntry, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(normId) && string.Equals(AppSettings.NormalizeGameIdentifier(g), normId, StringComparison.OrdinalIgnoreCase)));
                if (!string.IsNullOrEmpty(normId))
                {
                    settings.GameOverrides?.Remove(normId);
                }
                settingsService.Save(settings);
            }

            if (profileService != null)
            {
                var matchingProfiles = profileService.Profiles
                    .Where(p => MatchesIdentifier(p, gameEntry) && IsGamingCategory(p.Category))
                    .ToList();
                foreach (var p in matchingProfiles)
                {
                    profileService.RemoveProfile(p.AppId);
                }
            }
        }

        /// <summary>
        /// Ensures backward compatibility with older settings.json files that may have saved Grid mode.
        /// Applications page now exclusively uses List view.
        /// </summary>
        public static ApplicationViewMode NormalizeApplicationsViewMode(AppSettings? settings)
        {
            if (settings != null && settings.ApplicationsViewMode != ApplicationViewMode.List)
            {
                settings.ApplicationsViewMode = ApplicationViewMode.List;
            }
            return ApplicationViewMode.List;
        }
    }
}
