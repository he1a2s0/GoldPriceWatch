using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GoldPriceWatch.Components
{
    /// <summary>
    /// 自定义应用通知内容，用于 HandyControl 的 Notification。
    /// 支持消息、标题、图标和样式自定义。
    /// </summary>
    public partial class AppNotification : UserControl
    {
        public AppNotification(string message)
        {
            InitializeComponent();
            DataContext = this;
            MessageText = message;
            Title = "黄金价格警报";
            // Icon can be set via XAML or code
            Background = new SolidColorBrush(Color.FromRgb(255, 235, 59)); // 黄色背景用于警报
            Foreground = Brushes.Black;
        }

        /// <summary>
        /// 通知标题。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 通知消息。
        /// </summary>
        public string MessageText { get; set; }

        /// <summary>
        /// 通知持续时间（毫秒），0 表示无限。
        /// </summary>
        public int Duration { get; set; } = 5000; // 5秒自动消失
    }
}
