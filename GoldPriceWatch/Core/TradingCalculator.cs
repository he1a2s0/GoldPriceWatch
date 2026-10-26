using System.Collections.Generic;
using System.Linq;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Core
{
    /// <summary>实际盈亏计算结果。</summary>
    public class ProfitResult
    {
        public decimal TotalBuyWeight { get; set; }
        public decimal TotalSellWeight { get; set; }
        public decimal RemainingWeight { get; set; }
        public decimal TotalCost { get; set; }
        public decimal RemainingCost { get; set; }
        public decimal GrossProceeds { get; set; }
        public decimal TotalFees { get; set; }
        public decimal NetProfit { get; set; }
    }

    /// <summary>
    /// 持仓成本与盈亏计算：买入无手续费，卖出按费率收取；剩余成本按重量比例分摊。
    /// </summary>
    public static class TradingCalculator
    {
        public static ProfitResult CalculateRealizedProfit(IEnumerable<OrderRecord> orders, decimal feeRate)
        {
            var buys = orders.Where(o => o.Side == OrderSide.Buy).ToList();
            var sells = orders.Where(o => o.Side == OrderSide.Sell).ToList();
            var result = new ProfitResult();

            result.TotalBuyWeight = buys.Sum(x => x.Weight);
            result.TotalCost = buys.Sum(x => x.Price * x.Weight);

            result.TotalSellWeight = sells.Sum(x => x.Weight);
            result.GrossProceeds = sells.Sum(x => x.Price * x.Weight);
            result.TotalFees = result.GrossProceeds * feeRate;

            result.RemainingWeight = result.TotalBuyWeight - result.TotalSellWeight;
            if (result.TotalBuyWeight > 0)
            {
                result.RemainingCost = result.TotalCost * (result.RemainingWeight / result.TotalBuyWeight);
            }

            var costOfSoldGoods = result.TotalCost - result.RemainingCost;
            var netSellIncome = result.GrossProceeds - result.TotalFees;
            result.NetProfit = netSellIncome - costOfSoldGoods;

            return result;
        }

        /// <summary>保本卖出价 = 剩余成本 / (剩余重量 * (1 - 费率))。</summary>
        public static decimal CalculateBreakEvenPrice(decimal remainingCost, decimal remainingWeight, decimal feeRate)
        {
            if (remainingWeight == 0) return 0;
            return remainingCost / (remainingWeight * (1 - feeRate));
        }
    }
}
