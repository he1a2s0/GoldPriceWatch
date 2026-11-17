using System;
using System.Collections.Generic;
using System.IO;
using GoldPriceWatch.Model;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GoldPriceWatch.Core
{
    /// <summary>
    /// 价格历史数据库（SQLite）：每次取价落一行，同时保存国内金价（元/克）与国际金价（美元/盎司）。
    /// 存储于 %LocalAppData%\GoldPriceWatch\goldprice.db。
    ///
    /// 容量控制：Raw(0) 明细仅保留 7 天，之后按小时聚合为 Hour(1)；
    /// Hour 保留 60 天，之后按天聚合为 Day(2)；Day 保留 3 年。
    /// 查询时按时间跨度做分桶平均，最多返回 maxPoints 个点，保证长时间区间也能流畅绘制。
    /// </summary>
    public sealed class PriceHistoryDatabase
    {
        private const int KindRaw = 0;
        private const int KindHour = 1;
        private const int KindDay = 2;

        /// <summary>分桶平均的最低粒度：同一价格 60 秒内不重复落库。</summary>
        private const long DuplicateWindowMs = 60_000;

        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoldPriceWatch");

        private static readonly string DataFile = Path.Combine(DataDir, "goldprice.db");

        private readonly string _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DataFile
        }.ToString();

        private readonly object _sync = new();
        private readonly ILogger<PriceHistoryDatabase>? _logger;

        // 最近一次落库的内容，用于跳过重复写入
        private long _lastTimeMs;
        private decimal _lastPrice;
        private decimal? _lastInternationalPrice;

        // 写入失败只记一次日志，避免每个刷新周期都刷屏
        private bool _writeFailureLogged;

        public PriceHistoryDatabase(ILogger<PriceHistoryDatabase>? logger = null)
        {
            _logger = logger;
            try
            {
                Directory.CreateDirectory(DataDir);
                using var conn = Open();
                CreateSchema(conn);
                LoadLastState(conn);
                Compact(conn);
            }
            catch (Exception ex)
            {
                // 数据库不可用时不影响主流程（行情展示照常，只是不记录历史）
                _logger?.LogError(ex, "价格历史数据库初始化失败，历史记录不可用：{File}", DataFile);
            }
        }

        /// <summary>写入一个价格点。价格相同且间隔不足 60 秒时跳过，避免无意义的重复行。</summary>
        public void Add(DateTime time, decimal price, decimal? internationalPrice)
        {
            if (price <= 0) return;

            var timeMs = ToUnixMs(time);
            lock (_sync)
            {
                var unchanged = _lastTimeMs > 0
                    && price == _lastPrice
                    && (internationalPrice == null || internationalPrice == _lastInternationalPrice)
                    && timeMs - _lastTimeMs < DuplicateWindowMs;
                if (unchanged) return;

                try
                {
                    using var conn = Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText =
                        "INSERT INTO PriceHistory (TimeMs, Price, IntlPrice, Kind) VALUES ($t, $p, $ip, $k)";
                    cmd.Parameters.AddWithValue("$t", timeMs);
                    cmd.Parameters.AddWithValue("$p", (double)price);
                    cmd.Parameters.AddWithValue("$ip", internationalPrice.HasValue ? (double)internationalPrice.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("$k", KindRaw);
                    cmd.ExecuteNonQuery();

                    _lastTimeMs = timeMs;
                    _lastPrice = price;
                    _lastInternationalPrice = internationalPrice;
                }
                catch (Exception ex)
                {
                    // 写入失败静默忽略，只记一次日志
                    if (!_writeFailureLogged)
                    {
                        _writeFailureLogged = true;
                        _logger?.LogWarning(ex, "价格历史写入失败，后续同类错误不再记录");
                    }
                }
            }
        }

        /// <summary>批量导入（旧版 holding.json 中保存的价格历史迁移用）。</summary>
        public void Import(IEnumerable<PricePoint> points)
        {
            lock (_sync)
            {
                try
                {
                    using var conn = Open();
                    using var tx = conn.BeginTransaction();
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "INSERT INTO PriceHistory (TimeMs, Price, IntlPrice, Kind) VALUES ($t, $p, $ip, $k)";

                    var timeParam = cmd.Parameters.Add("$t", SqliteType.Integer);
                    var priceParam = cmd.Parameters.Add("$p", SqliteType.Real);
                    var intlParam = cmd.Parameters.Add("$ip", SqliteType.Real);
                    var kindParam = cmd.Parameters.Add("$k", SqliteType.Integer);

                    foreach (var point in points)
                    {
                        if (point.Price <= 0) continue;
                        timeParam.Value = ToUnixMs(point.Time);
                        priceParam.Value = (double)point.Price;
                        intlParam.Value = point.InternationalPrice.HasValue
                            ? (double)point.InternationalPrice.Value
                            : DBNull.Value;
                        kindParam.Value = KindRaw;
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    LoadLastState(conn);
                }
                catch (Exception ex)
                {
                    // 迁移失败静默忽略
                    _logger?.LogWarning(ex, "旧版价格历史迁移失败");
                }
            }
        }

        /// <summary>数据库内最早一条记录的时间，空库返回 null。</summary>
        public DateTime? GetEarliestTime()
        {
            lock (_sync)
            {
                try
                {
                    using var conn = Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT MIN(TimeMs) FROM PriceHistory";
                    var value = cmd.ExecuteScalar();
                    if (value is null || value is DBNull) return null;
                    return FromUnixMs(System.Convert.ToInt64(value));
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 导入历史日线（Kind=Day）。时间点已存在时跳过，可重复执行。
        /// </summary>
        public void ImportDaily(IEnumerable<PricePoint> points)
        {
            lock (_sync)
            {
                try
                {
                    using var conn = Open();
                    using var tx = conn.BeginTransaction();
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
INSERT INTO PriceHistory (TimeMs, Price, IntlPrice, Kind)
SELECT $t, $p, $ip, $k
WHERE NOT EXISTS (SELECT 1 FROM PriceHistory WHERE TimeMs = $t)";

                    var timeParam = cmd.Parameters.Add("$t", SqliteType.Integer);
                    var priceParam = cmd.Parameters.Add("$p", SqliteType.Real);
                    var intlParam = cmd.Parameters.Add("$ip", SqliteType.Real);
                    var kindParam = cmd.Parameters.Add("$k", SqliteType.Integer);

                    foreach (var point in points)
                    {
                        if (point.Price <= 0) continue;
                        timeParam.Value = ToUnixMs(point.Time);
                        priceParam.Value = (double)point.Price;
                        intlParam.Value = point.InternationalPrice.HasValue
                            ? (double)point.InternationalPrice.Value
                            : DBNull.Value;
                        kindParam.Value = KindDay;
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    LoadLastState(conn);
                }
                catch (Exception ex)
                {
                    // 导入失败静默忽略
                    _logger?.LogWarning(ex, "历史日线导入数据库失败");
                }
            }
        }

        /// <summary>
        /// 查询 [from, to] 区间的价格曲线，按跨度分桶平均，最多返回 maxPoints 个点（升序）。
        /// </summary>
        public List<PricePoint> Query(DateTime from, DateTime to, int maxPoints)
        {
            var result = new List<PricePoint>();
            var fromMs = ToUnixMs(from);
            var toMs = ToUnixMs(to);
            if (toMs <= fromMs || maxPoints <= 0) return result;

            var bucketMs = Math.Max(1L, (toMs - fromMs) / maxPoints);

            lock (_sync)
            {
                try
                {
                    using var conn = Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
SELECT AVG(TimeMs) AS T, AVG(Price), AVG(IntlPrice)
FROM PriceHistory
WHERE TimeMs >= $from AND TimeMs <= $to
GROUP BY TimeMs / $bucket
ORDER BY T";
                    cmd.Parameters.AddWithValue("$bucket", bucketMs);
                    cmd.Parameters.AddWithValue("$from", fromMs);
                    cmd.Parameters.AddWithValue("$to", toMs);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        result.Add(new PricePoint
                        {
                            // 用桶内真实时间的均值定位，避免时间被对齐到桶起点（日线数据会偏差一天）
                            Time = FromUnixMs((long)reader.GetDouble(0)),
                            Price = Math.Round((decimal)reader.GetDouble(1), 4),
                            InternationalPrice = reader.IsDBNull(2)
                                ? null
                                : Math.Round((decimal)reader.GetDouble(2), 4)
                        });
                    }
                }
                catch (Exception ex)
                {
                    // 查询失败返回空集合
                    _logger?.LogWarning(ex, "价格历史查询失败：{From:yyyy-MM-dd HH:mm} ~ {To:yyyy-MM-dd HH:mm}",
                        from, to);
                }
            }

            return result;
        }

        private SqliteConnection Open()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
            return conn;
        }

        private static void CreateSchema(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS PriceHistory (
    TimeMs    INTEGER NOT NULL,
    Price     REAL    NOT NULL,
    IntlPrice REAL    NULL,
    Kind      INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_PriceHistory_TimeMs ON PriceHistory (TimeMs);";
            cmd.ExecuteNonQuery();
        }

        private void LoadLastState(SqliteConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TimeMs, Price, IntlPrice FROM PriceHistory ORDER BY TimeMs DESC LIMIT 1";
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return;

            _lastTimeMs = reader.GetInt64(0);
            _lastPrice = (decimal)reader.GetDouble(1);
            _lastInternationalPrice = reader.IsDBNull(2) ? null : (decimal)reader.GetDouble(2);
        }

        /// <summary>把老数据逐级聚合，控制库体积：Raw→Hour（7 天）、Hour→Day（60 天）、Day 保留 3 年。</summary>
        private static void Compact(SqliteConnection conn)
        {
            var nowMs = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            AggregateAndDelete(conn, KindRaw, KindHour, TimeSpan.FromDays(7), 3_600_000L, nowMs);
            AggregateAndDelete(conn, KindHour, KindDay, TimeSpan.FromDays(60), 86_400_000L, nowMs);

            using var cleanup = conn.CreateCommand();
            cleanup.CommandText = "DELETE FROM PriceHistory WHERE Kind = $kind AND TimeMs < $cutoff";
            cleanup.Parameters.AddWithValue("$kind", KindDay);
            cleanup.Parameters.AddWithValue("$cutoff", nowMs - (long)TimeSpan.FromDays(1095).TotalMilliseconds);
            cleanup.ExecuteNonQuery();
        }

        private static void AggregateAndDelete(
            SqliteConnection conn, int fromKind, int toKind, TimeSpan olderThan, long bucketMs, long nowMs)
        {
            var cutoff = nowMs - (long)olderThan.TotalMilliseconds;

            using var tx = conn.BeginTransaction();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO PriceHistory (TimeMs, Price, IntlPrice, Kind)
SELECT (TimeMs / $bucket) * $bucket, AVG(Price), AVG(IntlPrice), $toKind
FROM PriceHistory
WHERE Kind = $fromKind AND TimeMs < $cutoff
GROUP BY TimeMs / $bucket";
                cmd.Parameters.AddWithValue("$bucket", bucketMs);
                cmd.Parameters.AddWithValue("$toKind", toKind);
                cmd.Parameters.AddWithValue("$fromKind", fromKind);
                cmd.Parameters.AddWithValue("$cutoff", cutoff);
                cmd.ExecuteNonQuery();
            }

            using (var del = conn.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM PriceHistory WHERE Kind = $fromKind AND TimeMs < $cutoff";
                del.Parameters.AddWithValue("$fromKind", fromKind);
                del.Parameters.AddWithValue("$cutoff", cutoff);
                del.ExecuteNonQuery();
            }

            tx.Commit();
        }

        private static long ToUnixMs(DateTime time) =>
            new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Local)).ToUnixTimeMilliseconds();

        private static DateTime FromUnixMs(long ms) =>
            DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime;
    }
}
