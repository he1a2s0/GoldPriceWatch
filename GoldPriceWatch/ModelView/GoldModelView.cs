using CommunityToolkit.Mvvm.ComponentModel;
using GoldPriceWatch.Components;
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

        public GoldModelView()
        {
            _config = App.Current.Services.GetRequiredService<GoldInvestmentConfig>();
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
        /// Computed current total value based on config.
        /// </summary>
        public string CurrentTotalValue => $"{_config.GetCurrentTotalValue(_currentGoldResult):F2} {_config.Currency}";

        /// <summary>
        /// Computed purchase total value.
        /// </summary>
        public string PurchaseTotalValue => $"{(_config.PurchasePrice * _config.TotalGrams):F2} {_config.Currency}";

        private GoldResult? _currentGoldResult;

        /// <summary>
        /// Handles incoming GoldResult notifications.
        /// Updates display values, checks alerts, triggers visual changes.
        /// </summary>
        public async Task Handle(GoldResult notification, CancellationToken cancellationToken)
        {
            _currentGoldResult = notification;
            LastUpdated = DateTime.Now;

            // Update basic price info
            //CurrentPriceText = $"￥{notification.CurrentPrice}";
            CurrentPriceText = $"{notification.CurrentPrice}";
            PurchasePriceText = $"B: {_config.PurchasePrice:F0}({_config.TotalGrams:F1}{_config.Unit.GetDescription()})";

            // Compute profit/loss
            var profitLoss = _config.GetCurrentProfitLoss(notification);
            ProfitLossAmountText = profitLoss >= 0 ? $"+{profitLoss:F2}" : $"{profitLoss:F2}";

            var profitLossPercentage = notification.CalculateLossPercentage(_config.PurchasePrice); // Note: Negative for profit
            ProfitLossPercentageText = profitLossPercentage <= 0 ? $"+{Math.Abs(profitLossPercentage):F2}%" : $"{profitLossPercentage:F2}%";

            // Update trend
            var changePercentage = notification.CalculatePriceChangePercentage();
            IsRise = changePercentage >= 0;
            PriceRangePercentage = $"{Math.Abs(changePercentage):F2}%";

            // Reset previous alerts
            IsRiseAlert = false;
            IsDropAlert = false;
            IsFlashing = false;

            // Only perform alert checks if config is valid (not empty/null)
            if (_config.PurchasePrice > 0 && _config.TotalGrams > 0)
            {
                // Check alerts using config methods
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
        /// Command to open config window (bind to MenuItem).
        /// </summary>
        public void OpenConfigWindow()
        {
            var configWindow = new GoldInvestmentWindow();
            configWindow.DataContext = new GoldInvestmentViewModel(); // Assuming ViewModel from previous
            configWindow.ShowDialog();
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