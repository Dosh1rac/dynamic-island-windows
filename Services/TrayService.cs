using System;
using System.Drawing;
using System.Windows.Forms;
using DynamicIsland.Helpers;

namespace DynamicIsland.Services
{
    public interface ITrayService : IDisposable
    {
        void Initialize(Action onToggleVisible, Action onToggleClickThrough, Action onPickCity, Action onPickTheme, Action onExit);
        void UpdateAutoStartStatus();
    }

    public class TrayService : ITrayService
    {
        private NotifyIcon? _notifyIcon;
        private ToolStripMenuItem? _autoStartMenuItem;

        public void Initialize(Action onToggleVisible, Action onToggleClickThrough, Action onPickCity, Action onPickTheme, Action onExit)
        {
            var contextMenu = new ContextMenuStrip();

            var toggleItem = new ToolStripMenuItem("Показать / Скрыть остров", null, (s, e) => onToggleVisible());
            toggleItem.Font = new Font(toggleItem.Font, FontStyle.Bold);

            var clickThroughItem = new ToolStripMenuItem("Сквозной клик (Ctrl+Shift+I)", null, (s, e) => onToggleClickThrough());

            var themeItem = new ToolStripMenuItem("🎨 Оформление и жидкое стекло...", null, (s, e) => onPickTheme());

            var cityItem = new ToolStripMenuItem("📍 Сменить город погоды...", null, (s, e) => onPickCity());

            _autoStartMenuItem = new ToolStripMenuItem("Автозапуск с Windows", null, (s, e) =>
            {
                bool newState = !AutoStartHelper.IsAutoStartEnabled();
                AutoStartHelper.SetAutoStart(newState);
                UpdateAutoStartStatus();
            });
            UpdateAutoStartStatus();

            var exitItem = new ToolStripMenuItem("Выход", null, (s, e) => onExit());

            contextMenu.Items.Add(toggleItem);
            contextMenu.Items.Add(clickThroughItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(themeItem);
            contextMenu.Items.Add(cityItem);
            contextMenu.Items.Add(_autoStartMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            // Создаем стильную минималистичную иконку капсулы для трея программно
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(20, 20, 20));
                g.FillEllipse(brush, 4, 10, 24, 12);
                using var eyeBrush = new SolidBrush(System.Drawing.Color.White);
                g.FillEllipse(eyeBrush, 9, 13, 5, 5);
                g.FillEllipse(eyeBrush, 18, 13, 5, 5);
            }
            var iconHandle = bitmap.GetHicon();
            var icon = Icon.FromHandle(iconHandle);

            _notifyIcon = new NotifyIcon
            {
                Icon = icon,
                Text = "Dynamic Island",
                Visible = true,
                ContextMenuStrip = contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) => onToggleVisible();
        }

        public void UpdateAutoStartStatus()
        {
            if (_autoStartMenuItem != null)
            {
                _autoStartMenuItem.Checked = AutoStartHelper.IsAutoStartEnabled();
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
