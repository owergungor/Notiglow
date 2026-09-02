using System.Windows;
using System.Windows.Controls;
using GlowBorder.Models;
using GlowBorder.Services;
using UserControl = System.Windows.Controls.UserControl;

namespace GlowBorder.UI.Views
{
    public partial class NotificationsView : UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;

        public NotificationsView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;

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
        }

        private void BurstMode_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            BurstMode mode = BurstMode.Restart;
            if (RadExtend.IsChecked == true) mode = BurstMode.Extend;
            else if (RadQueue.IsChecked == true) mode = BurstMode.Queue;
            else if (RadIgnore.IsChecked == true) mode = BurstMode.Ignore;

            var settings = _settingsService.Current;
            settings.BurstMode = mode;
            _settingsService.Save(settings);
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
