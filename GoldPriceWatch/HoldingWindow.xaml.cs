using GoldPriceWatch.Core;
using GoldPriceWatch.ModelView;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Windows;

namespace GoldPriceWatch
{
    /// <summary>
    /// 持仓监控窗口：买卖流水、费率、监控设置、统计与价格走势图。
    /// 关闭时隐藏到托盘，可经托盘菜单再次显示。
    /// </summary>
    public partial class HoldingWindow : Window
    {
        private readonly HoldingStore _store;

        /// <summary>展开状态下的左栏宽度（收起时临时改为铺满，展开后还原）。</summary>
        private GridLength _expandedLeftWidth;

        /// <summary>右侧面板是否处于收起状态。</summary>
        private bool _rightCollapsed;

        /// <summary>收起时右栏（含分割条）的宽度，用于展开时把窗口还原。</summary>
        private double _rightPanelWidth;

        /// <summary>展开状态下的窗口最小宽度（收起时需临时放宽）。</summary>
        private double _expandedMinWidth;

        /// <summary>收起状态下的最小宽度：左栏最小 460 + 按钮 + 边距 + 边框。</summary>
        private const double CollapsedMinWidth = 520;

        public HoldingWindow()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<HoldingViewModel>();
            _store = App.Current.Services.GetRequiredService<HoldingStore>();
            Closing += HoldingWindow_Closing;

            _expandedLeftWidth = LeftColumn.Width;
            _expandedMinWidth = MinWidth;

            // 需等布局完成后才能拿到右栏实际宽度，故在 Loaded 时恢复上次的面板状态
            Loaded += HoldingWindow_Loaded;
        }

        private void HoldingWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyRightPanelCollapsed(_store.RightPanelCollapsed, persist: false);
        }

        /// <summary>切到指定 Tab。0=交易记录, 1=监控提醒, 2=历史记录。</summary>
        public void SelectTab(int index)
        {
            if (index >= 0 && index < LeftTabs.Items.Count)
                LeftTabs.SelectedIndex = index;
        }

        /// <summary>查看选中历史记录的详情（只读界面）。</summary>
        private void OpenHistory_Click(object sender, RoutedEventArgs e) => OpenSelectedHistory();

        /// <summary>双击历史记录行等同于点击「查看详情」。</summary>
        private void OpenHistory_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => OpenSelectedHistory();

        private void OpenSelectedHistory()
        {
            if (DataContext is not HoldingViewModel vm || vm.SelectedHistory is not { } record)
                return;

            new HistoryWindow(record) { Owner = this }.Show();
        }

        /// <summary>删除选中的历史记录（先二次确认）。</summary>
        private void DeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not HoldingViewModel vm || vm.SelectedHistory is not { } record)
                return;

            var result = MessageBox.Show(this, $"确定删除历史记录 {record.Name}？", "删除历史记录",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                vm.DeleteHistory(record);
            }
        }

        /// <summary>清空当前交易记录（先二次确认）。</summary>
        private void ClearOrders_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not HoldingViewModel vm || vm.Orders.Count == 0)
                return;

            var result = MessageBox.Show(this, $"确定清空全部 {vm.Orders.Count} 条交易记录？", "清空交易记录",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                vm.ClearOrders();
            }
        }

        private void ToggleRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyRightPanelCollapsed(!_rightCollapsed, persist: true);
        }

        /// <summary>切换右侧面板显示/隐藏；persist=true 时写回 holding.json 以便下次启动记忆。</summary>
        private void ApplyRightPanelCollapsed(bool collapsed, bool persist)
        {
            var changed = _rightCollapsed != collapsed;

            // 记录收起前的左栏宽度与右栏实宽，用于展开时原样还原
            if (changed && collapsed)
            {
                _expandedLeftWidth = LeftColumn.Width;
                _rightPanelWidth = RightPanel.ActualWidth + SplitterColumn.ActualWidth;
            }

            _rightCollapsed = collapsed;
            RightPanel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            RightColumn.Width = collapsed ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = collapsed ? new GridLength(0) : new GridLength(6);

            // 收起后左栏铺满整个窗口，展开后还原原宽度与上限
            LeftColumn.Width = collapsed ? new GridLength(1, GridUnitType.Star) : _expandedLeftWidth;
            LeftColumn.MaxWidth = collapsed ? double.PositiveInfinity : 820;

            // 窗体宽度跟随右栏一起收窄/展开（状态未变化时不重复调整，避免多次 Loaded 叠减）
            if (changed)
            {
                if (collapsed)
                {
                    MinWidth = CollapsedMinWidth;
                    Width = Math.Max(MinWidth, Width - _rightPanelWidth);
                }
                else
                {
                    MinWidth = _expandedMinWidth;
                    Width = Math.Max(MinWidth, Width + _rightPanelWidth);
                }
            }

            ToggleRightButton.Content = collapsed ? "‹" : "›";
            ToggleRightButton.ToolTip = collapsed ? "展开右侧面板" : "收起右侧面板";

            if (persist)
            {
                _store.RightPanelCollapsed = collapsed;
                _store.Save();
            }
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
