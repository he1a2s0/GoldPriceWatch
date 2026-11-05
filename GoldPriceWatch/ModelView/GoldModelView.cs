using CommunityToolkit.Mvvm.ComponentModel;
using GoldPriceWatch.Components;
using GoldPriceWatch.Core;
using GoldPriceWatch.Enums;
using GoldPriceWatch.Model;
using HandyControl.Controls;
using HandyControl.Data;
using Masuit.Tools.Systems;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace GoldPriceWatch.ModelView
{
    /// <summary>
    /// ViewModel for the main gold price monitoring window.
    /// Handles GoldResult notifications, computes profit/loss, alerts based on config thresholds.
    /// Supports visual alerts like color changes and flashing.
    /// </summary>
    public partial class GoldModelView : ObservableObject, INotificationHandler<GoldResult>, INotificationHandler<FetchStatusChanged>
    {
        private readonly GoldInvestmentConfig _config;
        private readonly HoldingStore _store;

        public GoldModelView()
        {
            _config = App.Current.Services.GetRequiredService<GoldInvestmentConfig>();
            _store = App.Current.Services.GetRequiredService<HoldingStore>();
            // 订单/费率变化时实时刷新悬浮框的持仓与盈亏显示
            _store.OrdersChanged += OnOrdersChanged;
        }

        [ObservableProperty]
        private string? currentPriceText;

        [ObservableProperty]
        private string? purchasePriceText;

        [ObservableProperty]
        private string? profitLossAmountText;

        [ObservableProperty]
        private string? profitLossPercentageText;

        [ObservableProperty]
        private bool isRise; // True for rise, False for drop

        [ObservableProperty]
        private string? priceRangePercentage;

        [ObservableProperty]
        private DateTime lastUpdated = DateTime.Now;

        [ObservableProperty]
        private bool isRiseAlert; // Triggers rise alert styling

        [ObservableProperty]
        private bool isDropAlert; // Triggers drop alert styling

        [ObservableProperty]
        private bool isFlashing; // Triggers flashing animation

        [ObservableProperty]
        private bool isFetching;

        [ObservableProperty]
        private bool isWaiting = true;

        [ObservableProperty]
        private string fetchStatusText = "等待中";

        /// <summary>
        /// 基于订单的持仓：均价、剩余克数、剩余成本。
        /// 与持仓监控窗口共用同一套订单数据与算法（TradingCalculator），保证两处口径一致；
        /// 无订单时为 0 持仓，不再回退到 config 的旧固定值（那会与持仓监控显示不一致）。
        /// </summary>
        private (decimal AvgPrice, decimal Grams, decimal Cost) GetHolding()
        {
            if (_store.Orders == null || _store.Orders.Count == 0) return (0m, 0m, 0m);

            var feeRate = _store.FeeRatePercent / 100m;
            var p = TradingCalculator.CalculateRealizedProfit(_store.Orders, feeRate);

            var avg = p.RemainingWeight > 0 ? p.RemainingCost / p.RemainingWeight : 0m;
            return (avg, p.RemainingWeight, p.RemainingCost);
        }

        /// <summary>
        /// 当前总市值（不扣手续费，反映持仓在市场上的公允价值）。
        /// </summary>
        public string CurrentTotalValue
        {
            get
            {
                var currentPrice = decimal.TryParse(_currentGoldResult?.CurrentPrice, out var cp) ? cp : 0m;
                var grams = GetHolding().Grams;
                return $"{(currentPrice * grams):F2} {_config.Currency}";
            }
        }

        /// <summary>
        /// 持仓总成本（剩余持仓成本）。
        /// </summary>
        public string PurchaseTotalValue
        {
            get
            {
                var cost = GetHolding().Cost;
                return $"{cost:F2} {_config.Currency}";
            }
        }

        private GoldResult? _currentGoldResult;

        /// <summary>
        /// Handles incoming GoldResult notifications.
        /// Updates display values, checks alerts, triggers visual changes.
        /// </summary>
        public async Task Handle(GoldResult notification, CancellationToken cancellationToken)
        {
            _currentGoldResult = notification;
            LastUpdated = DateTime.Now;

            // 与持仓监控窗口一致：只用订单计算的持仓
            var totalGrams = GetHolding().Grams;

            // Update basic price info
            CurrentPriceText = $"{notification.CurrentPrice}";
            UpdateHoldingDisplay(notification);

            // Update trend
            var changePercentage = notification.CalculatePriceChangePercentage();
            IsRise = changePercentage >= 0;
            PriceRangePercentage = $"{Math.Abs(changePercentage):F2}%";

            // Reset previous alerts
            IsRiseAlert = false;
            IsDropAlert = false;
            IsFlashing = false;

            // 有持仓时才做报警检查
            if (totalGrams > 0)
            {
                // Check alerts using config methods
                // 注意：config 方法内部仍读 PurchasePrice / TotalGrams 固定值，
                // 这里复用它们做阈值触发判断，message 里的金额/盈亏百分比将用 config 值计算。
                // 若订单算出来的均价与 config 固定值差异较大，报警 message 会有偏差——
                // 但阈值触发本身不受影响，因为报警的"涨价/降价百分比"口径是基于金价的，
                // 与持仓均价无关。
                var (dropTriggered, dropMessage) = _config.CheckDropAlert(notification);
                var (riseTriggered, riseMessage) = _config.CheckRiseAlert(notification);
                var (volatilityTriggered, volatilityMessage) = _config.CheckVolatilityAlert(notification);
                var (targetReached, targetMessage) = _config.CheckTargetSell(notification);
                var (stopLossTriggered, stopLossMessage) = _config.CheckStopLoss(notification);

                bool anyAlert = dropTriggered || riseTriggered || volatilityTriggered || targetReached || stopLossTriggered;

                if (anyAlert && _config.ShowNotice)
                {
                    // Trigger visual alert
                    IsFlashing = true;

                    // Specific alert types for coloring
                    if (riseTriggered || targetReached)
                    {
                        IsRiseAlert = true;
                    }
                    else if (dropTriggered || stopLossTriggered)
                    {
                        IsDropAlert = true;
                    }

                    // Collect non-empty messages
                    var messages = new[] { dropMessage, riseMessage, volatilityMessage, targetMessage, stopLossMessage }
                        .Where(m => !string.IsNullOrEmpty(m));
                    var fullMessage = string.Join("\n", messages);

                    // Show soft notification
                    await ShowAlertNotificationAsync(fullMessage);
                }
            }

            // Notify UI updates
            OnPropertyChanged(nameof(CurrentTotalValue));
            OnPropertyChanged(nameof(PurchaseTotalValue));
        }

        /// <summary>
        /// 根据最新价格与订单重新计算并刷新持仓均价、剩余克数与盈亏显示。
        /// 价格通知与订单变更事件都会调用本方法，保证悬浮框实时反映持仓变化。
        /// </summary>
        private void UpdateHoldingDisplay(GoldResult notification)
        {
            var (purchasePrice, totalGrams, costBasis) = GetHolding();

            // 与持仓监控窗口口径一致：均价两位小数、克数四位小数
            PurchasePriceText = $"{purchasePrice:F2}({totalGrams:F4}g)";

            // Compute profit/loss（按现价扣卖出手续费，与持仓窗口口径一致）
            var feeRate = _store.FeeRatePercent / 100m;
            var currentPrice = decimal.TryParse(notification.CurrentPrice, out var cp) ? cp : 0m;
            var marketValue = currentPrice * totalGrams;
            var profitLoss = marketValue * (1 - feeRate) - costBasis;

            ProfitLossAmountText = profitLoss >= 0 ? $"+{profitLoss:F2}" : $"{profitLoss:F2}";

            var profitLossPercentage = costBasis > 0
                ? (profitLoss / costBasis) * 100m
                : 0m;
            ProfitLossPercentageText = profitLossPercentage >= 0
                ? $"+{profitLossPercentage:F2}%"
                : $"{profitLossPercentage:F2}%";

            OnPropertyChanged(nameof(CurrentTotalValue));
            OnPropertyChanged(nameof(PurchaseTotalValue));
        }

        /// <summary>订单/费率变化时用最新价格重算持仓与盈亏。</summary>
        private void OnOrdersChanged(object? sender, EventArgs e)
        {
            if (_currentGoldResult == null) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(() => UpdateHoldingDisplay(_currentGoldResult));
                return;
            }

            UpdateHoldingDisplay(_currentGoldResult);
        }

        public Task Handle(FetchStatusChanged notification, CancellationToken cancellationToken)
        {
            IsFetching = notification.State == FetchState.Fetching;
            IsWaiting = notification.State == FetchState.Waiting;
            FetchStatusText = IsFetching ? "获取中..." : "...";

            return Task.CompletedTask;
        }

        /// <summary>
        /// Shows a soft alert notification using HandyControl.
        /// Non-intrusive, no user confirmation required.
        /// </summary>
        private async Task ShowAlertNotificationAsync(string message)
        {
            // Ensure UI thread
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Notification.Show(new AppNotification(message), ShowAnimation.Fade, true);
            });
        }

        /// <summary>
        /// Command to exit app (bind to MenuItem).
        /// </summary>
        public void ExitApp()
        {
            Application.Current.Shutdown();
        }
    }
}