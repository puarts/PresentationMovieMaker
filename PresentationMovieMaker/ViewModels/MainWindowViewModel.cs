using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Text.Json;
using NAudio.Wave;
using System.Diagnostics;
using Microsoft.Win32;
using PresentationMovieMaker.Utilities;
using PresentationMovieMaker.DataModels;
using System.Reflection;
using System.Resources;
using Microsoft.CognitiveServices.Speech;
using System.Speech.Recognition;
using System.Drawing;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace PresentationMovieMaker.ViewModels
{
    public enum EyePattern
    {
        Open,
        Close
    }

    public partial class MainWindowViewModel : CompositeDisposableBase, ILogger
    {
        private AudioFileReader? _bgmFileReader = null;
        private readonly WaveOutEvent _bgmOutputDevice = new();
        private readonly WaveOutEvent _soundEffectOutputDevice = new();
        private readonly WaveOutEvent _outputDevice = new();
        private readonly Dictionary<string, AudioFileReader> _soundEffectFileReaders = new();
        private int _pageIndex = 0;

        private CancellationTokenSource? _cancellationTokenSource;
        private CancellationTokenSource? _goToNexPageCancellationTokenSource;
        private CancellationTokenSource? _goBackToPreviousPageCancellationTokenSource;
        private Task? _playTask = null;
        private Task? _playPageTask = null;
        private readonly Dictionary<char, BitmapSource> _pronuunciationBitmaps = new();
        private readonly Dictionary<EyePattern, BitmapSource> _eyeBitmaps = new();
        private readonly TextConverter _textConverter = new();
        private readonly Utilities.SpeechRecognizer _recognizer = new();
        readonly Random _randGenerator = new();

        public MainWindowViewModel()
        {
            Settings.Add(BouyomiChanPath);
            Settings.Add(BouyomiChanRemoteTalkPath);
            Settings.Add(FaceRotation);
            Settings.Add(FaceRotateCenterX);
            Settings.Add(FaceRotateCenterY);
            Settings.Add(FaceRotateSpeed);
            Settings.Add(ShowTimeCode);

            MovieSetting = new MovieSettingViewModel(this)
            {
                Parent = this
            };

            //SoundUtility.GetSynthesizer().PhonemeReached += (object? sender, System.Speech.Synthesis.PhonemeReachedEventArgs e) =>
            //{
            //    CurrentPronunciation.Value += e.Phoneme + " ";
            //};

            SoundUtility.GetSynthesizer().VisemeReached += (object? sender, System.Speech.Synthesis.VisemeReachedEventArgs e) =>
            {
                if (_pronuunciationBitmaps.Count == 0)
                {
                    return;
                }

                var pron = TextUtility.ConvertVisemeToPronuounciation(e.Viseme);
                //CurrentPronunciation.Value += $"[{e.Viseme}→{pron}]";
                if (char.IsLetter(pron))
                {
                    MouthBitmap.Value = _pronuunciationBitmaps[pron];
                }
            };

            SoundUtility.GetSynthesizer().SpeakProgress += MainWindowViewModel_SpeakProgress;

            SoundUtility.AddSpeakProgressCallback((object? sender, System.Speech.Synthesis.SpeakProgressEventArgs e) =>
            {
                //CurrentPronunciation.Value += e.Text;
            });

            _ = OpenPlayWindowCommand.Subscribe(() =>
            {
                View?.Dispatcher.Invoke(() =>
                {
                    UpdatePlayWindowWidth();

                    PlayWindow = new Views.PlayWindow
                    {
                        Owner = View
                    };
                    PlayWindow?.Show();
                });
            });

            _ = PlayCommand.Subscribe(() =>
            {
                if (IsPlaying.Value)
                {
                    CancelPlaying();
                }
                else
                {
                    PlaySlideshow();
                }

            }).AddTo(Disposable);

            CancelPlayingCommand.Subscribe(() => CancelPlaying()).AddTo(Disposable);

            GoToNextPageCommand.Subscribe(() =>
            {
                if (_goToNexPageCancellationTokenSource is null)
                {
                    return;
                }

                _goToNexPageCancellationTokenSource.Cancel();
            }).AddTo(Disposable);

            GoBackToPreviousPageCommand.Subscribe(() =>
            {
                if (_goBackToPreviousPageCancellationTokenSource is null)
                {
                    return;
                }

                _goBackToPreviousPageCancellationTokenSource.Cancel();
            }).AddTo(Disposable);

            Subscribe(PauseCommand, () =>
            {
                IsPaused.Value = !IsPaused.Value;
            });

            Subscribe(OpenSettingFolderCommand, () =>
            {
                var openDir = Path.GetDirectoryName(SettingPath.ActualPath.Value);
                if (!Directory.Exists(openDir))
                {
                    return;
                }

                Process.Start("explorer", openDir);
            });

            Subscribe(CreateNewSettingCommand, () =>
            {
                MovieSetting.ResetToDefault();
            });

            Subscribe(ExportNarrationCommand, () =>
            {
                var narrations = MovieSetting.PageInfos.SelectMany(x => x.NarrationInfos).Select(x => x.SpeechText.Value);
                var exportText = string.Join("\n\n", narrations);
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "ナレーションのエクスポート",
                    Filter = "テキストファイル|*.txt"
                };
                if (saveFileDialog.ShowDialog(View) ?? false)
                {
                    var outputPath = saveFileDialog.FileName;
                    File.WriteAllText(outputPath, exportText);
                    WriteLogLine($"ナレーションをエクスポートしました。\"{outputPath}\"");
                }
            });

            OpenSettingCommand.Subscribe(() =>
            {
                var dialog = new OpenFileDialog();
                if (!string.IsNullOrEmpty(SettingPath.ActualPath.Value))
                {
                    var dir = Path.GetDirectoryName(SettingPath.ActualPath.Value);
                    if (Directory.Exists(dir))
                    {
                        dialog.InitialDirectory = dir;
                    }
                }

                dialog.Filter = "設定ファイル (*.json)|*.json|全てのファイル (*.*)|*.*";
                if (dialog.ShowDialog() == true)
                {
                    SettingPath.Path.Value = dialog.FileName;
                }
            }).AddTo(Disposable);

            SaveSettingCommand.Subscribe(() =>
            {
                if (string.IsNullOrEmpty(SettingPath.ActualPath.Value))
                {
                    var dialog = new SaveFileDialog();
                    if (dialog.ShowDialog() != true)
                    {
                        return;
                    }

                    SettingPath.Path.Value = dialog.FileName;
                }

                SaveSettings(SettingPath.ActualPath.Value);
                WriteLogLine($"設定を保存しました。\n{SettingPath.ActualPath.Value}");
            }).AddTo(Disposable);

            const string cacheDirName = "SlideCache";
            const float cacheImageSizeMultiply = 1.0f / 8.0f;
            Subscribe(SaveSlideCacheCommand, () =>
            {
                var dir = Path.GetDirectoryName(SettingPath.ActualPath.Value);
                if (dir is null || !Directory.Exists(dir))
                {
                    WriteErrorLogLine($"設定ファイルのディレクトリが取得できません。");
                    return;
                }

                _ = Task.Run(() =>
                {
                    var cacheDir = Path.Combine(dir, cacheDirName);
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                    }
                    Directory.CreateDirectory(cacheDir);
                    foreach (var path in EnumerateSlideImagePaths())
                    {
                        var resizedImage = ImageUtility.ReadResizedImage(path, cacheImageSizeMultiply);

                        string outputPath = Path.Combine(cacheDir, Path.GetFileName(path));
                        resizedImage.Save(outputPath);
                        WriteLogLine($"キャッシュ保存: {outputPath}");
                    }
                });
            });

            Subscribe(RelocateNarrationInfoCommand, () =>
            {
                var dir = Path.GetDirectoryName(SettingPath.ActualPath.Value);
                if (dir is null || !Directory.Exists(dir))
                {
                    WriteErrorLogLine($"設定ファイルのディレクトリが取得できません。");
                    return;
                }

                _ = Task.Run(() =>
                {

                    var cacheDir = Path.Combine(dir, cacheDirName);
                    var cacheImages = Directory.EnumerateFiles(cacheDir).Select(x => ImageData.CreateFromFile(x)).ToArray();
                    var relocationMap = new Dictionary<string, string>();
                    foreach (var filePath in EnumerateSlideImagePaths())
                    {
                        var image = ImageUtility.ReadResizedImage(filePath, cacheImageSizeMultiply);
                        var imageData = ImageData.CreateFromImage(image, filePath);

                        var matchedImagePath = ImageUtility.FindMatchedImage(imageData, cacheImages, 0);
                        if (matchedImagePath is null)
                        {
                            continue;
                        }

                        var source = Path.GetFileNameWithoutExtension(filePath);
                        var destination = Path.GetFileNameWithoutExtension(matchedImagePath) ?? throw new Exception();
                        WriteLogLine($"{source} => {destination}");
                        if (source != destination)
                        {
                            relocationMap[source] = destination;
                        }
                    }

                    var copiedPages = MovieSetting.PageInfos.Select(x => x.ToSerializable()).ToDictionary(x => Path.GetFileNameWithoutExtension(x.ImagePath));
                    foreach (var pageInfo in MovieSetting.PageInfos)
                    {
                        var name = Path.GetFileNameWithoutExtension(pageInfo.ImagePath.Value.Path.Value);
                        if (!relocationMap.ContainsKey(name))
                        {
                            continue;
                        }

                        var destName = relocationMap[name];
                        WriteLogLine($"コピー {destName} => {name}");
                        var serial = copiedPages[destName] ?? throw new Exception();
                        var origPath = pageInfo.ImagePath.Value.Path.Value;
                        SingletonDispatcher.Invoke(() =>
                        {
                            // Collection 書き換えるのでUIスレッドで実行が必要
                            pageInfo.DeepCopyFrom(serial);
                        });
                        pageInfo.ImagePath.Value.Path.Value = origPath;
                    }
                });
            });

            SettingPath.ActualPath.Subscribe(path =>
            {
                LoadSettings(path);
            }).AddTo(Disposable);

            Subscribe(IsPlaying, isPlaying =>
            {
                if (isPlaying)
                {
                    PlayButtonLabel.Value = "停止";
                }
                else
                {
                    PlayButtonLabel.Value = "再生開始";
                }
            });
            Subscribe(IsPaused, isPaused =>
            {
                if (isPaused)
                {
                    PauseButtonLabel.Value = "再開";
                }
                else
                {
                    PauseButtonLabel.Value = "一時停止";
                }
            });


            Subscribe(MovieSetting.PageNumberPosX
                .CombineLatest(MovieSetting.PageNumberPosY), _ =>
            {
                UpdatePageNumberPositions();
            });
            Subscribe(MovieSetting.CaptionMarginBottom
                .CombineLatest(MovieSetting.CaptionMarginLeft), _ =>
            {
                SyncCaptionMargin();
            });

            Subscribe(MovieSetting.FaceImageWidth, value =>
            {
                ActualFaceImageWidth.Value = value;
            });

            Subscribe(PlayWindowWidth, value =>
            {
                UpdatePageNumberPosX();
                UpdateCaptionWidth();
                PreviewWindowWidth.Value = value / 4;
            });
            Subscribe(PlayWindowHeight, value =>
            {
                UpdatePageNumberPosY();
                PreviewWindowHeight.Value = value / 4;
            });
            Subscribe(CaptionMarginLeft, value =>
            {
                UpdateCaptionWidth();
            });

            Subscribe(BouyomiChanPath.Value.ActualPath, value =>
            {
                SoundUtility.BouyomiChanExePath = value;
            });
            Subscribe(BouyomiChanRemoteTalkPath.Value.ActualPath, value =>
            {
                SoundUtility.BouyomiChanRemoteTalkExePath = value;
            });
            Subscribe(CurrentPage, x =>
            {
                SyncPageInfo(x);
            });

            Subscribe(MovieSetting.SlideBackgroundImagePath.Value.ActualPath
                .CombineLatest(MovieSetting.PageInfos.CollectionChangedAsObservable().StartWith(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset))), _ =>
            {
                UpdatePlayWindowWidth();
            });

            Subscribe(MovieSetting.SelectedPageInfo, x =>
            {
                SyncPageInfo(x);
                BlackOpacity.Value = 0.0;
            });

            Subscribe(SlideSubImages.CollectionChangedAsObservable(), x =>
            {
                SlideSubImageCount.Value = SlideSubImages.Count;
            });
            Subscribe(PlayWindowHeight.CombineLatest(SlideSubImageMargin), value =>
            {
                SlideSubImageMaxHeight.Value = value.First - SlideSubImageMargin.Value * 2;
            });
            Subscribe(CurrentPageTitleVerticalAlignment, value =>
            {
                switch (value)
                {
                    case VerticalAlignment.Center:
                        {
                            // タイトルの高さが分からないので、適当に1/3にしておく
                            CurrentPageTitleVerticalOffset.Value = PlayWindowHeight.Value;
                        }
                        break;
                    default:
                        CurrentPageTitleVerticalOffset.Value = 0;
                        break;
                }
            });
        }

        private void UpdatePlayWindowWidth()
        {
            if (!FitsWindowSizeToBackgroundImage.Value)
            {
                return;
            }

            var bgPath = MovieSetting.SlideBackgroundImagePath.Value.ActualPath.Value;
            if (File.Exists(bgPath))
            {
                // スライド背景があればスライド背景からウインドウサイズ設定
                var (Width, Height) = ImageUtility.GetImageSize(bgPath);
                AspectRatio.Value = Width / (double)Height;
                PlayWindowWidth.Value = Width;
                PlayWindowHeight.Value = Height;
            }
            else
            {
                // スライド背景がなければ、最初のページの画像サイズでウインドウサイズを設定
                var firstPage = MovieSetting.PageInfos.FirstOrDefault();
                if (firstPage is not null)
                {
                    var path = firstPage.ImagePath.Value.ActualPath.Value;
                    if (ImageUtility.IsImageFile(path) && File.Exists(path))
                    {
                        var (Width, Height) = ImageUtility.GetImageSize(path);
                        AspectRatio.Value = Width / (double)Height;
                        PlayWindowWidth.Value = Width;
                        PlayWindowHeight.Value = Height;
                    }
                }
            }
        }

        internal void SyncPageInfo(PageInfoViewModel? page)
        {
            SetPageInfoByPageType(page?.PageType.Value ?? default);

            CurrentPageTitle.Value = page?.Title.Value ?? string.Empty;
            CurrentPageDescription.Value = page?.GetDescription() ?? string.Empty;
            SlideSubImageMargin.Value = page?.SubImageMargin.Value ?? 30.0;

            Application.Current.Dispatcher.Invoke(() =>
            {
                SlideSubImages.Clear();
                if (page is not null)
                {
                    foreach (var imagePath in page.SubImagePaths)
                    {
                        SlideSubImages.Add(imagePath);
                    }
                }
            });
        }

        private IEnumerable<string> EnumerateSlideImagePaths()
        {
            return MovieSetting.PageInfos.Select(pageInfo =>
            {
                if (pageInfo.ImagePath.Value.IsEmpty())
                {
                    return null;
                }
                return pageInfo.ImagePath.Value.Path.Value;
            }).Where(x => x != null).Cast<string>();
        }

        private void MainWindowViewModel_SpeakProgress(object? sender, System.Speech.Synthesis.SpeakProgressEventArgs e)
        {
            if (NextSoundEffectCharacterPosition < 0 || string.IsNullOrEmpty(NextSoundEffectPath))
            {
                return;
            }

            if (e.CharacterPosition >= NextSoundEffectCharacterPosition)
            {
                PlaySoundEffect(NextSoundEffectPath, null);
                NextSoundEffectCharacterPosition = -1;
            }
        }

        public void UpdateHeight()
        {
            PlayWindowHeight.Value = PlayWindowWidth.Value / AspectRatio.Value;
        }
        public void UpdateWidth()
        {
            PlayWindowWidth.Value = AspectRatio.Value * PlayWindowHeight.Value;
        }

        public int NextSoundEffectCharacterPosition { get; set; } = -1;
        public string NextSoundEffectPath { get; set; } = string.Empty;

        private void recognizer_SpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
        {
            //WriteLogLine($"{e.Result.Text}");
        }

        private void UpdateCaptionWidth()
        {
            CaptionWidth.Value = PlayWindowWidth.Value - CaptionMarginLeft.Value * 2;
        }

        private void ResetFace()
        {
            if (_pronuunciationBitmaps.ContainsKey('n'))
            {
                MouthBitmap.Value = _pronuunciationBitmaps['n'];
            }
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Open];
            FaceOpacity.Value = 1.0;
        }

        public void SyncCaptionMargin()
        {
            this.CaptionMarginBottom.Value = MovieSetting.CaptionMarginBottom.Value;
            this.CaptionMarginLeft.Value = MovieSetting.CaptionMarginLeft.Value;
        }

        public void CancelPlaying()
        {
            if (_playTask is null || _cancellationTokenSource is null)
            {
                return;
            }

            IsPaused.Value = false;
            _cancellationTokenSource.Cancel();
        }

        private void PlayNarrationAudio(PathViewModel audioPath, float volume, CancellationToken linkedCt)
        {
            WaitResume();
            var actualPath = audioPath.ActualPath.Value;
            if (!File.Exists(actualPath))
            {
                WriteErrorLogLine($"ファイルが見つかりません。\"{actualPath}\"");
                return;
            }

            PlayAudio(actualPath, (float)volume, linkedCt);
        }

        private void PrepareToStartBgm(string bgmPath, float targetVolume, bool isFadeInEnabled = false)
        {
            if (File.Exists(bgmPath))
            {
                WriteLogLine($"BGMをボリューム{MovieSetting.BgmVolume.Value}で再生します。\"{bgmPath}\" isFadeInEnabled={isFadeInEnabled}");
                var reader = new AudioFileReader(bgmPath);
                _bgmFileReader = reader;
                if (isFadeInEnabled)
                {
                    _bgmFileReader.Volume = 0.0f;
                }
                else
                {
                    _bgmFileReader.Volume = targetVolume;
                }
                LoopStream loop = new(reader);
                _bgmOutputDevice.Init(loop);

                _bgmOutputDevice.Volume = 1.0f;

            }
            else
            {
                WriteErrorLogLine($"BGMファイルが存在しませんでした。\"{bgmPath}\"");
            }
        }
        private void StartPreparedBgm(string bgmPath, float targetVolume, bool isFadeInEnabled = false, int fadeMilliseconds = 1000)
        {
            _bgmOutputDevice.Play();

            if (isFadeInEnabled)
            {
                int intervalMilliseconds = fadeMilliseconds;
                int samples = 10;
                int sleepMilliseconds = intervalMilliseconds / samples;
                float increment = targetVolume / samples;
                for (int i = 0; i < samples; ++i)
                {
                    this.Sleep(sleepMilliseconds);

                    float nextVolume = _bgmFileReader!.Volume + increment;
                    _bgmFileReader.Volume = nextVolume > targetVolume ? targetVolume : nextVolume;
                }
            }
            _bgmFileReader!.Volume = targetVolume;
        }

        private void StartBgm(string bgmPath, float targetVolume, bool isFadeInEnabled = false, int fadeMilliseconds = 1000)
        {
            PrepareToStartBgm(bgmPath, targetVolume, isFadeInEnabled);
            StartPreparedBgm(bgmPath, targetVolume, isFadeInEnabled, fadeMilliseconds);
        }

        private void FadeOutCurrentBgm(int bgmFadeMiliseconds)
        {
            if (_bgmFileReader is null)
            {
                return;
            }

            if (_bgmOutputDevice.PlaybackState != PlaybackState.Stopped)
            {
                int fadeOutMilliseconds = bgmFadeMiliseconds;
                int fadeOutSamples = 10;
                int sleepMilliseconds = fadeOutMilliseconds / fadeOutSamples;
                float volumeDecrement = _bgmFileReader.Volume / fadeOutSamples;
                for (int i = 0; i < fadeOutSamples; ++i)
                {
                    Thread.Sleep(sleepMilliseconds);

                    float nextVolume = _bgmFileReader.Volume - volumeDecrement;
                    _bgmFileReader.Volume = nextVolume < 0.0f ? 0.0f : nextVolume;
                }
                _bgmFileReader.Volume = 0.0f;

                _bgmOutputDevice.Stop();
                _bgmFileReader.Volume = 1.0f;
            }
        }

        public void SyncBgmVolumeFromSetting()
        {
            if (_bgmFileReader is not null)
            {
                _bgmFileReader.Volume = (float)MovieSetting.BgmVolume.Value;
            }
        }

        private void WaitResume()
        {
            while (IsPaused.Value)
            {
                Thread.Sleep(100);
            }
        }

        public void SpeechText(NarrationInfoViewModel info, CancellationToken? linkedCt)
        {
            CurrentForegroundColor.Value = System.Windows.Media.Color.FromArgb(
                (byte)info.TextColorA.Value,
                (byte)info.TextColorR.Value,
                (byte)info.TextColorG.Value,
                (byte)info.TextColorB.Value);
            const string ApplauseMark = "[applause]";
            foreach (var text in info.EnumerateSpeechTextPerPeriod())
            {
                WaitResume();
                string inputText = text;
                CurrentPronunciation.Value = string.Empty;

                // SE再生の特殊記号解析
                {
                    int applauseIndex = text.IndexOf(ApplauseMark);
                    if (applauseIndex >= 0)
                    {
                        // SEの再生を予約
                        NextSoundEffectCharacterPosition = applauseIndex;
                        NextSoundEffectPath = ApplausePath.Value;
                        inputText = text.Replace(ApplauseMark, string.Empty);
                    }
                }

                // Description の更新
                {
                    string pattern = @"\[(\d+)\]";
                    var matches = Regex.Matches(inputText, pattern);
                    if (matches.Any())
                    {
                        var match = matches.First();
                        this.CurrentPageDescription.Value = info.Parent.GetDescription(match.Value);
                        inputText = TextUtility.RemoveMatchesString(inputText, matches);
                    }
                }

                {
                    IsCaptionVisible.Value = true && !HideCaption.Value;
                    CurrentText.Value = inputText;
                    var speechText = inputText.Replace(Environment.NewLine, "");
                    info.StartSpeech(speechText, linkedCt, MovieSetting.NarrationLineBreakInterval.Value, () =>
                    {
                        BlinkEyeRandom();
                    });
                    CurrentText.Value = string.Empty;

                    WaitResume();

                    IsCaptionVisible.Value = false;
                }
            }
        }

        public ReactiveProperty<bool> HideCaption { get; } = new(false);

        private void PlayAudio(string actualPath, float volume, CancellationToken linkedCt)
        {
            var audioFile = new AudioFileReader(actualPath)
            {
                Volume = volume
            };
            _outputDevice.Init(audioFile);

            var duration = SoundUtility.GetWavFileDuration(actualPath);

            _outputDevice.Play();

            // 再生完了を待つ
            Wait(duration, linkedCt);

            _outputDevice.Stop();
        }

        private void AddSoundEffectOutputDevice(string actualPath)
        {
            var audioFile = new AudioFileReader(actualPath);
            _soundEffectFileReaders.Add(
                actualPath, audioFile);
        }

        private void PlaySoundEffect(string path, CancellationToken? linkedCt)
        {
            if (!_soundEffectFileReaders.ContainsKey(path))
            {
                AddSoundEffectOutputDevice(path);
            }
            if (_soundEffectOutputDevice.PlaybackState != PlaybackState.Stopped)
            {
                _soundEffectOutputDevice.Stop();
            }

            var audioFile = _soundEffectFileReaders[path];
            audioFile.Position = 0;
            audioFile.Volume = 0.5f;
            _soundEffectOutputDevice.Init(audioFile);
            _soundEffectOutputDevice.Play();
        }

        private void AnimateFaceRotation()
        {
            var ms = CurrentAnimationTime.Value;
            var degreeUnit = ms * FaceRotateSpeed.Value / 360;
            FaceRotation.Value = Math.Sin(Math.PI * degreeUnit / 180) * 2;
        }

        private void BlinkEyeRandom()
        {
            var randValue = _randGenerator.Next(100);
            if (randValue > 85)
            {
                BlinkEye(100);
            }
            else if (randValue > 80)
            {
                BlinkEye(800);
            }
        }

        private void BlinkEye(int milliseconds)
        {
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Close];
            Thread.Sleep(milliseconds);
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Open];
        }

        private static bool IsCancelException(Exception exception)
        {
            if (IsCanceledExceptionType(exception))
            {
                return true;
            }
            if (exception is AggregateException aggregateException)
            {
                if (IsCanceledExceptionType(aggregateException.InnerExceptions.FirstOrDefault()))
                {
                    return true;
                }
                if (IsCanceledExceptionType(exception.InnerException))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsCanceledExceptionType(Exception? exception)
        {
            return exception is TaskCanceledException || exception is OperationCanceledException;
        }


        private static void Wait(TimeSpan duration, CancellationToken linkedCt)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.Elapsed < duration)
            {
                Thread.Sleep(10);
                linkedCt.ThrowIfCancellationRequested();
            }
            stopwatch.Stop();
        }

        public ReactiveProperty<string> Log { get; } = new ReactiveProperty<string>();

        public Window? View { get; set; } = null;

        private Views.PlayWindow? _playWindow;
        public Views.PlayWindow? PlayWindow
        {
            get => _playWindow;
            set
            {
                _playWindow = value;
                if (_playWindow is not null)
                {
                    _playWindow.DataContext = this;
                }
            }
        }

        private void UpdatePageNumberPositions()
        {
            UpdatePageNumberPosX();
            UpdatePageNumberPosY();
        }

        public void UpdatePageNumberPosX()
        {
            ActualPageNumberPosX.Value = MovieSetting.PageNumberPosX.Value * PlayWindowWidth.Value;
        }

        public void UpdatePageNumberPosY()
        {
            ActualPageNumberPosY.Value = MovieSetting.PageNumberPosY.Value * PlayWindowWidth.Value;
        }

        public ReactiveProperty<BitmapSource> BodyBitmap { get; } = new ReactiveProperty<BitmapSource>();
        public ReactiveProperty<BitmapSource> FaceBitmap { get; } = new ReactiveProperty<BitmapSource>();
        public ReactiveProperty<BitmapSource> EyeBitmap { get; } = new ReactiveProperty<BitmapSource>();
        public ReactiveProperty<BitmapSource> MouthBitmap { get; } = new ReactiveProperty<BitmapSource>();

        public DoublePropertyViewModel FaceRotation { get; } = new DoublePropertyViewModel("顔の回転");
        public DoublePropertyViewModel FaceRotateCenterX { get; } = new DoublePropertyViewModel("顔の回転中心X");
        public DoublePropertyViewModel FaceRotateCenterY { get; } = new DoublePropertyViewModel("顔の回転中心Y");
        public DoublePropertyViewModel FaceRotateSpeed { get; } = new DoublePropertyViewModel("顔の回転速度");
        public BoolPropertyViewModel ShowTimeCode { get; } = new BoolPropertyViewModel("タイムコード表示");

        public ReactiveProperty<double> FaceOpacity { get; } = new ReactiveProperty<double>(1.0);

        public ReactiveProperty<string> CurrentPronunciation { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<bool> IsCaptionVisible { get; } = new ReactiveProperty<bool>();

        public ReactiveProperty<double> CaptionMarginLeft { get; } = new ReactiveProperty<double>(60);
        public ReactiveProperty<double> CaptionMarginBottom { get; } = new ReactiveProperty<double>(60);


        public ReactiveProperty<double> CaptionWidth { get; } = new ReactiveProperty<double>();
        public ReactiveProperty<string> CurrentText { get; } = new ReactiveProperty<string>(string.Empty);

        public ReactiveProperty<System.Windows.Media.Color> CurrentForegroundColor { get; } = new ReactiveProperty<System.Windows.Media.Color>();

        public ReactiveProperty<bool> ActualPageNumberVisibility { get; } = new ReactiveProperty<bool>();

        public ReactiveProperty<double> ActualPageNumberPosX { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<double> ActualPageNumberPosY { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<int> CurrentPageNumber { get; } = new ReactiveProperty<int>();


        public Duration MediaDucration { get; set; } = new Duration();

        public bool IsMediaLoaded { get; set; } = false;

        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>("解説動画メーカー");

        public ReactiveProperty<string> CurrentPageTitle { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> CurrentPageDescription { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<VerticalAlignment> CurrentPageTitleVerticalAlignment { get; } = new();
        public ReactiveProperty<HorizontalAlignment> CurrentPageTitleHorizontalAlignment { get; } = new();
        public ReactiveProperty<TextAlignment> CurrentPageTitleTextAlignment { get; } = new();
        public ReactiveProperty<double> CurrentPageTitleVerticalOffset { get; } = new();
        public ReactiveProperty<double> CurrentPageTitleHorizontalOffset { get; } = new();

        public ReactiveProperty<bool> FitsWindowSizeToBackgroundImage { get; } = new(true);
        public ReactiveProperty<bool> IsPlaying { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsPaused { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> PlayButtonLabel { get; } = new ReactiveProperty<string>("再生開始");
        public ReactiveProperty<string> PauseButtonLabel { get; } = new ReactiveProperty<string>("一時停止");

        public ReactiveCommand OpenPlayWindowCommand { get; } = new ReactiveCommand();
        public ReactiveCommand PlayCommand { get; } = new ReactiveCommand();
        public ReactiveCommand CancelPlayingCommand { get; } = new ReactiveCommand();
        public ReactiveCommand PauseCommand { get; } = new ReactiveCommand();

        public ReactiveCommand GoToNextPageCommand { get; } = new ReactiveCommand();
        public ReactiveCommand GoBackToPreviousPageCommand { get; } = new ReactiveCommand();

        public ReactiveCommand OpenSettingCommand { get; } = new ReactiveCommand();
        public ReactiveCommand SaveSettingCommand { get; } = new ReactiveCommand();
        public ReactiveCommand OpenSettingFolderCommand { get; } = new ReactiveCommand();
        public ReactiveCommand CreateNewSettingCommand { get; } = new ReactiveCommand();

        public ReactiveCommand ExportNarrationCommand { get; } = new();

        public ReactiveCommand SaveSlideCacheCommand { get; } = new ReactiveCommand();
        public ReactiveCommand RelocateNarrationInfoCommand { get; } = new ReactiveCommand();

        public ReactiveProperty<double> PreviewWindowWidth { get; } = new ReactiveProperty<double>(320);
        public ReactiveProperty<double> PreviewWindowHeight { get; } = new ReactiveProperty<double>(240);
        public ReactiveProperty<double> PlayWindowWidth { get; } = new ReactiveProperty<double>(640);
        public ReactiveProperty<double> PlayWindowHeight { get; } = new ReactiveProperty<double>(480);

        public ReactiveProperty<double> AspectRatio { get; } = new ReactiveProperty<double>(640 / 480);

        public ReactiveProperty<int> PageCount { get; } = new ReactiveProperty<int>();

        /// <summary>
        /// ダブルバッファリング用
        /// </summary>
        public MediaElementViewModel MediaElement1 { get; } = new MediaElementViewModel();

        /// <summary>
        /// ダブルバッファリング用
        /// </summary>
        public MediaElementViewModel MediaElement2 { get; } = new MediaElementViewModel();

        public ObservableCollection<PathViewModel> SlideSubImages { get; } = new ObservableCollection<PathViewModel>();
        public ReactiveProperty<int> SlideSubImageCount { get; } = new();
        public ReactiveProperty<double> SlideSubImageMaxHeight { get; } = new ReactiveProperty<double>(500);
        public ReactiveProperty<double> SlideSubImageMargin { get; } = new ReactiveProperty<double>(30);

        public ReactiveProperty<double> BlackOpacity { get; } = new ReactiveProperty<double>(0.0);

        public ReactiveProperty<BitmapSource> ImageSource { get; } = new ReactiveProperty<BitmapSource>();

        public PathViewModel SettingPath { get; } = new PathViewModel();

        public MovieSettingViewModel MovieSetting { get; }

        public PathPropertyViewModel BouyomiChanPath { get; } = new PathPropertyViewModel("棒読みちゃんパス");
        public PathPropertyViewModel BouyomiChanRemoteTalkPath { get; } = new PathPropertyViewModel("RemoteTalkパス");
        public StringPropertyViewModel ApplausePath { get; } = new StringPropertyViewModel("歓声パス(.wav)");

        public ReactiveProperty<double> ActualFaceWidth { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<double> WindowScale { get; } = new ReactiveProperty<double>();

        public ObservableCollection<IPropertyViewModel> Settings { get; } = new ObservableCollection<IPropertyViewModel>();

        public Stopwatch CurrentTime { get; } = new Stopwatch();
        public ReactiveProperty<string> CurrentTimeText { get; } = new ReactiveProperty<string>("00:00:00");
        public ReactiveProperty<double> CurrentAnimationTime { get; } = new ReactiveProperty<double>(0.0);

        public ReactiveProperty<PageInfoViewModel?> CurrentPage { get; } = new ReactiveProperty<PageInfoViewModel?>();

        public ReactiveProperty<double> ActualFaceImageWidth { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<double> TitleFontSize { get; } = new ReactiveProperty<double>(70.0);
        public ReactiveProperty<double> DescriptionFontSize { get; } = new ReactiveProperty<double>(50.0);

        public void WriteLogLine(string message)
        {
            
            Log.Value += $"[{DateTime.Now.ToShortTimeString() }]" + message + Environment.NewLine;
        }
        public void WriteErrorLogLine(string message)
        {
            Log.Value += "エラー: " + message + Environment.NewLine;
        }
        public void WriteErrorLogLine(string message, Exception exception)
        {
            StringBuilder errorMessage = new(message);
            var tmpException = exception;
            while (tmpException != null)
            {
                errorMessage.AppendLine(tmpException.Message);
                errorMessage.AppendLine(tmpException.StackTrace);
                tmpException = tmpException.InnerException;
            }

            WriteErrorLogLine(errorMessage.ToString());
        }

        private void SaveSettings(string path)
        {
            SaveSettings(MovieSetting.ToSerializable(), path);
        }

        public static string ApplicationSettingPath
        {
            get
            {
                var assembly = Assembly.GetEntryAssembly() ?? throw new Exception();
                var dirPath = Path.GetDirectoryName(assembly.Location) ?? throw new Exception();
                var path = Path.Combine(dirPath, "ApplicationSetting.json");
                return path;
            }
        }

        public void LoadApplicationSettings()
        {
            var path = ApplicationSettingPath;
            var deserialized = LoadSettings<ApplicationSetting>(path);
            if (deserialized is null)
            {
                return;
            }

            this.SettingPath.Path.Value = deserialized.MovieSettingPath;
            SoundUtility.AzureServiceRegion = deserialized.AzureServiceRegion;
            SoundUtility.AzureSubscriptionKey = deserialized.AzureSubscriptionKey;
            ApplausePath.Value = deserialized.AudioApplausePath;


            BouyomiChanPath.Value.Path.Value = deserialized.BouyomiChanPath;
            BouyomiChanRemoteTalkPath.Value.Path.Value = deserialized.BouyomiChanRemoteTalkPath;
        }

        public void SaveApplicationSettings()
        {
            var setting = new ApplicationSetting
            {
                MovieSettingPath = this.SettingPath.ActualPath.Value,
                BouyomiChanPath = BouyomiChanPath.Value.ActualPath.Value,
                BouyomiChanRemoteTalkPath = BouyomiChanRemoteTalkPath.Value.ActualPath.Value,
                AzureSubscriptionKey = SoundUtility.AzureSubscriptionKey,
                AzureServiceRegion = SoundUtility.AzureServiceRegion
            };
            var path = ApplicationSettingPath;
            SaveSettings(setting, path);
        }

        private static string? SaveSettings<T>(T target, string path)
        {
            var savePath = path;
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };
            var jsonText = JsonSerializer.Serialize(target, options);
            File.WriteAllText(savePath, jsonText);
            return savePath;
        }

        private void LoadSettings(string path)
        {
            var deserialized = LoadSettings<MovieSetting>(path);
            if (deserialized is null)
            {
                return;
            }

            MovieSetting.DeepCopyFrom(deserialized);
            SetupFace();
        }

        private void SetupFace()
        {
            string root = @"..\..\..\Resources\boy";
            UpdateBitmapSource(Path.Combine(root, @"mouth_boy1_a.png"), x => _pronuunciationBitmaps['a'] = x);
            UpdateBitmapSource(Path.Combine(root, @"mouth_boy1_i.png"), x => _pronuunciationBitmaps['i'] = x);
            UpdateBitmapSource(Path.Combine(root, @"mouth_boy1_u.png"), x => _pronuunciationBitmaps['u'] = x);
            UpdateBitmapSource(Path.Combine(root, @"mouth_boy1_e.png"), x => _pronuunciationBitmaps['e'] = x);
            UpdateBitmapSource(Path.Combine(root, @"mouth_boy1_o.png"), x => _pronuunciationBitmaps['o'] = x);
            UpdateBitmapSource(Path.Combine(root, @"mouth_boy1_n.png"), x => _pronuunciationBitmaps['n'] = x);
            _eyeBitmaps[EyePattern.Open] = null;
            _eyeBitmaps[EyePattern.Close] = null;

            UpdateBitmapSource(MovieSetting.ImageMouthAPath.Value, x => _pronuunciationBitmaps['a'] = x);
            UpdateBitmapSource(MovieSetting.ImageMouthIPath.Value, x => _pronuunciationBitmaps['i'] = x);
            UpdateBitmapSource(MovieSetting.ImageMouthUPath.Value, x => _pronuunciationBitmaps['u'] = x);
            UpdateBitmapSource(MovieSetting.ImageMouthEPath.Value, x => _pronuunciationBitmaps['e'] = x);
            UpdateBitmapSource(MovieSetting.ImageMouthOPath.Value, x => _pronuunciationBitmaps['o'] = x);
            UpdateBitmapSource(MovieSetting.ImageMouthNPath.Value, x => _pronuunciationBitmaps['n'] = x);

            UpdateBitmapSource(MovieSetting.ImageEyeOpenPath.Value, x => _eyeBitmaps[EyePattern.Open] = x);
            UpdateBitmapSource(MovieSetting.ImageEyeClosePath.Value, x => _eyeBitmaps[EyePattern.Close] = x);
            UpdateBitmapSource(MovieSetting.ImageBodyPath.Value, x => BodyBitmap.Value = x);
            UpdateBitmapSource(MovieSetting.ImageFaceBasePath.Value, x => FaceBitmap.Value = x);

            FaceRotateCenterX.Value = 120;
            FaceRotateCenterY.Value = 120;
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Open];
            if (_pronuunciationBitmaps.ContainsKey('n'))
            {
                MouthBitmap.Value = _pronuunciationBitmaps['n'];
            }
        }

        private void UpdateBitmapSource(PathViewModel pathViewModel, Action<BitmapSource> update)
        {
            if (pathViewModel.IsValidPath())
            {
                update(CreateBitmapSource(pathViewModel.ActualPath.Value));
            }
        }

        private void UpdateBitmapSource(string path, Action<BitmapSource> update)
        {
            if (File.Exists(path))
            {
                update(CreateBitmapSource(path));
            }
        }

        private static BitmapSource CreateBitmapSource(string path)
        {
            return ImageUtility.ConvertBitmapToBitmapSource(new System.Drawing.Bitmap(path));
        }

        private T? LoadSettings<T>(string path)
            where T : class
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var jsonText = File.ReadAllText(path);

            try
            {
                var deserialized = JsonSerializer.Deserialize<T>(jsonText);
                if (deserialized == null)
                {
                    WriteErrorLogLine($"\"{path}\" の読み込みに失敗しました。");
                    return null;
                }

                return deserialized;
            }
            catch (Exception exception)
            {
                WriteErrorLogLine($"\"{path}\" の読み込みに失敗しました。{exception.Message}\n{exception.StackTrace}");
                return null;
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static BitmapSource ConvertBitmapToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            IntPtr hbitmap = bitmap.GetHbitmap();
            var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            DeleteObject(hbitmap);
            return bitmapSource;
        }
    }
}
