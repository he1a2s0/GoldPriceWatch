using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using GoldPriceWatch.Core;

namespace GoldPriceWatch.Model
{
    /// <summary>
    /// 一条持仓历史快照：交易流水、费率、保存时的实时价格与价格历史。
    /// 持仓与盈亏不落盘，加载时按流水重新计算，避免与计算口径变化产生偏差。
    /// </summary>
    public class HistoryRecord
    {
        /// <summary>记录名称，自动为 yyyy-MM-dd_HHmmss。</summary>
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        /// <summary>卖出费率（百分比）。</summary>
        public decimal FeeRatePercent { get; set; }

        /// <summary>保存时的实时金价（元/克）。</summary>
        public decimal CurrentPrice { get; set; }

        public List<OrderRecord> Orders { get; set; } = new();

        public List<PricePoint> PriceHistory { get; set; } = new();

        private HistorySummary? _summary;

        /// <summary>按流水现算的持仓与盈亏（结果缓存，只算一次）。</summary>
        [JsonIgnore]
        public HistorySummary Summary => _summary ??= BuildSummary();

        [JsonIgnore]
        public int OrderCount => Orders.Count;

        [JsonIgnore]
        public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        private HistorySummary BuildSummary()
        {
            var feeRate = FeeRatePercent / 100m;
            var profit = TradingCalculator.CalculateRealizedProfit(Orders, feeRate);
            var summary = new HistorySummary
            {
                FeeRate = feeRate,
                RemainingWeight = profit.RemainingWeight,
                RemainingCost = profit.RemainingCost,
                NetProfit = profit.NetProfit,
                AvgCostPrice = profit.RemainingWeight > 0 ? profit.RemainingCost / profit.RemainingWeight : 0m,
                BreakEvenPrice = TradingCalculator.CalculateBreakEvenPrice(
                    profit.RemainingCost, profit.RemainingWeight, feeRate)
            };

            summary.MarketValue = CurrentPrice * profit.RemainingWeight;
            summary.FloatingProfit = summary.MarketValue * (1 - feeRate) - profit.RemainingCost;
            summary.FloatingProfitPercent = profit.RemainingCost > 0
                ? summary.FloatingProfit / profit.RemainingCost * 100m
                : 0m;
            return summary;
        }
    }

    /// <summary>历史记录的持仓与盈亏汇总（只读，用于列表与详情展示）。</summary>
    public class HistorySummary
    {
        public decimal FeeRate { get; set; }
        public decimal RemainingWeight { get; set; }
        public decimal RemainingCost { get; set; }
        public decimal AvgCostPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal FloatingProfit { get; set; }
        public decimal FloatingProfitPercent { get; set; }
        public decimal NetProfit { get; set; }
        public decimal BreakEvenPrice { get; set; }
    }
}
