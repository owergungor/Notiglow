using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private readonly Action _updateAction;
        private ToolStripMenuItem _enableMenuItem = null!;
        private ToolStripMenuItem _gamingMenuItem = null!;
        private ToolStripMenuItem _testItem = null!;
        private ToolStripMenuItem _openItem = null!;
        private ToolStripMenuItem _updateItem = null!;
        private ToolStripMenuItem _exitItem = null!;
        private ContextMenuStrip _menu = null!;

        public bool IsVisible => _notifyIcon.Visible;
        public Icon? CurrentIcon => _notifyIcon.Icon;
        internal ContextMenuStrip ContextMenuForTesting => _menu;

        public TrayService(SettingsService settingsService, Action openSettingsAction, Action testAnimationAction, Action exitAppAction, Action? updateAction = null)
        {
            _settingsService = settingsService;
            _openSettingsAction = openSettingsAction;
            _testAnimationAction = testAnimationAction;
            _exitAppAction = exitAppAction;
            _updateAction = updateAction ?? openSettingsAction;
            _notifyIcon = new NotifyIcon { Text = LocalizationService.Instance.GetString("Tray.Tooltip"), Icon = LoadTrayIcon() };
            BuildContextMenu();
            _notifyIcon.DoubleClick += (s, e) => _openSettingsAction();
            _settingsService.SettingsChanged += OnSettingsChanged;
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
            ApplyTheme();
            _notifyIcon.Visible = true;
        }

        private static Icon LoadTrayIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/NotiGlow.ico", UriKind.Absolute);
                var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
                if (stream != null) using (stream) return new Icon(stream);
            }
            catch (Exception ex) { LoggerService.LogWarning($"Failed loading tray icon from pack URI: {ex.Message}"); }
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "NotiGlow.ico");
                if (File.Exists(path)) return new Icon(path);
            }
            catch (Exception ex) { LoggerService.LogWarning($"Failed loading tray icon from BaseDirectory: {ex.Message}"); }
            try
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe) && Icon.ExtractAssociatedIcon(exe) is Icon icon) return icon;
            }
            catch (Exception ex) { LoggerService.LogWarning($"Failed extracting executable icon: {ex.Message}"); }
            return SystemIcons.Application;
        }

        private void BuildContextMenu()
        {
            _menu = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Segoe UI", 9F), Renderer = new TrayMenuRenderer() };
            _openItem = new ToolStripMenuItem(LocalizationService.Instance.GetString("Tray.Show"), null, (s, e) => _openSettingsAction());
            _enableMenuItem = new ToolStripMenuItem(GetNotificationToggleLabel(), null, (s, e) =>
            {
                var settings = _settingsService.Current;
                settings.MasterEnabled = !_enableMenuItem.Checked;
                _settingsService.Save(settings);
            }) { Checked = _settingsService.Current.MasterEnabled, CheckOnClick = false };
            _gamingMenuItem = new ToolStripMenuItem(LocalizationService.Instance.GetString("Tray.GamingMode"), null, (s, e) =>
            {
                var settings = _settingsService.Current;
                settings.GamingModeEnabled = !_gamingMenuItem.Checked;
                _settingsService.Save(settings);
            }) { Checked = _settingsService.Current.GamingModeEnabled };
            _testItem = new ToolStripMenuItem(LocalizationService.Instance.GetString("Tray.TestAnimation"), null, (s, e) => _testAnimationAction());
            _updateItem = new ToolStripMenuItem(GetUpdateLabel(), null, (s, e) =>
            {
                if (PackageService.GetUpdateRoute(PackageService.IsPackaged) == UpdateRoute.MicrosoftStorePage) PackageService.OpenStorePage();
                else _updateAction();
            });
            _exitItem = new ToolStripMenuItem(LocalizationService.Instance.GetString("Tray.Exit"), null, (s, e) => _exitAppAction());
            _menu.Items.AddRange(new ToolStripItem[] { _openItem, new ToolStripSeparator(), _enableMenuItem, _gamingMenuItem, new ToolStripSeparator(), _testItem, _updateItem, new ToolStripSeparator(), _exitItem });
            _notifyIcon.ContextMenuStrip = _menu;
        }

        private string GetNotificationToggleLabel() => LocalizationService.Instance.GetString(_settingsService.Current.MasterEnabled ? "Tray.DisableNotifications" : "Tray.EnableNotifications");
        private string GetUpdateLabel() => LocalizationService.Instance.GetString(PackageService.IsPackaged ? "Advanced.UpdateInStore" : "Advanced.CheckForUpdates");

        private void OnLanguageChanged(object? sender, string language)
        {
            _openItem.Text = LocalizationService.Instance.GetString("Tray.Show");
            _enableMenuItem.Text = GetNotificationToggleLabel();
            _gamingMenuItem.Text = LocalizationService.Instance.GetString("Tray.GamingMode");
            _testItem.Text = LocalizationService.Instance.GetString("Tray.TestAnimation");
            _updateItem.Text = GetUpdateLabel();
            _exitItem.Text = LocalizationService.Instance.GetString("Tray.Exit");
            _notifyIcon.Text = LocalizationService.Instance.GetString("Tray.Tooltip");
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            _enableMenuItem.Checked = _settingsService.Current.MasterEnabled;
            _enableMenuItem.Text = GetNotificationToggleLabel();
            _gamingMenuItem.Checked = _settingsService.Current.GamingModeEnabled;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var settings = _settingsService.Current;
            var palette = ThemeService.GetPalette(settings.ColorTheme, ThemeService.GetIsDark(settings.ThemeMode));
            var renderer = (TrayMenuRenderer)_menu.Renderer!;
            renderer.BackColor = ParseColor(palette.CardBackground);
            renderer.ForeColor = ParseColor(palette.TextPrimary);
            renderer.BorderColor = ParseColor(palette.BorderColor);
            renderer.HoverColor = ParseColor(palette.ControlHoverBackground);
            renderer.SelectedColor = ParseColor(palette.DropdownSelectedBackground);
            _menu.BackColor = renderer.BackColor;
            _menu.ForeColor = renderer.ForeColor;
            foreach (ToolStripItem item in _menu.Items) { item.BackColor = renderer.BackColor; item.ForeColor = renderer.ForeColor; }
            _menu.Invalidate();
        }

        public void RefreshTheme() => ApplyTheme();

        private static Color ParseColor(string value) => ColorTranslator.FromHtml(value);

        public void ShowNotification(string title, string message) => _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);

        public void Dispose()
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
            LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
        }

        private sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
        {
            public Color BackColor { get; set; } = Color.FromArgb(23, 23, 23);
            public Color ForeColor { get; set; } = Color.White;
            public Color BorderColor { get; set; } = Color.DimGray;
            public Color HoverColor { get; set; } = Color.FromArgb(41, 41, 41);
            public Color SelectedColor { get; set; } = Color.DodgerBlue;
            public TrayMenuRenderer() : base(new ProfessionalColorTable()) { }
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { using var brush = new SolidBrush(BackColor); e.Graphics.FillRectangle(brush, e.AffectedBounds); }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using var pen = new Pen(BorderColor); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var bounds = new Rectangle(Point.Empty, e.Item.Size);
                using var brush = new SolidBrush(e.Item.Selected ? (e.Item.Enabled ? HoverColor : BackColor) : BackColor);
                e.Graphics.FillRectangle(brush, bounds);
                if (e.Item.Selected) { using var accent = new SolidBrush(SelectedColor); e.Graphics.FillRectangle(accent, new Rectangle(0, 3, 3, Math.Max(0, bounds.Height - 6))); }
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? ForeColor : SystemColors.GrayText; base.OnRenderItemText(e); }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int y = e.Item.Height / 2; using var pen = new Pen(BorderColor); e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }
        }
    }
}
