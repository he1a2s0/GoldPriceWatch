using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldPriceWatch.Enums
{

    /// <summary>
    /// 警报阈值类型枚举。
    /// </summary>
    public enum AlertThresholdType
    {
        [Description("百分比")]
        Percentage,  // 百分比
        [Description("金额")]
        Amount       // 金额
    }
}
