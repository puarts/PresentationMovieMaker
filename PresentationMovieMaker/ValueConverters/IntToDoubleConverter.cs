using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PresentationMovieMaker.Views
{
    public class IntToDoubleConverter: IValueConverter
    {
        public static IntToDoubleConverter Instance => new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)(int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (int)(double)value;
        }
    }
}
