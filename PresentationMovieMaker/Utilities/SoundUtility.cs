using Microsoft.CognitiveServices.Speech;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
//using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;

namespace PresentationMovieMaker.Utilities
{
    public static class SoundUtility
    {
        private static string speechRecognitionLanguage = "ja-JP";

        public static string AzureSubscriptionKey { get; set; } = string.Empty;
        public static string AzureServiceRegion { get; set; } = string.Empty;
        public static string BouyomiChanExePath { get; set; } = string.Empty;
        public static string BouyomiChanRemoteTalkExePath { get; set; } = string.Empty;
        public static string SoftalkExePath { get; set; } = string.Empty;

        public const string NanamiVoiceName = "ja-JP-NanamiNeural";
        public const string KeitaVoiceName = "ja-JP-KeitaNeural";
        public const string SofTalkDefaultVoiceName = "SofTalkDefault";
        public const string BouyomiChanDefaultVoiceName = "BouyomiChanDefault";
        public const string VoicevoxVoiceName = "VoicevoxDefault";

        private static System.Speech.Synthesis.SpeechSynthesizer? _synth = null;
        private static SpeechSynthesizer? _cognitiveServicesSynth = null;

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern int mciSendString(string command,
           StringBuilder? buffer, int bufferSize, IntPtr hwndCallback);

        public static bool IsVoiceVoxVoice(string? voiceName)
        {
            return voiceName == VoicevoxVoiceName;
        }

        public static bool IsNeuralVoice(string voiceName)
        {
            return voiceName == NanamiVoiceName || voiceName == KeitaVoiceName;
        }

        public static void PlaySound(string filePath)
        {
            string aliasName = "MediaFile";

            string cmd;

            //ファイルを開く
            cmd = "open \"" + filePath + "\" alias " + aliasName;
            if (mciSendString(cmd, null, 0, IntPtr.Zero) != 0)
            {
                return;
            }

            //再生する
            cmd = "play " + aliasName;
            mciSendString(cmd, null, 0, IntPtr.Zero);
        }

        public static void StopSound()
        {
            string aliasName = "MediaFile";

            string cmd;

            //再生しているWAVEを停止する
            cmd = "stop " + aliasName;
            mciSendString(cmd, null, 0, IntPtr.Zero);
            //閉じる
            cmd = "close " + aliasName;
            mciSendString(cmd, null, 0, IntPtr.Zero);
        }

        public static TimeSpan GetWavFileDuration(string fileName)
        {
            var wf = new AudioFileReader(fileName);
            return wf.TotalTime;
        }

        private static void ExecuteBouyomiChanIfNotExecuted()
        {
            Process[] processes = Process.GetProcessesByName("BouyomiChan");
            if (processes.Length > 0)
            {
                return;
            }

            string exePath = BouyomiChanExePath;
            var process = new Process();
            var startInfo = new ProcessStartInfo(exePath)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process.StartInfo = startInfo;
            process.Start();
        }

        private static Process ExecuteBouyomiChan(string args)
        {
            ExecuteBouyomiChanIfNotExecuted();
            Process[] remoteTalkProcesses = Process.GetProcessesByName("RemoteTalk.exe");
            var process = remoteTalkProcesses.Length > 0 ? remoteTalkProcesses[0] : new Process();

            string exePath = BouyomiChanRemoteTalkExePath;
            var startInfo = new ProcessStartInfo(exePath, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            process.StartInfo = startInfo;
            process.Start();
            return process;
        }

        public static void CancelBouyomiChanSpeaking()
        {
            var args = $"/Clear";
            var process = ExecuteBouyomiChan(args);
            process.WaitForExit();
        }


        public static bool CheckBouyomiChanPlaying()
        {
            var args = $"/GetNowPlaying";
            var process = ExecuteBouyomiChan(args);
            process.WaitForExit();
            return process.ExitCode == 1;
        }

        public static void SpeakWithBouyomiChan(string message, int volume = 100, int rate=100)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            var args = $"/Talk \"{message}\" {rate} -1 {volume} 0";
            var process = ExecuteBouyomiChan(args);
            process.WaitForExit();
        }

