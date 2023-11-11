using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PresentationMovieMaker.Views
{
    public class InverseOpacityConverter: IValueConverter
    {
        public static InverseOpacityConverter Instance => new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return 1.0 - (double)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new Exception();
        }
    }
}
