namespace GoldPriceWatch.Model
{
    /// <summary>价格历史点（用于走势图表）。</summary>
    public class PricePoint
    {
        public DateTime Time { get; set; }
        public decimal Price { get; set; }
    }
}
