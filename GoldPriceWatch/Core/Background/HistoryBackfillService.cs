using GoldPriceWatch.Core.Fetcher;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Background
{
    /// <summary>
    /// 首次运行时用最近 3 年的每日金价初始化数据库。
    /// 库中已有足够早的历史（7 天前就有数据）时直接跳过；导入按时间点去重，可重复执行。
    /// </summary>
    public class HistoryBackfillService : BackgroundService
    {
        /// <summary>需要覆盖的历史长度。</summary>
        private static readonly TimeSpan HistorySpan = TimeSpan.FromDays(1095);

        private readonly PriceHistoryDatabase _db;
        private readonly HistoricalGoldFetcher _fetcher;
        private readonly ILogger<HistoryBackfillService> _logger;

        public HistoryBackfillService(
            PriceHistoryDatabase db, HistoricalGoldFetcher fetcher, ILogger<HistoryBackfillService> logger)
        {
            _db = db;
            _fetcher = fetcher;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // 先让界面和实时取价跑起来
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                if (stoppingToken.IsCancellationRequested) return;

                var since = DateTime.Now - HistorySpan;
                var earliest = _db.GetEarliestTime();
                if (earliest.HasValue && earliest.Value <= since.AddDays(7))
                {
                    _logger.LogInformation("历史数据已覆盖 {Earliest:yyyy-MM-dd}，跳过补齐", earliest.Value);
                    return;
                }

                _logger.LogInformation("开始补齐历史日线，起始 {Since:yyyy-MM-dd}", since);
                var points = await _fetcher.FetchDailyAsync(since);
                if (points.Count > 0)
                {
                    _db.ImportDaily(points);
                    _logger.LogInformation("历史日线补齐完成：{Count} 天，{From:yyyy-MM-dd} ~ {To:yyyy-MM-dd}",
                        points.Count, points[0].Time, points[^1].Time);
                }
                else
                {
                    _logger.LogWarning("历史日线补齐失败：未获取到数据");
                }
            }
            catch (Exception ex)
            {
                // 补历史失败不影响实时取价
                _logger.LogError(ex, "历史日线补齐异常");
            }
        }
    }
}
