using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PresentationMovieMaker.ValueConverters
{
    public class MultiplyMarginConverter : IMultiValueConverter
    {
        public static MultiplyMarginConverter Instance {get;} = new MultiplyMarginConverter();

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue)
            {
                return new Thickness(0.0);
            }

            double virtualCanvasWidth = (double)values[0];
            double actualCanvasWidth = (double)values[1];
            var faceWidth = (Thickness)values[2];
            double mult = (actualCanvasWidth / virtualCanvasWidth);
            return new Thickness(
                faceWidth.Left * mult,
                faceWidth.Top * mult,
                faceWidth.Right * mult,
                faceWidth.Bottom * mult
                );
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
