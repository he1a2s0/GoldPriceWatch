using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace GoldPriceWatch.Convert
{
    public class EnumToTitleValueConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            // 获取枚举值对应的Display特性
            MemberInfo[] memberInfo = value.GetType().GetMember(value.ToString());
            if (memberInfo.Length > 0)
            {
                var attributes = memberInfo[0].GetCustomAttributes(typeof(DisplayAttribute), false);
                if (attributes.Length > 0)
                {
                    // 返回 "Display名称 (枚举值)" 格式
                    return $"{((DisplayAttribute)attributes[0]).Name} ({value})";
                }
            }

            // 如果没有Display特性，直接返回枚举值
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 这里简化处理，实际应用中可能需要更复杂的转换逻辑
            if (value == null) return null;

            string stringValue = value.ToString();
            int index = stringValue.IndexOf('(');
            if (index > 0)
            {
                string enumValue = stringValue.Substring(index + 1).TrimEnd(')');
                return Enum.Parse(targetType, enumValue);
            }

            return Enum.Parse(targetType, stringValue);
        }
    }
}
