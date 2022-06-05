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
            var text = "����́H" + Environment.NewLine + "�ق�";
            var delimiters = new string[]
                {
                    "�B",
                    "?"+ Environment.NewLine,
                    "�H"+ Environment.NewLine,
                    "�I"+ Environment.NewLine,
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
            Assert.AreEqual("����́H" + Environment.NewLine, split[0]);
        }

        [TestMethod]
        public void DurationCalcTest()
        {
            var synth = new System.Speech.Synthesis.SpeechSynthesizer();
            var message = "����ɂ��́A����͉��������̔������Ԃ��擾����T���v���ł��B";

            {
                synth.Rate = 0;
                var sec = CalculateDuration(synth, message).TotalSeconds;
                System.Console.WriteLine($"{sec}�b");
            }

            {
                synth.Rate = 10;
                var sec = CalculateDuration(synth, message).TotalSeconds;
                System.Console.WriteLine($"{sec}�b");
            }

            {
                synth.Rate = -10;
                var sec = CalculateDuration(synth, message).TotalSeconds;
                System.Console.WriteLine($"{sec}�b");
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
