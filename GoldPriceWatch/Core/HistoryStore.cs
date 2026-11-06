using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GoldPriceWatch.Model;

namespace GoldPriceWatch.Core
{
    /// <summary>
    /// 持仓历史记录持久化：每条记录含流水、费率、保存时的价格与价格历史。
    /// 存储于 %LocalAppData%\GoldPriceWatch\history.json。
    /// </summary>
    public class HistoryStore
    {
        private static readonly string DataDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoldPriceWatch");
        private static readonly string DataFile = Path.Combine(DataDir, "history.json");

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        /// <summary>全部历史记录，按创建时间倒序。</summary>
        public List<HistoryRecord> Records { get; set; } = new();

        public static HistoryStore Load()
        {
            try
            {
                if (File.Exists(DataFile))
                {
                    var json = File.ReadAllText(DataFile);
                    return JsonSerializer.Deserialize<HistoryStore>(json, JsonOptions) ?? new HistoryStore();
                }
            }
            catch (Exception)
            {
                // 文件损坏时回退到空列表
            }
            return new HistoryStore();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                Records = Records.OrderByDescending(r => r.CreatedAt).ToList();
                File.WriteAllText(DataFile, JsonSerializer.Serialize(this, JsonOptions));
            }
            catch (Exception)
            {
                // 写入失败时静默忽略，不影响主流程
            }
        }
    }
}
