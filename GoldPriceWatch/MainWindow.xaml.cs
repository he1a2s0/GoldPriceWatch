using GoldPriceWatch.Model;
using GoldPriceWatch.ModelView;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GoldPriceWatch
{
    public static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        public static void HideFromAltTab(Window window)
        {
            var helper = new WindowInteropHelper(window);
            var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
        }

        /// <summary>窗口在屏幕上的物理像素矩形。</summary>
        public static RECT GetPhysicalRect(Window window)
        {
            GetWindowRect(new WindowInteropHelper(window).Handle, out var rect);
            return rect;
        }

        /// <summary>各显示器在虚拟桌面中的物理像素矩形。</summary>
        public static List<RECT> GetMonitorRects()
        {
            var monitors = new List<RECT>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT rect, IntPtr dwData) =>
                {
                    monitors.Add(rect);
                    return true;
                }, IntPtr.Zero);
            return monitors;
        }

        /// <summary>按物理像素移动窗口（不改尺寸、不改层级）。</summary>
        public static void MoveToPhysical(Window window, int left, int top)
        {
            SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, left, top, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }
}

namespace GoldPriceWatch
{
    /// <summary>
    /// 悬浮框：无边框透明置顶窗体，显示当前金价与盈亏。
    /// 关闭时隐藏到托盘（仅能经托盘菜单退出）。
    /// </summary>
    public partial class MainWindow : Window
    {
        // 至少保留可见的物理像素尺寸
        private const int MinimumVisibleWidth = 60;
        private const int MinimumVisibleHeight = 40;

        private readonly GoldInvestmentConfig _config;
        private GoldModelView? _viewModel;

        public MainWindow(GoldModelView MainModel, GoldInvestmentConfig config)
        {
            InitializeComponent();
            DataContext = _viewModel = MainModel;
            _config = config;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowHelper.HideFromAltTab(this);
            RestoreWindowPosition();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 托盘菜单退出时放行；否则关闭即隐藏到托盘
            if (App.Current.IsShuttingDown)
            {
                SaveWindowPosition();
                return;
            }

            e.Cancel = true;
            SaveWindowPosition();
            Hide();
        }

        private void RestoreWindowPosition()
        {
            if (!_config.MainWindowLeft.HasValue || !_config.MainWindowTop.HasValue)
            {
                return;
            }

            // 位置以屏幕物理像素保存：逐显示器 DPI 感知下 DIP 与像素的换算随所在显示器变化，
            // 用 DIP 保存的坐标换到另一块屏幕后会落到屏幕外（悬浮框"消失"）
            var rect = WindowHelper.GetPhysicalRect(this);
            var width = Math.Max(rect.Right - rect.Left, MinimumVisibleWidth);
            var height = Math.Max(rect.Bottom - rect.Top, MinimumVisibleHeight);

            var savedLeft = (int)_config.MainWindowLeft.Value;
            var savedTop = (int)_config.MainWindowTop.Value;

            var monitors = WindowHelper.GetMonitorRects();
            if (monitors.Count == 0)
            {
                return;
            }

            // 选保存位置所在的显示器；不在任何显示器上时选距离最近的
            var target = monitors[0];
            long nearestDistance = long.MaxValue;
            foreach (var monitor in monitors)
            {
                if (savedLeft >= monitor.Left && savedLeft < monitor.Right &&
                    savedTop >= monitor.Top && savedTop < monitor.Bottom)
                {
                    target = monitor;
                    break;
                }

                var dx = Math.Max(0, Math.Max(monitor.Left - savedLeft, savedLeft - monitor.Right));
                var dy = Math.Max(0, Math.Max(monitor.Top - savedTop, savedTop - monitor.Bottom));
                var distance = (long)dx * dx + (long)dy * dy;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    target = monitor;
                }
            }

            // 只在目标显示器内挪动，保证整个悬浮框可见
            var maxLeft = Math.Max(target.Left, target.Right - width);
            var maxTop = Math.Max(target.Top, target.Bottom - height);
            var left = Math.Min(Math.Max(savedLeft, target.Left), maxLeft);
            var top = Math.Min(Math.Max(savedTop, target.Top), maxTop);

            WindowHelper.MoveToPhysical(this, left, top);

            if (left != savedLeft || top != savedTop)
            {
                // 位置被修正过（例如显示器拔掉/换分辨率），回写修正后的坐标
                _config.MainWindowLeft = left;
                _config.MainWindowTop = top;
            }
        }

        private void SaveWindowPosition()
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            // 同样按物理像素保存，避免跨 DPI 显示器时坐标失真
            var rect = WindowHelper.GetPhysicalRect(this);
            _config.MainWindowLeft = rect.Left;
            _config.MainWindowTop = rect.Top;
        }

        Point _pressedPosition;
        bool _isDragMoved = false;

        void Window_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 双击：切换持仓监控窗体显示/隐藏（与托盘双击一致）
            if (e.ClickCount == 2)
            {
                if (App.Current.Services.GetService(typeof(Core.TrayIconService)) is Core.TrayIconService tray)
                {
                    tray.ToggleHolding();
                }
                e.Handled = true;
                return;
            }

            _pressedPosition = e.GetPosition(this);
        }

        void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            // 超过系统拖动阈值才开始拖动，避免单击/双击时的微小位移误触发 DragMove
            var current = e.GetPosition(this);
            if (Math.Abs(current.X - _pressedPosition.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(current.Y - _pressedPosition.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            _isDragMoved = true;
            DragMove();
        }

        void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragMoved)
            {
                _isDragMoved = false;
                SaveWindowPosition();
                e.Handled = true;
            }
        }

        private void MenuOpenConfig_Click(object sender, RoutedEventArgs e)
        {
            var window = App.Current.Services.GetRequiredService<HoldingWindow>();
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
            window.SelectTab(1); // 切到"监控提醒"页
        }

        private void MenuOpenHolding_Click(object sender, RoutedEventArgs e)
        {
            var window = App.Current.Services.GetRequiredService<HoldingWindow>();
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }

        private void MenuHideFloating_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            // 与托盘菜单"退出"一致：先放开各窗口的关闭拦截，再退出并清理托盘图标
            App.Current.IsShuttingDown = true;
            if (App.Current.Services.GetService(typeof(Core.TrayIconService)) is Core.TrayIconService tray)
            {
                tray.Dispose();
            }
            Application.Current.Shutdown();
        }
    }
}
