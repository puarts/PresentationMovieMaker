using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using PresentationMovieMaker.Utilities;
using System.IO;
using NAudio.Wave;
using System.Threading;

namespace PresentationMovieMakerTest
{
    [TestClass]
    public class AudioTest
    {
        [TestMethod]
        public void LoopAudioFeedTest()
        {
            var bgmPath = @"F:\trunk\Projects\FehVideo\Bgm\風花雪月\Fire Emblem Three Houses - Hungry March.wav";
            var reader = new AudioFileReader(bgmPath);
            var bgmFileReader = reader;
            bgmFileReader.Volume = 0.0f;
            LoopStream loop = new(reader);
            WaveOutEvent bgmOutputDevice = new();
            bgmOutputDevice.Init(loop);

            bgmOutputDevice.Volume = 1.0f;
            bgmOutputDevice.Play();

            // フェードイン
            {
                var targetVolume = 1.0f;
                var fadeMilliseconds = 3000;
                int intervalMilliseconds = fadeMilliseconds;
                int samples = 100;
                int sleepMilliseconds = intervalMilliseconds / samples;
                float increment = targetVolume / samples;
                for (int i = 0; i < samples; ++i)
                {
                    Thread.Sleep(sleepMilliseconds);

                    float nextVolume = bgmFileReader!.Volume + increment;
                    bgmFileReader.Volume = nextVolume > targetVolume ? targetVolume : nextVolume;
                }
                bgmFileReader.Volume = targetVolume;
            }


            // フェードアウト
            {
                int fadeOutMilliseconds = 3000;
                int fadeOutSamples = 100;
                int sleepMilliseconds = fadeOutMilliseconds / fadeOutSamples;
                float volumeDecrement = bgmFileReader.Volume / fadeOutSamples;
                for (int i = 0; i < fadeOutSamples; ++i)
                {
                    Thread.Sleep(sleepMilliseconds);

                    float nextVolume = bgmFileReader.Volume - volumeDecrement;
                    bgmFileReader.Volume = nextVolume < 0.0f ? 0.0f : nextVolume;
                }
                bgmFileReader.Volume = 0.0f;

                bgmOutputDevice.Stop();
            }
        }
    }
}
