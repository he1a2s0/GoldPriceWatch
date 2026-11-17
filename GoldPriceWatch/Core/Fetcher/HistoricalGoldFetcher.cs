using GoldPriceWatch.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Fetcher
{
    /// <summary>
    /// 历史金价（日 K 线）取价，用于首次运行时把最近若干年的历史填充进数据库。
    /// 国内取沪金主力连续 AU0（元/克），国际取伦敦金 XAU（美元/盎司），按交易日合并成每天一个点。
    /// </summary>
    public class HistoricalGoldFetcher
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(25);

        private readonly ILogger<HistoricalGoldFetcher> _logger;

        public HistoricalGoldFetcher(ILogger<HistoricalGoldFetcher> logger)
        {
            _logger = logger;
        }

        /// <summary>抓取 since 之后（含）的每日收盘价，按日期升序返回。</summary>
        public async Task<List<PricePoint>> FetchDailyAsync(DateTime since)
        {
            var cnyTask = FetchSeriesAsync(Constants.sinaAu0DailyAPI, "d", "c", since, "沪金AU0");
            var usdTask = FetchSeriesAsync(Constants.sinaXauDailyAPI, "date", "close", since, "伦敦金XAU");
            await Task.WhenAll(cnyTask, usdTask);

            var cny = cnyTask.Result;
            var usd = usdTask.Result;
            _logger.LogInformation("历史日线解析完成：沪金 {CnyDays} 天，伦敦金 {UsdDays} 天，起始 {Since:yyyy-MM-dd}",
                cny.Count, usd.Count, since);

            if (cny.Count == 0) return new List<PricePoint>();

            // 以国内交易日为主轴，国际价格缺失时沿用前一个交易日的收盘价，保证两条曲线都有值
            var usdDates = usd.Keys.OrderBy(d => d).ToList();
            var lastUsd = (decimal?)null;
            var usdIndex = 0;

            var points = new List<PricePoint>(cny.Count);
            foreach (var date in cny.Keys.OrderBy(d => d))
            {
                while (usdIndex < usdDates.Count && usdDates[usdIndex] <= date)
                {
                    lastUsd = usd[usdDates[usdIndex]];
                    usdIndex++;
                }

                points.Add(new PricePoint
                {
                    Time = date.AddHours(15).AddMinutes(30),
                    Price = cny[date],
                    InternationalPrice = lastUsd
                });
            }

            return points;
        }

        /// <summary>
        /// 解析新浪 K 线接口。返回内容是 JS 包裹的数组（var _XAU=([...]);），
        /// 截取首个 [ 到末个 ] 后按 JSON 解析。
        /// </summary>
        private async Task<Dictionary<DateTime, decimal>> FetchSeriesAsync(
            string url, string dateKey, string closeKey, DateTime since, string name)
        {
            var result = new Dictionary<DateTime, decimal>();
            try
            {
                using var http = new HttpClient { Timeout = Timeout };
                // 接口声明的字符集不合法，GetStringAsync 会直接抛异常；按字节读取后自行解码（内容为 ASCII JSON）
                var bytes = await http.GetByteArrayAsync(url);
                var text = Encoding.UTF8.GetString(bytes);

                var start = text.IndexOf('[');
                var end = text.LastIndexOf(']');
                if (start < 0 || end <= start) return result;

                using var doc = JsonDocument.Parse(text.Substring(start, end - start + 1));
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (!item.TryGetProperty(dateKey, out var dateElement) ||
                        !item.TryGetProperty(closeKey, out var closeElement))
                    {
                        continue;
                    }

                    if (!DateTime.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var date))
                    {
                        continue;
                    }

                    if (date < since.Date) continue;

                    if (!decimal.TryParse(closeElement.GetString(), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out var close) || close <= 0)
                    {
                        continue;
                    }

                    result[date.Date] = close;
                }
            }
            catch (Exception ex)
            {
                // 单个数据源失败时返回已解析到的部分
                _logger.LogWarning(ex, "历史日线获取失败：{Name}", name);
            }

            return result;
        }
    }
}
