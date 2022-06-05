using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMakerTest
{
    public class ScopedDirectoryCreator : IDisposable
    {
        public ScopedDirectoryCreator()
        {
            var name = Assembly.GetExecutingAssembly().GetName().Name;
            var workDir = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid().ToString("N")}");
            DirectoryPath = workDir;
            Directory.CreateDirectory(workDir);
        }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, true);
        }

        public string DirectoryPath { get; }
    }
}
