using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace GoldPriceWatch.Convert
{
    public class BoolToStringConverter : IValueConverter
    {
        // 正向转换：bool → string
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // 可以添加参数处理来自定义输出
                if (parameter is string options)
                {
                    var parts = options.Split('|');
                    if (parts.Length == 2)
                    {
                        return boolValue ? parts[0] : parts[1];
                    }
                }

                // 默认转换
                return boolValue ? "是" : "否";
            }
            return "无效值";
        }

        // 反向转换：string → bool (通常不需要)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
