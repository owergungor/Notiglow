using System;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NotiGlow.Core.Helpers;
using NotiGlow.Models;
using NotiGlow.Services;

namespace NotiGlow.UI.Views
{
    public sealed partial class DisplayView : Microsoft.UI.Xaml.Controls.UserControl
    {
        private SettingsService? _settingsService;
        private GlowManager? _glowManager;

        public DisplayView()
        {
            this.InitializeComponent();
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            Loaded += (s, e) =>
            {
                ScrollViewerHelper.EnableReliableMouseWheel(RootScrollViewer);
                ScrollViewerHelper.AttachToChildComboBoxes(this);
            };
        }

        public void DetachEvents()
        {
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, string languageCode)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshDetectedMonitors();
                UpdateLocalizedTexts();
            });
        }

        private void UpdateLocalizedTexts()
        {
            TxtDisplayTitle.Text = LocalizationService.Instance.GetString("Display.Title");
            TxtDisplaySubtitle.Text = LocalizationService.Instance.GetString("Display.Subtitle");
            TxtBtnTestAnimation.Text = LocalizationService.Instance.GetString("Common.TestAnimation");
            TxtSectionTargetDisplay.Text = LocalizationService.Instance.GetString("Display.ModeSection");
            TxtActiveMonitor.Text = LocalizationService.Instance.GetString("Display.ActiveMonitor");
            TxtActiveMonitorDesc.Text = LocalizationService.Instance.GetString("Display.ActiveMonitorDesc");
            TxtPrimaryMonitor.Text = LocalizationService.Instance.GetString("Display.PrimaryMonitor");
            TxtPrimaryMonitorDesc.Text = LocalizationService.Instance.GetString("Display.PrimaryMonitorDesc");
            TxtAllMonitors.Text = LocalizationService.Instance.GetString("Display.AllMonitors");
            TxtAllMonitorsDesc.Text = LocalizationService.Instance.GetString("Display.AllMonitorsDesc");
            TxtDetectedDisplays.Text = LocalizationService.Instance.GetString("Display.DetectedMonitors");
        }

        public void Initialize(SettingsService settingsService, GlowManager? glowManager = null)
        {
            _settingsService = settingsService;
            _glowManager = glowManager;
            LoadSettings();
            RefreshDetectedMonitors();
            UpdateLocalizedTexts();
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
                var border = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackground"],
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["BorderColor"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(14)
                };

                var sp = new StackPanel { Spacing = 4 };
                string monitorFormat = LocalizationService.Instance.GetString("Display.MonitorFormat", index, screen.DeviceName);
                string primaryBadge = screen.Primary ? $" {LocalizationService.Instance.GetString("Display.PrimaryBadge")}" : string.Empty;
                string resolutionText = LocalizationService.Instance.GetString("Display.ResolutionFormat", screen.Bounds.Width, screen.Bounds.Height, screen.BitsPerPixel);

                sp.Children.Add(new TextBlock
                {
                    Text = $"{monitorFormat}{primaryBadge}",
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });
                sp.Children.Add(new TextBlock
                {
                    Text = resolutionText,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextSecondary"],
                    FontSize = 12
                });

                border.Child = sp;
                PnlMonitors.Children.Add(border);
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
            if (settings.MonitorMode != mode)
            {
                settings.MonitorMode = mode;
                _settingsService.Save(settings);
            }
        }

        private void BtnTestAnimation_Click(object sender, RoutedEventArgs e)
        {
            if (_glowManager == null || _settingsService == null) return;

            var testProfile = new AppProfile
            {
                AppId = "DisplayTest",
                Name = "Display Test",
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
