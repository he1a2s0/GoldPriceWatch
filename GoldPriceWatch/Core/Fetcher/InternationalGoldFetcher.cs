using GoldPriceWatch.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Fetcher
{
    /// <summary>
    /// 国际金价（伦敦金现货，美元/盎司）取价。
    /// 数据源：新浪财经行情接口，返回形如
    /// var hq_str_hf_XAU="3848.78,3856.240,3848.78,3849.13,3864.53,3845.70,09:18:00,3856.24,3858.88,...,伦敦金（现货黄金）";
    /// 下标：0 最新价，4 最高，5 最低，7 昨收，8 开盘。
    /// hq.sinajs.cn 会限流（不带浏览器 UA 返回 404），失败时切换到内容格式完全相同的镜像源。
    /// 与国内金价接口相互独立：失败时返回 null，不影响国内取价。
    /// </summary>
    public class InternationalGoldFetcher
    {
        private static readonly Regex QuoteRegex =
            new("hq_str_hf_XAU\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);

        /// <summary>主备数据源，返回格式相同，按顺序尝试。</summary>
        private static readonly string[] Sources = { Constants.sinaIntlGoldAPI, Constants.guojijinjiaAPI };

        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

        private readonly ILogger<InternationalGoldFetcher> _logger;
        private DateTime _lastFailureLogTime = DateTime.MinValue;

        public InternationalGoldFetcher(ILogger<InternationalGoldFetcher> logger)
        {
            _logger = logger;
        }

        public async Task<GoldResult?> TryFetchAsync()
        {
            string? lastError = null;
            Exception? lastException = null;

            foreach (var url in Sources)
            {
                try
                {
                    var result = await FetchAsync(url);
                    if (result != null) return result;
                    lastError = $"{url} 未返回有效行情";
                }
                catch (Exception ex)
                {
                    lastError = $"{url} 请求失败：{ex.Message}";
                    lastException = ex;
                }
            }

            // 所有数据源都失败时只记一条日志（限流，避免刷屏），不抛出，避免拖垮主价管道
            LogFailure($"国际金价取价失败：{lastError}", lastException);
            return null;
        }

        private static async Task<GoldResult?> FetchAsync(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Referer", "https://finance.sina.com.cn");
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var response = await Http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            // 接口声明的字符集无效（GBK），按字节读取后自行解码（只解析数字字段，中文名称不参与解析）
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var text = Encoding.UTF8.GetString(bytes);

            var match = QuoteRegex.Match(text);
            if (!match.Success) return null;

            var fields = match.Groups[1].Value.Split(',');
            if (fields.Length < 9) return null;

            if (!decimal.TryParse(fields[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var price) ||
                price <= 0)
            {
                return null;
            }

            return new GoldResult
            {
                Source = "新浪财经",
                Currency = "USD",
                CurrentPrice = fields[0],
                HighPrice = fields[4],
                LowPrice = fields[5],
                OpenPrice = fields[8],
                SettlementPrice = fields[7]
            };
        }

        /// <summary>同一分钟内只记一条失败日志，避免刷新间隔很短时刷屏。</summary>
        private void LogFailure(string message, Exception? exception = null)
        {
            var now = DateTime.Now;
            if (now - _lastFailureLogTime < TimeSpan.FromMinutes(1)) return;
            _lastFailureLogTime = now;
            _logger.LogWarning(exception, message);
        }
    }
}
