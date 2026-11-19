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
                string norm = AppSettings.NormalizeGameIdentifier(g);
                if (!string.IsNullOrEmpty(norm))
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

            string targetNorm = AppSettings.NormalizeGameIdentifier(identifier);
            string profIdNorm = AppSettings.NormalizeGameIdentifier(profile.AppId);
            string profExeNorm = !string.IsNullOrEmpty(profile.ExecutablePath)
                ? AppSettings.NormalizeGameIdentifier(profile.ExecutablePath)
                : string.Empty;

            if (string.Equals(profile.AppId, identifier, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(profile.ExecutablePath) &&
                string.Equals(profile.ExecutablePath, identifier, StringComparison.OrdinalIgnoreCase)) return true;
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
