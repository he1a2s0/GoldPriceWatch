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
        public static async Task<GoldResult> GetGoldPrice()
        {
            IGoldPriceFetcher[] platforms = [new cmbchinaFetcher(), new guojijinjiaFetcher(), new huilvbiaoFetcher()];
            foreach (var platform in platforms)
            {
                try
                {

                    var result = await platform.FetchCurrentPriceAsync();

                    return result;
                }
                finally
                {
                    
                }
            }

            return new GoldResult();
        }
    }
}
