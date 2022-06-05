using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.DataModels
{
    public class MovieSetting
    {
        public string ImageRoot { get; set; } = string.Empty;
        public string AudioRoot { get; set; } = string.Empty;
        public string VoiceName { get; set; } = string.Empty;
        public int NarrationLineBreakInterval { get; set; } = 100;
        public List<PageInfo> PageInfos { get; set; } = new List<PageInfo>();

        public string BgmPath { get; set; } = string.Empty;
        public double BgmVolume { get; set; } = 1.0;
        public double NarrationVolume { get; set; } = 1.0;

        public int BeginBlankMilliseconds { get; set; } = 3000;
        public int EndBlankMilliseconds { get; set; } = 3000;

        public int PagingIntervalMilliseconds { get; set; } = 300;
        public bool ShowPageNumber { get; set; } = false;
        public double PageNumberPosX { get; set; } = 0;
        public double PageNumberPosY { get; set; } = 0;
        public double PageNumberFontSize { get; set; } = 30;

        public int BgmFadeOutMilliseconds { get; set; } = 3000;

        public double CaptionMarginLeft { get; set; } = 60;
        public double CaptionMarginBottom { get; set; } = 60;

        public double FaceImageWidth { get; set; } = 130;

        public double CaptionFontSize { get; set; } = 30.0;

        public string DefaultPageTurningAudioPath { get; set; } = string.Empty;
        public double DefaultPageTurningAudioVolume { get; set; } = 1.0;

        // キャラクター設定
        public string ImageMouthAPath { get; set; } = string.Empty;
        public string ImageMouthIPath { get; set; } = string.Empty;
        public string ImageMouthUPath { get; set; } = string.Empty;
        public string ImageMouthEPath { get; set; } = string.Empty;
        public string ImageMouthOPath { get; set; } = string.Empty;
        public string ImageMouthNPath { get; set; } = string.Empty;
        public string ImageEyeOpenPath { get; set; } = string.Empty;
        public string ImageEyeClosePath { get; set; } = string.Empty;
        public string ImageBodyPath { get; set; } = string.Empty;
        public string ImageFaceBasePath { get; set; } = string.Empty;
    }
}
