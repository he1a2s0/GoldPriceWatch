using MediatR;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Background
{
    public class LoopFetcherService : BackgroundService
    {

        private readonly IMediator mediator;

        public LoopFetcherService(IMediator mediator)
        {
            this.mediator = mediator;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 在应用运行期间持续执行
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {

                    await Invoke();

                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    
                }
            }
        }

        public async Task Invoke()
        {
            var result = await RequestCloud.GetGoldPrice();

            mediator.Publish(result);
        }
    }
}
