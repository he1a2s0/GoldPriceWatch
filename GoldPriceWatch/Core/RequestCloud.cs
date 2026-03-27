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

        public static async Task<GoldResult> GetGoldPrice()
        {
            var platform = _platforms[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _platforms.Length;

            try
            {
                var result = await platform.FetchCurrentPriceAsync();
                return result;
            }
            finally
            {

            }
        }
    }
}
