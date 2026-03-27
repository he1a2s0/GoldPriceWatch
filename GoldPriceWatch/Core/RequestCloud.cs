using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Util;
using Flurl.Http.Newtonsoft;
using GoldPriceWatch.Core.Fetcher;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Core
{
    public class RequestCloud
    {
        private static readonly IGoldPriceFetcher[] _platforms = [new cmbchinaFetcher(), new guojijinjiaFetcher(), new huilvbiaoFetcher()];
        private static int _currentIndex = 0;

        // 每个 platform 下线后，至少等待此时长再做健康检查
        private static readonly TimeSpan _recoveryInterval = TimeSpan.FromMinutes(2);

        private static readonly PlatformHealth[] _health = _platforms
            .Select(_ => new PlatformHealth())
            .ToArray();

        // 后台健康检查任务（仅启动一次）
        private static readonly Task _healthCheckTask = RunHealthCheckLoopAsync();

        public static async Task<GoldResult> GetGoldPrice()
        {
            // 从当前索引开始，找到第一个在线的 platform
            for (int i = 0; i < _platforms.Length; i++)
            {
                int idx = (_currentIndex + i) % _platforms.Length;

                if (!_health[idx].IsOnline)
                    continue;

                // 找到可用 platform，推进索引到下一个位置
                _currentIndex = (idx + 1) % _platforms.Length;

                GoldResult? result = null;
                try
                {
                    result = await _platforms[idx].FetchCurrentPriceAsync();
                }
                catch
                {
                    // 请求异常，标记下线
                    _health[idx].MarkOffline();
                    continue;
                }

                // 检查结果是否有效
                if (result == null ||
                    !decimal.TryParse(result.CurrentPrice, out decimal price) ||
                    price == 0)
                {
                    _health[idx].MarkOffline();
                    continue;
                }

                _health[idx].MarkOnline();
                return result;
            }

            // 所有 platform 均不可用
            return new GoldResult();
        }

        // 后台循环：定期对已下线的 platform 发起探测，恢复则重新上线
        private static async Task RunHealthCheckLoopAsync()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));

                for (int i = 0; i < _platforms.Length; i++)
                {
                    var h = _health[i];
                    if (h.IsOnline)
                        continue;

                    // 尚未到恢复探测时间
                    if (DateTime.UtcNow - h.OfflineSince < _recoveryInterval)
                        continue;

                    try
                    {
                        var result = await _platforms[i].FetchCurrentPriceAsync();
                        if (result != null &&
                            decimal.TryParse(result.CurrentPrice, out decimal price) &&
                            price != 0)
                        {
                            h.MarkOnline();
                        }
                    }
                    catch
                    {
                        // 仍不可用，更新下线时间使下次探测推迟
                        h.MarkOffline();
                    }
                }
            }
        }

        private class PlatformHealth
        {
            public bool IsOnline { get; private set; } = true;
            public DateTime OfflineSince { get; private set; }

            public void MarkOffline()
            {
                IsOnline = false;
                OfflineSince = DateTime.UtcNow;
            }

            public void MarkOnline()
            {
                IsOnline = true;
            }
        }
    }
}
