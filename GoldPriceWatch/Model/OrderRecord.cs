using System.Text.Json.Serialization;

namespace GoldPriceWatch.Model
{
    /// <summary>买卖方向。</summary>
    public enum OrderSide
    {
        Buy,
        Sell
    }

    /// <summary>买入/卖出流水记录。</summary>
    public class OrderRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public OrderSide Side { get; set; }

        /// <summary>成交单价（元/克）。</summary>
        public decimal Price { get; set; }

        /// <summary>成交克重（克）。</summary>
        public decimal Weight { get; set; }

        public DateTime Time { get; set; } = DateTime.Now;

        [JsonIgnore]
        public decimal Amount => Price * Weight;

        [JsonIgnore]
        public string SideText => Side == OrderSide.Buy ? "买入" : "卖出";
    }
}
