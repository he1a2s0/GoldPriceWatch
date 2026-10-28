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
        /// 无订单时返回 null 表示回退到 config 固定值。
        /// </summary>
        private (decimal AvgPrice, decimal Grams, decimal Cost)? ComputeHoldingFromOrders()
        {
            if (_store.Orders == null || _store.Orders.Count == 0) return null;

            var feeRate = _store.FeeRatePercent / 100m;
            var p = TradingCalculator.CalculateRealizedProfit(_store.Orders, feeRate);

            if (p.RemainingWeight <= 0) return null;

            var avg = p.RemainingCost / p.RemainingWeight;
            return (avg, p.RemainingWeight, p.RemainingCost);
        }

        /// <summary>
        /// 当前总市值（订单优先，回退到 config；不扣手续费，反映持仓在市场上的公允价值）。
        /// </summary>
        public string CurrentTotalValue
        {
            get
            {
                var currentPrice = decimal.TryParse(_currentGoldResult?.CurrentPrice, out var cp) ? cp : 0m;
                var h = ComputeHoldingFromOrders();
                var grams = h?.Grams ?? _config.TotalGrams;
                return $"{(currentPrice * grams):F2} {_config.Currency}";
            }
        }

        /// <summary>
        /// 持仓总成本（订单优先，回退到 config）。
        /// </summary>
        public string PurchaseTotalValue
        {
            get
            {
                var h = ComputeHoldingFromOrders();
                var cost = h?.Cost ?? (_config.PurchasePrice * _config.TotalGrams);
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

            // 从订单计算实时持仓；无订单时回退到 config 的固定值（兼容旧数据）
            decimal purchasePrice;
            decimal totalGrams;
            bool fromOrders = false;

            var holding = ComputeHoldingFromOrders();
            if (holding.HasValue)
            {
                purchasePrice = holding.Value.AvgPrice;
                totalGrams = holding.Value.Grams;
                fromOrders = true;
            }
            else
            {
                purchasePrice = _config.PurchasePrice;
                totalGrams = _config.TotalGrams;
            }

            // Update basic price info
            CurrentPriceText = $"{notification.CurrentPrice}";
            PurchasePriceText = fromOrders
                ? $"持仓 {purchasePrice:F0}({totalGrams:F2}g)"
                : $"B: {purchasePrice:F0}({totalGrams:F1}{_config.Unit.GetDescription()})";

            // Compute profit/loss（按现价扣卖出手续费，与持仓窗口口径一致）
            var feeRate = _store.FeeRatePercent / 100m;
            var currentPrice = decimal.TryParse(notification.CurrentPrice, out var cp) ? cp : 0m;
            var marketValue = currentPrice * totalGrams;
            var costBasis = purchasePrice * totalGrams;
            var profitLoss = marketValue * (1 - feeRate) - costBasis;

            ProfitLossAmountText = profitLoss >= 0 ? $"+{profitLoss:F2}" : $"{profitLoss:F2}";

            var profitLossPercentage = costBasis > 0
                ? (profitLoss / costBasis) * 100m
                : 0m;
            ProfitLossPercentageText = profitLossPercentage >= 0
                ? $"+{profitLossPercentage:F2}%"
                : $"{profitLossPercentage:F2}%";

            // Update trend
            var changePercentage = notification.CalculatePriceChangePercentage();
            IsRise = changePercentage >= 0;
            PriceRangePercentage = $"{Math.Abs(changePercentage):F2}%";

            // Reset previous alerts
            IsRiseAlert = false;
            IsDropAlert = false;
            IsFlashing = false;

            // 有有效持仓时才做报警检查
            if (purchasePrice > 0 && totalGrams > 0)
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