using Flurl.Http;
using GoldPriceWatch.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core.Fetcher
{
    public class cmbchinaFetcher : IGoldPriceFetcher
    {
        public record cmbchinaData(string avePrice, string curPrice, string goldNo, string high, string low, string open, string preClose, string time, string tradeCount, string upDown, string variety);
        public record cmbchinaBody(cmbchinaData[] data, string time);
        public record cmbchinaResult(cmbchinaBody body, string errorMsg, string returnCode);
        public async Task<GoldResult> FetchCurrentPriceAsync()
        {
            try
            {
                var result = await (await Constants.cmbchinaAPI.GetAsync())
                .GetJsonAsync<cmbchinaResult>();

                var data = result.body.data[0];
                return new GoldResult
                {
                    CurrentPrice = data.curPrice,
                    HighPrice = data.high,
                    LowPrice = data.low,
                    Currency = data.variety,
                    OpenPrice = data.open,
                    SettlementPrice = data.preClose
                };
            }
            catch (Exception)
            {

                throw;
            }

        }
    }
}