        public static Process SpeakWithSofTalk(string message, int volume = 100, int rate=100)
        {
            string exePath = SoftalkExePath;
            var args = $"/t:0 /u:0 /s:{rate} /v:{volume} /w:{message}";
            var startInfo = new ProcessStartInfo(exePath, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = new Process()
            {
                StartInfo = startInfo
            };
            process.Start();
            return process;
        }

        public static Task SpeakWithSsml(string message, string voiceName = NanamiVoiceName, int volume = 100, double speedRate = 1.0, int pitchHz = 0)
        {
            return GetCognitiveServicesSynthesizer().SpeakSsmlAsync(CreateTextReadOut(
                message, voiceName, pitchHz, speedRate, volume));
        }

        public static TimeSpan CalculateDurationOfSpeech(string message, string? voiceName = null, int volume = 100, int speedRate = 0)
        {
            var voice = IsVoiceVoxVoice(voiceName) ? null : voiceName;
            var synth = GetSynthesizer();
            var currentPrompt = synth.GetCurrentlySpokenPrompt();
            if (currentPrompt != null && !currentPrompt.IsCompleted)
            {
                // 再生中は例外が発生してしまうので、0を返す
                return new TimeSpan();
            }

            if (voice is not null)
            {
                synth.SelectVoice(voice);
            }

            synth.Volume = volume;

            // -10 ～ +10 の範囲
            synth.Rate = speedRate;

            using (var stream = new MemoryStream())
            {
                synth.SetOutputToWaveStream(stream);

                synth.Speak(message);
                stream.Seek(0, SeekOrigin.Begin);
                using (var wfr = new WaveFileReader(stream))
                {
                    TimeSpan totalTime = wfr.TotalTime;
                    return totalTime;
                }
            }
        }

        public static System.Speech.Synthesis.Prompt Speak(
            string message, string? voiceName = null, int volume = 100, int speedRate = 0)
        {
            var synth = GetSynthesizer();

            if (synth.State == System.Speech.Synthesis.SynthesizerState.Speaking)
            {
                return synth.GetCurrentlySpokenPrompt();
            }

            // Configure the audio output.   
            synth.SetOutputToDefaultAudioDevice();

            if (voiceName is not null)
            {
                synth.SelectVoice(voiceName);
            }

            synth.Volume = volume;

            // -10 ～ +10 の範囲
            synth.Rate = speedRate;

            return synth.SpeakAsync(message);
        }

        public static void CancelSpeakingAll()
        {
            var synth = GetSynthesizer();
            synth.SpeakAsyncCancelAll();
        }

        public static void AddSpeakProgressCallback(Action<object?, System.Speech.Synthesis.SpeakProgressEventArgs> callback)
        {
            var synth = GetSynthesizer();
            synth.SpeakProgress += (object? sender, System.Speech.Synthesis.SpeakProgressEventArgs e) =>
            {
                callback(sender, e);
            };
        }

        public static void AddVisemeReachedCallback(Action<object?, System.Speech.Synthesis.VisemeReachedEventArgs> callback)
        {
            var synth = GetSynthesizer();
            synth.VisemeReached += (object? sender, System.Speech.Synthesis.VisemeReachedEventArgs e) =>
            {
                callback(sender, e);
            };
        }

        public static Task CancelNeuralSpeaking()
        {
            return GetCognitiveServicesSynthesizer().StopSpeakingAsync();
        }

        public static void CancelSpeaking(System.Speech.Synthesis.Prompt prompt)
        {
            GetSynthesizer().SpeakAsyncCancel(prompt);
        }

        public static IEnumerable<string> GetInstalledVoiceNames()
        {
            yield return SofTalkDefaultVoiceName;
            yield return BouyomiChanDefaultVoiceName;
            yield return NanamiVoiceName;
            yield return KeitaVoiceName;
            foreach (var voice in GetSynthesizer().GetInstalledVoices())
            {
                yield return voice.VoiceInfo.Name;
            }
            yield return VoicevoxVoiceName;
        }

        public static string GetHarukaVoiceName()
        {
            return GetSynthesizer().GetInstalledVoices().First().VoiceInfo.Name;
        }

        public static bool IsValidVoiceName(string name)
        {
            return GetInstalledVoiceNames().Any(x => x == name);
        }


        public static System.Speech.Synthesis.SpeechSynthesizer GetSynthesizer()
        {
            if (_synth is null)
            {
                _synth = new System.Speech.Synthesis.SpeechSynthesizer();
            }

            return _synth;
        }

        private static SpeechSynthesizer GetCognitiveServicesSynthesizer()
        {
            if (_cognitiveServicesSynth is null)
            {
                var speechConfig = SpeechConfig.FromSubscription(AzureSubscriptionKey, AzureServiceRegion);
                speechConfig.SpeechRecognitionLanguage = speechRecognitionLanguage;
                _cognitiveServicesSynth = new SpeechSynthesizer(speechConfig);
            }

            return _cognitiveServicesSynth;
        }

        private static string CreateTextReadOut(string text, string voiceName, int pitch = 0, double rate = 1, int volume = 100)
        {
            string pitchString = pitch >= 0 ? $"+{pitch}Hz" : $"{pitch}Hz";
            //return $"<speak version='1.0' xmlns='https://www.w3.org/2001/10/synthesis' xml:lang='{speechRecognitionLanguage}'><voice name='{NanamiSpeechName}'>{text}</voice></speak>";
            return $"<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" xmlns:mstts=\"http://www.w3.org/2001/mstts\" xmlns:emo=\"http://www.w3.org/2009/10/emotionml\" version=\"1.0\" xml:lang=\"{speechRecognitionLanguage}\"><voice name=\"{voiceName}\"><prosody rate=\"{rate}\" pitch=\"{pitchString}\" volume=\"{volume}\">{text}</prosody></voice></speak> ";
        }
    }
}
