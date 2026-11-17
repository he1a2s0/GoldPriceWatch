using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldPriceWatch.Core
{
    public static class Constants
    {
        public static string zheshangAPI = "https://jin.20021002.xyz/api.php?type=zs";
        public static string huilvbiaoAPI = "https://www.huilvbiao.com/api/gold_indexApi";
        public static string guojijinjiaAPI = "https://www.guojijinjia.com/d/gold.js";
        public static string cmbchinaAPI = "https://m.cmbchina.com/api/rate/gold?no=AUTD";
        /// <summary>新浪财经国际现货黄金（伦敦金，美元/盎司）。</summary>
        public static string sinaIntlGoldAPI = "https://hq.sinajs.cn/list/hf_XAU";
        // 历史日 K 线（首次运行填充数据库用）
        public static string sinaXauDailyAPI = "https://stock2.finance.sina.com.cn/futures/api/jsonp.php/var%20_XAU=/GlobalFuturesService.getGlobalFuturesDailyKLine?symbol=XAU";
        public static string sinaAu0DailyAPI = "https://stock2.finance.sina.com.cn/futures/api/jsonp.php/var%20_AU0=/InnerFuturesNewService.getDailyKLine?symbol=AU0";

    }
}
