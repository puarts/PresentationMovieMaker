using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.IO;

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

        public static int CalcImageByteDifference(
            byte[] byte1, byte[] byte2, int height, int width, int stride, int bpp)
        {
            int diffTotal = 0;
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    int index = y * stride + x * bpp;
                    for (int c = 0; c < 4; ++c)
                    {
                        var diff = (byte1[index + c] - byte2[index + c]);
                        diffTotal += diff * diff;
                        //diffTotal += Math.Abs(byte1[index + c] - byte2[index + c]);
                    }
                }
            }

            return diffTotal;
        }

        public static int GetBytePerPixel(BitmapData bitmapData)
        {
            switch (bitmapData.PixelFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    return 4;
                default:
                    throw new Exception("Unsupported format");
            }
        }

        public static Image ReadResizedImage(string filePath, float sizeMultiply)
        {
            var image = Image.FromFile(filePath);
            var bitmap = new Bitmap(image);
            return ImageUtility.ResizeImage(bitmap, new System.Drawing.Size((int)(image.Width * sizeMultiply), (int)(image.Height * sizeMultiply)));
        }

        public static Image ResizeImage(Image imgToResize, System.Drawing.Size size)
        {
            return (Image)(new Bitmap(imgToResize, size));
        }

        public static string? FindMatchedImage(ImageData imageData, IEnumerable<ImageData> cacheFiles, int diffThreshold = int.MaxValue)
        {
            int fileNum = ExtractNumberFromFileName(imageData.SourcePath);
            int minDiff = int.MaxValue;
            string? minDiffPath = null;
            foreach (var cacheImageData in cacheFiles)
            {
                int diff = cacheImageData.CalcImageByteDifference(imageData);
                if (diff > diffThreshold)
                {
                    continue;
                }

                if (diff < minDiff)
                {
                    minDiff = diff;
                    minDiffPath = cacheImageData.SourcePath;
                }
                else if (diff == minDiff && diff < int.MaxValue)
                {
                    if (minDiffPath is null)
                    {
                        minDiff = diff;
                        minDiffPath = cacheImageData.SourcePath;
                    }
                    else if (cacheImageData.SourcePath != null && minDiffPath != null)
                    {
                        int cacheFileNum = ExtractNumberFromFileName(cacheImageData.SourcePath);
                        var cacheNumDiff = Math.Abs(fileNum - cacheFileNum);
                        if (cacheNumDiff < Math.Abs(fileNum - ExtractNumberFromFileName(minDiffPath)))
                        {
                            minDiff = diff;
                            minDiffPath = cacheImageData.SourcePath;
                        }
                    }
                }
            }

            return minDiffPath;
        }

        private static int ExtractNumberFromFileName(string? imagePath)
        {
            var fileName = imagePath != null ? Path.GetFileNameWithoutExtension(imagePath) : null;
            if (fileName is not null)
            {
                return int.Parse(fileName.Split("-").Last().TrimStart('0'));
            }

            return int.MaxValue;
        }
    }
}
