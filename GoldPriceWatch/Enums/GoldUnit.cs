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
    /// 黄金单位枚举。
    /// </summary>
    public enum GoldUnit
    {
        [Description("g")]
        Gram,  // 克
        [Description("oz")]
        Ounce  // 盎司
    }
}
