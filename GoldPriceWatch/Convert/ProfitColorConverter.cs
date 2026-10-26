using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Convert
{
    /// <summary>盈亏颜色：正红负绿（A 股习惯），零灰。</summary>
    public class ProfitColorConverter : IValueConverter
    {
        private static readonly Brush UpBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x4C, 0x4C));
        private static readonly Brush DownBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xB8, 0x6E));
        private static readonly Brush FlatBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xA5));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return d > 0 ? UpBrush : d < 0 ? DownBrush : FlatBrush;
            return FlatBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>买卖方向颜色：买红卖绿。</summary>
    public class SideColorConverter : IValueConverter
    {
        private static readonly Brush BuyBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0x4C, 0x4C));
        private static readonly Brush SellBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xB8, 0x6E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is OrderSide.Sell ? SellBrush : BuyBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
