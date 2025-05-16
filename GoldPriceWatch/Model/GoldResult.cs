
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldPriceWatch.Model
{
    public class GoldResult : INotification
    {
        // 基础信息
        public string? Source { get; set; }         // 数据来源(如LBMA、SGE等)
        public string? Currency { get; set; }       // 货币类型(USD, CNY等)
        // 价格数据
        public string? CurrentPrice { get; set; }   // 当前金价
        public string? HighPrice { get; set; }      // 当日最高价
        public string? LowPrice { get; set; }       // 当日最低价
        public string? OpenPrice { get; set; }      // 开盘价
        public string? SettlementPrice { get; set; } // 结算价

        public GoldResult()
        {
            // 初始化默认值
            Source = "Unknown";
            Currency = "Unknown";
            CurrentPrice = "0.00";
            HighPrice = "0.00";
            LowPrice = "0.00";
            OpenPrice = "0.00";
            SettlementPrice = "0.00";

        }

        /// <summary>
        /// 计算涨幅百分比（基于开盘价与当前价）
        /// </summary>
        /// <returns>涨幅百分比（正数表示上涨，负数表示下跌），保留2位小数</returns>
        /// <exception cref="InvalidOperationException">当价格数据无效时抛出</exception>
        public decimal CalculatePriceChangePercentage()
        {
            if (!decimal.TryParse(CurrentPrice, out decimal current) ||
                !decimal.TryParse(OpenPrice, out decimal open))
            {
                return -1;
            }

            if (open == 0)
            {
                return -1;
            }

            // 涨幅百分比 = ((当前价 - 开盘价) / 开盘价) * 100
            decimal percentage = ((current - open) / open) * 100;
            return Math.Round(percentage, 2);
        }

        /// <summary>
        /// 计算相对于预定值的亏损百分比
        /// </summary>
        /// <param name="targetPrice">预定价格</param>
        /// <returns>亏损百分比（正数表示亏损，负数表示盈利），保留2位小数</returns>
        /// <exception cref="InvalidOperationException">当价格数据无效时抛出</exception>
        public decimal CalculateLossPercentage(decimal targetPrice)
        {
            if (!decimal.TryParse(CurrentPrice, out decimal current))
            {
                return -1;
            }

            if (targetPrice <= 0)
            {
                return -1;
            }

            // 亏损百分比 = ((预定价 - 当前价) / 预定价) * 100
            decimal percentage = ((targetPrice - current) / targetPrice) * 100;
            return Math.Round(percentage, 2);
        }

        /// <summary>
        /// 计算价格波动范围（最高价与最低价的百分比差异）
        /// </summary>
        /// <returns>波动范围百分比，保留2位小数</returns>
        /// <exception cref="InvalidOperationException">当价格数据无效时抛出</exception>
        public decimal CalculatePriceRangePercentage()
        {
            if (!decimal.TryParse(HighPrice, out decimal high) ||
                !decimal.TryParse(LowPrice, out decimal low))
            {
                return -1;
            }

            if (low == 0)
            {
                return -1;
            }

            // 波动范围 = ((最高价 - 最低价) / 最低价) * 100
            decimal rangePercentage = ((high - low) / low) * 100;
            return Math.Round(rangePercentage, 2);
        }
    }
}
