using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldPriceWatch.Components;
using GoldPriceWatch.Core;
using GoldPriceWatch.Core.Background;
using GoldPriceWatch.Enums;
using GoldPriceWatch.Model;
using HandyControl.Controls;
using HandyControl.Data;
using Masuit.Tools.Systems;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GoldPriceWatch.ModelView
{
    /// <summary>
    /// 持仓监控视图模型：买卖流水、费率、持仓与浮盈、价格历史与阈值提醒。
    /// 复用宿主既有取价管道（订阅 MediatR 的 GoldResult 通知），不额外发起网络请求。
    /// </summary>
    public partial class HoldingViewModel : ObservableObject, INotificationHandler<GoldResult>
    {
        private readonly HoldingStore _store;
        private readonly GoldInvestmentConfig _config;
        private readonly HistoryStore _history;

        // 提醒状态：越过阈值后置位，回到区间内复位，避免重复弹窗
        private bool _highAlertActive;
        private bool _lowAlertActive;

        public ObservableCollection<OrderRecord> Orders { get; }

        public ObservableCollection<PricePoint> PriceHistory { get; }

        public ObservableCollection<HistoryRecord> HistoryRecords { get; }

        [ObservableProperty]
        private HistoryRecord? selectedHistory;

        /// <summary>保存历史记录的结果提示文本（如“已保存 2026-11-06_152334”）。</summary>
        [ObservableProperty]
        private string saveHistoryResultText = string.Empty;

        /// <summary>结果提示是否可见，保存成功后显示 5 秒自动消失。</summary>
        [ObservableProperty]
        private bool isSaveHistoryResultVisible;

        public HoldingViewModel()
        {
            _store = App.Current.Services.GetRequiredService<HoldingStore>();
            _config = App.Current.Services.GetRequiredService<GoldInvestmentConfig>();
            _history = App.Current.Services.GetRequiredService<HistoryStore>();
            Orders = new ObservableCollection<OrderRecord>(_store.Orders.OrderBy(o => o.Time));
            PriceHistory = new ObservableCollection<PricePoint>(_store.PriceHistory.OrderBy(p => p.Time));
            HistoryRecords = new ObservableCollection<HistoryRecord>(
                _history.Records.OrderByDescending(r => r.CreatedAt));

            feeRatePercent = _store.FeeRatePercent;
            weightCorrection = _store.WeightCorrection;
            weightCorrectionText = _store.WeightCorrection.ToString(CultureInfo.CurrentCulture);
            alertEnabled = _store.AlertEnabled;
            alertHighPrice = _store.AlertHighPrice;
            alertLowPrice = _store.AlertLowPrice;

            // 旧版数据迁移带来的刷新间隔：写入全局配置后清除迁移标记
            if (_store.RefreshIntervalSeconds.HasValue)
            {
                _config.RefreshIntervalSeconds = _store.RefreshIntervalSeconds.Value;
                _store.RefreshIntervalSeconds = null;
                _store.Save();
            }
            refreshIntervalSeconds = _config.RefreshIntervalSeconds;

            Recalculate();
        }

        #region 新增流水输入
        [ObservableProperty]
        private bool newIsBuy = true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddOrderCommand))]
        private string newPrice = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddOrderCommand))]
        private string newAmount = string.Empty;

        private bool CanAddOrder() =>
            decimal.TryParse(NewPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) && p > 0 &&
            decimal.TryParse(NewAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var a) && a > 0;

        [RelayCommand(CanExecute = nameof(CanAddOrder))]
        private void AddOrder()
        {
            var price = decimal.Parse(NewPrice, CultureInfo.InvariantCulture);
            var amount = decimal.Parse(NewAmount, CultureInfo.InvariantCulture);
            var order = new OrderRecord
            {
                Side = NewIsBuy ? OrderSide.Buy : OrderSide.Sell,
                Price = price,
                // 克重由总价 / 单价反推，保持金额精度
                Weight = price > 0 ? amount / price : 0m,
                Amount = amount,
                Time = DateTime.Now
            };
            Orders.Add(order);
            NewPrice = string.Empty;
            NewAmount = string.Empty;
            Persist();
            Recalculate();
            _store.NotifyOrdersChanged();
        }

        [RelayCommand]
        private void DeleteOrder(OrderRecord? order)
        {
            if (order is null) return;
            Orders.Remove(order);
            Persist();
            Recalculate();
            _store.NotifyOrdersChanged();
        }

        /// <summary>清空当前交易记录（是否二次确认由调用方决定），并同步刷新持仓显示。</summary>
        public void ClearOrders()
        {
            if (Orders.Count == 0) return;
            Orders.Clear();
            Persist();
            Recalculate();
            _store.NotifyOrdersChanged();
        }

        /// <summary>立即刷新：直接触发一次取价（与后台循环共用同一管道）。</summary>
        [RelayCommand]
        private async Task RefreshNowAsync()
        {
            var fetcher = App.Current.Services.GetRequiredService<LoopFetcherService>();
            await fetcher.Invoke(CancellationToken.None);
        }
        #endregion

        #region 历史记录

        /// <summary>
        /// 把当前流水、费率、实时价格与价格历史存为一条快照。
        /// 名称按 yyyy-MM-dd_HHmmss 自动生成；流水做深拷贝，后续增删不影响已存记录。
        /// </summary>
        [RelayCommand]
        private void SaveCurrentAsHistory()
        {
            var now = DateTime.Now;
            var record = new HistoryRecord
            {
                Name = now.ToString("yyyy-MM-dd_HHmmss"),
                CreatedAt = now,
                FeeRatePercent = FeeRatePercent,
                WeightCorrection = WeightCorrection,
                CurrentPrice = CurrentPrice,
                Orders = Orders.Select(o => new OrderRecord
                {
                    Id = o.Id,
                    Side = o.Side,
                    Price = o.Price,
                    Weight = o.Weight,
                    Amount = o.Amount,
                    Time = o.Time
                }).ToList(),
                PriceHistory = PriceHistory.Select(p => new PricePoint { Time = p.Time, Price = p.Price }).ToList()
            };

            HistoryRecords.Insert(0, record);
            SelectedHistory = record;
            PersistHistory();

            // 按钮左侧显示结果提示，5 秒后自动消失
            SaveHistoryResultText = $"已保存 {record.Name}";
            IsSaveHistoryResultVisible = true;
            _ = HideSaveHistoryResultAfterDelayAsync();
        }

        /// <summary>延迟 5 秒隐藏保存结果提示；连续保存时旧任务不会提前关掉新提示。</summary>
        private async Task HideSaveHistoryResultAfterDelayAsync()
        {
            var shownText = SaveHistoryResultText;
            await Task.Delay(5000);
            if (SaveHistoryResultText != shownText) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                _ = dispatcher.InvokeAsync(() => IsSaveHistoryResultVisible = false);
                return;
            }
            IsSaveHistoryResultVisible = false;
        }

        /// <summary>删除一条历史记录（是否二次确认由调用方决定）。</summary>
        public void DeleteHistory(HistoryRecord? record)
        {
            if (record is null) return;
            HistoryRecords.Remove(record);
            SelectedHistory = HistoryRecords.FirstOrDefault();
            PersistHistory();
        }

        private void PersistHistory()
        {
            _history.Records = HistoryRecords.ToList();
            _history.Save();
        }
        #endregion

        #region 设置
        [ObservableProperty]
        private decimal feeRatePercent;

        partial void OnFeeRatePercentChanged(decimal value)
        {
            Persist();
            Recalculate();
            _store.NotifyOrdersChanged();
        }

        /// <summary>克数修正（克）：额外加到当前持仓量上，可为负数。</summary>
        [ObservableProperty]
        private decimal weightCorrection;

        partial void OnWeightCorrectionChanged(decimal value)
        {
            Persist();
            Recalculate();
            _store.NotifyOrdersChanged();
        }

        /// <summary>
        /// 克数修正的输入文本。用字符串承载输入，避免 decimal 直接绑定时 "-"、"1." 等
        /// 输入中间态转换失败，保证小数与负数可自由录入。
        /// </summary>
        [ObservableProperty]
        private string weightCorrectionText = "0";

        partial void OnWeightCorrectionTextChanged(string value)
        {
            // 清空视为不修正
            if (string.IsNullOrWhiteSpace(value))
            {
                WeightCorrection = 0m;
                return;
            }

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out var v) ||
                decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out v))
            {
                WeightCorrection = v;
            }
            // "-"、"-1." 等中间态解析不出新值时保持原数值与原文本，等待继续输入
        }

        [ObservableProperty]
        private int refreshIntervalSeconds;

        partial void OnRefreshIntervalSecondsChanged(int value) => _config.RefreshIntervalSeconds = value;

        /// <summary>悬浮框透明度（百分比 30~100）。修改后立即应用到悬浮框并由配置持久化。</summary>
        public double FloatingOpacityPercent
        {
            get => _config.FloatingOpacity * 100;
            set
            {
                _config.FloatingOpacity = value / 100;
                OnPropertyChanged();
                ApplyFloatingOpacity();
            }
        }

        /// <summary>外部（悬浮框 Ctrl+滚轮）调整透明度后，刷新本属性的显示。</summary>
        public void RefreshFloatingOpacity() => OnPropertyChanged(nameof(FloatingOpacityPercent));

        private void ApplyFloatingOpacity()
        {
            foreach (var window in Application.Current.Windows.OfType<MainWindow>())
            {
                window.ApplyFloatingOpacity();
            }
        }

        [ObservableProperty]
        private bool alertEnabled;

        partial void OnAlertEnabledChanged(bool value) => Persist();

        [ObservableProperty]
        private decimal alertHighPrice;

        partial void OnAlertHighPriceChanged(decimal value) => Persist();

        [ObservableProperty]
        private decimal alertLowPrice;

        partial void OnAlertLowPriceChanged(decimal value) => Persist();
        #endregion

        #region 提醒与通知配置（转发 GoldInvestmentConfig）

        public List<SelectedValue<AlertThresholdType>> AlertThresholdTypeList =>
            typeof(AlertThresholdType).GetDescriptionAndValue().Select(it => new SelectedValue<AlertThresholdType>
            {
                Value = (AlertThresholdType)it.Value,
                Title = it.Key
            }).ToList();

        public bool ShowNotice
        {
            get => _config.ShowNotice;
            set { _config.ShowNotice = value; OnPropertyChanged(); }
        }

        public decimal? TargetSellPrice
        {
            get => _config.TargetSellPrice;
            set { if (_config.TargetSellPrice != value) { _config.TargetSellPrice = value; OnPropertyChanged(); } }
        }

        public decimal? StopLossPrice
        {
            get => _config.StopLossPrice;
            set { if (_config.StopLossPrice != value) { _config.StopLossPrice = value; OnPropertyChanged(); } }
        }

        public AlertThresholdType DropAlertType
        {
            get => _config.DropAlertType;
            set { if (_config.DropAlertType != value) { _config.DropAlertType = value; OnPropertyChanged(); } }
        }

        public decimal DropAlertThreshold
        {
            get => _config.DropAlertThreshold;
            set { if (_config.DropAlertThreshold != value) { _config.DropAlertThreshold = value; OnPropertyChanged(); } }
        }

        public AlertThresholdType RiseAlertType
        {
            get => _config.RiseAlertType;
            set { if (_config.RiseAlertType != value) { _config.RiseAlertType = value; OnPropertyChanged(); } }
        }

        public decimal RiseAlertThreshold
        {
            get => _config.RiseAlertThreshold;
            set { if (_config.RiseAlertThreshold != value) { _config.RiseAlertThreshold = value; OnPropertyChanged(); } }
        }

        public decimal? VolatilityThreshold
        {
            get => _config.VolatilityThreshold;
            set { if (_config.VolatilityThreshold != value) { _config.VolatilityThreshold = value; OnPropertyChanged(); } }
        }

        public bool EnableSettlementAlert
        {
            get => _config.EnableSettlementAlert;
            set { if (_config.EnableSettlementAlert != value) { _config.EnableSettlementAlert = value; OnPropertyChanged(); } }
        }
        #endregion

        #region 行情与持仓展示
        [ObservableProperty]
        private decimal currentPrice;

        [ObservableProperty]
        private decimal highPrice;

        [ObservableProperty]
        private decimal lowPrice;

        [ObservableProperty]
        private decimal settlementPrice;

        [ObservableProperty]
        private string lastUpdateText = "未更新";

        [ObservableProperty]
        private string statusText = string.Empty;

        [ObservableProperty]
        private decimal remainingWeight;

        [ObservableProperty]
        private decimal remainingCost;

        [ObservableProperty]
        private decimal avgCostPrice;

        [ObservableProperty]
        private decimal marketValue;

        [ObservableProperty]
        private decimal floatingProfit;

        [ObservableProperty]
        private decimal floatingProfitPercent;

        [ObservableProperty]
        private decimal netProfit;

        [ObservableProperty]
        private decimal breakEvenPrice;
        #endregion

        private decimal FeeRate => FeeRatePercent / 100m;

        private void Recalculate()
        {
            var profit = TradingCalculator.CalculateRealizedProfit(Orders, FeeRate);
            // 克数修正额外叠加在流水计算出的持仓量上，成本不变
            var remainingWeight = profit.RemainingWeight + WeightCorrection;
            RemainingWeight = remainingWeight;
            RemainingCost = profit.RemainingCost;
            NetProfit = profit.NetProfit;
            AvgCostPrice = remainingWeight > 0 ? profit.RemainingCost / remainingWeight : 0m;
            BreakEvenPrice = TradingCalculator.CalculateBreakEvenPrice(profit.RemainingCost, remainingWeight, FeeRate);

            MarketValue = CurrentPrice * RemainingWeight;
            FloatingProfit = MarketValue * (1 - FeeRate) - RemainingCost;
            FloatingProfitPercent = RemainingCost > 0 ? FloatingProfit / RemainingCost * 100m : 0m;
        }

        /// <summary>接收宿主取价管道的价格通知，切到 UI 线程后更新。</summary>
        public async Task Handle(GoldResult notification, CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                await dispatcher.InvokeAsync(() => Apply(notification));
                return;
            }
            Apply(notification);
        }

        private void Apply(GoldResult notification)
        {
            if (!decimal.TryParse(notification.CurrentPrice, out var price) || price <= 0)
            {
                StatusText = "获取金价失败";
                return;
            }

            CurrentPrice = price;
            HighPrice = ParseOrZero(notification.HighPrice);
            LowPrice = ParseOrZero(notification.LowPrice);
            SettlementPrice = ParseOrZero(notification.SettlementPrice);
            LastUpdateText = $"{DateTime.Now:HH:mm:ss} ({notification.Source})";
            StatusText = string.Empty;

            var point = new PricePoint { Time = DateTime.Now, Price = price };
            // 与最后一点间隔过近则覆盖，避免重复点
            if (PriceHistory.Count > 0 && (point.Time - PriceHistory[^1].Time).TotalSeconds < 5)
                PriceHistory[^1] = point;
            else
                PriceHistory.Add(point);

            Recalculate();
            CheckAlerts(price);
            Persist();
        }

        private static decimal ParseOrZero(string? text) =>
            decimal.TryParse(text, out var v) ? v : 0m;

        private void CheckAlerts(decimal price)
        {
            if (!AlertEnabled) return;

            if (AlertHighPrice > 0 && price >= AlertHighPrice)
            {
                if (!_highAlertActive)
                {
                    _highAlertActive = true;
                    Notify($"金价已涨至 {price:F2}，达到提醒线 {AlertHighPrice:F2}");
                }
            }
            else _highAlertActive = false;

            if (AlertLowPrice > 0 && price <= AlertLowPrice)
            {
                if (!_lowAlertActive)
                {
                    _lowAlertActive = true;
                    Notify($"金价已跌至 {price:F2}，触及提醒线 {AlertLowPrice:F2}");
                }
            }
            else _lowAlertActive = false;
        }

        private static void Notify(string message) =>
            Notification.Show(new AppNotification(message), ShowAnimation.Fade, true);

        private void Persist()
        {
            _store.Orders = Orders.ToList();
            _store.PriceHistory = PriceHistory.ToList();
            _store.FeeRatePercent = FeeRatePercent;
            _store.WeightCorrection = WeightCorrection;
            _store.AlertEnabled = AlertEnabled;
            _store.AlertHighPrice = AlertHighPrice;
            _store.AlertLowPrice = AlertLowPrice;
            _store.Save();
        }
    }
}
