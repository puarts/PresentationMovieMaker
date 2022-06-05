using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.Utilities
{
    public static class PathUtility
    {
        public static bool ComparePaths(string pathA, string pathB)
        {
            if (string.IsNullOrEmpty(pathA) || string.IsNullOrEmpty(pathB))
            {
                return pathA == pathB;
            }

            return Path.GetFullPath(pathA).ToLower() == Path.GetFullPath(pathB).ToLower();
        }
    }
}
