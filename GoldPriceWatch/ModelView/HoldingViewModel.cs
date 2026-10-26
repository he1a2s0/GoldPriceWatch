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
        private readonly HoldingStore _store = HoldingStore.Load();
        private readonly GoldInvestmentConfig _config;

        // 提醒状态：越过阈值后置位，回到区间内复位，避免重复弹窗
        private bool _highAlertActive;
        private bool _lowAlertActive;

        public ObservableCollection<OrderRecord> Orders { get; }

        public ObservableCollection<PricePoint> PriceHistory { get; }

        public HoldingViewModel()
        {
            _config = App.Current.Services.GetRequiredService<GoldInvestmentConfig>();
            Orders = new ObservableCollection<OrderRecord>(_store.Orders.OrderBy(o => o.Time));
            PriceHistory = new ObservableCollection<PricePoint>(_store.PriceHistory.OrderBy(p => p.Time));

            feeRatePercent = _store.FeeRatePercent;
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
        private string newWeight = string.Empty;

        private bool CanAddOrder() =>
            decimal.TryParse(NewPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) && p > 0 &&
            decimal.TryParse(NewWeight, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) && w > 0;

        [RelayCommand(CanExecute = nameof(CanAddOrder))]
        private void AddOrder()
        {
            var order = new OrderRecord
            {
                Side = NewIsBuy ? OrderSide.Buy : OrderSide.Sell,
                Price = decimal.Parse(NewPrice, CultureInfo.InvariantCulture),
                Weight = decimal.Parse(NewWeight, CultureInfo.InvariantCulture),
                Time = DateTime.Now
            };
            Orders.Add(order);
            NewPrice = string.Empty;
            NewWeight = string.Empty;
            Persist();
            Recalculate();
        }

        [RelayCommand]
        private void DeleteOrder(OrderRecord? order)
        {
            if (order is null) return;
            Orders.Remove(order);
            Persist();
            Recalculate();
        }

        /// <summary>立即刷新：直接触发一次取价（与后台循环共用同一管道）。</summary>
        [RelayCommand]
        private async Task RefreshNowAsync()
        {
            var fetcher = App.Current.Services.GetRequiredService<LoopFetcherService>();
            await fetcher.Invoke(CancellationToken.None);
        }
        #endregion

        #region 设置
        [ObservableProperty]
        private decimal feeRatePercent;

        partial void OnFeeRatePercentChanged(decimal value)
        {
            Persist();
            Recalculate();
        }

        [ObservableProperty]
        private int refreshIntervalSeconds;

        partial void OnRefreshIntervalSecondsChanged(int value) => _config.RefreshIntervalSeconds = value;

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

        #region 投资配置（转发 GoldInvestmentConfig）

        public List<SelectedValue<GoldUnit>> GoldUnitList =>
            typeof(GoldUnit).GetDescriptionAndValue().Select(it => new SelectedValue<GoldUnit>
            {
                Value = (GoldUnit)it.Value,
                Title = it.Key
            }).ToList();

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

        public decimal PurchasePrice
        {
            get => _config.PurchasePrice;
            set { if (_config.PurchasePrice != value) { _config.PurchasePrice = value; OnPropertyChanged(); } }
        }

        public decimal TotalGrams
        {
            get => _config.TotalGrams;
            set { if (_config.TotalGrams != value) { _config.TotalGrams = value; OnPropertyChanged(); } }
        }

        public GoldUnit Unit
        {
            get => _config.Unit;
            set { if (_config.Unit != value) { _config.Unit = value; OnPropertyChanged(); } }
        }

        public string Currency
        {
            get => _config.Currency;
            set { if (_config.Currency != value) { _config.Currency = value; OnPropertyChanged(); } }
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

        public string NotificationChannelsString
        {
            get => string.Join(",", _config.NotificationChannels ?? Array.Empty<string>());
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    _config.NotificationChannels = Array.Empty<string>();
                else
                    _config.NotificationChannels = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
                OnPropertyChanged();
            }
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
            RemainingWeight = profit.RemainingWeight;
            RemainingCost = profit.RemainingCost;
            NetProfit = profit.NetProfit;
            AvgCostPrice = profit.RemainingWeight > 0 ? profit.RemainingCost / profit.RemainingWeight : 0m;
            BreakEvenPrice = TradingCalculator.CalculateBreakEvenPrice(profit.RemainingCost, profit.RemainingWeight, FeeRate);

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
            _store.AlertEnabled = AlertEnabled;
            _store.AlertHighPrice = AlertHighPrice;
            _store.AlertLowPrice = AlertLowPrice;
            _store.Save();
        }
    }
}
