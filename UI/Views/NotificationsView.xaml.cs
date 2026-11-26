using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.UI.Views
{
    public sealed partial class NotificationsView : UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;
        private bool _isInitializing = false;

        public NotificationsView()
        {
            this.InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(RootScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);
                UpdateLocalizedTexts();
            };
        }

        public void DetachEvents()
        {
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        }

        public void Initialize(SettingsService settingsService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            LoadSettings();
            UpdateLocalizedTexts();
        }

        private void OnLanguageChanged(object? sender, string langCode)
        {
            DispatcherQueue.TryEnqueue(UpdateLocalizedTexts);
        }

        private void UpdateLocalizedTexts()
        {
            TxtNotificationsTitle.Text = LocalizationService.Instance.GetString("Notifications.Title");
            TxtNotificationsSubtitle.Text = LocalizationService.Instance.GetString("Notifications.Subtitle");
            TxtBtnTestAnimation.Text = LocalizationService.Instance.GetString("Common.TestAnimation");
            ToolTipService.SetToolTip(BtnTestAnimation, LocalizationService.Instance.GetString("Common.TestAnimationToolTip"));

            TxtSectionBurstMode.Text = LocalizationService.Instance.GetString("Notifications.OverlapSection");
            TxtRestartTitle.Text = LocalizationService.Instance.GetString("Notifications.Restart");
            TxtRestartDesc.Text = LocalizationService.Instance.GetString("Notifications.RestartDesc");

            TxtExtendTitle.Text = LocalizationService.Instance.GetString("Notifications.Extend");
            TxtExtendDesc.Text = LocalizationService.Instance.GetString("Notifications.ExtendDesc");

            TxtQueueTitle.Text = LocalizationService.Instance.GetString("Notifications.Queue");
            TxtQueueDesc.Text = LocalizationService.Instance.GetString("Notifications.QueueDesc");

            TxtIgnoreTitle.Text = LocalizationService.Instance.GetString("Notifications.Ignore");
            TxtIgnoreDesc.Text = LocalizationService.Instance.GetString("Notifications.IgnoreDesc");
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;
            _isInitializing = true;

            switch (_settingsService.Current.BurstMode)
            {
                case BurstMode.Extend:
                    RadExtend.IsChecked = true;
                    break;
                case BurstMode.Queue:
                    RadQueue.IsChecked = true;
                    break;
                case BurstMode.Ignore:
                    RadIgnore.IsChecked = true;
                    break;
                case BurstMode.Restart:
                default:
                    RadRestart.IsChecked = true;
                    break;
            }

            _isInitializing = false;
        }

        private void BurstMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _settingsService == null) return;

            BurstMode mode = BurstMode.Restart;
            if (RadExtend.IsChecked == true) mode = BurstMode.Extend;
            else if (RadQueue.IsChecked == true) mode = BurstMode.Queue;
            else if (RadIgnore.IsChecked == true) mode = BurstMode.Ignore;

            var settings = _settingsService.Current;
            if (settings.BurstMode != mode)
            {
                settings.BurstMode = mode;
                _settingsService.Save(settings);
            }
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;
            var testProfile = new AppProfile
            {
                AppId = "TestApp",
                Name = "NotiGlow Test",
                ColorHex = _settingsService.Current.DefaultColorHex,
                DurationMs = _settingsService.Current.DefaultDurationMs,
                Intensity = _settingsService.Current.DefaultIntensity,
                Style = _settingsService.Current.DefaultStyle,
                Thickness = _settingsService.Current.DefaultThickness,
                GlowSize = _settingsService.Current.DefaultGlowSize
            };
            _glowManager.TriggerProfile(testProfile);
        }
    }
}
