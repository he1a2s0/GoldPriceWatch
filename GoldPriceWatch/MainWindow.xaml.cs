using GoldPriceWatch.Core;
using GoldPriceWatch.Model;
using GoldPriceWatch.ModelView;
using MediatR;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double MinimumVisibleWidth = 60d;
        private const double MinimumVisibleHeight = 40d;

        private readonly GoldInvestmentConfig _config;
        private GoldModelView? _viewModel;
        private Point _startPoint;

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
            SaveWindowPosition();
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

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.ExitApp();
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            new GoldInvestmentWindow().Show();
        }
    }
}