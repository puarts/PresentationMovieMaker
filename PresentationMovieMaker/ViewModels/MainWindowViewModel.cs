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

namespace PresentationMovieMaker.ViewModels
{
    public enum EyePattern
    {
        Open,
        Close
    }

    public class MainWindowViewModel : CompositeDisposableBase, ILogger
    {
        private AudioFileReader? _bgmFileReader = null;
        private WaveOutEvent _bgmOutputDevice = new WaveOutEvent();
        private WaveOutEvent _soundEffectOutputDevice = new WaveOutEvent();
        private WaveOutEvent _outputDevice = new WaveOutEvent();
        private Dictionary<string, AudioFileReader> _soundEffectFileReaders = new();
        private int _pageIndex = 0;

        private CancellationTokenSource? _cancellationTokenSource;
        private CancellationTokenSource? _goToNexPageCancellationTokenSource;
        private CancellationTokenSource? _goBackToPreviousPageCancellationTokenSource;
        private Task? _playTask = null;
        private Task? _playPageTask = null;
        private Dictionary<char, BitmapSource> _pronuunciationBitmaps = new Dictionary<char, BitmapSource>();
        private Dictionary<EyePattern, BitmapSource> _eyeBitmaps = new Dictionary<EyePattern, BitmapSource>();
        private TextConverter _textConverter = new TextConverter();
        private Utilities.SpeechRecognizer _recognizer = new Utilities.SpeechRecognizer();
        Random _randGenerator = new Random();

        public MainWindowViewModel()
        {
            Settings.Add(BouyomiChanPath);
            Settings.Add(BouyomiChanRemoteTalkPath);
            Settings.Add(FaceRotation);
            Settings.Add(FaceRotateCenterX);
            Settings.Add(FaceRotateCenterY);
            Settings.Add(FaceRotateSpeed);

            MovieSetting.Value = new MovieSettingViewModel(this);

            //SoundUtility.GetSynthesizer().PhonemeReached += (object? sender, System.Speech.Synthesis.PhonemeReachedEventArgs e) =>
            //{
            //    CurrentPronunciation.Value += e.Phoneme + " ";
            //};

            SoundUtility.GetSynthesizer().VisemeReached += (object? sender, System.Speech.Synthesis.VisemeReachedEventArgs e) =>
            {
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
                    // 最初のページの画像サイズでウインドウサイズを設定
                    {
                        var firstPage = MovieSetting.Value.PageInfos.FirstOrDefault();
                        if (firstPage is not null)
                        {
                            var path = firstPage.ImagePath.Value.ActualPath.Value;
                            if (IsImageFile(path) && File.Exists(path))
                            {
                                var size = ImageUtility.GetImageSize(path);
                                AspectRatio.Value = size.Width / (double)size.Height;
                                PlayWindowWidth.Value = size.Width;
                            }
                        }
                    }
                    PlayWindow = new Views.PlayWindow();
                    PlayWindow.Owner = View;
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

                    WriteLogLine("再生開始");
                    CurrentTime.Stop();
                    CurrentTime.Start();
                    CurrentAnimationTime.Value = 0.0;
                    IsPlaying.Value = true;
                    _cancellationTokenSource = new CancellationTokenSource();
                    CancellationToken ct = _cancellationTokenSource.Token;
                    _playTask = Task.Run(() =>
                    {
                        ActualPageNumberVisibility.Value = MovieSetting.Value.ShowPageNumber.Value;
                        try
                        {
                            PlaySlideshow(ct);
                        }
                        catch (OperationCanceledException e)
                        {
                            WriteLogLine($"{nameof(OperationCanceledException)} thrown with message: {e.Message}");
                        }
                        finally
                        {
                            SoundUtility.CancelSpeakingAll();
                            if (_bgmOutputDevice.PlaybackState != PlaybackState.Stopped)
                            {
                                _bgmOutputDevice.Stop();
                            }

                            if (_soundEffectOutputDevice.PlaybackState != PlaybackState.Stopped)
                            {
                                _soundEffectOutputDevice.Stop();
                            }

                            View?.Dispatcher.Invoke(() =>
                            {
                                //PlayWindow?.Hide();
                                //PlayWindow?.Close();
                            });

                            _cancellationTokenSource.Dispose();
                            _cancellationTokenSource = null;
                            MediaElement1.ImagePath.Value = string.Empty;
                            MediaElement2.ImagePath.Value = string.Empty;
                            IsPlaying.Value = false;
                            ActualPageNumberVisibility.Value = false;
                            CurrentText.Value = string.Empty;
                            IsCaptionVisible.Value = false;
                            StartPageIndex = 0;
                            CurrentTime.Stop();
                        }
                    }, ct);
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
                var openDir = Path.GetDirectoryName(SettingPath.Value);
                if (!Directory.Exists(openDir))
                {
                    return;
                }

                Process.Start("explorer", openDir);
            });

