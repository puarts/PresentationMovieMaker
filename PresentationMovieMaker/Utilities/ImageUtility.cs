using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;

namespace PresentationMovieMaker.Utilities
{
    public static class ImageUtility
    {
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public static (int Width, int Height) GetImageSize(string path)
        {
            Bitmap img = new Bitmap(path);
            return (img.Width, img.Height);
        }

        public static BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            IntPtr hbitmap = bitmap.GetHbitmap();
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);
            return bitmapSource;
        }
    }
}
