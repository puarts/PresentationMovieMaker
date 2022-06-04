using NAudio.Wave;
using PresentationMovieMaker.DataModels;
using PresentationMovieMaker.Utilities;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace PresentationMovieMaker.ViewModels
{
    public class PageInfoViewModel : CompositeDisposableBase, ISelectable
    {
        private CancellationTokenSource? _playAudioCancellationTokenSource = null;

        public PageInfoViewModel(MovieSettingViewModel parent)
        {
            Parent = parent;
            PagingIntervalMilliseconds.Value = parent.PagingIntervalMilliseconds.Value;

            PlayAudioCommand.Subscribe(() =>
            {
                if (IsAudioPlaying.Value)
                {
                    if (_playAudioCancellationTokenSource is null)
                    {
                        return;
                    }

                    _playAudioCancellationTokenSource.Cancel();
                }
                else
                {
                    IsAudioPlaying.Value = true;
                    Task.Run(() =>
                    {
                        using (_playAudioCancellationTokenSource = new CancellationTokenSource())
                        {
                            var ct = _playAudioCancellationTokenSource.Token;
                            try
                            {
                                foreach (var info in NarrationInfos)
                                {
                                    info.StartPlaying(ct);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                            }
                            finally
                            {
                                foreach (var info in NarrationInfos)
                                {
                                    info.StopPlaying();
                                }
                                IsAudioPlaying.Value = false;
                            }
                        }
                        _playAudioCancellationTokenSource = null;
                    });
                }
            }).AddTo(Disposable);

            IsAudioPlaying.Subscribe(isAudioPlaying =>
            {
                if (isAudioPlaying)
                {
                    PlayAudioButtonLabel.Value = "音声停止";
                }
                else
                {
                    PlayAudioButtonLabel.Value = "音声再生";
                }
            }).AddTo(Disposable);

            Subscribe(AddNarrationInfoCommand, () =>
            {
                AddNarrationInfos(new[] { new NarrationInfoViewModel(this) });
                foreach (var info in EnumerateSelectedPageInfosWithoutMyself())
                {
                    info.AddNarrationInfos(new[] { new NarrationInfoViewModel(this) });
                }
            });

            Subscribe(RemoveNarrationInfoCommand, () =>
            {
                if (NarrationInfos.Count == 0)
                {
                    return;
                }

                NarrationInfos.RemoveAt(NarrationInfos.Count - 1);
            });

            Subscribe(MoveNarrationInfoUpCommand, () =>
            {
                if (SelectedNarrationInfo.Value is null)
                {
                    return;
                }

                var pageInfo = SelectedNarrationInfo.Value;
                var index = NarrationInfos.IndexOf(pageInfo);
                if (index <= 0)
                {
                    return;
                }

                MoveNarrationInfo(index, index - 1);
            });
            Subscribe(MoveNarrationInfoDownCommand, () =>
            {
                if (SelectedNarrationInfo.Value is null)
                {
                    return;
                }

                var pageInfo = SelectedNarrationInfo.Value;
                var index = NarrationInfos.IndexOf(pageInfo);
                if (index < 0 || index == NarrationInfos.Count - 1)
                {
                    return;
                }

                MoveNarrationInfo(pageInfo, index + 1);
            });

            Subscribe(TotalDuration, value =>
            {
                Parent.UpdateDuration();
            });

            Subscribe(TotalCharCount, value =>
            {
                Parent.UpdateCharCount();
            });

            NarrationInfos.CollectionChanged += (o, e) =>
            {
                UpdateDuration();
                UpdateCharCount();
            };
        }

        public PageInfoViewModel(int pageNumber, MovieSettingViewModel parent)
            : this(parent)
        {
            PageNumber.Value = pageNumber;
        }

        public PageInfoViewModel(PageInfo dataModel, int pageNumber, MovieSettingViewModel parent)
            : this(pageNumber, parent)
        {
            DeepCopyFrom(dataModel);
        }

        public void DeepCopyFrom(PageInfo dataModel)
        {
            ImagePath.Value = new PathViewModel(dataModel.ImagePath);
            BgmPath.Value = new PathViewModel(dataModel.BgmPath);
            BgmVolume.Value = dataModel.BgmVolume;
            OverwritesBgmVolume.Value = dataModel.OverwritesBgmVolume;
            BgmFadeMiliseconds.Value = dataModel.BgmFadeMiliseconds;
            RotationAngle.Value = dataModel.RotationAngle;
            MediaVolume.Value = dataModel.MediaVolume;
            PagingIntervalMilliseconds.Value = dataModel.PagingIntervalMilliseconds;

            NarrationInfos.Clear();
            if (dataModel.NarrationInfos.Any())
            {
                foreach (var info in dataModel.NarrationInfos)
                {
                    NarrationInfos.Add(new NarrationInfoViewModel(info, this));
                }
            }
        }

        public MovieSettingViewModel Parent { get; }

        private bool _isSelected = false;
        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }

            set
            {
                SetProperty(ref _isSelected, value);
            }
        }
        public ReactiveProperty<double> MediaVolume { get; } = new ReactiveProperty<double>(1.0);

        public ReactiveProperty<int> TotalCharCount { get; } = new ReactiveProperty<int>();

        public ReactiveProperty<int> PagingIntervalMilliseconds { get; } = new ReactiveProperty<int>(300);

        public ReactiveProperty<TimeSpan> TotalDuration { get; } = new ReactiveProperty<TimeSpan>();

        public ReactiveProperty<string> PlayAudioButtonLabel { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<NarrationInfoViewModel> SelectedNarrationInfo { get; } = new ReactiveProperty<NarrationInfoViewModel>();


        public ReactiveProperty<PathViewModel> BgmPath { get; } = new ReactiveProperty<PathViewModel>(new PathViewModel());
        public ReactiveProperty<double> BgmVolume { get; } = new ReactiveProperty<double>(1.0);
        public ReactiveProperty<bool> OverwritesBgmVolume { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<int> BgmFadeMiliseconds { get; } = new ReactiveProperty<int>(1000);

        public ReactiveProperty<bool> IsAudioPlaying { get; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<int> PageNumber { get; } = new ReactiveProperty<int>();

        public ReactiveProperty<PathViewModel> ImagePath { get; } = new ReactiveProperty<PathViewModel>(new PathViewModel());

        public ReactiveProperty<double> RotationAngle { get; } = new ReactiveProperty<double>(0);


        public ReactiveCommand PlayAudioCommand { get; } = new ReactiveCommand();
        public ReactiveCommand AddNarrationInfoCommand { get; } = new ReactiveCommand();

        public ReactiveCommand RemoveNarrationInfoCommand { get; } = new ReactiveCommand();

        public ReactiveCommand MoveNarrationInfoUpCommand { get; } = new ReactiveCommand();
        public ReactiveCommand MoveNarrationInfoDownCommand { get; } = new ReactiveCommand();


        public ObservableCollection<NarrationInfoViewModel> NarrationInfos { get; } = new ObservableCollection<NarrationInfoViewModel>();


        public PageInfo ToSerializable()
        {
            var serial = new PageInfo();
            serial.ImagePath = ImagePath.Value.Path.Value;
            serial.BgmPath = BgmPath.Value?.Path.Value ?? String.Empty;
            serial.BgmVolume = BgmVolume.Value;
            serial.OverwritesBgmVolume = OverwritesBgmVolume.Value;
            serial.BgmFadeMiliseconds = BgmFadeMiliseconds.Value;
            serial.RotationAngle = RotationAngle.Value;
            serial.MediaVolume = MediaVolume.Value;
            serial.NarrationInfos.AddRange(NarrationInfos.Select(x => x.ToSerializable()));
            serial.PagingIntervalMilliseconds = PagingIntervalMilliseconds.Value;
            return serial;
        }

        public void UpdateDuration()
        {
            TimeSpan duration = new TimeSpan();
            foreach (var narrationInfo in NarrationInfos)
            {
                duration += narrationInfo.TotalDuration.Value;
            }

            TotalDuration.Value = duration;
        }
        public void UpdateCharCount()
        {
            int count = 0;
            foreach (var narrationInfo in NarrationInfos)
{
                count += narrationInfo.TotalCharCount.Value;
            }
            TotalCharCount.Value = count;
        }

        public void MoveNarrationInfo(int elemIndex, int destinationIndex)
        {
            var pageInfo = NarrationInfos[elemIndex];
            MoveNarrationInfo(pageInfo, destinationIndex);
        }

        public IEnumerable<PageInfoViewModel> EnumerateSelectedPageInfosWithoutMyself()
        {
            return Parent.PageInfos.Where(x => x != this && x.IsSelected);
        }

        private void MoveNarrationInfo(NarrationInfoViewModel info, int destinationIndex)
        {
            // Removeで選択が解除されるので保持しておく
            var selected = SelectedNarrationInfo.Value;

            NarrationInfos.Remove(info);
            NarrationInfos.Insert(destinationIndex, info);

            // 選択状態の復帰
            SelectedNarrationInfo.Value = selected;
        }

        private void AddNarrationInfos(IEnumerable<NarrationInfoViewModel> infos)
        {
            var infoArray = infos.ToArray();
            if (SelectedNarrationInfo.Value is null)
            {
                foreach (var pageInfo in infoArray)
                {
                    NarrationInfos.Add(pageInfo);
                }
            }
            else
            {
                var insertIndex = NarrationInfos.IndexOf(SelectedNarrationInfo.Value);
                for (int i = infoArray.Length - 1; i >= 0; i--)
                {
                    var pageInfo = infoArray[i];
                    NarrationInfos.Insert(insertIndex + 1, pageInfo);
                }
            }
        }
    }
}
