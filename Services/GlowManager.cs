using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using GlowBorder.Core.Win32;
using GlowBorder.Models;
using GlowBorder.Overlay;

namespace GlowBorder.Services
{
    public class GlowManager
    {
        private readonly SettingsService _settingsService;
        private readonly ProfileService _profileService;
        private readonly GameDetectionService _gameDetectionService;
        private readonly NotificationDeduplicator _deduplicator = new();
        private readonly Dictionary<string, OverlayWindow> _activeOverlayWindows = new();

        private bool _isAnimating = false;
        private AppProfile? _activeProfile = null;
        private readonly Queue<AppProfile> _notificationQueue = new();

        public bool IsAnimating => _isAnimating;

        public GlowManager(SettingsService settingsService, ProfileService profileService)
        {
            _settingsService = settingsService;
            _profileService = profileService;
            _gameDetectionService = new GameDetectionService(settingsService);
        }

        public void TriggerNotification(NotificationItem notification)
        {
            if (!_settingsService.Current.MasterEnabled) return;

            // Notification Deduplication
            if (_deduplicator.IsDuplicate(notification))
            {
                LoggerService.LogInfo($"Duplicate notification suppressed: AppId='{notification.AppId}', AppName='{notification.AppName}'");
                return;
            }

            var profile = _profileService.GetProfile(notification.AppId, notification.AppName);
            
            if (profile == null)
            {
                LoggerService.LogInfo($"Notification from untracked app ignored: AppId='{notification.AppId}', AppName='{notification.AppName}'");
                return;
            }

            if (!profile.Enabled)
            {
                LoggerService.LogInfo($"Notification from disabled profile ignored: {profile.Name}");
                return;
            }

            TriggerProfile(profile);
        }

        public void TriggerProfile(AppProfile profile)
        {
            if (!_settingsService.Current.MasterEnabled) return;

            var settings = _settingsService.Current;

            // Gaming Mode Evaluation
            if (_gameDetectionService.IsGameRunning())
            {
                LoggerService.LogInfo($"Active Game detected ({_gameDetectionService.ActiveGameName}) during trigger");
                if (!settings.GlowDuringGames)
                {
                    LoggerService.LogInfo("Glow suppressed due to Gaming Mode settings");
                    return;
                }

                if (settings.OnlyImportantInGames && profile.Priority != NotificationPriority.High)
                {
                    LoggerService.LogInfo("Non-high priority notification suppressed in Gaming Mode");
                    return;
                }
            }

            // Apply adjustments (Gaming multipliers, OLED Mode, Accessibility)
            profile = AdjustProfileParameters(profile, settings);

            // Handle Burst Mode logic
            if (_isAnimating)
            {
                switch (settings.BurstMode)
                {
                    case BurstMode.Ignore:
                        LoggerService.LogInfo($"BurstMode IGNORE: Dropped glow for {profile.Name}");
                        return;

                    case BurstMode.Queue:
                        _notificationQueue.Enqueue(profile);
                        LoggerService.LogInfo($"BurstMode QUEUE: Queued glow for {profile.Name}");
                        return;

                    case BurstMode.Extend:
                        LoggerService.LogInfo($"BurstMode EXTEND: Extending glow for {profile.Name}");
                        break;

                    case BurstMode.Restart:
                    default:
                        LoggerService.LogInfo($"BurstMode RESTART: Restarting glow for {profile.Name}");
                        StopAllOverlays();
                        break;
                }
            }

            PlayGlowInternal(profile);
        }

        private AppProfile AdjustProfileParameters(AppProfile original, AppSettings settings)
        {
            double intensity = original.Intensity;
            int duration = original.DurationMs;
            GlowStyle style = original.Style;

            // Gaming mode scaling
            if (settings.GamingModeEnabled && _gameDetectionService.IsGameRunning())
            {
                if (settings.ReduceIntensityInGames)
                {
                    intensity *= Math.Clamp(settings.GamingIntensityMultiplier, 0.2, 1.0);
                }
                if (settings.ReduceDurationInGames)
                {
                    duration = (int)(duration * Math.Clamp(settings.GamingDurationMultiplier, 0.2, 1.0));
                }
            }

            // OLED Mode scaling
            if (settings.OledMode)
            {
                intensity = Math.Min(intensity, 0.5);
            }

            // Accessibility Reduce Glow & Reduce Motion
            if (settings.ReduceGlow)
            {
                intensity = Math.Min(intensity, 0.35);
            }
            if (settings.ReduceMotion || settings.ReduceAnimations)
            {
                if (style == GlowStyle.Sweep || style == GlowStyle.Comet || style == GlowStyle.Ripple)
                {
                    style = GlowStyle.Ambient;
                }
                duration = Math.Min(duration, 2500);
            }

            return new AppProfile
            {
                AppId = original.AppId,
                Name = original.Name,
                Enabled = original.Enabled,
                ColorHex = original.ColorHex,
                DurationMs = Math.Max(500, duration),
                Intensity = Math.Clamp(intensity, 0.05, 1.0),
                Thickness = original.Thickness,
                GlowSize = original.GlowSize,
                Style = style,
                Priority = original.Priority,
                Speed = original.Speed,
                CoreBrightness = original.CoreBrightness,
                TrailLength = original.TrailLength
            };
        }

        private void PlayGlowInternal(AppProfile profile)
        {
            _isAnimating = true;
            _activeProfile = profile;

            List<Screen> targetScreens = GetTargetScreens();

            int completedCount = 0;
            Action onSingleFinished = () =>
            {
                completedCount++;
                if (completedCount >= targetScreens.Count)
                {
                    _isAnimating = false;
                    _activeProfile = null;

                    if (_notificationQueue.Count > 0)
                    {
                        var nextProfile = _notificationQueue.Dequeue();
                        PlayGlowInternal(nextProfile);
                    }
                }
            };

            foreach (var screen in targetScreens)
            {
                string key = screen.DeviceName;
                if (!_activeOverlayWindows.TryGetValue(key, out var overlayWin))
                {
                    overlayWin = new OverlayWindow(screen);
                    _activeOverlayWindows[key] = overlayWin;
                }

                overlayWin.PlayGlow(profile, onSingleFinished);
            }
        }

        private List<Screen> GetTargetScreens()
        {
            var mode = _settingsService.Current.MonitorMode;
            var screens = new List<Screen>();

            switch (mode)
            {
                case MonitorMode.PrimaryMonitor:
                    screens.Add(MonitorHelper.GetPrimaryScreen());
                    break;

                case MonitorMode.AllMonitors:
                    screens.AddRange(MonitorHelper.GetAllScreens());
                    break;

                case MonitorMode.ActiveMonitor:
                default:
                    screens.Add(MonitorHelper.GetActiveScreen());
                    break;
            }

            return screens;
        }

        public void StopAllOverlays()
        {
            _notificationQueue.Clear();
            foreach (var win in _activeOverlayWindows.Values)
            {
                win.StopGlow();
            }
            _isAnimating = false;
            _activeProfile = null;
        }
    }
}
