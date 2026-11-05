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

        /// <summary>成交克重（克）。由 Amount / Price 反推，旧数据未写 Amount 时仍可直接存入。</summary>
        public decimal Weight { get; set; }

        /// <summary>成交总金额（元）。新录入必填；旧数据为 null 时回退 Price * Weight。</summary>
        public decimal? Amount { get; set; }

        public DateTime Time { get; set; } = DateTime.Now;

        /// <summary>实际成交金额：优先持久化的 Amount，没有时用 Price*Weight 兜底。</summary>
        [JsonIgnore]
        public decimal GetAmount => Amount ?? Price * Weight;

        [JsonIgnore]
        public string SideText => Side == OrderSide.Buy ? "买入" : "卖出";
    }
}
