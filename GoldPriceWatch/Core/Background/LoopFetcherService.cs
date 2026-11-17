using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using GoldPriceWatch.Core.Fetcher;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Core.Background
{
    /// <summary>
    /// 后台取价循环：按配置的刷新间隔轮询金价，并通过 MediatR 发布结果与状态。
    /// </summary>
    public class LoopFetcherService : BackgroundService
    {
        private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(4);

        private readonly IMediator mediator;
        private readonly GoldInvestmentConfig _config;
        private readonly InternationalGoldFetcher _internationalFetcher;
        private readonly ILogger<LoopFetcherService> _logger;

        private string? _lastSource;
        private DateTime _lastErrorLogTime = DateTime.MinValue;

        public LoopFetcherService(
            IMediator mediator,
            GoldInvestmentConfig config,
            InternationalGoldFetcher internationalFetcher,
            ILogger<LoopFetcherService> logger)
        {
            this.mediator = mediator;
            _config = config;
            _internationalFetcher = internationalFetcher;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 在应用运行期间持续执行
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await mediator.Publish(new FetchStatusChanged(FetchState.Fetching), stoppingToken);

                    await InvokeWithTimeout(stoppingToken);

                    await mediator.Publish(new FetchStatusChanged(FetchState.Waiting), stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, _config.RefreshIntervalSeconds)), stoppingToken);
                }
                catch (Exception ex)
                {
                    // 取价失败可能每个周期都发生，限制为一分钟一条
                    var now = DateTime.Now;
                    if (now - _lastErrorLogTime > TimeSpan.FromMinutes(1))
                    {
                        _lastErrorLogTime = now;
                        _logger.LogWarning(ex, "金价获取失败");
                    }

                    await mediator.Publish(new FetchStatusChanged(FetchState.Waiting), CancellationToken.None);
                }
            }
        }

        public async Task Invoke(CancellationToken cancellationToken)
        {
            // 国内金价与国际金价并行获取，国际金价失败不影响国内结果
            var domesticTask = RequestCloud.GetGoldPrice();
            var internationalTask = _internationalFetcher.TryFetchAsync();

            var result = await domesticTask;
            var international = await internationalTask;
            if (international != null)
            {
                result.InternationalPrice = international.CurrentPrice;
                result.InternationalSource = international.Source;
            }

            _logger.LogDebug("取价成功：{Price} 元/克（{Source}），国际 {Intl} 美元/盎司",
                result.CurrentPrice, result.Source, result.InternationalPrice ?? "--");

            if (result.Source != null && result.Source != _lastSource)
            {
                _lastSource = result.Source;
                _logger.LogInformation("国内金价数据源切换为：{Source}", result.Source);
            }

            await mediator.Publish(result, cancellationToken);
        }

        private async Task InvokeWithTimeout(CancellationToken cancellationToken)
        {
            var fetchTask = Invoke(cancellationToken);
            var timeoutTask = Task.Delay(FetchTimeout, cancellationToken);
            var completedTask = await Task.WhenAny(fetchTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Gold price fetch timeout after {FetchTimeout.TotalSeconds:F0}s.");
            }

            await fetchTask;
        }
    }
}
