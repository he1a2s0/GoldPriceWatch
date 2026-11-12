using Flurl.Http;
using GoldPriceWatch.Model;
using System;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Fetcher
{
    /// <summary>
    /// 浙商银行财富金积存报价。
    /// 接口返回：{"code":200,"msg":"success","data":{"price":846.67,"change":-14.16,
    /// "change_pct":-1.64,"prev_close":860.83,"update_time":"17:42:04","cached":true,...}}
    /// </summary>
    public class zheshangFetcher : IGoldPriceFetcher
    {
        public async Task<GoldResult> FetchCurrentPriceAsync()
        {
            var json = await (await Constants.zheshangAPI.GetAsync()).GetStringAsync();

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
            {
                string msg = root.TryGetProperty("msg", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                throw new Exception($"zheshang: api returned {code}, {msg}");
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new Exception("zheshang: missing data");

            decimal price = ReadDecimal(data, "price");
            decimal prevClose = ReadDecimal(data, "prev_close");

            if (price <= 0)
                throw new Exception("zheshang: invalid price");

            // 接口不提供当日最高/最低价，用当前价占位，避免界面显示 0.00
            string priceText = price.ToString("F2", CultureInfo.InvariantCulture);

            return new GoldResult
            {
                Source = "浙商银行",
                Currency = "CNY",
                CurrentPrice = priceText,
                // 接口只给 prev_close，没有开盘价；用昨结算作为涨跌幅基准，与 change_pct 口径一致
                OpenPrice = prevClose > 0 ? prevClose.ToString("F2", CultureInfo.InvariantCulture) : priceText,
                SettlementPrice = prevClose.ToString("F2", CultureInfo.InvariantCulture),
                HighPrice = priceText,
                LowPrice = priceText
            };
        }

        private static decimal ReadDecimal(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                return 0m;

            return property.ValueKind switch
            {
                JsonValueKind.Number => property.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0m,
                _ => 0m
            };
        }
    }
}
