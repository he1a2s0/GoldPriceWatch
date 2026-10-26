using GoldPriceWatch.ModelView;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Windows;

namespace GoldPriceWatch
{
    /// <summary>
    /// 持仓监控窗口：买卖流水、费率、监控设置、统计与价格走势图。
    /// 关闭时隐藏到托盘，可经托盘菜单再次打开。
    /// </summary>
    public partial class HoldingWindow : Window
    {
        public HoldingWindow()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<HoldingViewModel>();
            Closing += HoldingWindow_Closing;
        }

        private void HoldingWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 托盘菜单退出时放行；否则隐藏到托盘（关闭后无法再次显示）
            if (App.Current.IsShuttingDown)
            {
                return;
            }

            e.Cancel = true;
            Hide();
        }
    }
}
