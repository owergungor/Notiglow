using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using GlowBorder.Models;
using GlowBorder.Services;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace GlowBorder.UI.Views
{
    public partial class DisplayView : System.Windows.Controls.UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;

        public DisplayView()
        {
            InitializeComponent();
        }

        public void Initialize(SettingsService settingsService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            LoadSettings();
            RefreshDetectedMonitors();
        }

        private void LoadSettings()
        {
            if (_settingsService == null) return;

            switch (_settingsService.Current.MonitorMode)
            {
                case MonitorMode.PrimaryMonitor:
                    RadPrimaryMonitor.IsChecked = true;
                    break;
                case MonitorMode.AllMonitors:
                    RadAllMonitors.IsChecked = true;
                    break;
                case MonitorMode.ActiveMonitor:
                default:
                    RadActiveMonitor.IsChecked = true;
                    break;
            }
        }

        private void RefreshDetectedMonitors()
        {
            PnlMonitors.Children.Clear();
            int index = 1;
            foreach (var screen in Screen.AllScreens)
            {
                var card = new CardControl
                {
                    Margin = new Thickness(0, 0, 0, 4),
                    Padding = new Thickness(14)
                };

                var sp = new StackPanel();
                sp.Children.Add(new TextBlock
                {
                    Text = $"Monitor {index}: {screen.DeviceName} {(screen.Primary ? "(Primary)" : "")}",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"Resolution: {screen.Bounds.Width} x {screen.Bounds.Height} | BitsPerPixel: {screen.BitsPerPixel}",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                card.Header = sp;
                PnlMonitors.Children.Add(card);
                index++;
            }
        }

        private void MonitorMode_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsService == null) return;

            MonitorMode mode = MonitorMode.ActiveMonitor;
            if (RadPrimaryMonitor.IsChecked == true) mode = MonitorMode.PrimaryMonitor;
            else if (RadAllMonitors.IsChecked == true) mode = MonitorMode.AllMonitors;

            var settings = _settingsService.Current;
            settings.MonitorMode = mode;
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
