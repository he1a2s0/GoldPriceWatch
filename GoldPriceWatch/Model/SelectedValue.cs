using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoldPriceWatch.Model
{
    public class SelectedValue<T>
    {
        public string Title { get; set; }
        public T Value { get; set; }
    }
}
