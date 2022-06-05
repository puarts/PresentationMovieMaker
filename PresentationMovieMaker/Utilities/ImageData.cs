using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.Utilities
{
    public class ImageData
    {
        public ImageData(byte[] imageBytes, int width, int height, int stride, int bytePerPixel, string? path = null)
        {
            ImageBytes = imageBytes;
            Width = width;
            Height = height;
            Stride = stride;
            BytePerPixel = bytePerPixel;
            SourcePath = path;
        }

        public byte[] ImageBytes { get; }
        public int Width { get; }
        public int Height { get; }
        public int Stride { get; }
        public int BytePerPixel { get; }
        public string? SourcePath { get; }

        public int CalcImageByteDifference(ImageData other)
        {
            if (Width != other.Width || Height != other.Height || Stride != other.Stride || BytePerPixel != other.BytePerPixel)
            {
                return int.MaxValue;
            }
            return ImageUtility.CalcImageByteDifference(
                ImageBytes, other.ImageBytes,
                Height, Width, Stride, BytePerPixel);
        }

        public static ImageData CreateFromBitmap(Bitmap bitmap, string? path = null)
        {
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            int bsize = bitmapData.Stride * bitmap.Height;
            byte[] byteData = new byte[bsize];
            Marshal.Copy(bitmapData.Scan0, byteData, 0, bsize);
            bitmap.UnlockBits(bitmapData);
            return new ImageData(
                byteData, bitmap.Width, bitmap.Height, bitmapData.Stride, ImageUtility.GetBytePerPixel(bitmapData), path);
        }

        public static ImageData CreateFromFile(string path)
        {
            using var image = Image.FromFile(path);
            return CreateFromImage(image, path);
        }

        public static ImageData CreateFromImage(Image image, string? path = null)
        {
            using var bitmap = new Bitmap(image);
            var result = CreateFromBitmap(bitmap, path);
            return result;
        }
    }
}
