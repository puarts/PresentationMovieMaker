using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PresentationMovieMaker.ValueConverters
{
    public class ImageConverter : IValueConverter
    {
        public static ImageConverter Instance { get; } = new ImageConverter();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null)
            {
                return null;
            }

            var path = (string)value;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            using (var fs = new FileStream(path, FileMode.Open))
            {
                var decoder = BitmapDecoder.Create(
                    fs,
                    BitmapCreateOptions.None,
                    BitmapCacheOption.OnLoad);
                var bmp = new WriteableBitmap(decoder.Frames[0]);
                bmp.Freeze();
                return bmp;
            }
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
