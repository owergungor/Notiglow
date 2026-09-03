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

            _notifyIcon = new NotifyIcon
            {
                Text = "NotiGlow - Ambient Notification Utility",
                Visible = true,
                Icon = SystemIcons.Application
            };

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new Icon(iconPath);
                }
            }
            catch
            {
                // Fallback to SystemIcons.Application
            }

            BuildContextMenu();

            _notifyIcon.DoubleClick += (s, e) => _openSettingsAction();
            _settingsService.SettingsChanged += OnSettingsChanged;
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