            Subscribe(CreateNewSettingCommand, () =>
            {
                MovieSetting.Value = new MovieSettingViewModel(this);
            });

            OpenSettingCommand.Subscribe(() =>
            {
                var dialog = new OpenFileDialog();
                if (!string.IsNullOrEmpty(SettingPath.Value))
                {
                    var dir = Path.GetDirectoryName(SettingPath.Value);
                    if (Directory.Exists(dir))
                    {
                        dialog.InitialDirectory = dir;
                    }
                }

                dialog.Filter = "設定ファイル (*.json)|*.json|全てのファイル (*.*)|*.*";
                if (dialog.ShowDialog() == true)
                {
                    SettingPath.Value = dialog.FileName;
                }
            }).AddTo(Disposable);

            SaveSettingCommand.Subscribe(() =>
            {
                WriteLogLine($"設定を保存しました。\n{SettingPath.Value}");
                SaveSettings(SettingPath.Value);
            }).AddTo(Disposable);

            const string cacheDirName = "SlideCache";
            const float cacheImageSizeMultiply = 1.0f / 8.0f;
            Subscribe(SaveSlideCacheCommand, () =>
            {
                var dir = Path.GetDirectoryName(SettingPath.Value);
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
                var dir = Path.GetDirectoryName(SettingPath.Value);
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

                    var copiedPages = MovieSetting.Value.PageInfos.Select(x => x.ToSerializable()).ToDictionary(x => Path.GetFileNameWithoutExtension(x.ImagePath));
                    foreach (var pageInfo in MovieSetting.Value.PageInfos)
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
                        pageInfo.DeepCopyFrom(serial);
                        pageInfo.ImagePath.Value.Path.Value = origPath;
                    }
                });
            });

            SettingPath.Subscribe(path =>
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


            Subscribe(MovieSetting, value =>
            {
                value.Parent = this;
                UpdatePageNumberPositions();
                SyncCaptionMargin();
            });

            Subscribe(PlayWindowWidth, value =>
            {
                UpdatePageNumberPosX();
                UpdateCaptionWidth();
            });
            Subscribe(PlayWindowHeight, value =>
            {
                UpdatePageNumberPosY();
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
        }

        private IEnumerable<string> EnumerateSlideImagePaths()
        {
            return MovieSetting.Value.PageInfos.Select(pageInfo =>
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
            MouthBitmap.Value = _pronuunciationBitmaps['n'];
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Open];
            FaceOpacity.Value = 1.0;
        }

        public void SyncCaptionMargin()
        {
            this.CaptionMarginBottom.Value = MovieSetting.Value.CaptionMarginBottom.Value;
            this.CaptionMarginLeft.Value = MovieSetting.Value.CaptionMarginLeft.Value;
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

        private static bool IsImageFile(string path)
        {
            switch (Path.GetExtension(path))
            {
                case ".png":
                case ".jpg":
                case ".bmp":
                    return true;
                default:
                    return false;
            }
        }

        public int StartPageIndex { get; set; } = 0;

        private void PlaySlideshow(CancellationToken ct)
        {
            ResetFace();

            MediaElement1.Opacity.Value = 1.0;
            MediaElement2.Opacity.Value = 1.0;
            BlackOpacity.Value = 0.0;

            var imagePaths = new ReactiveProperty<string>[]
            {
                MediaElement1.ImagePath,
                MediaElement2.ImagePath,
            };
            var rotateAngles = new ReactiveProperty<double>[]
            {
                MediaElement1.RotationAngle,
                MediaElement2.RotationAngle,
            };
            var bufferVisibilities = new ReactiveProperty<bool>[]
            {
                MediaElement1.Visiibility,
                MediaElement2.Visiibility,
            };

            _pageIndex = StartPageIndex;

            CurrentPageNumber.Value = 1;
            {
                var pageInfo = MovieSetting.Value.PageInfos[_pageIndex];
                if (pageInfo != null && IsImageFile(pageInfo.ImagePath.Value.ActualPath.Value))
                {
                    var currentBufferIndex = _pageIndex % 2;
                    var nextBufferIndex = currentBufferIndex == 0 ? 1 : 0;
                    if (imagePaths[currentBufferIndex].Value != pageInfo.ImagePath.Value.ActualPath.Value)
                    {
                        this.IsMediaLoaded = false;
                        WriteLogLine($"Start load MediaElement: bufferIndex = {currentBufferIndex}");
                        imagePaths[currentBufferIndex].Value = pageInfo.ImagePath.Value.ActualPath.Value;
                        View?.Dispatcher.Invoke(() =>
                        {
                            // manualだとPauseしないとずっと読み込まれない
                            PlayWindow?.PlayMediaElement(currentBufferIndex);
                        });
                        //while (!this.IsMediaLoaded)
                        //{
                        //    Thread.Sleep(30);
                        //}
                    }

                    bufferVisibilities[1].Value = currentBufferIndex == 1;
                    bufferVisibilities[0].Value = true;
                    if (StartPageIndex == 0)
                    {
                        Thread.Sleep(5000);
                    }


                }
            }

            // BGM の開始
            StartBgm(MovieSetting.Value.BgmPath.Value, (float)MovieSetting.Value.BgmVolume.Value);

            Task.Run(() =>
            {
                while (IsPlaying.Value)
                {
                    var timeOffset = (int)((1000 / 30) * _randGenerator.NextDouble());
                    Thread.Sleep(timeOffset);
                    CurrentAnimationTime.Value += timeOffset;
                    AnimateFaceRotation();
                }
            });

            Thread.Sleep(500);

            var pageCount = MovieSetting.Value.PageInfos.Count;
            for (; _pageIndex < pageCount; ++_pageIndex)
            {
                WaitResume();
                CurrentPageNumber.Value = _pageIndex + 1;
                var currentBufferIndex = _pageIndex % 2;
                var nextBufferIndex = currentBufferIndex == 0 ? 1 : 0;
                var pageInfo = MovieSetting.Value.PageInfos[_pageIndex];
                var nextPageInfo = _pageIndex + 1 < pageCount ? MovieSetting.Value.PageInfos[_pageIndex + 1] : null;

                using (_goToNexPageCancellationTokenSource = new CancellationTokenSource())
                using (_goBackToPreviousPageCancellationTokenSource = new CancellationTokenSource())
                {
                    var goNextCt = _goToNexPageCancellationTokenSource.Token;
                    var goBackCt = _goBackToPreviousPageCancellationTokenSource.Token;
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, goNextCt, goBackCt))
                    {
                        var linkedCt = linkedCts.Token;

                        Task.Run(() =>
                        {
                            if (pageInfo.BgmPath is not null && !(pageInfo.BgmPath.Value.IsEmpty()))
                            {
                                FadeOutCurrentBgm(pageInfo.BgmFadeMiliseconds.Value);
                                float volume = pageInfo.OverwritesBgmVolume.Value ? (float)pageInfo.BgmVolume.Value : (float)MovieSetting.Value.BgmVolume.Value;
                                StartBgm(pageInfo.BgmPath.Value.ActualPath.Value, volume, true, pageInfo.BgmFadeMiliseconds.Value);
                            }
                            else if (pageInfo.OverwritesBgmVolume.Value && _bgmFileReader is not null)
                            {
                                // 現在流れているBGMの音量を変更する
                                if (pageInfo.BgmVolume.Value == 0.0f)
                                {
                                    _bgmOutputDevice.Stop();
                                }
                                else
                                {
                                    if (_bgmOutputDevice.PlaybackState != PlaybackState.Playing)
                                    {
                                        _bgmOutputDevice.Play();
                                    }
                                    _bgmFileReader.Volume = (float)pageInfo.BgmVolume.Value;
                                }
                            }
                        }, linkedCt);


                        _playPageTask = Task.Run(() =>
                        {
                            try
                            {
                                //while (!this.IsMediaLoaded)
                                //{
                                //    Thread.Sleep(30);
                                //}

                                if (MediaDucration.HasTimeSpan)
                                {
                                    // 動画の時はサイズが違って後ろが見えちゃうので、後ろのバッファも消す必要があるので仕方なく消す
                                    bufferVisibilities[1].Value = currentBufferIndex == 1;
                                    bufferVisibilities[0].Value = currentBufferIndex == 0;
                                }
                                else
                                {
                                    // 後ろのバッファを消さない事で、ガビガビしなくなる
                                    bufferVisibilities[1].Value = currentBufferIndex == 1;
                                    bufferVisibilities[0].Value = true;
                                }
                                //bufferVisibilities[nextBufferIndex].Value = false;
                                //bufferVisibilities[currentBufferIndex].Value = true;

                                //if (currentBufferIndex == 0)
                                //{
                                //bufferVisibilities[currentBufferIndex].Value = true;
                                //bufferVisibilities[nextBufferIndex].Value = false;
                                //}
                                //else
                                //{
                                //}
                                //Thread.Sleep(100);
                                //PlayWindow?.ResetVisibleChangedFlag();
                                //if (!bufferVisibilities[currentBufferIndex].Value)
                                //{
                                //    bufferVisibilities[currentBufferIndex].Value = true;
                                //    PlayWindow?.WaitVisibleChanged();
                                //}

                                //PlayWindow?.ResetVisibleChangedFlag();
                                //if (bufferVisibilities[nextBufferIndex].Value)
                                //{
                                //    bufferVisibilities[nextBufferIndex].Value = false;
                                //    PlayWindow?.WaitVisibleChanged();
                                //}

                                if (nextPageInfo is not null)
                                {
                                    rotateAngles[nextBufferIndex].Value = nextPageInfo.RotationAngle.Value;
                                    if (imagePaths[nextBufferIndex].Value != nextPageInfo.ImagePath.Value.ActualPath.Value)
                                    {
                                        WriteLogLine($"Start load MediaElement: bufferIndex = {nextBufferIndex}");
                                        this.IsMediaLoaded = false;
                                        imagePaths[nextBufferIndex].Value = nextPageInfo.ImagePath.Value.ActualPath.Value;
                                        //while (!this.IsMediaLoaded) ;
                                        View?.Dispatcher.Invoke(() =>
                                        {
                                            PlayWindow?.PlayMediaElement(nextBufferIndex);
                                        });
                                    }
                                }

                                // ロード後にちょっとスリープを入れないとガビガビしてしまう
                                //Thread.Sleep(100);
                                //bufferVisibilities[nextBufferIndex].Value = false;

                                //Thread.Sleep(pageInfo.PagingIntervalMilliseconds.Value);

                                linkedCt.ThrowIfCancellationRequested();

                                var stopwatch = new Stopwatch();
                                stopwatch.Start();
                                TimeSpan mediaDuration = new TimeSpan(0);
                                if (MediaDucration.HasTimeSpan)
                                {
                                    mediaDuration = MediaDucration.TimeSpan;
                                    View?.Dispatcher.Invoke(() =>
                                    {
                                        PlayWindow?.SetMediaVolume(pageInfo.MediaVolume.Value, currentBufferIndex);
                                        PlayWindow?.PlayMediaElement(currentBufferIndex);
                                    });
                                }

                                View?.Dispatcher.Invoke(() =>
                                {
                                    // 動画再生
                                    PlayWindow?.PlayMediaElement(currentBufferIndex);
                                });

                                // ページ切り替え時の音が設定されていない場合は、デフォルトのSEを再生
                                if (_pageIndex != 0
                                && (!pageInfo.NarrationInfos.Any() || pageInfo.NarrationInfos.First().AudioPaths.Count == 0))
                                {
                                    var audioPath = MovieSetting.Value.DefaultPageTurningAudioPath.Value;
                                    if (!audioPath.IsEmpty())
                                    {
                                        PlayNarrationAudio(audioPath, 1.0f, linkedCt);
                                    }
                                }

                                foreach (var info in pageInfo.NarrationInfos)
                                {
                                    void PlayAllNarrationAudio(NarrationInfoViewModel info)
                                    {
                                        if (info.AudioPaths.Count > 0)
                                        {
                                            foreach (var audioPath in info.AudioPaths)
                                            {
                                                PlayNarrationAudio(audioPath, (float)info.AudioVolume.Value, linkedCt);
                                            }
                                        }
                                    }

                                    this.CaptionMarginBottom.Value = info.CaptionMarginBottom.Value;

                                    // とりあえず先にオーディオ再生
                                    if (info.IsAudioParallel.Value)
                                    {
                                        Task.Run(() => PlayAllNarrationAudio(info), linkedCt);
                                    }
                                    else
                                    {
                                        PlayAllNarrationAudio(info);
                                    }


                                    // 合成音声再生
                                    {
                                        WaitResume();
                                        ThreadUtility.Wait(info.PreBlankMilliseconds.Value, ct, 100, () =>
                                        {
                                            BlinkEyeRandom();
                                        });

                                        SpeechText(info, linkedCt);

                                        ResetFace();
                                        //ThreadUtility.Wait(info.PostBlankMilliseconds.Value, ct, 100, () =>
                                        //{
                                        //    BlinkEyeRandom();
                                        //});
                                    }
                                }

                                // 動画の場合は再生完了を待つ
                                while (stopwatch.Elapsed < mediaDuration)
                                {
                                    Thread.Sleep(100);
                                    linkedCt.ThrowIfCancellationRequested();
                                }
                                stopwatch.Stop();

                                linkedCt.ThrowIfCancellationRequested();

                                Thread.Sleep(pageInfo.PagingIntervalMilliseconds.Value);
                            }
                            catch (Exception exception)
                            {
                                SoundUtility.CancelSpeakingAll();
                                if (IsCancelException(exception))
                                {
                                    if (goBackCt.IsCancellationRequested)
                                    {
                                        if (_pageIndex > 0)
                                        {
                                            _pageIndex = _pageIndex - 2;
                                        }
                                    }
                                }
                                else
                                {
                                    throw;
                                }
                            }
                            finally
                            {
                                _outputDevice?.Stop();
                            }
                        }, linkedCt);

                        try
                        {
                            _playPageTask.Wait();
                        }
                        catch (Exception exception)
                        {
                            SoundUtility.CancelSpeakingAll();
                            if (!IsCancelException(exception))
                            {
                                throw;
                            }
                        }
                        _playPageTask = null;

                        ct.ThrowIfCancellationRequested();
                    }
                }

                _goToNexPageCancellationTokenSource = null;
                _goBackToPreviousPageCancellationTokenSource = null;
            }

            // BGM のフェードアウト
            var task = Task.Run(() =>
            {
                FadeOutCurrentBgm(MovieSetting.Value.BgmFadeOutMilliseconds.Value);
            });

            // 終了後の余白
            {
                // フェードアウト
                for (int i = 100; i >= 0; --i)
                {
                    double opacity = i / 100.0;
                    BlackOpacity.Value = 1.0 - opacity;
                    //MediaElement1.Opacity.Value = opacity;
                    //MediaElement2.Opacity.Value = opacity;
                    //FaceOpacity.Value = opacity;
                    Thread.Sleep(30);
                }
            }

            task.Wait();
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

        private void StartBgm(string bgmPath, float targetVolume, bool isFadeInEnabled = false, int fadeMilliseconds = 1000)
        {
            if (File.Exists(bgmPath))
            {
                WriteLogLine($"BGMをボリューム{MovieSetting.Value.BgmVolume.Value}で再生します。\"{bgmPath}\"");
                var reader = new AudioFileReader(bgmPath);
                _bgmFileReader = reader;
                _bgmFileReader.Volume = 0.0f;
                LoopStream loop = new LoopStream(reader);
                _bgmOutputDevice.Init(loop);

                _bgmOutputDevice.Volume = 1.0f;
                if (!isFadeInEnabled)
                {
                    SyncBgmVolumeFromSetting();
                }

                _bgmOutputDevice.Play();

                if (isFadeInEnabled)
                {
                    int intervalMilliseconds = fadeMilliseconds;
                    int samples = 10;
                    int sleepMilliseconds = intervalMilliseconds / samples;
                    float increment = targetVolume / samples;
                    for (int i = 0; i < samples; ++i)
                    {
                        Thread.Sleep(sleepMilliseconds);

                        float nextVolume = _bgmFileReader.Volume + increment;
                        _bgmFileReader.Volume = nextVolume > targetVolume ? targetVolume : nextVolume;
                    }
                }
                _bgmFileReader.Volume = targetVolume;
            }
            else
            {
                WriteErrorLogLine($"BGMファイルが存在しませんでした。\"{bgmPath}\"");
            }
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
            }
        }

        public void SyncBgmVolumeFromSetting()
        {
            _bgmOutputDevice.Volume = (float)MovieSetting.Value.BgmVolume.Value;
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
                int applauseIndex = text.IndexOf(ApplauseMark);
                if (applauseIndex >= 0)
                {
                    // SEの再生を予約
                    NextSoundEffectCharacterPosition = applauseIndex;
                    NextSoundEffectPath = ApplausePath.Value;
                    inputText = text.Replace(ApplauseMark, string.Empty);
                }

                {
                    IsCaptionVisible.Value = true;
                    CurrentText.Value = inputText;
                    var speechText = inputText.Replace(Environment.NewLine, "");
                    info.StartSpeech(speechText, linkedCt, MovieSetting.Value.NarrationLineBreakInterval.Value, () =>
                    {
                        BlinkEyeRandom();
                    });
                    CurrentText.Value = string.Empty;

                    WaitResume();

                    IsCaptionVisible.Value = false;
                }
            }
        }

        private void PlayAudio(string actualPath, float volume, CancellationToken linkedCt)
        {
            var audioFile = new AudioFileReader(actualPath);
            audioFile.Volume = volume;
            _outputDevice.Init(audioFile);
            _outputDevice.Volume = 1.0f;

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
                BlinkEye(1000);
            }
        }

        private void BlinkEye(int milliseconds)
        {
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Close];
            Thread.Sleep(milliseconds);
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Open];
        }

        private bool IsCancelException(Exception exception)
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

        private bool IsCanceledExceptionType(Exception? exception)
        {
            return exception is TaskCanceledException || exception is OperationCanceledException;
        }


        private void Wait(TimeSpan duration, CancellationToken linkedCt)
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
            ActualPageNumberPosX.Value = MovieSetting.Value.PageNumberPosX.Value * PlayWindowWidth.Value;
        }

        public void UpdatePageNumberPosY()
        {
            ActualPageNumberPosY.Value = MovieSetting.Value.PageNumberPosY.Value * PlayWindowWidth.Value;
        }
        public ReactiveProperty<BitmapSource> BodyBitmap { get; } = new ReactiveProperty<BitmapSource>();
        public ReactiveProperty<BitmapSource> FaceBitmap { get; } = new ReactiveProperty<BitmapSource>();
        public ReactiveProperty<BitmapSource> EyeBitmap { get; } = new ReactiveProperty<BitmapSource>();
        public ReactiveProperty<BitmapSource> MouthBitmap { get; } = new ReactiveProperty<BitmapSource>();

        public DoublePropertyViewModel FaceRotation { get; } = new DoublePropertyViewModel("顔の回転");
        public DoublePropertyViewModel FaceRotateCenterX { get; } = new DoublePropertyViewModel("顔の回転中心X");
        public DoublePropertyViewModel FaceRotateCenterY { get; } = new DoublePropertyViewModel("顔の回転中心Y");
        public DoublePropertyViewModel FaceRotateSpeed { get; } = new DoublePropertyViewModel("顔の回転速度");

        public ReactiveProperty<double> FaceOpacity { get; } = new ReactiveProperty<double>(1.0);

        public ReactiveProperty<string> CurrentPronunciation { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<bool> IsCaptionVisible { get; } = new ReactiveProperty<bool>();

        public ReactiveProperty<double> CaptionMarginLeft { get; } = new ReactiveProperty<double>(60);
        public ReactiveProperty<double> CaptionMarginBottom { get; } = new ReactiveProperty<double>(60);

        public ReactiveProperty<double> CaptionWidth { get; } = new ReactiveProperty<double>();
        public ReactiveProperty<string> CurrentText { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<System.Windows.Media.Color> CurrentForegroundColor { get; } = new ReactiveProperty<System.Windows.Media.Color>();

        public ReactiveProperty<bool> ActualPageNumberVisibility { get; } = new ReactiveProperty<bool>();

        public ReactiveProperty<double> ActualPageNumberPosX { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<double> ActualPageNumberPosY { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<int> CurrentPageNumber { get; } = new ReactiveProperty<int>();


        public Duration MediaDucration { get; set; } = new Duration();

        public bool IsMediaLoaded { get; set; } = false;

        public ReactiveProperty<string> Title { get; } = new ReactiveProperty<string>("解説動画メーカー");

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

        public ReactiveCommand SaveSlideCacheCommand { get; } = new ReactiveCommand();
        public ReactiveCommand RelocateNarrationInfoCommand { get; } = new ReactiveCommand();

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

        public ReactiveProperty<double> BlackOpacity { get; } = new ReactiveProperty<double>(0.0);

        public ReactiveProperty<BitmapSource> ImageSource { get; } = new ReactiveProperty<BitmapSource>();

        public ReactiveProperty<string> SettingPath { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<MovieSettingViewModel> MovieSetting { get; } = new ReactiveProperty<MovieSettingViewModel>();

        public PathPropertyViewModel BouyomiChanPath { get; } = new PathPropertyViewModel("棒読みちゃんパス");
        public PathPropertyViewModel BouyomiChanRemoteTalkPath { get; } = new PathPropertyViewModel("RemoteTalkパス");
        public StringPropertyViewModel ApplausePath { get; } = new StringPropertyViewModel("歓声パス(.wav)");

        public ObservableCollection<IPropertyViewModel> Settings { get; } = new ObservableCollection<IPropertyViewModel>();

        public Stopwatch CurrentTime { get; } = new Stopwatch();
        public ReactiveProperty<double> CurrentAnimationTime { get; } = new ReactiveProperty<double>(0.0);

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
            StringBuilder errorMessage = new StringBuilder(message);
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
            SaveSettings(MovieSetting.Value.ToSerializable(), path);
        }

        private string GetApplicationSettingPath()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly is null)
            {
                throw new Exception();
            }

            var dirPath = Path.GetDirectoryName(assembly.Location);
            if (dirPath is null)
            {
                throw new Exception();
            }

            var path = Path.Combine(dirPath, "ApplicationSetting.json");
            return path;
        }

        public void LoadApplicationSettings()
        {
            var path = GetApplicationSettingPath();
            var deserialized = LoadSettings<ApplicationSetting>(path);
            if (deserialized is null)
            {
                return;
            }

            this.SettingPath.Value = deserialized.MovieSettingPath;
            SoundUtility.AzureServiceRegion = deserialized.AzureServiceRegion;
            SoundUtility.AzureSubscriptionKey = deserialized.AzureSubscriptionKey;
            ApplausePath.Value = deserialized.AudioApplausePath;


            BouyomiChanPath.Value.Path.Value = deserialized.BouyomiChanPath;
            BouyomiChanRemoteTalkPath.Value.Path.Value = deserialized.BouyomiChanRemoteTalkPath;
        }

        public void SaveApplicationSettings()
        {
            var setting = new ApplicationSetting();
            setting.MovieSettingPath = this.SettingPath.Value;
            setting.BouyomiChanPath = BouyomiChanPath.Value.ActualPath.Value;
            setting.BouyomiChanRemoteTalkPath = BouyomiChanRemoteTalkPath.Value.ActualPath.Value;
            setting.AzureSubscriptionKey = SoundUtility.AzureSubscriptionKey;
            setting.AzureServiceRegion = SoundUtility.AzureServiceRegion;
            var path = GetApplicationSettingPath();
            SaveSettings(setting, path);
        }

        private void SaveSettings<T>(T target, string path)
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };
            var jsonText = JsonSerializer.Serialize(target, options);
            File.WriteAllText(path, jsonText);
        }

        private void LoadSettings(string path)
        {
            var deserialized = LoadSettings<MovieSetting>(path);
            if (deserialized is null)
            {
                return;
            }

            MovieSetting.Value = new MovieSettingViewModel(deserialized, this);
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

            UpdateBitmapSource(MovieSetting.Value.ImageMouthAPath.Value, x => _pronuunciationBitmaps['a'] = x);
            UpdateBitmapSource(MovieSetting.Value.ImageMouthIPath.Value, x => _pronuunciationBitmaps['i'] = x);
            UpdateBitmapSource(MovieSetting.Value.ImageMouthUPath.Value, x => _pronuunciationBitmaps['u'] = x);
            UpdateBitmapSource(MovieSetting.Value.ImageMouthEPath.Value, x => _pronuunciationBitmaps['e'] = x);
            UpdateBitmapSource(MovieSetting.Value.ImageMouthOPath.Value, x => _pronuunciationBitmaps['o'] = x);
            UpdateBitmapSource(MovieSetting.Value.ImageMouthNPath.Value, x => _pronuunciationBitmaps['n'] = x);

            UpdateBitmapSource(MovieSetting.Value.ImageEyeOpenPath.Value, x => _eyeBitmaps[EyePattern.Open] = x);
            UpdateBitmapSource(MovieSetting.Value.ImageEyeClosePath.Value, x => _eyeBitmaps[EyePattern.Close] = x);
            UpdateBitmapSource(MovieSetting.Value.ImageBodyPath.Value, x => BodyBitmap.Value = x);
            UpdateBitmapSource(MovieSetting.Value.ImageFaceBasePath.Value, x => FaceBitmap.Value = x);

            FaceRotateCenterX.Value = 120;
            FaceRotateCenterY.Value = 120;
            EyeBitmap.Value = _eyeBitmaps[EyePattern.Open];
            MouthBitmap.Value = _pronuunciationBitmaps['n'];
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

        private BitmapSource CreateBitmapSource(string path)
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
