using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PresentationMovieMaker.Views
{
    public class RateToPositionConverter : IValueConverter
    {
        public static RateToPositionConverter Instance { get; } = new RateToPositionConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var rate = (double)value;
            var maxValue = (double)parameter;
            return maxValue * rate;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
