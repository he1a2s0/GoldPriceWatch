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
        [Description("克")]
        Gram,  // 克
        [Description("盎司")]
        Ounce  // 盎司
    }
}
