using Flurl.Http;
using GoldPriceWatch.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Fetcher
{
    public class huilvbiaoFetcher : IGoldPriceFetcher
    {
        public async Task<GoldResult> FetchCurrentPriceAsync()
        {
			try
			{
                var result = await (await Constants.huilvbiaoAPI.GetAsync())
                .GetStringAsync();

                var MatchGoldRegex = "(?<=var\\s*?hq_str_gds_AUTD\\s*?=\\s*?\").*(?=\";)";
                var MatchVar = Regex.Match(result, MatchGoldRegex);
                if (MatchVar.Success)
                {
                    string[] vals = MatchVar.Value.Split(",");
                    return new GoldResult
                    {
                        CurrentPrice = vals[0],
                        HighPrice = vals[4],
                        LowPrice = vals[5],
                        Currency = "Gold",
                        OpenPrice = vals[8],
                        SettlementPrice = vals[7]
                    };
                }
                throw new Exception("Regex match failed");
            }
			catch (Exception)
			{

				throw;
			}

        }
    }
}
