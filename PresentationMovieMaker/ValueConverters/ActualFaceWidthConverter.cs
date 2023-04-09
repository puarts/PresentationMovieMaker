using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PresentationMovieMaker.Views
{
    public class ActualMarginConverter : IMultiValueConverter
    {
        public static ActualMarginConverter Instance { get; } = new ActualMarginConverter();

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue)
            {
                return new Thickness(0.0);
            }

            double virtualCanvasWidth = (double)values[0];
            double actualCanvasWidth = (double)values[1];
            double faceWidth = (double)values[2];
            return new Thickness(faceWidth * (actualCanvasWidth / virtualCanvasWidth));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ActualFaceWidthConverter : IMultiValueConverter
    {
        public static ActualFaceWidthConverter Instance { get; } = new ActualFaceWidthConverter();

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values[0] == DependencyProperty.UnsetValue)
            {
                return 1.0;
            }

            double virtualCanvasWidth = (double)values[0];
            if (virtualCanvasWidth < 0.0001)
            {
                return 1.0;
            }

            double actualCanvasWidth = (double)values[1];
            double faceWidth = (double)values[2];
            var result = faceWidth * (actualCanvasWidth / virtualCanvasWidth);
            if (result < 0.0001)
            {
                return 1.0;
            }
            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
