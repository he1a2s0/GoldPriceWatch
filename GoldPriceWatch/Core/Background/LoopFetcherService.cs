using MediatR;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
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

        public LoopFetcherService(IMediator mediator, GoldInvestmentConfig config)
        {
            this.mediator = mediator;
            _config = config;
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
                catch (Exception)
                {
                    await mediator.Publish(new FetchStatusChanged(FetchState.Waiting), CancellationToken.None);
                }
            }
        }

        public async Task Invoke(CancellationToken cancellationToken)
        {
            var result = await RequestCloud.GetGoldPrice();

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
