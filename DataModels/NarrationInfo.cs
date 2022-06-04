using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.DataModels
{
    public class NarrationInfo
    {
        public string SpeechText { get; set; } = string.Empty;
        public double SpeechVolume { get; set; } = 1.0;
        public double SpeechSpeedRate { get; set; } = 1.0;
        public List<string> AudioPaths { get; set; } = new List<string>();
        public int PreBlankMilliseconds { get; set; } = 0;
        public int PostBlankMilliseconds { get; set; } = 0;
        public double AudioVolume { get; set; } = 1.0;
        public bool IsAudioParallel { get; set; } = false;
        public double CaptionMarginBottom { get; set; } = 60;

        public int TextColorR { get; set; } = 0xff;
        public int TextColorG { get; set; } = 0xff;
        public int TextColorB { get; set; } = 0xff;
        public int TextColorA { get; set; } = 0xff;
    }
}
