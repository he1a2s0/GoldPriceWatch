using CommunityToolkit.Mvvm.ComponentModel;
using GoldPriceWatch.Model;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldPriceWatch.ModelView
{
    public partial class GoldModelView : ObservableObject, INotificationHandler<GoldResult>
    {
        [ObservableProperty]
        private string? text;
        [ObservableProperty]
        private bool? rise;
        [ObservableProperty]
        private string? priceRangePercentage;

        public async Task Handle(GoldResult notification, CancellationToken cancellationToken)
        {
            Text = $"￥{notification.CurrentPrice}";
            Rise = notification.CalculatePriceRangePercentage() > 0;
            PriceRangePercentage = $"{notification.CalculatePriceRangePercentage()}%";
        }
    }
}
