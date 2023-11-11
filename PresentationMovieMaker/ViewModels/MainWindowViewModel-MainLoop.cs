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

namespace PresentationMovieMaker.ViewModels
{
    public partial class MainWindowViewModel
    {
        public void PlaySlideshow(int startPageIndex = 0)
        {
            if (IsPlaying.Value)
            {
                CancelPlaying();
                _playTask?.Wait();
            }

            WriteLogLine("再生開始");
            CurrentTime.Start();
            var timerCancellationTokenSource = new CancellationTokenSource();
            var timerCancellationToken = timerCancellationTokenSource.Token;
            void UpdateTimerText()
            {
                CurrentTimeText.Value = CurrentTime.Elapsed.ToString(@"hh\:mm\:ss");
            }
            var timerUpdateTask = Task.Run(() =>
            {
                while (!timerCancellationToken.IsCancellationRequested)
                {
                    UpdateTimerText();
                    Thread.Sleep(500);
                }
                CurrentTime.Stop();
                CurrentTime.Reset();
                UpdateTimerText();
            }, timerCancellationToken);

            CurrentAnimationTime.Value = 0.0;
            IsPlaying.Value = true;
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken ct = _cancellationTokenSource.Token;
            _playTask = Task.Run(() =>
            {
                ActualPageNumberVisibility.Value = MovieSetting.ShowPageNumber.Value;
                try
                {
                    PlaySlideshow(startPageIndex, ct);
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

                    View?.Dispatcher.InvokeAsync(() =>
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

                    timerCancellationTokenSource.Cancel();
                    timerUpdateTask.Wait(ct);
                    timerUpdateTask.Dispose();
                }
            }, ct);
        }

        private void PlaySlideshow(int startPageIndex, CancellationToken ct)
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

            _pageIndex = startPageIndex;

            CurrentPageNumber.Value = 1;
            {
                var pageInfo = MovieSetting.PageInfos[_pageIndex];
                if (pageInfo != null)
                {
                    if (ImageUtility.IsImageFile(pageInfo.ImagePath.Value.ActualPath.Value))
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
                                PlayWindow?.SlideView?.PlayMediaElement(currentBufferIndex);
                            }, ct);
                            //while (!this.IsMediaLoaded)
                            //{
                            //    this.Sleep(30);
                            //}
                        }

                        bufferVisibilities[1].Value = currentBufferIndex == 1;
                        bufferVisibilities[0].Value = true;
                    }

