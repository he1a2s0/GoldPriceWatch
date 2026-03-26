using GoldPriceWatch.Enums;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GoldPriceWatch.Model
{
    /// <summary>
    /// 配置类，用于管理黄金投资的买入信息、警报阈值等。
    /// 该类支持JSON序列化，便于从配置文件加载。
    /// 可以扩展以支持更多警报类型或通知渠道。
    /// </summary>
    public class GoldInvestmentConfig
    {
        private const string DefaultConfigDirectory = "GoldPriceWatch";
        private const string DefaultConfigFileName = "goldconfig.json";
        private readonly string _filePath;
        private bool _isLoading;

        // Backing fields
        private bool _showNotice = false;
        private decimal _purchasePrice = 0m;
        private decimal _totalGrams = 0m;
        private GoldUnit _unit = GoldUnit.Gram;
        private string _currency = "CNY";
        private decimal? _targetSellPrice;
        private decimal? _stopLossPrice;
        private AlertThresholdType _dropAlertType = AlertThresholdType.Percentage;
        private decimal _dropAlertThreshold = 5m;
        private AlertThresholdType _riseAlertType = AlertThresholdType.Percentage;
        private decimal _riseAlertThreshold = 5m;
        private decimal? _volatilityThreshold = 2m;
        private bool _enableSettlementAlert = true;
        private string[] _notificationChannels = { "Console" };
        private double? _mainWindowLeft;
        private double? _mainWindowTop;

        /// <summary>
        /// 无参构造函数，用于JSON反序列化。
        /// </summary>
        [JsonConstructor]
        public GoldInvestmentConfig()
        {
            _filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DefaultConfigDirectory,
                DefaultConfigFileName);

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            LoadFromFile();
        }

        /// <summary>
        /// 构造函数，初始化默认值并尝试从文件加载配置。
        /// </summary>
        /// <param name="configFilePath">可选的配置文件路径；默认为用户本地应用数据目录下的 goldconfig.json。</param>
        public GoldInvestmentConfig(string? configFilePath) : this()
        {
            if (!string.IsNullOrEmpty(configFilePath))
            {
                _filePath = configFilePath;
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            LoadFromFile();
        }

        [JsonPropertyName("showNotice")]
        public bool ShowNotice
        {
            get => _showNotice;
            set
            {
                _showNotice = value;
                if (!_isLoading) SaveToFile();
            }
        }

        /// <summary>
        /// 买入价格（每克或每盎司，取决于单位）。
        /// </summary>
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "买入价格不能小于0。")]
        [JsonPropertyName("purchasePrice")]
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                if (_purchasePrice != value)
                {
                    _purchasePrice = Math.Max(0m, value); // 如果小于0，设置为0
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 总克数（或总盎司数）。
        /// </summary>
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "总克数不能小于0。")]
        [JsonPropertyName("totalGrams")]
        public decimal TotalGrams
        {
            get => _totalGrams;
            set
            {
                if (_totalGrams != value)
                {
                    _totalGrams = Math.Max(0m, value); // 如果小于0，设置为0
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 价格单位（克或盎司）。
        /// </summary>
        [JsonPropertyName("unit")]
        public GoldUnit Unit
        {
            get => _unit;
            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 货币类型（与GoldResult同步）。
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency
        {
            get => _currency;
            set
            {
                if (_currency != value)
                {
                    _currency = !string.IsNullOrWhiteSpace(value) ? value.Trim() : "CNY"; // 如果为空，设置为默认
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 目标卖出价格（可选，用于盈利提醒）。
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "目标卖出价格不能小于0。")]
        [JsonPropertyName("targetSellPrice")]
        public decimal? TargetSellPrice
        {
            get => _targetSellPrice;
            set
            {
                if (_targetSellPrice != value)
                {
                    _targetSellPrice = value.HasValue ? Math.Max(0m, value.Value) : null; // 如果<=0，设置为0而不是null，避免崩溃
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 止损价格（可选，用于强制卖出警报）。
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "止损价格不能小于0。")]
        [JsonPropertyName("stopLossPrice")]
        public decimal? StopLossPrice
        {
            get => _stopLossPrice;
            set
            {
                if (_stopLossPrice != value)
                {
                    _stopLossPrice = value.HasValue ? Math.Max(0m, value.Value) : null; // 如果<=0，设置为0而不是null，避免崩溃
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 降价报警阈值类型（百分比或金额）。
        /// </summary>
        [JsonPropertyName("dropAlertType")]
        public AlertThresholdType DropAlertType
        {
            get => _dropAlertType;
            set
            {
                if (_dropAlertType != value)
                {
                    _dropAlertType = value;
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 降价报警阈值（百分比：正值表示下跌百分比；金额：正值表示下跌金额）。
        /// </summary>
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "降价报警阈值不能小于0。")]
        [JsonPropertyName("dropAlertThreshold")]
        public decimal DropAlertThreshold
        {
            get => _dropAlertThreshold;
            set
            {
                if (_dropAlertThreshold != value)
                {
                    _dropAlertThreshold = Math.Max(0m, value); // 如果小于0，设置为0
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 涨价提醒阈值类型（百分比或金额）。
        /// </summary>
        [JsonPropertyName("riseAlertType")]
        public AlertThresholdType RiseAlertType
        {
            get => _riseAlertType;
            set
            {
                if (_riseAlertType != value)
                {
                    _riseAlertType = value;
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 涨价提醒阈值（百分比：正值表示上涨百分比；金额：正值表示上涨金额）。
        /// </summary>
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "涨价提醒阈值不能小于0。")]
        [JsonPropertyName("riseAlertThreshold")]
        public decimal RiseAlertThreshold
        {
            get => _riseAlertThreshold;
            set
            {
                if (_riseAlertThreshold != value)
                {
                    _riseAlertThreshold = Math.Max(0m, value); // 如果小于0，设置为0
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 波动率警报阈值（当日价格波动百分比超过此值时警报）。
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "波动率阈值不能小于0。")]
        [JsonPropertyName("volatilityThreshold")]
        public decimal? VolatilityThreshold
        {
            get => _volatilityThreshold;
            set
            {
                if (_volatilityThreshold != value)
                {
                    _volatilityThreshold = value.HasValue ? Math.Max(0m, value.Value) : null; // 如果<=0，设置为0
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 是否启用每日结算价提醒。
        /// </summary>
        [JsonPropertyName("enableSettlementAlert")]
        public bool EnableSettlementAlert
        {
            get => _enableSettlementAlert;
            set
            {
                if (_enableSettlementAlert != value)
                {
                    _enableSettlementAlert = value;
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 通知渠道（例如：Console, Email, Push 等，可扩展为枚举或列表）。
        /// </summary>
        [JsonPropertyName("notificationChannels")]
        public string[] NotificationChannels
        {
            get => _notificationChannels;
            set
            {
                if (!ReferenceEquals(_notificationChannels, value) || !_notificationChannels.SequenceEqual(value ?? Array.Empty<string>()))
                {
                    var filtered = value?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray() ?? Array.Empty<string>();
                    _notificationChannels = filtered.Length > 0 ? filtered : new[] { "Console" }; // 如果为空，设置为默认
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 主窗口左侧位置（用于恢复窗口位置）。
        /// </summary>
        [JsonPropertyName("mainWindowLeft")]
        public double? MainWindowLeft
        {
            get => _mainWindowLeft;
            set
            {
                if (_mainWindowLeft != value)
                {
                    _mainWindowLeft = value;
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 主窗口顶部位置（用于恢复窗口位置）。
        /// </summary>
        [JsonPropertyName("mainWindowTop")]
        public double? MainWindowTop
        {
            get => _mainWindowTop;
            set
            {
                if (_mainWindowTop != value)
                {
                    _mainWindowTop = value;
                    if (!_isLoading) SaveToFile();
                }
            }
        }

        /// <summary>
        /// 从JSON文件加载配置（如果存在）。
        /// </summary>
        private void LoadFromFile()
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            _isLoading = true;
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                string json = File.ReadAllText(_filePath);

                // 使用 JsonDocument 手动解析，避免构造函数问题
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // 手动设置属性
                if (root.TryGetProperty("purchasePrice", out var prop))
                {
                    PurchasePrice = prop.GetDecimal();
                }
                if (root.TryGetProperty("totalGrams", out prop))
                {
                    TotalGrams = prop.GetDecimal();
                }
                if (root.TryGetProperty("unit", out prop))
                {
                    Unit = TryParseEnum(prop, GoldUnit.Gram);
                }
                if (root.TryGetProperty("currency", out prop))
                {
                    Currency = prop.GetString() ?? "CNY";
                }
                if (root.TryGetProperty("targetSellPrice", out prop))
                {
                    TargetSellPrice = prop.ValueKind == JsonValueKind.Null ? null : prop.GetDecimal();
                }
                if (root.TryGetProperty("stopLossPrice", out prop))
                {
                    StopLossPrice = prop.ValueKind == JsonValueKind.Null ? null : prop.GetDecimal();
                }
                if (root.TryGetProperty("dropAlertType", out prop))
                {
                    DropAlertType = TryParseEnum(prop, AlertThresholdType.Percentage);
                }
                if (root.TryGetProperty("dropAlertThreshold", out prop))
                {
                    DropAlertThreshold = prop.GetDecimal();
                }
                if (root.TryGetProperty("riseAlertType", out prop))
                {
                    RiseAlertType = TryParseEnum(prop, AlertThresholdType.Percentage);
                }
                if (root.TryGetProperty("riseAlertThreshold", out prop))
                {
                    RiseAlertThreshold = prop.GetDecimal();
                }
                if (root.TryGetProperty("volatilityThreshold", out prop))
                {
                    VolatilityThreshold = prop.ValueKind == JsonValueKind.Null ? null : prop.GetDecimal();
                }
                if (root.TryGetProperty("enableSettlementAlert", out prop))
                {
                    EnableSettlementAlert = prop.GetBoolean();
                }
                if (root.TryGetProperty("notificationChannels", out prop) && prop.ValueKind == JsonValueKind.Array)
                {
                    NotificationChannels = prop.EnumerateArray().Select(p => p.GetString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                }
                if (root.TryGetProperty("mainWindowLeft", out prop))
                {
                    MainWindowLeft = prop.ValueKind == JsonValueKind.Null ? null : prop.GetDouble();
                }
                if (root.TryGetProperty("mainWindowTop", out prop))
                {
                    MainWindowTop = prop.ValueKind == JsonValueKind.Null ? null : prop.GetDouble();
                }
            }
            catch (Exception ex)
            {
                // TODO: Log error
                Console.WriteLine($"Failed to load config: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private TEnum TryParseEnum<TEnum>(JsonElement jsonElement, TEnum defaultValue) where TEnum : struct, Enum
        {
            if (Enum.TryParse(jsonElement.GetRawText(), true, out TEnum unit))
                return unit;

            return defaultValue;
        }

        /// <summary>
        /// 将当前配置保存到JSON文件。
        /// </summary>
        private void SaveToFile()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                // TODO: Log error or handle as needed
                Console.WriteLine($"Failed to save config: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否触发降价报警。
        /// </summary>
        /// <param name="goldResult">当前黄金价格数据。</param>
        /// <returns>是否触发报警，以及触发详情。</returns>
        public (bool IsTriggered, string Message) CheckDropAlert(GoldResult goldResult)
        {
            if (goldResult is null)
            {
                return (false, "价格数据为空，无法计算降价。");
            }

            var lossPercentage = goldResult.CalculateLossPercentage(PurchasePrice);
            if (lossPercentage < 0) // 无效数据
            {
                return (false, "价格数据无效，无法计算降价。");
            }

            if (TotalGrams <= 0)
            {
                return (false, "总克数无效，无法计算亏损金额。");
            }

            decimal thresholdValue;
            decimal currentPrice = decimal.TryParse(goldResult.CurrentPrice, out var cp) ? cp : 0m;
            decimal priceDiff = PurchasePrice - currentPrice; // 每克价格差（正为亏损）

            if (DropAlertType == AlertThresholdType.Percentage)
            {
                thresholdValue = DropAlertThreshold;
                if (lossPercentage >= thresholdValue)
                {
                    var lossAmount = priceDiff * TotalGrams;
                    return (true, $"降价报警：当前亏损 {lossPercentage:F2}%（金额约 {lossAmount:F2} {Currency}），超过阈值 {thresholdValue}%");
                }
            }
            else // Amount - 基于每克的金额阈值
            {
                thresholdValue = DropAlertThreshold; // 直接每克阈值
                if (priceDiff >= thresholdValue)
                {
                    var lossPercentageActual = PurchasePrice > 0 ? (priceDiff / PurchasePrice) * 100 : 0m;
                    var lossAmount = priceDiff * TotalGrams;
                    return (true, $"降价报警：当前每克亏损超过阈值 {thresholdValue:F2} {Currency}（亏损率 {lossPercentageActual:F2}%，总金额 {lossAmount:F2} {Currency}）");
                }
            }

            return (false, string.Empty);
        }

        /// <summary>
        /// 检查是否触发涨价提醒。
        /// </summary>
        /// <param name="goldResult">当前黄金价格数据。</param>
        /// <returns>是否触发提醒，以及提醒详情。</returns>
        public (bool IsTriggered, string Message) CheckRiseAlert(GoldResult goldResult)
        {
            if (goldResult is null)
            {
                return (false, "价格数据为空，无法计算涨幅。");
            }

            var changePercentage = goldResult.CalculatePriceChangePercentage();
            if (changePercentage < 0) // 无效数据
            {
                return (false, "价格数据无效，无法计算涨幅。");
            }

            if (TotalGrams <= 0)
            {
                return (false, "总克数无效，无法计算盈利金额。");
            }

            decimal thresholdValue;
            decimal currentPrice = decimal.TryParse(goldResult.CurrentPrice, out var cp) ? cp : 0m;
            decimal priceDiff = currentPrice - PurchasePrice; // 每克价格差（正为盈利）

            if (RiseAlertType == AlertThresholdType.Percentage)
            {
                thresholdValue = RiseAlertThreshold;
                if (changePercentage >= thresholdValue)
                {
                    var profitAmount = priceDiff * TotalGrams;
                    return (true, $"涨价提醒：当前涨幅 {changePercentage:F2}%（金额约 {profitAmount:F2} {Currency}），超过阈值 {thresholdValue}%");
                }
            }
            else // Amount - 基于每克的金额阈值
            {
                thresholdValue = RiseAlertThreshold; // 直接每克阈值
                if (priceDiff >= thresholdValue)
                {
                    var changePercentageActual = PurchasePrice > 0 ? (priceDiff / PurchasePrice) * 100 : 0m;
                    var profitAmount = priceDiff * TotalGrams;
                    return (true, $"涨价提醒：当前每克盈利超过阈值 {thresholdValue:F2} {Currency}（涨幅 {changePercentageActual:F2}%，总金额 {profitAmount:F2} {Currency}）");
                }
            }

            return (false, string.Empty);
        }

        /// <summary>
        /// 检查是否触发波动率警报。
        /// </summary>
        /// <param name="goldResult">当前黄金价格数据。</param>
        /// <returns>是否触发警报，以及警报详情。</returns>
        public (bool IsTriggered, string Message) CheckVolatilityAlert(GoldResult goldResult)
        {
            if (!VolatilityThreshold.HasValue || goldResult is null)
            {
                return (false, string.Empty);
            }

            var rangePercentage = goldResult.CalculatePriceRangePercentage();
            if (rangePercentage < 0) // 无效数据
            {
                return (false, "价格数据无效，无法计算波动率。");
            }

            if (rangePercentage >= VolatilityThreshold.Value)
            {
                return (true, $"波动率警报：当日价格波动 {rangePercentage:F2}%，超过阈值 {VolatilityThreshold.Value}%");
            }

            return (false, string.Empty);
        }

        /// <summary>
        /// 检查是否达到目标卖出价。
        /// </summary>
        /// <param name="goldResult">当前黄金价格数据。</param>
        /// <returns>是否达到目标，以及详情。</returns>
        public (bool IsReached, string Message) CheckTargetSell(GoldResult goldResult)
        {
            if (!TargetSellPrice.HasValue || goldResult is null)
            {
                return (false, string.Empty);
            }

            var currentPrice = decimal.TryParse(goldResult.CurrentPrice, out var cp) ? cp : 0m;
            if (currentPrice >= TargetSellPrice.Value)
            {
                var profitPercentage = PurchasePrice > 0 ? ((currentPrice - PurchasePrice) / PurchasePrice) * 100 : 0m; // 避免除零
                var profitAmount = (currentPrice - PurchasePrice) * TotalGrams;
                return (true, $"达到卖出目标：当前价 {currentPrice:F2} {Currency}，盈利 {profitPercentage:F2}%（金额 {profitAmount:F2} {Currency}）");
            }

            return (false, string.Empty);
        }

        /// <summary>
        /// 检查是否触发止损。
        /// </summary>
        /// <param name="goldResult">当前黄金价格数据。</param>
        /// <returns>是否触发止损，以及详情。</returns>
        public (bool IsTriggered, string Message) CheckStopLoss(GoldResult goldResult)
        {
            if (!StopLossPrice.HasValue || goldResult is null)
            {
                return (false, string.Empty);
            }

            var currentPrice = decimal.TryParse(goldResult.CurrentPrice, out var cp) ? cp : 0m;
            if (currentPrice <= StopLossPrice.Value)
            {
                var lossPercentage = PurchasePrice > 0 ? ((PurchasePrice - currentPrice) / PurchasePrice) * 100 : 0m; // 避免除零
                var lossAmount = (PurchasePrice - currentPrice) * TotalGrams;
                return (true, $"止损警报：当前价 {currentPrice:F2} {Currency}，亏损 {lossPercentage:F2}%（金额 {lossAmount:F2} {Currency}）");
            }

            return (false, string.Empty);
        }

        /// <summary>
        /// 获取当前总投资价值。
        /// </summary>
        /// <param name="goldResult">当前黄金价格数据。</param>
        /// <returns>当前总价值。</returns>
        public decimal GetCurrentTotalValue(GoldResult goldResult)
        {
            if (goldResult is null)
            {
                return 0m;
            }
            return decimal.TryParse(goldResult.CurrentPrice, out var cp) ? cp * TotalGrams : 0m;
        }

        /// <summary>
        /// 获取当前盈亏金额。
        /// </summary>
        /// <param name="goldResult">当前黄金价格数据。</param>
        /// <returns>盈亏金额（正为盈利，负为亏损）。</returns>
        public decimal GetCurrentProfitLoss(GoldResult goldResult)
        {
            if (goldResult is null)
            {
                return 0;
            }
            var currentValue = GetCurrentTotalValue(goldResult);
            var purchaseValue = PurchasePrice * TotalGrams;
            return currentValue - purchaseValue;
        }
    }
}