namespace GoldPriceWatch.Model
{
    /// <summary>价格历史点（用于走势图表，持久化在 SQLite 中）。</summary>
    public class PricePoint
    {
        public DateTime Time { get; set; }

        /// <summary>国内金价（元/克）。</summary>
        public decimal Price { get; set; }

        /// <summary>国际金价（美元/盎司），早期数据可能为空。</summary>
        public decimal? InternationalPrice { get; set; }
    }
}
