using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Controls
{
    /// <summary>
    /// 自绘价格走势图表。
    /// 左轴为中国金价（元/克），右轴为国际金价（美元/盎司），带 X/Y 坐标轴、网格、
    /// 保本价虚线，以及鼠标悬停的十字虚线与数据提示。
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

        private static readonly Typeface TextFace =
            new(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xF1));
        private static readonly Brush AxisBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x95));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x78));
        private static readonly Brush CrossBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA0, 0xA6));
        private static readonly Brush IntlBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
        private static readonly Brush ChipBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x3C));

        private INotifyCollectionChanged? _subscribed;
        private Point? _hover;
        private double _dpi = 1;

        public PriceChartControl()
        {
            SizeChanged += (_, _) => InvalidateVisual();
            SnapsToDevicePixels = true;
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (PriceChartControl)d;
            if (ctrl._subscribed != null) ctrl._subscribed.CollectionChanged -= ctrl.OnCollectionChanged;
            ctrl._subscribed = e.NewValue as INotifyCollectionChanged;
            if (ctrl._subscribed != null) ctrl._subscribed.CollectionChanged += ctrl.OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _hover = e.GetPosition(this);
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = null;
            InvalidateVisual();
        }

        private FormattedText MakeText(string text, double size, Brush brush, FontWeight? weight = null) =>
            new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(TextFace.FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
                size, brush, _dpi);

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            var w = ActualWidth;
            var h = ActualHeight;
            if (w < 120 || h < 90) return;

            _dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            var points = ItemsSource?.Cast<PricePoint>()
                .Where(p => p != null)
                .OrderBy(p => p.Time)
                .ToList() ?? new();

            // 背景
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));

            var hasIntl = points.Any(p => p.InternationalPrice.HasValue);
            var left = 64.0;
            var right = hasIntl ? 78.0 : 22.0;
            const double top = 38;
            var bottom = 32.0;
            var plotW = w - left - right;
            var plotH = h - top - bottom;
            if (plotW <= 20 || plotH <= 20) return;

            var plotRect = new Rect(left, top, plotW, plotH);

            if (points.Count == 0)
            {
                var empty = MakeText("暂无价格数据，等待首次刷新…", 14, AxisBrush);
                dc.DrawText(empty, new Point((w - empty.Width) / 2, (h - empty.Height) / 2));
                return;
            }

            // ── 国内金价纵轴范围（含保本价） ──
            decimal min = points.Min(p => p.Price);
            decimal max = points.Max(p => p.Price);
            if (BreakEvenPrice > 0)
            {
                min = Math.Min(min, BreakEvenPrice);
                max = Math.Max(max, BreakEvenPrice);
            }
            var margin = (max - min) * 0.08M;
            if (margin < 1M) margin = 1M;
            min -= margin;
            max += margin;
            var range = max - min;
            if (range <= 0) range = 1M;

            // ── 国际金价纵轴范围 ──
            var intlValues = points.Where(p => p.InternationalPrice.HasValue)
                .Select(p => p.InternationalPrice!.Value).ToList();
            var intlMin = 0M;
            var intlMax = 1M;
            if (intlValues.Count > 0)
            {
                intlMin = intlValues.Min();
                intlMax = intlValues.Max();
                var intlMargin = (intlMax - intlMin) * 0.08M;
                if (intlMargin <= 0M) intlMargin = 0.5M;
                intlMin -= intlMargin;
                intlMax += intlMargin;
            }
            var intlRange = intlMax - intlMin;
            if (intlRange <= 0) intlRange = 1M;

            // ── 横轴：按真实时间映射，保证不同区间的点数密度一致 ──
            var t0 = points[0].Time;
            var t1 = points[^1].Time;
            var spanMs = Math.Max(1.0, (t1 - t0).TotalMilliseconds);

            double X(DateTime t) => points.Count == 1
                ? left + plotW / 2
                : left + plotW * (t - t0).TotalMilliseconds / spanMs;
            double Y(decimal price) => top + plotH * (1 - (double)((price - min) / range));
            double YIntl(decimal price) => top + plotH * (1 - (double)((price - intlMin) / intlRange));

            // ── 横向网格 + 左右两侧价格刻度 ──
            var gridPen = new Pen(GridBrush, 1);
            for (var g = 0; g <= 4; g++)
            {
                var y = top + plotH * g / 4;
                dc.DrawLine(gridPen, new Point(left, y), new Point(left + plotW, y));

                var price = max - range * g / 4;
                var label = MakeText(price.ToString("F2"), 11, TextBrush);
                dc.DrawText(label, new Point(left - label.Width - 6, y - label.Height / 2));

                if (hasIntl)
                {
                    var intlPrice = intlMax - intlRange * g / 4;
                    var intlLabel = MakeText(intlPrice.ToString("F2"), 11, IntlBrush);
                    dc.DrawText(intlLabel, new Point(left + plotW + 6, y - intlLabel.Height / 2));
                }
            }

            // ── 纵向网格 + 时间刻度 ──
            var xPen = new Pen(GridBrush, 1) { DashStyle = DashStyles.Dot };
            for (var g = 0; g <= 4; g++)
            {
                var x = left + plotW * g / 4;
                dc.DrawLine(xPen, new Point(x, top), new Point(x, top + plotH));

                var t = t0.AddMilliseconds(spanMs * g / 4);
                var label = MakeText(FormatTime(t, spanMs), 11, TextBrush);
                var lx = g switch
                {
                    0 => x,
                    4 => x - label.Width,
                    _ => x - label.Width / 2
                };
                dc.DrawText(label, new Point(lx, top + plotH + 7));
            }

            // ── 坐标轴 ──
            var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(0xC9, 0xC9, 0xD2)), 1);
            dc.DrawLine(axisPen, new Point(left, top), new Point(left, top + plotH));
            dc.DrawLine(axisPen, new Point(left, top + plotH), new Point(left + plotW, top + plotH));
            if (hasIntl)
            {
                dc.DrawLine(axisPen, new Point(left + plotW, top), new Point(left + plotW, top + plotH));
            }

            // 轴标题
            var cnyTitle = MakeText("元/克", 10, AxisBrush);
            dc.DrawText(cnyTitle, new Point(left - cnyTitle.Width - 4, top - 26));
            if (hasIntl)
            {
                var intlTitle = MakeText("美元/盎司", 10, IntlBrush);
                dc.DrawText(intlTitle, new Point(left + plotW + 6, top - 26));
            }

            // ── 保本价虚线（国内金价轴） ──
            if (BreakEvenPrice > 0)
            {
                var beY = Y(BreakEvenPrice);
                if (beY >= top - 1 && beY <= top + plotH + 1)
                {
                    var beBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B));
                    var bePen = new Pen(beBrush, 1.2) { DashStyle = DashStyles.Dash };
                    dc.DrawLine(bePen, new Point(left, beY), new Point(left + plotW, beY));
                    var beLabel = MakeText($"保本 {BreakEvenPrice:F2}", 11, beBrush);
                    dc.DrawText(beLabel, new Point(left + plotW - beLabel.Width, beY - beLabel.Height - 2));
                }
            }

            // ── 国内金价曲线 + 面积渐变（涨红跌绿） ──
            var last = points[^1].Price;
            var first = points[0].Price;
            var lineColor = last >= first ? Color.FromRgb(0xE8, 0x4C, 0x4C) : Color.FromRgb(0x2E, 0xB8, 0x6E);
            var lineBrush = new SolidColorBrush(lineColor);
            var linePts = points.Select(p => new Point(X(p.Time), Y(p.Price))).ToList();

            if (linePts.Count > 1)
            {
                var fillGeo = new StreamGeometry();
                using (var ctx = fillGeo.Open())
                {
                    ctx.BeginFigure(new Point(linePts[0].X, top + plotH), true, false);
                    foreach (var pt in linePts) ctx.LineTo(pt, false, false);
                    ctx.LineTo(new Point(linePts[^1].X, top + plotH), false, false);
                }
                fillGeo.Freeze();
                var fillBrush = new LinearGradientBrush(
                    Color.FromArgb(0x50, lineColor.R, lineColor.G, lineColor.B),
                    Color.FromArgb(0x04, lineColor.R, lineColor.G, lineColor.B), 90);
                dc.DrawGeometry(fillBrush, null, fillGeo);

                dc.DrawGeometry(null, new Pen(lineBrush, 1.8) { LineJoin = PenLineJoin.Round },
                    BuildLine(linePts));
            }

            // ── 国际金价曲线（叠在国内曲线上方，便于同时观察两条曲线） ──
            var intlPoints = points.Where(p => p.InternationalPrice.HasValue).ToList();
            if (intlPoints.Count > 1)
            {
                dc.DrawGeometry(null, new Pen(IntlBrush, 1.4) { LineJoin = PenLineJoin.Round },
                    BuildLine(intlPoints.Select(p => new Point(X(p.Time), YIntl(p.InternationalPrice!.Value)))));
            }

            // 最新点
            var lastPt = linePts[^1];
            dc.DrawEllipse(lineBrush, null, lastPt, 4, 4);
            if (hasIntl && intlPoints.Count > 0)
            {
                var lastIntl = intlPoints[^1];
                dc.DrawEllipse(IntlBrush, null, new Point(X(lastIntl.Time), YIntl(lastIntl.InternationalPrice!.Value)), 3.5, 3.5);
            }

            // ── 鼠标悬停：十字虚线与数据提示 ──
            if (_hover is { } hover && plotRect.Contains(hover))
            {
                DrawHover(dc, points, hover, w, h, left, top, plotW, plotH, intlMin, intlRange, X, Y);
            }
        }

        private void DrawHover(
            DrawingContext dc, List<PricePoint> points, Point hover, double w, double h,
            double left, double top, double plotW, double plotH,
            decimal intlMin, decimal intlRange,
            Func<DateTime, double> X, Func<decimal, double> Y)
        {
            // 找离鼠标最近的点
            var best = 0;
            var bestDistance = double.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var distance = Math.Abs(X(points[i].Time) - hover.X);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            var point = points[best];
            var px = X(point.Time);
            var py = Y(point.Price);

            var crossPen = new Pen(CrossBrush, 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(crossPen, new Point(px, top), new Point(px, top + plotH));
            dc.DrawLine(crossPen, new Point(left, py), new Point(left + plotW, py));

            // 国内价定位点
            dc.DrawEllipse(Brushes.White, null, new Point(px, py), 6, 6);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B)), null, new Point(px, py), 4, 4);

            // 国际价定位点
            Point? intlPt = null;
            if (point.InternationalPrice.HasValue)
            {
                var iy = top + plotH * (1 - (double)((point.InternationalPrice.Value - intlMin) / intlRange));
                intlPt = new Point(px, iy);
                dc.DrawEllipse(Brushes.White, null, intlPt.Value, 5.5, 5.5);
                dc.DrawEllipse(IntlBrush, null, intlPt.Value, 3.5, 3.5);
            }

            // X 轴时间点标签
            var timeText = point.Time.ToString("yyyy-MM-dd HH:mm:ss");
            var timeLabel = MakeText(timeText, 11, Brushes.White);
            var chipW = timeLabel.Width + 12;
            var chipX = Math.Clamp(px - chipW / 2, left - 8, left + plotW - chipW + 8);
            var chipY = top + plotH + 3;
            dc.DrawRoundedRectangle(ChipBrush, null, new Rect(chipX, chipY, chipW, timeLabel.Height + 4), 3, 3);
            dc.DrawText(timeLabel, new Point(chipX + 6, chipY + 2));

            // 提示框内容：时间 + 各个价格
            var lines = new List<(string Text, Brush Brush)>
            {
                (timeText, TextBrush),
                ($"中国金价  {point.Price:F2} 元/克", new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x0B))),
            };
            if (point.InternationalPrice.HasValue)
            {
                lines.Add(($"国际金价  {point.InternationalPrice.Value:F2} 美元/盎司", IntlBrush));
            }
            else
            {
                lines.Add(("国际金价  暂无", AxisBrush));
            }

            var texts = lines.Select(l => MakeText(l.Text, 12, l.Brush)).ToList();
            var boxW = texts.Max(t => t.Width) + 20;
            var boxH = texts.Sum(t => t.Height) + 12;

            const double offset = 14;
            var boxX = px + offset;
            if (boxX + boxW > w - 4) boxX = px - offset - boxW;
            var boxY = (intlPt?.Y ?? py) - boxH / 2;
            if (boxY < 4) boxY = 4;
            if (boxY + boxH > h - 4) boxY = h - 4 - boxH;

            var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xE0)), 1);
            dc.DrawRoundedRectangle(Brushes.White, borderPen, new Rect(boxX, boxY, boxW, boxH), 4, 4);

            var textY = boxY + 6;
            foreach (var text in texts)
            {
                dc.DrawText(text, new Point(boxX + 10, textY));
                textY += text.Height;
            }
        }

        private static StreamGeometry BuildLine(IEnumerable<Point> points)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                var first = true;
                foreach (var pt in points)
                {
                    if (first)
                    {
                        ctx.BeginFigure(pt, false, false);
                        first = false;
                    }
                    else
                    {
                        ctx.LineTo(pt, true, false);
                    }
                }
            }
            geo.Freeze();
            return geo;
        }

        /// <summary>按时间跨度选择合适的刻度格式。</summary>
        private static string FormatTime(DateTime time, double spanMs)
        {
            var span = TimeSpan.FromMilliseconds(spanMs);
            if (span <= TimeSpan.FromDays(2)) return time.ToString("HH:mm");
            if (span <= TimeSpan.FromDays(14)) return time.ToString("MM-dd HH:mm");
            if (span <= TimeSpan.FromDays(120)) return time.ToString("MM-dd");
            return time.ToString("yyyy-MM");
        }
    }
}
