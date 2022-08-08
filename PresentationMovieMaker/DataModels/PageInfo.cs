using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.DataModels
{
    public enum PageType
    {
        Title,
        SectionHeader,
        TitleAndBody,
    }

    public class PageInfo
    {
        public PageInfo()
        {
        }

        public PageInfo(string imagePath, IEnumerable<string> audioPaths)
        {
            ImagePath = imagePath;

            var info = new NarrationInfo();
            NarrationInfos.Add(info);
            foreach (var audioPath in audioPaths)
            {
                info.AudioPaths.Add(audioPath);
            }
        }

        public string ImagePath { get; set; } = string.Empty;

        public string BgmPath { get; set; } = string.Empty;
        public int BgmFadeMiliseconds { get; set; } = 3000;
        public double BgmVolume { get; set; } = 1.0;
        public bool OverwritesBgmVolume { get; set; } = false;


        public double MediaVolume { get; set; } = 1.0;

        public double RotationAngle { get; set; } = 0;
        public List<NarrationInfo> NarrationInfos { get; set; } = new List<NarrationInfo>();
        public int PagingIntervalMilliseconds { get; set; } = 300;


        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;


        public PageType PageType { get; set; } = PageType.TitleAndBody;

        public List<string> SubImagePaths { get; set; } = new List<string>();

    }
}
