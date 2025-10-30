using CommunityToolkit.Mvvm.ComponentModel;
using GoldPriceWatch.Enums;
using GoldPriceWatch.Model;
using Masuit.Tools.Systems;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace GoldPriceWatch.ModelView
{
    /// <summary>
    /// ViewModel for GoldInvestmentConfig, using CommunityToolkit.Mvvm for two-way binding.
    /// Public properties delegate to the underlying GoldInvestmentConfig instance and notify changes.
    /// </summary>
    public partial class GoldInvestmentViewModel : ObservableObject
    {
        private readonly GoldInvestmentConfig _config;

        public GoldInvestmentViewModel()
        {
            _config = App.Current.Services.GetRequiredService<GoldInvestmentConfig>();
        }

        public List<SelectedValue<GoldUnit>> GoldUnitList => typeof(GoldUnit).GetDescriptionAndValue().Select(it => new SelectedValue<GoldUnit>
        {
            Value = ((GoldUnit)it.Value),
            Title = it.Key
        }).ToList();
        public List<SelectedValue<AlertThresholdType>> AlertThresholdTypeList => typeof(AlertThresholdType).GetDescriptionAndValue().Select(it => new SelectedValue<AlertThresholdType>
        {
            Value = (AlertThresholdType)it.Value,
            Title = it.Key
        }).ToList();

        /// <summary>
        /// 展示通知
        /// </summary>
        public bool ShowNotice
        {
            get => _config.ShowNotice;
            set
            {
                _config.ShowNotice = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the purchase price, with two-way binding support.
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "买入价格必须大于0。")]
        public decimal PurchasePrice
        {
            get => _config.PurchasePrice;
            set
            {
                if (_config.PurchasePrice != value)
                {
                    _config.PurchasePrice = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the total grams, with two-way binding support.
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "总克数必须大于0。")]
        public decimal TotalGrams
        {
            get => _config.TotalGrams;
            set
            {
                if (_config.TotalGrams != value)
                {
                    _config.TotalGrams = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the gold unit, with two-way binding support.
        /// </summary>
        public GoldUnit Unit
        {
            get => _config.Unit;
            set
            {
                if (_config.Unit != value)
                {
                    _config.Unit = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the currency, with two-way binding support.
        /// </summary>
        public string Currency
        {
            get => _config.Currency;
            set
            {
                if (_config.Currency != value)
                {
                    _config.Currency = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the target sell price (nullable), with two-way binding support.
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "目标卖出价格必须大于0。")]
        public decimal? TargetSellPrice
        {
            get => _config.TargetSellPrice;
            set
            {
                if (_config.TargetSellPrice != value)
                {
                    _config.TargetSellPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the stop loss price (nullable), with two-way binding support.
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "止损价格必须大于0。")]
        public decimal? StopLossPrice
        {
            get => _config.StopLossPrice;
            set
            {
                if (_config.StopLossPrice != value)
                {
                    _config.StopLossPrice = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the drop alert type, with two-way binding support.
        /// </summary>
        public AlertThresholdType DropAlertType
        {
            get => _config.DropAlertType;
            set
            {
                if (_config.DropAlertType != value)
                {
                    _config.DropAlertType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the drop alert threshold, with two-way binding support.
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "降价报警阈值必须大于0。")]
        public decimal DropAlertThreshold
        {
            get => _config.DropAlertThreshold;
            set
            {
                if (_config.DropAlertThreshold != value)
                {
                    _config.DropAlertThreshold = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the rise alert type, with two-way binding support.
        /// </summary>
        public AlertThresholdType RiseAlertType
        {
            get => _config.RiseAlertType;
            set
            {
                if (_config.RiseAlertType != value)
                {
                    _config.RiseAlertType = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the rise alert threshold, with two-way binding support.
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "涨价提醒阈值必须大于0。")]
        public decimal RiseAlertThreshold
        {
            get => _config.RiseAlertThreshold;
            set
            {
                if (_config.RiseAlertThreshold != value)
                {
                    _config.RiseAlertThreshold = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the volatility threshold (nullable), with two-way binding support.
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "波动率阈值必须大于0。")]
        public decimal? VolatilityThreshold
        {
            get => _config.VolatilityThreshold;
            set
            {
                if (_config.VolatilityThreshold != value)
                {
                    _config.VolatilityThreshold = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to enable settlement alert, with two-way binding support.
        /// </summary>
        public bool EnableSettlementAlert
        {
            get => _config.EnableSettlementAlert;
            set
            {
                if (_config.EnableSettlementAlert != value)
                {
                    _config.EnableSettlementAlert = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the notification channels as a comma-separated string, with two-way binding support.
        /// </summary>
        public string NotificationChannelsString
        {
            get => string.Join(",", _config.NotificationChannels ?? Array.Empty<string>());
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _config.NotificationChannels = Array.Empty<string>();
                }
                else
                {
                    _config.NotificationChannels = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToArray();
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Access the underlying config instance.
        /// </summary>
        public GoldInvestmentConfig Config => _config;
    }
}
