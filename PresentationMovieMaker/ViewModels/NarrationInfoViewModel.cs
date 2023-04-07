using NAudio.Wave;
using PresentationMovieMaker.DataModels;
using PresentationMovieMaker.Utilities;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PresentationMovieMaker.ViewModels
{
    public class NarrationInfoViewModel : CompositeDisposableBase
    {
        private WaveOutEvent _outputDevice = new WaveOutEvent();
        private CancellationTokenSource? _playSelectedAudioCancellationTokenSource = null;
        private Prompt? _currentPrompt = null;
        private Process? _currentSoftalkProcess = null;
        private CancellationTokenSource _softalkCancellationTokenSource = new CancellationTokenSource();



        public NarrationInfoViewModel(PageInfoViewModel parent)
        {
            AudioPaths.CollectionChanged += AudioPaths_CollectionChanged;
            Parent = parent;
            SelectedVoiceName.Value = parent.Parent.VoiceName.Value;

            Subscribe(AddAudioPathCommand, () =>
            {
                CreateAndAddNewAudioPath();
                foreach (var narrationInfo in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    narrationInfo.CreateAndAddNewAudioPath();
                }
            });

            Subscribe(RemoveAudioPathCommand, () =>
            {
                AudioPaths.RemoveAt(AudioPaths.Count - 1);
            });

            Subscribe(PlaySelectedAudioCommand, () =>
            {
                if (SelectedAudioPath.Value is null)
                {
                    return;
                }

                if (IsSelectedAudioPlaying.Value)
                {
                    if (_playSelectedAudioCancellationTokenSource is null)
                    {
                        return;
                    }

                    _playSelectedAudioCancellationTokenSource.Cancel();
                    IsSelectedAudioPlaying.Value = false;
                }
                else
                {
                    IsSelectedAudioPlaying.Value = true;
                    Task.Run(() =>
                    {
                        using (_playSelectedAudioCancellationTokenSource = new CancellationTokenSource())
                        {
                            var ct = _playSelectedAudioCancellationTokenSource.Token;
                            try
                            {
                                PlayAudio(SelectedAudioPath.Value, ct);
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            finally
                            {
                                _outputDevice?.Stop();
                                IsSelectedAudioPlaying.Value = false;
                            }
                        }
                        _playSelectedAudioCancellationTokenSource = null;
                    });
                }
            });


            Subscribe(IsSelectedAudioPlaying, isAudioPlaying =>
            {
                if (isAudioPlaying)
                {
                    PlaySelectedAudioButtonLabel.Value = "停止";
                }
                else
                {
                    PlaySelectedAudioButtonLabel.Value = "選択した音声の再生";
                }
            });

            Subscribe(AudioRoot, value =>
            {
                foreach (var audioPath in AudioPaths)
                {
                    audioPath.RootPath.Value = value;
                }
            });

            Subscribe(SelectedAudioPath, value =>
            {
                IsAudioPathSelected.Value = value != null;
            });

            Subscribe(ReadTextCommand, () =>
            {
                if (IsReadingText.Value)
                {
                    StopSpeech();
                    IsReadingText.Value = false;
                }
                else
                {
                    IsReadingText.Value = true;
                    Task.Run(() =>
                    {
                        SoundUtility.CancelSpeakingAll();
                        StartSpeech();
                        IsReadingText.Value = false;
                    });
                }
            });
            Subscribe(IsReadingText, value =>
            {
                if (value)
                {
                    ReadTextButtonLabel.Value = "読み上げ停止";
                }
                else
                {
                    ReadTextButtonLabel.Value = "読み上げ開始";
                }
            });

            Subscribe(UpdateTotalDurationCommand, () =>
            {
                UpdateDuration();
            });

            Subscribe(SpeechText, value =>
            {
                UpdateDuration();
                UpdateCharCount();
            });

            Subscribe(SelectedVoiceName, value =>
            {
                UpdateDuration();
            });
            Subscribe(SpeechSpeedRate, value =>
            {
                UpdateDuration();
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.SpeechSpeedRate.Value = value;
                }
            });
            Subscribe(CaptionMarginBottom, value =>
            {
                UpdateDuration();
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.CaptionMarginBottom.Value = value;
                }
            });

            Subscribe(IsAudioParallel, value =>
            {
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.IsAudioParallel.Value = value;
                }
            });

            Subscribe(PreBlankMilliseconds, value =>
            {
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.PreBlankMilliseconds.Value = value;
                }
            });

            Subscribe(PostBlankMilliseconds, value =>
            {
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.PostBlankMilliseconds.Value = value;
                }
            });

            Subscribe(TextColorR, value =>
            {
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.TextColorR.Value = value;
                }
            });
            Subscribe(TextColorG, value =>
            {
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.TextColorG.Value = value;
                }
            });
            Subscribe(TextColorB, value =>
            {
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.TextColorB.Value = value;
                }
            });
            Subscribe(TextColorA, value =>
            {
                foreach (var info in EnumerateSelectedNattionInfosWithoutMyself())
                {
                    info.TextColorA.Value = value;
                }
            });
        }

        private void AudioPaths_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is PathViewModel audioPath)
                    {
                        AddSubscriptionToAudioPath(audioPath);
                    }
                }
            }
        }

        public NarrationInfoViewModel(NarrationInfo dataModel, PageInfoViewModel parent)
            : this(parent)
        {
            if (dataModel.AudioPaths is not null)
            {
                foreach (var path in dataModel.AudioPaths)
                {
                    AudioPaths.Add(new PathViewModel(path));
                }
            }
            SpeechText.Value = dataModel.SpeechText ?? string.Empty;
            SpeechVolume.Value = dataModel.SpeechVolume;
            SpeechSpeedRate.Value = dataModel.SpeechSpeedRate;
            CaptionMarginBottom.Value = dataModel.CaptionMarginBottom;
            PreBlankMilliseconds.Value = dataModel.PreBlankMilliseconds;
            PostBlankMilliseconds.Value = dataModel.PostBlankMilliseconds;
            AudioVolume.Value = dataModel.AudioVolume;
            IsAudioParallel.Value = dataModel.IsAudioParallel;
            TextColorR.Value = dataModel.TextColorR;
            TextColorG.Value = dataModel.TextColorG;
            TextColorB.Value = dataModel.TextColorB;
            TextColorA.Value = dataModel.TextColorA;
        }

        public PageInfoViewModel Parent { get; }

        public ReactiveProperty<int> TextColorR { get; } = new ReactiveProperty<int>(0xff);
        public ReactiveProperty<int> TextColorG { get; } = new ReactiveProperty<int>(0xff);
        public ReactiveProperty<int> TextColorB { get; } = new ReactiveProperty<int>(0xff);
        public ReactiveProperty<int> TextColorA { get; } = new ReactiveProperty<int>(0xff);

        public ReactiveProperty<int> PreBlankMilliseconds { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<int> PostBlankMilliseconds { get; } = new ReactiveProperty<int>();

        public ReactiveProperty<int> TotalCharCount { get; } = new ReactiveProperty<int>();

        public ReactiveProperty<TimeSpan> TotalDuration { get; } = new ReactiveProperty<TimeSpan>();

        public ReactiveProperty<double> CaptionMarginBottom { get; } = new ReactiveProperty<double>(60.0);
        public ReactiveProperty<double> SpeechSpeedRate { get; } = new ReactiveProperty<double>(1.0);

        public ReactiveProperty<double> SpeechVolume { get; } = new ReactiveProperty<double>(1.0);

        public ReactiveProperty<string> SelectedVoiceName { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<string> SpeechText { get; } = new ReactiveProperty<string>("");

        public ObservableCollection<PathViewModel> AudioPaths { get; } = new ObservableCollection<PathViewModel>();

        public ReactiveProperty<double> AudioVolume { get; } = new ReactiveProperty<double>(1.0);

        public ReactiveProperty<bool> IsAudioParallel { get; } = new ReactiveProperty<bool>(true);


        public ReactiveProperty<string> AudioRoot { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<bool> IsAudioPathSelected { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<PathViewModel> SelectedAudioPath { get; } = new ReactiveProperty<PathViewModel>();
        public ReactiveProperty<bool> IsSelectedAudioPlaying { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> PlaySelectedAudioButtonLabel { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<bool> IsReadingText { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> ReadTextButtonLabel { get; } = new ReactiveProperty<string>();


        public ReactiveCommand AddAudioPathCommand { get; } = new ReactiveCommand();

        public ReactiveCommand RemoveAudioPathCommand { get; } = new ReactiveCommand();

        public ReactiveCommand PlaySelectedAudioCommand { get; } = new ReactiveCommand();

        public ReactiveCommand ReadTextCommand { get; } = new ReactiveCommand();

        public ReactiveCommand UpdateTotalDurationCommand { get; } = new ReactiveCommand();



        public NarrationInfo ToSerializable()
        {
            var serial = new NarrationInfo();
            serial.AudioPaths.AddRange(AudioPaths.Select(x => x.Path.Value));
            serial.SpeechText = SpeechText.Value;
            serial.SpeechVolume = SpeechVolume.Value;
            serial.SpeechSpeedRate = SpeechSpeedRate.Value;
            serial.CaptionMarginBottom = CaptionMarginBottom.Value;
            serial.PostBlankMilliseconds = PostBlankMilliseconds.Value;
            serial.PreBlankMilliseconds = PreBlankMilliseconds.Value;
            serial.AudioVolume = AudioVolume.Value;
            serial.IsAudioParallel = IsAudioParallel.Value;
            serial.TextColorR = TextColorR.Value;
            serial.TextColorG = TextColorG.Value;
            serial.TextColorB = TextColorB.Value;
            serial.TextColorA = TextColorA.Value;
            return serial;
        }

        public void StartPlaying(CancellationToken? ct = null)
        {
            if (HasAudio())
            {
                PlayAudioAll(ct);
            }
            else
            {
                StartSpeech(ct);
            }
        }

        public void StopPlaying()
        {
            StopPlayingAudio();
            StopSpeech();
        }

        public IEnumerable<string> EnumerateSpeechTextPerLine()
        {
            foreach (var text in SpeechText.Value.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return text;
            }
        }
        public IEnumerable<string> EnumerateSpeechTextPerPeriod()
        {
            foreach (var text in TextUtility.SplitWithKeepingDelimiters(SpeechText.Value,
                new string[]
                {
                    "。",
                    "?"+ Environment.NewLine,
                    "？"+ Environment.NewLine,
                    "！"+ Environment.NewLine,
                    "!"+ Environment.NewLine,
                }))
            {
                yield return text.Trim(new[] { '\n', '\r' });
            }
        }
        private void CreateAndAddNewAudioPath()
        {
            var audioPath = new PathViewModel();
            audioPath.RootPath.Value = AudioRoot.Value;
            AudioPaths.Add(audioPath);
        }

        private void AddSubscriptionToAudioPath(PathViewModel audioPath)
        {
            Subscribe(audioPath.Path, path =>
            {
                SetSelectedAudioPaths(audioPath);
            });
        }

        private void SetSelectedAudioPaths(PathViewModel audioPath)
        {
            var audioIndex = AudioPaths.IndexOf(audioPath);
            var targetPaths = EnumerateSelectedNattionInfosWithoutMyself()
                .Where(x => audioIndex < x.AudioPaths.Count)
                .Select(x => x.AudioPaths[audioIndex]).ToArray();
            foreach (var targetPath in targetPaths)
            {
                targetPath.Path.Value = audioPath.Path.Value;
            }
        }

        private IEnumerable<PageInfoViewModel> EnumerateSelectedPageInfosWithoutMyself()
        {
            return Parent.EnumerateSelectedPageInfosWithoutMyself();
        }

        private IEnumerable<NarrationInfoViewModel> EnumerateSelectedNattionInfosWithoutMyself()
        {
            var narrationIndex = Parent.NarrationInfos.IndexOf(this);
            if (narrationIndex < 0)
            {
                yield break;
            }

            foreach (var info in EnumerateSelectedPageInfosWithoutMyself()
                .Where(x => narrationIndex < x.NarrationInfos.Count)
                .Select(x => x.NarrationInfos[narrationIndex]))
            {
                yield return info;
            }
        }

        private double GetSpeechVolume()
        {
            return SpeechVolume.Value * (Parent.Parent.Parent?.MovieSetting.NarrationVolume.Value ?? 1.0);
        }

        public void StartSpeech(string text, CancellationToken? ct = null, int intervalMilliseconds = 100, Action? waitCallback = null)
        {
            if (SoundUtility.IsVoiceVoxVoice(SelectedVoiceName.Value))
            {
                var speaker = 1;
                VoicevoxUtility.Speek(text, speaker).Wait();
            }
            else if (SelectedVoiceName.Value == SoundUtility.BouyomiChanDefaultVoiceName)
            {
                SoundUtility.SpeakWithBouyomiChan(
                    text,
                    (int)(GetSpeechVolume() * 100),
                    GetSofTalkSpeechRate());

                // リップシンクするためにMicrosoftのSpeechSynthesizerを利用する
                _currentPrompt = SoundUtility.Speak(text,
                    SoundUtility.GetHarukaVoiceName(),
                    0,
                    GetStandardSpeechRate());

                //Thread.Sleep(intervalMilliseconds);
                while (SoundUtility.CheckBouyomiChanPlaying() || (_currentPrompt != null && !_currentPrompt.IsCompleted))
                {
                    waitCallback?.Invoke();
                    Thread.Sleep(intervalMilliseconds);
                    if (ct != null && ct.Value.IsCancellationRequested)
                    {
                        StopSpeech();
                        ct.Value.ThrowIfCancellationRequested();
                    }
                }

                _currentPrompt = null;
                ct?.ThrowIfCancellationRequested();
            }
            else if (SelectedVoiceName.Value == SoundUtility.SofTalkDefaultVoiceName)
            {
                _currentSoftalkProcess = SoundUtility.SpeakWithSofTalk(
                    text,
                    (int)(GetSpeechVolume() * 100),
                    GetSofTalkSpeechRate());
                var token = ct ?? _softalkCancellationTokenSource.Token;
                var task = _currentSoftalkProcess.WaitForExitAsync(token);

                while (!task.IsCompleted)
                {
                    waitCallback?.Invoke();
                    Thread.Sleep(intervalMilliseconds);
                    if (ct != null && ct.Value.IsCancellationRequested)
                    {
                        StopSpeech();
                        ct.Value.ThrowIfCancellationRequested();
                    }
                }
                _currentSoftalkProcess.Dispose();
                _currentSoftalkProcess = null;
            }
            else if (SoundUtility.IsNeuralVoice(SelectedVoiceName.Value))
            {
                var task = SoundUtility.SpeakWithSsml(
                    text,
                    SelectedVoiceName.Value,
                    (int)(GetSpeechVolume() * 100),
                    SpeechSpeedRate.Value);
                while (!task.IsCompleted)
                {
                    waitCallback?.Invoke();
                    Thread.Sleep(intervalMilliseconds);
                    if (ct != null && ct.Value.IsCancellationRequested)
                    {
                        StopSpeech();
                        ct.Value.ThrowIfCancellationRequested();
                    }
                }
            }
            else
            {
                // 0.5～1.5 を -5～+5 に変換する
                int rate = GetStandardSpeechRate();
                _currentPrompt = SoundUtility.Speak(text,
                    SelectedVoiceName.Value,
                    (int)(GetSpeechVolume() * 100),
                    rate);
                while (_currentPrompt != null && !_currentPrompt.IsCompleted)
                {
                    waitCallback?.Invoke();
                    Thread.Sleep(intervalMilliseconds);
                    if (ct != null && ct.Value.IsCancellationRequested)
                    {
                        StopSpeech();
                        break;
                    }
                }

                _currentPrompt = null;
                ct?.ThrowIfCancellationRequested();
            }
        }

        public void StartSpeech(CancellationToken? ct = null, bool waitsBlank = true)
        {
            if (waitsBlank)
            {
                ThreadUtility.Wait(PreBlankMilliseconds.Value, ct);
            }

            Parent.Parent.Parent?.SpeechText(this, ct);

            if (waitsBlank)
            {
                ThreadUtility.Wait(PostBlankMilliseconds.Value, ct);
            }
        }

        private bool HasAudio()
        {
            return this.AudioPaths.Any();
        }

        private int GetSofTalkSpeechRate()
        {
            int rate = (int)(SpeechSpeedRate.Value * 100);
            return rate;
        }

        private int GetStandardSpeechRate()
        {
            // 0.5～1.5 を -5～+5 に変換する
            int rate = (int)((SpeechSpeedRate.Value - 1) * 10);
            return rate;
        }

        public void StopSpeech()
        {
            if (SelectedVoiceName.Value == SoundUtility.BouyomiChanDefaultVoiceName)
            {
                SoundUtility.CancelBouyomiChanSpeaking();
            }
            else if (SelectedVoiceName.Value == SoundUtility.SofTalkDefaultVoiceName && _currentSoftalkProcess != null)
            {
                _currentSoftalkProcess.Kill();
            }
            else if (SoundUtility.IsNeuralVoice(SelectedVoiceName.Value))
            {
                var task = SoundUtility.CancelNeuralSpeaking();
                task.Wait();
            }
            else
            {
                if (_currentPrompt is not null)
                {
                    SoundUtility.CancelSpeaking(_currentPrompt);
                }
            }
        }

        public void PlayAudioAll(CancellationToken? ct)
        {
            foreach (var audioPath in AudioPaths)
            {
                PlayAudio(audioPath, ct);
            }
        }

        public void StopPlayingAudio()
        {
            if (_outputDevice.PlaybackState != PlaybackState.Stopped)
            {
                _outputDevice.Stop();
            }
        }

        private void PlayAudio(PathViewModel audioName, CancellationToken? ct)
        {
            var audioPath = audioName.ActualPath.Value;
            if (!File.Exists(audioPath))
            {
                //WriteErrorLogLine($"ファイルが見つかりません。\"{audioPath}\"");
                return;
            }
            var audioFile = new AudioFileReader(audioPath);
            _outputDevice.Init(audioFile);

            var duration = SoundUtility.GetWavFileDuration(audioPath);

            _outputDevice.Play();

            // 再生完了を待つ
            Wait(duration, ct);

            _outputDevice.Stop();

            Thread.Sleep(300);
        }

        private void Wait(TimeSpan duration, CancellationToken? ct)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.Elapsed < duration)
            {
                Thread.Sleep(100);
                ct?.ThrowIfCancellationRequested();
            }
            stopwatch.Stop();
        }

        private void UpdateDuration()
        {
            if (SelectedVoiceName.Value is null 
                || SoundUtility.IsNeuralVoice(SelectedVoiceName.Value) 
                || SelectedVoiceName.Value == SoundUtility.SofTalkDefaultVoiceName
                || SelectedVoiceName.Value == SoundUtility.BouyomiChanDefaultVoiceName
                )
            {
                return;
            }

            var duration = SoundUtility.CalculateDurationOfSpeech(
                SpeechText.Value,
                SelectedVoiceName.Value,
                (int)(GetSpeechVolume() * 100),
                GetStandardSpeechRate());
            TotalDuration.Value = duration;

            Parent.UpdateDuration();
        }

        private void UpdateCharCount()
        {
            TotalCharCount.Value = SpeechText.Value.Length;
            Parent.UpdateCharCount();
        }
    }
}
