using System;
using System.Windows;
using Windows.UI.Notifications.Management;
using GlowBorder.Models;
using GlowBorder.Services;

namespace GlowBorder.UI
{
    public partial class OnboardingWindow : Wpf.Ui.Controls.FluentWindow
    {
        private readonly NotificationService _notificationService;
        private readonly GlowManager _glowManager;
        private readonly SettingsService _settingsService;
        private int _currentStep = 1;

        public OnboardingWindow(NotificationService notificationService, GlowManager glowManager, SettingsService settingsService)
        {
            InitializeComponent();
            _notificationService = notificationService;
            _glowManager = glowManager;
            _settingsService = settingsService;

            UpdateStepUI();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (Owner == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private async void BtnGrantAccess_Click(object sender, RoutedEventArgs e)
        {
            var status = await _notificationService.RequestAccessAsync();
            if (status == UserNotificationListenerAccessStatus.Allowed)
            {
                TxtOnboardingAccessStatus.Text = "Permission Status: Granted!";
                TxtOnboardingAccessStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                NotificationService.OpenWindowsNotificationSettings();
            }
        }

        private void BtnOnboardingTest_Click(object sender, RoutedEventArgs e)
        {
            var testProfile = new AppProfile
            {
                AppId = "OnboardingTest",
                Name = "Onboarding Demo",
                ColorHex = "#5865F2",
                DurationMs = 4000,
                Intensity = 0.8,
                Thickness = 4,
                GlowSize = 30,
                Style = GlowStyle.Pulse
            };

            _glowManager.TriggerProfile(testProfile);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepUI();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < 3)
            {
                _currentStep++;
                UpdateStepUI();
            }
            else
            {
                // Finish wizard
                var settings = _settingsService.Current;
                settings.FirstRunCompleted = true;
                _settingsService.Save(settings);

                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try
            {
                var settings = _settingsService.Current;
                if (!settings.FirstRunCompleted)
                {
                    settings.FirstRunCompleted = true;
                    _settingsService.Save(settings);
                }
            }
            catch { }
        }

        private void UpdateStepUI()
        {
            Step1.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;

            BtnBack.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Content = _currentStep == 3 ? "Get Started" : "Next Step";
        }
    }
}
