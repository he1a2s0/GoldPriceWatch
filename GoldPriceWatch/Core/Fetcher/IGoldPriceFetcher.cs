using GoldPriceWatch.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Fetcher
{
    public interface IGoldPriceFetcher
    {
        Task<GoldResult> FetchCurrentPriceAsync();
    }
}
