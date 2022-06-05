using Microsoft.VisualStudio.TestTools.UnitTesting;
using PresentationMovieMaker.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;

namespace PresentationMovieMakerTest
{


    [TestClass]
    public class ImageAnalysisTest
    {
        [TestMethod]
        public void ImageMatchingTest()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new Exception();
            var root = Path.Combine(assemblyDir, "../../../Resources/TestSlides");
            using (var dirCreator = new ScopedDirectoryCreator())
            {
                var workDir = dirCreator.DirectoryPath;
                foreach (var filePath in Directory.EnumerateFiles(root))
                {
                    var image = Image.FromFile(filePath);
                    var bitmap = new Bitmap(image);
                    var resizedImage = ImageUtility.ResizeImage(bitmap, image.Size / 8);

                    var outputPath = Path.Combine(workDir, Path.GetFileName(filePath));
                    resizedImage.Save(outputPath);
                }

                var cacheFiles = Directory.EnumerateFiles(workDir).ToArray();

                foreach (var filePath in Directory.EnumerateFiles(root))
                {
                    var matchedPath = FindMatchedFileImage(filePath, cacheFiles);
                    Assert.AreEqual(Path.GetFileName(matchedPath), Path.GetFileName(filePath));
                }
            }
        }

        private static string? FindMatchedFileImage(string filePath, IEnumerable<string> cacheFiles)
        {
            var cacheArray = cacheFiles.GetType().IsArray ? (string[])cacheFiles : cacheFiles.ToArray();
            var image = Image.FromFile(filePath);
            var bitmap = new Bitmap(image);
            var resizedImage = ImageUtility.ResizeImage(bitmap, image.Size / 8);
            var resizedBitmap = new Bitmap(resizedImage);
            var imageData = ImageData.CreateFromBitmap(resizedBitmap);

            Console.WriteLine($"finding similar image of {Path.GetFileNameWithoutExtension(filePath)}");
            var cacheImageDataList = cacheArray.Select(cachePath => ImageData.CreateFromFile(cachePath));
            return ImageUtility.FindMatchedImage(imageData, cacheImageDataList);
        }
    }
}
