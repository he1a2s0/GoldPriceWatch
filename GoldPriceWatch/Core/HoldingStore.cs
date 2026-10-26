using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Core
{
    /// <summary>
    /// 持仓监控数据持久化：买卖流水、费率、提醒阈值、价格历史。
    /// 存储于 %LocalAppData%\GoldPriceWatch\holding.json。
    /// </summary>
    public class HoldingStore
    {
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoldPriceWatch");
        private static readonly string DataFile = Path.Combine(DataDir, "holding.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public List<OrderRecord> Orders { get; set; } = new();

        /// <summary>卖出费率（百分比，默认 0.4%）。</summary>
        public decimal FeeRatePercent { get; set; } = 0.4m;

        public bool AlertEnabled { get; set; } = true;

        /// <summary>高价提醒线（0 表示不启用）。</summary>
        public decimal AlertHighPrice { get; set; }

        /// <summary>低价提醒线（0 表示不启用）。</summary>
        public decimal AlertLowPrice { get; set; }

        public List<PricePoint> PriceHistory { get; set; } = new();

        /// <summary>
        /// 一次性迁移用的刷新间隔（秒）。仅当从旧版 GoldMonitor 导入时填充，
        /// 由 HoldingViewModel 应用后清空；正常为空。
        /// </summary>
        public int? RefreshIntervalSeconds { get; set; }

        public static HoldingStore Load()
        {
            try
            {
                if (File.Exists(DataFile))
                {
                    var json = File.ReadAllText(DataFile);
                    return JsonSerializer.Deserialize<HoldingStore>(json, JsonOptions) ?? new HoldingStore();
                }

                // 首次运行：尝试迁移旧版 GoldMonitor 的数据与设置
                var migrated = MigrateFromLegacyGoldMonitor();
                if (migrated != null)
                {
                    migrated.Save();
                    return migrated;
                }
            }
            catch (Exception)
            {
                // 配置损坏时回退到默认值
            }
            return new HoldingStore();
        }

        #region 旧版 GoldMonitor 数据迁移
        private static readonly string LegacyDataFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoldMonitor", "data.json");

        /// <summary>
        /// 从旧版 GoldMonitor 的 data.json 迁移订单、费率/提醒设置与价格历史。
        /// 字段差异：旧版费率 FeeRate 为比率（0.004），此处存百分比（0.4）；
        /// 旧版 Id 为字符串，转为 Guid（解析失败则生成新值）。
        /// </summary>
        private static HoldingStore? MigrateFromLegacyGoldMonitor()
        {
            if (!File.Exists(LegacyDataFile)) return null;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(LegacyDataFile));
                var root = doc.RootElement;
                var store = new HoldingStore();

                if (root.TryGetProperty("Orders", out var orders) && orders.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in orders.EnumerateArray())
                    {
                        if (!TryGetDecimal(o, "Price", out var price) || !TryGetDecimal(o, "Weight", out var weight))
                            continue;

                        var order = new OrderRecord { Price = price, Weight = weight };

                        if (o.TryGetProperty("Id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                            && Guid.TryParse(idEl.GetString(), out var id))
                            order.Id = id;
                        if (o.TryGetProperty("Side", out var sideEl) && sideEl.TryGetInt32(out var side))
                            order.Side = side == 1 ? OrderSide.Sell : OrderSide.Buy;
                        if (o.TryGetProperty("Time", out var timeEl) && timeEl.TryGetDateTime(out var time))
                            order.Time = time;

                        store.Orders.Add(order);
                    }
                }

                if (root.TryGetProperty("Settings", out var settings))
                {
                    if (TryGetDecimal(settings, "FeeRate", out var feeRate))
                        store.FeeRatePercent = feeRate * 100m;
                    if (settings.TryGetProperty("AlertEnabled", out var alertEl)
                        && (alertEl.ValueKind == JsonValueKind.True || alertEl.ValueKind == JsonValueKind.False))
                        store.AlertEnabled = alertEl.GetBoolean();
                    if (TryGetDecimal(settings, "AlertHighPrice", out var high))
                        store.AlertHighPrice = high;
                    if (TryGetDecimal(settings, "AlertLowPrice", out var low))
                        store.AlertLowPrice = low;
                    if (settings.TryGetProperty("RefreshIntervalSeconds", out var intervalEl)
                        && intervalEl.TryGetInt32(out var interval) && interval > 0)
                        store.RefreshIntervalSeconds = interval;
                }

                if (root.TryGetProperty("PriceHistory", out var history) && history.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in history.EnumerateArray())
                    {
                        if (!TryGetDecimal(p, "Price", out var price))
                            continue;
                        var point = new PricePoint { Price = price };
                        if (p.TryGetProperty("Time", out var timeEl) && timeEl.TryGetDateTime(out var time))
                            point.Time = time;
                        store.PriceHistory.Add(point);
                    }
                }

                return store;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TryGetDecimal(JsonElement element, string name, out decimal value)
        {
            value = 0m;
            return element.TryGetProperty(name, out var prop) && prop.TryGetDecimal(out value);
        }
        #endregion

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                // 仅保留最近 3 天的价格历史，避免文件无限增长
                var cutoff = DateTime.Now.AddDays(-3);
                PriceHistory = PriceHistory.Where(p => p.Time >= cutoff).ToList();
                File.WriteAllText(DataFile, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception)
            {
                // 写入失败时静默忽略，不影响主流程
            }
        }
    }
}
