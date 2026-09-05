using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NotiGlow.Models;

namespace NotiGlow.Services
{
    public class TrayService : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly SettingsService _settingsService;
        private readonly Action _openSettingsAction;
        private readonly Action _testAnimationAction;
        private readonly Action _exitAppAction;

        private ToolStripMenuItem _enableMenuItem = null!;
        private ToolStripMenuItem _gamingMenuItem = null!;

        public bool IsVisible => _notifyIcon.Visible;
        public Icon? CurrentIcon => _notifyIcon.Icon;

        public TrayService(
            SettingsService settingsService,
            Action openSettingsAction,
            Action testAnimationAction,
            Action exitAppAction)
        {
            _settingsService = settingsService;
            _openSettingsAction = openSettingsAction;
            _testAnimationAction = testAnimationAction;
            _exitAppAction = exitAppAction;

            var icon = LoadTrayIcon();

            _notifyIcon = new NotifyIcon
            {
                Text = "NotiGlow - Ambient Notification Utility",
                Icon = icon
            };

            BuildContextMenu();

            _notifyIcon.DoubleClick += (s, e) => _openSettingsAction();
            _settingsService.SettingsChanged += OnSettingsChanged;

            _notifyIcon.Visible = true;
        }

        private static Icon LoadTrayIcon()
        {
            // 1. Try WPF Pack URI resource (embedded in assembly)
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/NotiGlow.ico", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo?.Stream != null)
                {
                    using (streamInfo.Stream)
                    {
                        var icon = new Icon(streamInfo.Stream);
                        LoggerService.LogInfo("Tray icon successfully loaded from pack URI resource.");
                        return icon;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Failed loading tray icon from pack URI: {ex.Message}");
            }

            // 2. Try file from AppDomain.CurrentDomain.BaseDirectory (safe against working directory changes)
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "NotiGlow.ico");
                if (File.Exists(iconPath))
                {
                    var icon = new Icon(iconPath);
                    LoggerService.LogInfo("Tray icon successfully loaded from BaseDirectory/Assets.");
                    return icon;
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Failed loading tray icon from BaseDirectory: {ex.Message}");
            }

            // 3. Try extracting associated icon from the running process executable (.exe)
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null)
                    {
                        LoggerService.LogInfo("Tray icon successfully extracted from running process executable.");
                        return extracted;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.LogWarning($"Failed extracting associated icon from executable: {ex.Message}");
            }

            // 4. Fallback to SystemIcons.Application
            LoggerService.LogWarning("Falling back to SystemIcons.Application for tray icon.");
            return SystemIcons.Application;
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            _enableMenuItem = new ToolStripMenuItem("✓ Glow Enabled", null, (s, e) =>
            {
                var settings = _settingsService.Current;
                settings.MasterEnabled = !_enableMenuItem.Checked;
                _settingsService.Save(settings);
            })
            {
                Checked = _settingsService.Current.MasterEnabled
            };

            _gamingMenuItem = new ToolStripMenuItem("🎮 Gaming Mode", null, (s, e) =>
            {
                var settings = _settingsService.Current;
                settings.GamingModeEnabled = !_gamingMenuItem.Checked;
                _settingsService.Save(settings);
            })
            {
                Checked = _settingsService.Current.GamingModeEnabled
            };

            var testItem = new ToolStripMenuItem("✨ Test Animation", null, (s, e) => _testAnimationAction());
            var settingsItem = new ToolStripMenuItem("⚙️ Open Settings", null, (s, e) => _openSettingsAction());
            var exitItem = new ToolStripMenuItem("❌ Exit", null, (s, e) => _exitAppAction());

            menu.Items.Add(_enableMenuItem);
            menu.Items.Add(_gamingMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(testItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            if (_enableMenuItem != null)
            {
                _enableMenuItem.Checked = _settingsService.Current.MasterEnabled;
            }
            if (_gamingMenuItem != null)
            {
                _gamingMenuItem.Checked = _settingsService.Current.GamingModeEnabled;
            }
        }

        public void ShowNotification(string title, string message)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
