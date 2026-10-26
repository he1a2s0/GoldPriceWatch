using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Controls
{
    /// <summary>
    /// 自绘价格走势图表：网格、面积渐变、保本价虚线与最新价标注。
    /// </summary>
    public class PriceChartControl : FrameworkElement
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IList), typeof(PriceChartControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

        public IList? ItemsSource
        {
            get => (IList?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty BreakEvenPriceProperty =
            DependencyProperty.Register(nameof(BreakEvenPrice), typeof(decimal), typeof(PriceChartControl),
                new FrameworkPropertyMetadata(0M, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>保本价（绘制虚线参考线，0 不绘制）。</summary>
        public decimal BreakEvenPrice
        {
            get => (decimal)GetValue(BreakEvenPriceProperty);
            set => SetValue(BreakEvenPriceProperty, value);
        }

        private INotifyCollectionChanged? _subscribed;

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (PriceChartControl)d;
            if (ctrl._subscribed != null) ctrl._subscribed.CollectionChanged -= ctrl.OnCollectionChanged;
            ctrl._subscribed = e.NewValue as INotifyCollectionChanged;
            if (ctrl._subscribed != null) ctrl._subscribed.CollectionChanged += ctrl.OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

        public PriceChartControl()
        {
            SizeChanged += (_, _) => InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w = ActualWidth;
            var h = ActualHeight;
            if (w < 80 || h < 60) return;

            var points = ItemsSource?.Cast<PricePoint>().OrderBy(p => p.Time).ToList();

            // 背景
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));

            const double left = 62, right = 18, top = 16, bottom = 28;
            var plotW = w - left - right;
            var plotH = h - top - bottom;
            if (plotW <= 0 || plotH <= 0) return;

            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xEB)), 1);
            var axisBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x78));
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            if (points == null || points.Count == 0)
            {
                var empty = new FormattedText("暂无价格数据，等待首次刷新…", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 14, axisBrush, dpi);
                dc.DrawText(empty, new Point((w - empty.Width) / 2, (h - empty.Height) / 2));
                return;
            }

            decimal min = points.Min(p => p.Price);
            decimal max = points.Max(p => p.Price);
            if (BreakEvenPrice > 0)
            {
                min = Math.Min(min, BreakEvenPrice);
                max = Math.Max(max, BreakEvenPrice);
            }
            // 上下各留 8% 余量，平坦时至少 ±1
            var margin = (max - min) * 0.08M;
            if (margin < 1M) margin = 1M;
            min -= margin;
            max += margin;
            var range = max - min;
            if (range <= 0) range = 1M;

            double X(int i) => left + (points.Count == 1 ? plotW / 2 : plotW * i / (points.Count - 1));
            double Y(decimal price) => top + plotH * (double)(1 - (price - min) / range);

            // 横向网格 + 价格标签（5 条）
            for (int g = 0; g <= 4; g++)
            {
                var price = min + range * g / 4;
                var y = Y(price);
                dc.DrawLine(gridPen, new Point(left, y), new Point(w - right, y));
                var label = new FormattedText(price.ToString("F2"), CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 11, axisBrush, dpi);
                dc.DrawText(label, new Point(left - label.Width - 6, y - label.Height / 2));
            }

            // 时间标签（首/中/尾）
            void DrawTimeLabel(int idx)
            {
                var t = new FormattedText(points[idx].Time.ToString("MM-dd HH:mm"), CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 11, axisBrush, dpi);
                var x = Math.Clamp(X(idx) - t.Width / 2, 2, w - t.Width - 2);
                dc.DrawText(t, new Point(x, h - bottom + 8));
            }
            DrawTimeLabel(0);
            if (points.Count > 2) DrawTimeLabel(points.Count / 2);
            if (points.Count > 1) DrawTimeLabel(points.Count - 1);

            // 保本价虚线
            if (BreakEvenPrice > 0)
            {
                var beY = Y(BreakEvenPrice);
                var bePen = new Pen(new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B)), 1.2) { DashStyle = DashStyles.Dash };
                dc.DrawLine(bePen, new Point(left, beY), new Point(w - right, beY));
                var beLabel = new FormattedText($"保本 {BreakEvenPrice:F2}", CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 11,
                    new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B)), dpi);
                dc.DrawText(beLabel, new Point(w - right - beLabel.Width, beY - beLabel.Height - 2));
            }

            // 走势线 + 面积渐变（涨红跌绿）
            var last = points[^1].Price;
            var first = points[0].Price;
            var lineColor = last >= first ? Color.FromRgb(0xE8, 0x4C, 0x4C) : Color.FromRgb(0x2E, 0xB8, 0x6E);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(X(0), Y(points[0].Price)), true, false);
                for (int i = 1; i < points.Count; i++)
                    ctx.LineTo(new Point(X(i), Y(points[i].Price)), true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(lineColor), 1.8), geo);

            var fillGeo = new StreamGeometry();
            using (var ctx = fillGeo.Open())
            {
                ctx.BeginFigure(new Point(X(0), top + plotH), false, false);
                ctx.LineTo(new Point(X(0), Y(points[0].Price)), false, false);
                for (int i = 1; i < points.Count; i++)
                    ctx.LineTo(new Point(X(i), Y(points[i].Price)), false, false);
                ctx.LineTo(new Point(X(points.Count - 1), top + plotH), false, false);
            }
            fillGeo.Freeze();
            var fillBrush = new LinearGradientBrush(
                Color.FromArgb(0x55, lineColor.R, lineColor.G, lineColor.B),
                Color.FromArgb(0x05, lineColor.R, lineColor.G, lineColor.B), 90);
            dc.DrawGeometry(fillBrush, null, fillGeo);

            // 最新点 + 标注
            var lastX = X(points.Count - 1);
            var lastY = Y(last);
            dc.DrawEllipse(new SolidColorBrush(lineColor), null, new Point(lastX, lastY), 4, 4);
            var lastLabel = new FormattedText(last.ToString("F2"), CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 12,
                new SolidColorBrush(Colors.White), dpi);
            var lx = lastX - lastLabel.Width - 8;
            if (lx < left) lx = lastX + 8;
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xC0, lineColor.R, lineColor.G, lineColor.B)),
                null, new Rect(lx - 4, lastY - lastLabel.Height / 2 - 3, lastLabel.Width + 8, lastLabel.Height + 6));
            dc.DrawText(lastLabel, new Point(lx, lastY - lastLabel.Height / 2));
        }
    }
}