                    CurrentPage.Value = pageInfo;
                }
            }

            PrepareToStartBgm(MovieSetting.BgmPath.Value, (float)MovieSetting.BgmVolume.Value);
            if (startPageIndex == 0)
            {
                // 録画開始しやすいように少し間を空ける
                this.Sleep(5000);
            }

            // BGM の開始
            StartPreparedBgm(MovieSetting.BgmPath.Value, (float)MovieSetting.BgmVolume.Value);

            Task.Run(() =>
            {
                while (IsPlaying.Value)
                {
                    var timeOffset = (int)((1000 / 30) * _randGenerator.NextDouble());
                    Thread.Sleep(timeOffset);
                    CurrentAnimationTime.Value += timeOffset;
                    AnimateFaceRotation();
                }
            }, ct);

            this.Sleep(500);

            var pageCount = MovieSetting.PageInfos.Count;
            for (; _pageIndex < pageCount; ++_pageIndex)
            {
                WaitResume();
                CurrentPageNumber.Value = _pageIndex + 1;
                var currentBufferIndex = _pageIndex % 2;
                var nextBufferIndex = currentBufferIndex == 0 ? 1 : 0;
                var pageInfo = MovieSetting.PageInfos[_pageIndex];
                var nextPageInfo = _pageIndex + 1 < pageCount ? MovieSetting.PageInfos[_pageIndex + 1] : null;

                if (!pageInfo.IsEnabled.Value) continue;

                CurrentPage.Value = pageInfo;

                using (_goToNexPageCancellationTokenSource = new CancellationTokenSource())
                using (_goBackToPreviousPageCancellationTokenSource = new CancellationTokenSource())
                {
                    var goNextCt = _goToNexPageCancellationTokenSource.Token;
                    var goBackCt = _goBackToPreviousPageCancellationTokenSource.Token;
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, goNextCt, goBackCt);
                    var linkedCt = linkedCts.Token;

                    Task.Run(() =>
                    {
                        if (pageInfo.BgmPath is not null && !(pageInfo.BgmPath.Value.IsEmpty()))
                        {
                            FadeOutCurrentBgm(pageInfo.BgmFadeMiliseconds.Value);
                            float volume = pageInfo.OverwritesBgmVolume.Value ? (float)pageInfo.BgmVolume.Value : (float)MovieSetting.BgmVolume.Value;
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


                    // 一回ダブルバッファリングは忘れる
                    this.IsMediaLoaded = false;
                    this.MediaDucration = new Duration();
                    currentBufferIndex = 0;
                    bool hasVideo = !string.IsNullOrEmpty(pageInfo.VideoPath.Value.ActualPath.Value);
                    if (hasVideo)
                    {
                        // 動画読み込み
                        bufferVisibilities[currentBufferIndex].Value = true;
                        imagePaths[currentBufferIndex].Value = pageInfo.VideoPath.Value.ActualPath.Value;
                        View?.Dispatcher.Invoke(() =>
                        {
                            // Playすることで開かれ、開かれたらコールバックで一時停止するので、
                            // 動画が停止した状態になる
                            PlayWindow?.SlideView?.PlayMediaElement(0);
                        }, ct);
                        while (!this.IsMediaLoaded)
                        {
                            Thread.Sleep(100);
                        }
                    }

                    _playPageTask = Task.Run(() =>
                    {
                        try
                        {
                            linkedCt.ThrowIfCancellationRequested();

                            var stopwatch = new Stopwatch();
                            if (hasVideo)
                            {
                                // 動画再生開始
                                View?.Dispatcher.Invoke(() =>
                                {
                                    PlayWindow?.SlideView?.SetMediaVolume(pageInfo.MediaVolume.Value, currentBufferIndex);
                                    PlayWindow?.SlideView?.PlayMediaElement(currentBufferIndex);
                                }, ct);
                            }

                            // ページ切り替え時の音が設定されていない場合は、デフォルトのSEを再生
                            var pageType = pageInfo.PageType.Value;
                            bool usesDefaultSe = _pageIndex != 0
                            && (!pageInfo.NarrationInfos.Any() || pageInfo.NarrationInfos.First().AudioPaths.Count == 0);
                            if (usesDefaultSe)
                            {
                                var audioPath = MovieSetting.GetDefaultPageTurningAudioPath(pageType);
                                if (!audioPath.IsEmpty())
                                {
                                    bool isParallel = pageType == PageType.TitleAndBody
                                        // ナレーションがあったら間を置かない
                                        || (pageType == PageType.SectionHeader && pageInfo.NarrationInfos.Any());
                                    if (isParallel)
                                    {
                                        Task.Run(() =>
                                            PlayNarrationAudio(audioPath, 1.0f, linkedCt), linkedCt);
                                    }
                                    else
                                    {
                                        PlayNarrationAudio(audioPath, 1.0f, linkedCt);
                                    }
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
                                    if (info.PreBlankMilliseconds.Value > 0)
                                    {
                                        ThreadUtility.Wait(info.PreBlankMilliseconds.Value, ct, 10, () =>
                                        {
                                            BlinkEyeRandom();
                                        });
                                    }

                                    SpeechText(info, linkedCt);

                                    ResetFace();
                                    if (info.PostBlankMilliseconds.Value > 0)
                                    {
                                        ThreadUtility.Wait(info.PostBlankMilliseconds.Value, ct, 10, () =>
                                        {
                                            BlinkEyeRandom();
                                        });
                                    }
                                }
                            }

                            // 動画の場合は再生完了を待つ
                            if (hasVideo && View is not null && PlayWindow is not null)
                            {
                                while (!View.Dispatcher.Invoke(() => PlayWindow.SlideView.IsMediaEnded()))
                                {
                                    Thread.Sleep(100);
                                    linkedCt.ThrowIfCancellationRequested();
                                }
                                bufferVisibilities[currentBufferIndex].Value = false;
                            }

                            linkedCt.ThrowIfCancellationRequested();

                            this.Sleep(pageInfo.PagingIntervalMilliseconds.Value);
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
                                        _pageIndex -= 2;
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
                        _playPageTask.Wait(ct);
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

                _goToNexPageCancellationTokenSource = null;
                _goBackToPreviousPageCancellationTokenSource = null;
            }

            // BGM のフェードアウト
            var task = Task.Run(() =>
            {
                FadeOutCurrentBgm(MovieSetting.BgmFadeOutMilliseconds.Value);
            }, ct);

            // 終了後の余白
            {
                // フェードアウト
                int sleepMilliseconds = MovieSetting.MovieFadeOutMilliseconds.Value / 100;
                for (int i = 100; i >= 0; --i)
                {
                    double opacity = i / 100.0;
                    BlackOpacity.Value = 1.0 - opacity;
                    //MediaElement1.Opacity.Value = opacity;
                    //MediaElement2.Opacity.Value = opacity;
                    //FaceOpacity.Value = opacity;
                    Thread.Sleep(sleepMilliseconds);
                }
                BlackOpacity.Value = 1.0;
            }

            task.Wait(ct);
        }

        internal void Sleep(int milliseconds)
        {
            if (milliseconds == 0) return;
            WriteLogLine($"Sleep {milliseconds} ms");
            Thread.Sleep(milliseconds);
        }

        private void SetPageInfoByPageType(PageType pageType)
        {
            switch (pageType)
            {
                case PageType.Title:
                case PageType.SectionHeader:
                    CurrentPageTitleVerticalAlignment.Value = VerticalAlignment.Center;
                    CurrentPageTitleHorizontalAlignment.Value = HorizontalAlignment.Center;
                    CurrentPageTitleTextAlignment.Value = TextAlignment.Center;
                    TitleFontSize.Value = 100;
                    break;
                case PageType.TitleAndBody:
                    CurrentPageTitleVerticalAlignment.Value = VerticalAlignment.Top;
                    CurrentPageTitleHorizontalAlignment.Value = HorizontalAlignment.Left;
                    CurrentPageTitleTextAlignment.Value = TextAlignment.Left;
                    TitleFontSize.Value = 70;
                    break;
            }
        }
    }
}
