using System;
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
    /// 持仓成本与盈亏计算：FIFO（先进先出）算法，直接从买入流水累加剩余成本，
    /// 避免"按重量比例分摊总成本"引入的舍入误差。
    /// </summary>
    public static class TradingCalculator
    {
        /// <summary>
        /// 用 FIFO 计算已实现盈亏与剩余持仓。
        /// 优先读取 OrderRecord.Amount（持久化字段），Amount 为 null 时回退 Price * Weight。
        /// </summary>
        public static ProfitResult CalculateRealizedProfit(IEnumerable<OrderRecord> orders, decimal feeRate)
        {
            var buys = orders.Where(o => o.Side == OrderSide.Buy)
                             .OrderBy(o => o.Time)
                             .Select(o => (Weight: o.Weight, Amount: o.GetAmount))
                             .ToList();
            var sells = orders.Where(o => o.Side == OrderSide.Sell)
                              .OrderBy(o => o.Time)
                              .Select(o => (Weight: o.Weight, Amount: o.GetAmount))
                              .ToList();

            var result = new ProfitResult
            {
                TotalBuyWeight = buys.Sum(b => b.Weight),
                TotalCost = buys.Sum(b => b.Amount),
                TotalSellWeight = sells.Sum(s => s.Weight),
                GrossProceeds = sells.Sum(s => s.Amount),
            };

            // FIFO 消耗：队列头是最早买入，卖出按时间顺序依次吃掉
            // 队列每条 = (剩余重量, 剩余成本, 原单价)；部分消耗时用 重量×单价 算成本，
            // 避免 amount * (consumed/weight) 中除法产生循环小数导致的舍入误差
            var queue = new Queue<(decimal Weight, decimal Cost, decimal UnitPrice)>();
            foreach (var b in buys)
            {
                if (b.Weight > 0) queue.Enqueue((b.Weight, b.Amount, b.Amount / b.Weight));
            }

            decimal costOfSoldGoods = 0m;

            foreach (var s in sells)
            {
                decimal toConsume = s.Weight;
                while (toConsume > 0 && queue.Count > 0)
                {
                    var head = queue.Dequeue();
                    if (head.Weight <= toConsume)
                    {
                        // 整条买入被吃掉
                        costOfSoldGoods += head.Cost;
                        toConsume -= head.Weight;
                    }
                    else
                    {
                        // 只吃一部分：用 重量 × 单价 算成本，完全消除除法舍入
                        var consumedCost = toConsume * head.UnitPrice;
                        costOfSoldGoods += consumedCost;
                        queue.Enqueue((head.Weight - toConsume, head.Cost - consumedCost, head.UnitPrice));
                        toConsume = 0;
                    }
                }
                // 若某笔卖出的重量超过了剩余买入（录入误差），多出来的部分按 0 成本处理
            }

            // 剩余持仓 = 队列里所有未被吃掉的买入之和（精确，无比例分摊）
            decimal remainingWeight = 0m, remainingCost = 0m;
            foreach (var item in queue)
            {
                remainingWeight += item.Weight;
                remainingCost += item.Cost;
            }
            result.RemainingWeight = remainingWeight;
            result.RemainingCost = remainingCost;

            result.TotalFees = result.GrossProceeds * feeRate;
            result.NetProfit = result.GrossProceeds - result.TotalFees - costOfSoldGoods;

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
