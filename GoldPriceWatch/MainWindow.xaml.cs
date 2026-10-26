using GoldPriceWatch.Model;
using GoldPriceWatch.ModelView;
using Microsoft.Extensions.DependencyInjection;
using System;
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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        public static void HideFromAltTab(Window window)
        {
            var helper = new WindowInteropHelper(window);
            var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
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
        private const double MinimumVisibleWidth = 60d;
        private const double MinimumVisibleHeight = 40d;

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

            var position = GetSafeWindowPosition(_config.MainWindowLeft.Value, _config.MainWindowTop.Value);
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = position.X;
            Top = position.Y;
        }

        private void SaveWindowPosition()
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            _config.MainWindowLeft = Left;
            _config.MainWindowTop = Top;
        }

        private Point GetSafeWindowPosition(double left, double top)
        {
            var width = ActualWidth;
            if (double.IsNaN(width) || width <= 0)
            {
                width = Width;
            }
            if (double.IsNaN(width) || width <= 0)
            {
                width = MinimumVisibleWidth;
            }

            var height = ActualHeight;
            if (double.IsNaN(height) || height <= 0)
            {
                height = Height;
            }
            if (double.IsNaN(height) || height <= 0)
            {
                height = MinimumVisibleHeight;
            }

            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

            var minLeft = virtualLeft - Math.Max(0d, width - MinimumVisibleWidth);
            var maxLeft = virtualRight - MinimumVisibleWidth;
            var minTop = virtualTop - Math.Max(0d, height - MinimumVisibleHeight);
            var maxTop = virtualBottom - MinimumVisibleHeight;

            var safeLeft = Math.Min(Math.Max(left, minLeft), maxLeft);
            var safeTop = Math.Min(Math.Max(top, minTop), maxTop);

            return new Point(safeLeft, safeTop);
        }

        Point _pressedPosition;
        bool _isDragMoved = false;

        void Window_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _pressedPosition = e.GetPosition(this);
        }

        void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed && _pressedPosition != e.GetPosition(this))
            {
                _isDragMoved = true;
                DragMove();
            }
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
    }
}
