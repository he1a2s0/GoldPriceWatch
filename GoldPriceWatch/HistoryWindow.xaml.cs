using System.Windows;
using GoldPriceWatch.Model;

namespace GoldPriceWatch
{
    /// <summary>
    /// 历史记录详情窗口（只读）：显示快照中的交易流水、费率、持仓与盈亏、价格走势。
    /// 持仓与盈亏由历史流水按保存时的费率重新计算，数据直接以 HistoryRecord 作为 DataContext。
    /// </summary>
    public partial class HistoryWindow : Window
    {
        public HistoryWindow(HistoryRecord record)
        {
            InitializeComponent();
            DataContext = record;
            Title = $"历史记录 - {record.Name}";
        }
    }
}
