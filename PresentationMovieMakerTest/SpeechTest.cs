using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Speech.Synthesis;

namespace PresentationMovieMakerTest
{
    [TestClass]
    public class SpeechTest
    {
        [TestMethod]
        public void SplitTextTest()
        {
            var text = "これは？" + Environment.NewLine + "ほげ";
            var delimiters = new string[]
                {
                    "。",
                    "?"+ Environment.NewLine,
                    "？"+ Environment.NewLine,
                    "！"+ Environment.NewLine,
                    "!"+ Environment.NewLine,
                };
            var splitMark = "[[[";
            var replaced = text;
            foreach (var delim in delimiters)
            {
                replaced = replaced.Replace(delim, delim + splitMark);
            }

            var split = replaced.Split(
                splitMark,
                StringSplitOptions.RemoveEmptyEntries);
            Assert.AreEqual(2, split.Length);
            Assert.AreEqual("これは？" + Environment.NewLine, split[0]);
        }

        [TestMethod]
        public void DurationCalcTest()
        {
            var synth = new System.Speech.Synthesis.SpeechSynthesizer();
            var message = "こんにちは、これは音声合成の発生時間を取得するサンプルです。";

            {
                synth.Rate = 0;
                var sec = CalculateDuration(synth, message).TotalSeconds;
                System.Console.WriteLine($"{sec}秒");
            }

            {
                synth.Rate = 10;
                var sec = CalculateDuration(synth, message).TotalSeconds;
                System.Console.WriteLine($"{sec}秒");
            }

            {
                synth.Rate = -10;
                var sec = CalculateDuration(synth, message).TotalSeconds;
                System.Console.WriteLine($"{sec}秒");
            }
        }

        private System.TimeSpan CalculateDuration(System.Speech.Synthesis.SpeechSynthesizer synth, string message)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                synth.SetOutputToWaveStream(stream);

                synth.Speak(message);
                stream.Seek(0, System.IO.SeekOrigin.Begin);
                using (var wfr = new NAudio.Wave.WaveFileReader(stream))
                {
                    return wfr.TotalTime;
                }
            }
        }
    }
}
