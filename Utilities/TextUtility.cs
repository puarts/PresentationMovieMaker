using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static PresentationMovieMaker.Utilities.TextUtility;

namespace PresentationMovieMaker.Utilities
{
    public static class TextUtility
    {
        private static Dictionary<int, char> _visemeToPron = new Dictionary<int, char>()
        {
            // 子音っぽいのは MinValue を入れてる
            // https://docs.microsoft.com/ja-jp/azure/cognitive-services/speech-service/how-to-speech-synthesis-viseme?pivots=programming-language-csharp#map-phonemes-to-visemes
            { 0, 'n' },
            { 1, 'a' },
            { 2, 'a' },
            { 3, 'a' },
            { 4, 'u' },
            { 5, 'a' },
            { 6, 'i' },
            { 7, 'u' },
            { 8, 'o' },
            { 9, 'o' },
            { 10, 'o' },
            { 11, 'a' },
            { 12, char.MinValue },
            { 13, char.MinValue },
            { 14, char.MinValue },
            { 15, char.MinValue },
            { 16, char.MinValue },
            { 17, char.MinValue },
            { 18, char.MinValue },
            { 19, char.MinValue },
            { 20, char.MinValue },
            { 21, char.MinValue },
        };

        public static char ConvertVisemeToPronuounciation(int viseme)
        {
            return _visemeToPron[viseme];
        }

        public static char GetPronuounciation(char source)
        {
            string aList = "あかさたなはまやらわ378";
            string iList = "いきしちにひみり124";
            string uList = "うくすつぬふむゆる9";
            string eList = "えけせてねへめれ";
            string oList = "おこそとのほもよろを56";
            if (aList.Contains(source))
            {
                return 'a';
            }
            if (iList.Contains(source))
            {
                return 'i';
            }
            if (uList.Contains(source))
            {
                return 'u';
            }
            if (eList.Contains(source))
            {
                return 'e';
            }
            if (oList.Contains(source))
            {
                return 'o';
            }
            return 'n';
        }

        public static IEnumerable<string> SplitWithKeepingDelimiters(string text, IEnumerable<string> delimiters)
        {
            var splitMark = "[[[";
            var replaced = text;
            foreach (var delim in delimiters)
            {
                replaced = replaced.Replace(delim, delim + splitMark);
            }

            foreach (var split in replaced.Split(
                splitMark,
                StringSplitOptions.RemoveEmptyEntries))
            {
                replaced = split;
                foreach (var delim in delimiters)
                {
                    replaced = replaced.Replace(delim, "");
                }

                if (replaced != String.Empty)
                {
                    yield return split;
                }
            }
        }
    }
}
