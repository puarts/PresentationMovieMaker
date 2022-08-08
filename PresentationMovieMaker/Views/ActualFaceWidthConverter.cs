using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PresentationMovieMaker.Views
{
    public class ActualFaceWidthConverter : IMultiValueConverter
    {
        public static ActualFaceWidthConverter Instance { get; } = new ActualFaceWidthConverter();

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            double virtualCanvasWidth = (double)values[0];
            double actualCanvasWidth = (double)values[1];
            double faceWidth = (double)values[2];
            return faceWidth * (actualCanvasWidth / virtualCanvasWidth);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
