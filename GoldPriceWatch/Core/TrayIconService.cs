using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace GoldPriceWatch.Core
{
    /// <summary>
    /// 托盘图标服务：拥有 NotifyIcon 与菜单，负责悬浮框显隐、打开持仓监控、配置与退出。
    /// 应用启动时创建即显示托盘图标；退出仅能经托盘菜单触发。
    /// </summary>
    public sealed class TrayIconService : IDisposable
    {
        private readonly WinForms.NotifyIcon _icon;
        private readonly WinForms.ToolStripMenuItem _toggleFloatingItem;
        private readonly WinForms.Timer _clickTimer;

        public TrayIconService()
        {
            _icon = new WinForms.NotifyIcon
            {
                Text = "黄金价格监控",
                Icon = CreateGoldIcon(),
                Visible = true
            };

            var menu = new WinForms.ContextMenuStrip();
            _toggleFloatingItem = new WinForms.ToolStripMenuItem("隐藏悬浮框", null, (_, _) => ToggleFloating());
            menu.Items.Add(_toggleFloatingItem);
            menu.Items.Add("打开持仓监控", null, (_, _) => ToggleHolding());
            menu.Items.Add("监控设置", null, (_, _) => ShowConfig());
            menu.Items.Add(new WinForms.ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => ExitApp());

            // 展开菜单前同步切换项文案
            menu.Opening += (_, _) =>
                _toggleFloatingItem.Text = IsFloatingVisible ? "隐藏悬浮框" : "显示悬浮框";

            _icon.ContextMenuStrip = menu;

            // 单击动作延迟一个双击间隔：若随后发生双击则取消，避免先触发一次切换
            _clickTimer = new WinForms.Timer { Interval = WinForms.SystemInformation.DoubleClickTime + 50 };
            _clickTimer.Tick += (_, _) =>
            {
                _clickTimer.Stop();
                ToggleFloating();
            };

            // 左键单击托盘图标：切换悬浮框显示/隐藏；右键只弹菜单
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button != WinForms.MouseButtons.Left) return;
                _clickTimer.Stop();
                _clickTimer.Start();
            };

            // 左键双击托盘图标：切换持仓监控窗口显示/隐藏
            _icon.MouseDoubleClick += (_, e) =>
            {
                if (e.Button != WinForms.MouseButtons.Left) return;
                _clickTimer.Stop(); // 取消本次双击中已排队的单击动作
                ToggleHolding();
            };
        }

        private static MainWindow Floating => App.Current.Services.GetRequiredService<MainWindow>();

        private static HoldingWindow Holding => App.Current.Services.GetRequiredService<HoldingWindow>();

        private bool IsFloatingVisible
        {
            get
            {
                var w = Floating;
                return w.IsVisible && w.WindowState != WindowState.Minimized;
            }
        }

        public void ToggleFloating()
        {
            if (IsFloatingVisible)
            {
                Floating.Hide();
            }
            else
            {
                ShowFloating();
            }
        }

        public void ShowFloating()
        {
            var w = Floating;
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
        }

        private bool IsHoldingVisible
        {
            get
            {
                var w = Holding;
                return w.IsVisible && w.WindowState != WindowState.Minimized;
            }
        }

        public void ToggleHolding()
        {
            if (IsHoldingVisible)
            {
                Holding.Hide();
                return;
            }
            var w = Holding;
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
        }

        private static void ShowConfig()
        {
            var w = App.Current.Services.GetRequiredService<HoldingWindow>();
            w.Show();
            w.WindowState = WindowState.Normal;
            w.Activate();
            w.SelectTab(1); // 切到"监控提醒"页
        }

        private void ExitApp()
        {
            // 先放开各窗口的关闭拦截，再退出
            App.Current.IsShuttingDown = true;
            _icon.Visible = false;
            _icon.Dispose();
            Application.Current.Shutdown();
        }

        /// <summary>运行时绘制金色 Au 圆形图标，避免依赖外部 ico 文件。</summary>
        private static Drawing.Icon CreateGoldIcon()
        {
            var bmp = new Drawing.Bitmap(32, 32);
            using (var g = Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(0xE0, 0xA8, 0x3E));
                g.FillEllipse(brush, 1, 1, 30, 30);
                using var font = new Drawing.Font("Arial", 11, Drawing.FontStyle.Bold);
                var size = g.MeasureString("Au", font);
                g.DrawString("Au", font, Drawing.Brushes.Black,
                    (32 - size.Width) / 2, (32 - size.Height) / 2);
            }
            return Drawing.Icon.FromHandle(bmp.GetHicon());
        }

        public void Dispose()
        {
            _clickTimer.Dispose();
            _icon.Dispose();
        }
    }
}
