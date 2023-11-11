using Microsoft.Win32;
using PresentationMovieMaker.DataModels;
using PresentationMovieMaker.Utilities;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PresentationMovieMaker.ViewModels
{
    public class MovieSettingViewModel : CompositeDisposableBase
    {
        private readonly ILogger _logger;
        public MovieSettingViewModel(ILogger logger)
        {
            _logger = logger;

            // デフォルト値
            CharacterHorizontalOffset.Value = 20;

            Properties.Add(Width);
            Properties.Add(Height);
            Properties.Add(SlideBackgroundImagePath);
            Properties.Add(ImageBodyPath);
            Properties.Add(ImageFaceBasePath);
            Properties.Add(ImageMouthAPath);
            Properties.Add(ImageMouthIPath);
            Properties.Add(ImageMouthUPath);
            Properties.Add(ImageMouthEPath);
            Properties.Add(ImageMouthOPath);
            Properties.Add(ImageMouthNPath);
            Properties.Add(ImageEyeClosePath);
            Properties.Add(ImageEyeOpenPath);
            Properties.Add(CharacterVerticalOffset);
            Properties.Add(CharacterHorizontalOffset);
            Properties.Add(DefaultPageTurningAudioPath);
            Properties.Add(DefaultSectionPageTurningAudioPath);
            Properties.Add(DefaultPageTurningAudioVolume);
            Properties.Add(CaptionBackgroundOpacity);
            Properties.Add(MovieFadeOutMilliseconds);

            PageInfos.CollectionChanged += (o, e) =>
            {
                UpdateDuration();
                UpdateCharCount();
            };

            ImageRoot.Subscribe(value =>
            {
                foreach (var pageInfo in PageInfos)
                {
                    pageInfo.ImagePath.Value.RootPath.Value = value;
                }
            }).AddTo(Disposable);

            AudioRoot.Subscribe(value =>
            {
                foreach (var pageInfo in PageInfos)
                {
                    foreach (var info in pageInfo.NarrationInfos)
                    {
                        info.AudioRoot.Value = value;
                    }
                }
            }).AddTo(Disposable);

            Subscribe(VoiceName, name =>
            {
                foreach (var pageInfo in PageInfos)
                {
                    foreach (var info in pageInfo.NarrationInfos)
                    {
                        info.SelectedVoiceName.Value = name;
                    }
                }
            });

            Subscribe(BrowseBgmFileCommand, () =>
            {
                var dialog = new OpenFileDialog
                {
                    // ファイルの種類を設定
                    Filter = "WAVファイル (*.wav)|*.wav|MP3ファイル (*.mp3)|*.mp3|全てのファイル (*.*)|*.*"
                };

                // ダイアログを表示する
                if (dialog.ShowDialog() == true)
                {
                    BgmPath.Value = dialog.FileName;
                }
            });

            Subscribe(PlayFromCurrentPageCommand, () =>
            {
                if (Parent is null)
                {
                    return;
                }

                var selectedPage = PageInfos.Where(x => x.IsSelected).LastOrDefault();
                if (selectedPage is null)
                {
                    return;
                }

                var startPageIndex = PageInfos.IndexOf(selectedPage);
                Parent.PlaySlideshow(startPageIndex);
            });

            MoveNarrationsToPreviousCommand.Subscribe(() =>
            {
                var selectedTopPage = PageInfos.Where(x => x.IsSelected).FirstOrDefault();
                if (selectedTopPage is null)
                {
                    return;
                }

                int currentPageIndex = PageInfos.IndexOf(selectedTopPage);
                for (int i = currentPageIndex; i < PageInfos.Count; ++i)
                {
                    var currentPage = PageInfos[i];
                    if (!currentPage.IsSelected)
                    {
                        continue;
                    }
                    var prevPage = PageInfos[i - 1];
                    var imagePath = prevPage.ImagePath.Value.Path.Value;
                    prevPage.DeepCopyFrom(currentPage.ToSerializable());
                    prevPage.ImagePath.Value.Path.Value = imagePath;
                }

            }).AddTo(Disposable);
            MoveNarrationsToNextCommand.Subscribe(() =>
            {
                var selectedTopPage = PageInfos.Where(x => x.IsSelected).FirstOrDefault();
                if (selectedTopPage is null)
                {
                    return;
                }

                int currentPageIndex = PageInfos.IndexOf(selectedTopPage);
                for (int i = PageInfos.Count - 1; i > currentPageIndex; --i)
                {
                    var currentPage = PageInfos[i - 1];
                    if (!currentPage.IsSelected)
                    {
                        continue;
                    }
                    var nextPage = PageInfos[i];
                    var imagePath = nextPage.ImagePath.Value.Path.Value;
                    nextPage.DeepCopyFrom(currentPage.ToSerializable());
                    nextPage.ImagePath.Value.Path.Value = imagePath;
                }
            }).AddTo(Disposable);

            AddPageCommand.Subscribe(() =>
            {
                AddNewPage();
            }).AddTo(Disposable);

            RemovePageCommand.Subscribe(() =>
            {
                var removeTargets = PageInfos.Where(x => x.IsSelected).ToArray();
                foreach (var target in removeTargets)
                {
                    PageInfos.Remove(target);
                }

                ReassignPageNumber();
            }).AddTo(Disposable);

            DuplicatePageCommand.Subscribe(() =>
            {
                foreach (var pageInfo in PageInfos.Where(x => x.IsSelected).ToArray())
                {
                    var newPage = AddNewPage();
                    newPage.DeepCopyFrom(pageInfo.ToSerializable());
                }
            }).AddTo(Disposable);

            var separator = "__page_info_separator__";
            CopyPageCommand.Subscribe(() =>
            {
                var text = string.Join(separator, PageInfos.Where(x => x.IsSelected).Select(x =>
                {
                    var serial = x.ToSerializable();
                    return JsonSerializer.Serialize(serial);
                }));
                Clipboard.SetData(DataFormats.Text, (Object)text);
            }).AddTo(Disposable);
            PastePageCommand.Subscribe(() =>
            {
                if (Clipboard.GetData(DataFormats.Text) is not string text)
                {
                    return;
                }


                var splited = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
                var selectedPages = PageInfos.Where(x => x.IsSelected).ToArray();
                if (selectedPages.Length == 0)
                {
                    return;
                }

                int max = Math.Min(splited.Length, selectedPages.Length);
                if (splited.Length == 1)
                {
                    var dataModel = JsonSerializer.Deserialize<PageInfo>(splited[0]);
                    if (dataModel is null)
                    {
                        return;
                    }

                    foreach (var pastePage in selectedPages)
                    {
                        var imagePath = pastePage.ImagePath.Value.Path.Value;
                        pastePage.DeepCopyFrom(dataModel);
                        pastePage.ImagePath.Value.Path.Value = imagePath;
                    }
                }
                else
                {
                    for (int i = 0; i < splited.Length; ++i)
                    {
                        var dataModel = JsonSerializer.Deserialize<PageInfo>(splited[i]);
                        if (dataModel is null)
                        {
                            continue;
                        }

                        PageInfoViewModel pastePage;
                        if (selectedPages.Length > i)
                        {
                            pastePage = selectedPages[i];
                        }
                        else
                        {
                            var lastPage = selectedPages.Last();
                            var index = PageInfos.IndexOf(lastPage);
                            var diff = i - selectedPages.Length - 1;
                            var pastePageIndex = index + diff;
                            if (pastePageIndex >= PageInfos.Count)
                            {
                                break;
                            }

                            pastePage = PageInfos[pastePageIndex];
                        }

                        var imagePath = pastePage.ImagePath.Value.Path.Value;
                        pastePage.DeepCopyFrom(dataModel);
                        pastePage.ImagePath.Value.Path.Value = imagePath;
                    }
                }

            }).AddTo(Disposable);

            MovePageUpCommand.Subscribe(() =>
            {
                if (SelectedPageInfo.Value is null)
                {
                    return;
                }

                var pageInfo = SelectedPageInfo.Value;
                var index = PageInfos.IndexOf(pageInfo);
                if (index <= 0)
                {
                    return;
                }

                MovePageInfo(index, index - 1);
            }).AddTo(Disposable);

            MovePageDownCommand.Subscribe(() =>
            {
                if (SelectedPageInfo.Value is null)
                {
                    return;
                }

                var pageInfo = SelectedPageInfo.Value;
                var index = PageInfos.IndexOf(pageInfo);
                if (index < 0 || index == PageInfos.Count - 1)
                {
                    return;
                }

                MovePageInfo(pageInfo, index + 1);
            }).AddTo(Disposable);

            SelectedPageInfo.Subscribe(selectedPageInfo =>
            {
                IsPageInfoSelected.Value = selectedPageInfo != null;
                if (Parent is not null)
                {
                    Parent.CurrentPage.Value = selectedPageInfo;
                }
            });

            Subscribe(SelectionChangedCommand, args =>
            {
                if (args.AddedItems is not null)
                {
                    foreach (var item in args.AddedItems.Cast<ISelectable>())
                    {
                        item.IsSelected = true;
                    }
                }
                if (args.RemovedItems is not null)
                {
                    foreach (var item in args.RemovedItems.Cast<ISelectable>())
                    {
                        item.IsSelected = false;
                    }
                }
            });

            Subscribe(PageNumberPosX, value =>
            {
                Parent?.UpdatePageNumberPosX();
            });
            Subscribe(PageNumberPosY, value =>
            {
                Parent?.UpdatePageNumberPosY();
            });
            Subscribe(CaptionMarginLeft, value =>
            {
                Parent?.SyncCaptionMargin();
            });
            Subscribe(CaptionMarginBottom, value =>
            {
                Parent?.SyncCaptionMargin();
            });

            _ = Width.CombineLatest(Height).Subscribe(x =>
            {
                if (Parent is null) return;
                Parent.PlayWindowWidth.Value = x.First;
                Parent.PlayWindowHeight.Value = x.Second;
            }).AddTo(Disposable);

            Subscribe(BgmVolume, value =>
            {
                Parent?.SyncBgmVolumeFromSetting();

            });
            Subscribe(SlideBackgroundImagePath.Value.ActualPath, value =>
            {
                UpdateSlideBackground(value);
            });
            Subscribe(DescriptionFontSize, value =>
            {
                if (Parent is not null)
                {
                    Parent.DescriptionFontSize.Value = value;
                }
            });

            VoiceName.Value = VoiceNames.First();
        }

        public MovieSettingViewModel(MovieSetting dataModel, ILogger logger)
            : this(logger)
        {
            DeepCopyFrom(dataModel);
        }

        public ILogger Logger => _logger;

        public MainWindowViewModel? Parent { get; set; }

        public ObservableCollection<IReactiveProperty> Properties { get; } = new ObservableCollection<IReactiveProperty>();

        public ReactiveProperty<int> PagingIntervalMilliseconds { get; } = new ReactiveProperty<int>(300);
        public ReactiveProperty<int> NarrationLineBreakInterval { get; } = new ReactiveProperty<int>(100);
        public ReactiveProperty<double> FaceImageWidth { get; } = new ReactiveProperty<double>(130);
        public ReactiveProperty<double> CaptionMarginLeft { get; } = new ReactiveProperty<double>(60);
        public ReactiveProperty<double> CaptionMarginBottom { get; } = new ReactiveProperty<double>(60);

        public ReactiveProperty<int> BgmFadeOutMilliseconds { get; } = new ReactiveProperty<int>(3000);
        public IntPropertyViewModel MovieFadeOutMilliseconds { get; } = new IntPropertyViewModel("動画フェードアウト時間(ms)", 0, 5000);

        public ReactiveProperty<bool> ShowPageNumber { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<double> PageNumberPosX { get; } = new ReactiveProperty<double>(0);
        public ReactiveProperty<double> PageNumberPosY { get; } = new ReactiveProperty<double>(0);
        public ReactiveProperty<double> PageNumberFontSize { get; } = new ReactiveProperty<double>(8.0);

        public DoublePropertyViewModel Width { get; } = new DoublePropertyViewModel("幅", 1, 1920);
        public DoublePropertyViewModel Height { get; } = new DoublePropertyViewModel("高さ", 1, 1920);

        public ReactiveProperty<double> BgmVolume { get; } = new ReactiveProperty<double>();
        public ReactiveProperty<double> NarrationVolume { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<string> BgmPath { get; } = new ReactiveProperty<string>();

        public DoublePropertyViewModel CharacterVerticalOffset { get; } = new("顔画像の縦オフセット", -500, 0);
        public DoublePropertyViewModel CharacterHorizontalOffset { get; } = new("顔画像の右オフセット", -500, 500);
        public ReactiveProperty<PathViewModel> ImageMouthAPath { get; set; } = new PathPropertyViewModel(nameof(ImageMouthAPath));
        public ReactiveProperty<PathViewModel> ImageMouthIPath { get; set; } = new PathPropertyViewModel(nameof(ImageMouthIPath));
        public ReactiveProperty<PathViewModel> ImageMouthUPath { get; set; } = new PathPropertyViewModel(nameof(ImageMouthUPath));
        public ReactiveProperty<PathViewModel> ImageMouthEPath { get; set; } = new PathPropertyViewModel(nameof(ImageMouthEPath));
        public ReactiveProperty<PathViewModel> ImageMouthOPath { get; set; } = new PathPropertyViewModel(nameof(ImageMouthOPath));
        public ReactiveProperty<PathViewModel> ImageMouthNPath { get; set; } = new PathPropertyViewModel(nameof(ImageMouthNPath));
        public ReactiveProperty<PathViewModel> ImageEyeOpenPath { get; set; } = new PathPropertyViewModel(nameof(ImageEyeOpenPath));
        public ReactiveProperty<PathViewModel> ImageEyeClosePath { get; set; } = new PathPropertyViewModel(nameof(ImageEyeClosePath));
        public ReactiveProperty<PathViewModel> ImageBodyPath { get; set; } = new PathPropertyViewModel(nameof(ImageBodyPath));
        public ReactiveProperty<PathViewModel> ImageFaceBasePath { get; set; } = new PathPropertyViewModel(nameof(ImageFaceBasePath));

        public ReactiveProperty<PathViewModel> SlideBackgroundImagePath { get; set; } = new PathPropertyViewModel(nameof(SlideBackgroundImagePath));

        public ReactiveProperty<BitmapSource> SlideBackgroundImage { get; } = new ReactiveProperty<BitmapSource>();


        public ReactiveProperty<PathViewModel> DefaultPageTurningAudioPath { get; set; } = new PathPropertyViewModel(nameof(DefaultPageTurningAudioPath));

        public ReactiveProperty<PathViewModel> DefaultSectionPageTurningAudioPath { get; set; } = new PathPropertyViewModel(nameof(DefaultSectionPageTurningAudioPath));

        public DoublePropertyViewModel DefaultPageTurningAudioVolume { get; set; } = new DoublePropertyViewModel(nameof(DefaultPageTurningAudioVolume));

        public DoublePropertyViewModel CaptionBackgroundOpacity { get; set; } = new DoublePropertyViewModel("キャプション背景不透明度");






        public ReactiveProperty<int> TotalCharCount { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<TimeSpan> TotalDuration { get; } = new ReactiveProperty<TimeSpan>();

        public ReactiveProperty<string> VoiceName { get; } = new ReactiveProperty<string>();

        public IEnumerable<string> VoiceNames { get; } = SoundUtility.GetInstalledVoiceNames();

        public ReactiveProperty<bool> IsPageInfoSelected { get; } = new ReactiveProperty<bool>();

        public ReactiveProperty<PageInfoViewModel?> SelectedPageInfo { get; } = new ReactiveProperty<PageInfoViewModel?>();

        public ReactiveProperty<string> ImageRoot { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> AudioRoot { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<double> CaptionFontSize { get; } = new ReactiveProperty<double>(30.0);

        public ReactiveProperty<double> DescriptionFontSize { get; } = new ReactiveProperty<double>(50.0);

        public ObservableCollection<PageInfoViewModel> PageInfos { get; } = new ObservableCollection<PageInfoViewModel>();

        public ReactiveCommand PlayFromCurrentPageCommand { get; } = new ReactiveCommand();

        public ReactiveCommand AddPageCommand { get; } = new ReactiveCommand();
        public ReactiveCommand RemovePageCommand { get; } = new ReactiveCommand();
        public ReactiveCommand CopyPageCommand { get; } = new ReactiveCommand();
        public ReactiveCommand PastePageCommand { get; } = new ReactiveCommand();
        public ReactiveCommand DuplicatePageCommand { get; } = new ReactiveCommand();
        
        public ReactiveCommand MovePageUpCommand { get; } = new ReactiveCommand();
        public ReactiveCommand MovePageDownCommand { get; } = new ReactiveCommand();
        public ReactiveCommand MoveNarrationsToPreviousCommand { get; } = new ReactiveCommand();
        public ReactiveCommand MoveNarrationsToNextCommand { get; } = new ReactiveCommand();
        public ReactiveCommand BrowseBgmFileCommand { get; } = new ReactiveCommand();

        public ReactiveCommand<SelectionChangedEventArgs> SelectionChangedCommand { get; } = new ReactiveCommand<SelectionChangedEventArgs>();


        public void MovePageInfo(int elemIndex, int destinationIndex)
        {
            var pageInfo = PageInfos[elemIndex];
            MovePageInfo(pageInfo, destinationIndex);
        }

        public void AddPageInfos(IEnumerable<string> imagePaths)
        {
            var newPageInfos = imagePaths.Select(x =>
            {
                var pageInfo = new PageInfoViewModel(this);
                pageInfo.ImagePath.Value.Path.Value = x;
                return pageInfo;
            });

            AddPageInfos(newPageInfos);
        }

        private void AddPageInfos(IEnumerable<PageInfoViewModel> pageInfos)
        {
            var infoArray = pageInfos.ToArray();
            if (SelectedPageInfo.Value is null)
            {
                foreach (var pageInfo in infoArray)
                {
                    PageInfos.Add(pageInfo);
                }
            }
            else
            {
                var insertIndex = PageInfos.IndexOf(SelectedPageInfo.Value);
                for (int i = infoArray.Length - 1; i >= 0; i--)
                {
                    var pageInfo = infoArray[i];
                    PageInfos.Insert(insertIndex + 1, pageInfo);
                }
            }
            ReassignPageNumber();
        }

        private void MovePageInfo(PageInfoViewModel pageInfo, int destinationIndex)
        {
            // Removeで選択が解除されるので保持しておく
            var selectedPageInfo = SelectedPageInfo.Value;

            PageInfos.Remove(pageInfo);
            PageInfos.Insert(destinationIndex, pageInfo);
            ReassignPageNumber();

            // 選択状態の復帰
            SelectedPageInfo.Value = selectedPageInfo;
        }

        private void ReassignPageNumber()
        {
            for (int i = 0; i < PageInfos.Count; i++)
            {
                var pageInfo = PageInfos[i];
                pageInfo.PageNumber.Value = i + 1;
            }
        }

        public void ResetToDefault()
        {
            DeepCopyFrom(new MovieSetting());
        }

        public void DeepCopyFrom(MovieSetting dataModel)
        {
            if (dataModel.PageInfos is not null)
            {
                PageInfos.Clear();
                for (int i = 0; i < dataModel.PageInfos.Count; ++i)
                {
                    var info = dataModel.PageInfos[i];
                    PageInfos.Add(new PageInfoViewModel(info, i + 1, this));
                }
            }

            ImageRoot.Value = dataModel.ImageRoot;
            AudioRoot.Value = dataModel.AudioRoot;
            if (SoundUtility.IsValidVoiceName(dataModel.VoiceName))
            {
                VoiceName.Value = dataModel.VoiceName;
            }

            BgmPath.Value = dataModel.BgmPath;
            BgmVolume.Value = dataModel.BgmVolume;
            Width.Value = dataModel.Width == 0 ? 640 : dataModel.Width;
            Height.Value = dataModel.Height == 0 ? 640 : dataModel.Height;
            ShowPageNumber.Value = dataModel.ShowPageNumber;
            PageNumberPosX.Value = dataModel.PageNumberPosX;
            PageNumberPosY.Value = dataModel.PageNumberPosY;
            PageNumberFontSize.Value = dataModel.PageNumberFontSize;
            BgmFadeOutMilliseconds.Value = dataModel.BgmFadeOutMilliseconds;
            MovieFadeOutMilliseconds.Value = dataModel.MovieFadeOutMilliseconds;
            CaptionMarginBottom.Value = dataModel.CaptionMarginBottom;
            CaptionMarginLeft.Value = dataModel.CaptionMarginLeft;
            FaceImageWidth.Value = dataModel.FaceImageWidth;
            CaptionFontSize.Value = dataModel.CaptionFontSize;
            DescriptionFontSize.Value = dataModel.DescriptionFontSize;
            NarrationVolume.Value = dataModel.NarrationVolume;
            PagingIntervalMilliseconds.Value = dataModel.PagingIntervalMilliseconds;

            CharacterVerticalOffset.Value = dataModel.CharacterVerticalOffset;
            CharacterHorizontalOffset.Value = dataModel.CharacterHorizontalOffset;
            ImageMouthAPath.Value.Path.Value = dataModel.ImageMouthAPath;
            ImageMouthIPath.Value.Path.Value = dataModel.ImageMouthIPath;
            ImageMouthUPath.Value.Path.Value = dataModel.ImageMouthUPath;
            ImageMouthEPath.Value.Path.Value = dataModel.ImageMouthEPath;
            ImageMouthOPath.Value.Path.Value = dataModel.ImageMouthOPath;
            ImageMouthNPath.Value.Path.Value = dataModel.ImageMouthNPath;
            ImageEyeOpenPath.Value.Path.Value = dataModel.ImageEyeOpenPath;
            ImageEyeClosePath.Value.Path.Value = dataModel.ImageEyeClosePath;
            ImageFaceBasePath.Value.Path.Value = dataModel.ImageFaceBasePath;
            ImageBodyPath.Value.Path.Value = dataModel.ImageBodyPath;
            SlideBackgroundImagePath.Value.Path.Value = dataModel.SlideBackgroundImagePath;
            DefaultPageTurningAudioPath.Value.Path.Value = dataModel.DefaultPageTurningAudioPath;
            DefaultSectionPageTurningAudioPath.Value.Path.Value = dataModel.DefaultSectionPageTurningAudioPath;
            CaptionBackgroundOpacity.Value = dataModel.CaptionBackgroundOpacity;
        }

        public MovieSetting ToSerializable()
        {
            var serial = new MovieSetting
            {
                AudioRoot = AudioRoot.Value,
                ImageRoot = ImageRoot.Value,
                VoiceName = VoiceName.Value,
                NarrationLineBreakInterval = NarrationLineBreakInterval.Value
            };
            serial.PageInfos.AddRange(PageInfos.Select(x => x.ToSerializable()));
            serial.BgmVolume = BgmVolume.Value;
            serial.PagingIntervalMilliseconds = PagingIntervalMilliseconds.Value;
            serial.Width = Width.Value;
            serial.Height = Height.Value;

            serial.BgmPath = BgmPath.Value;
            serial.ShowPageNumber = ShowPageNumber.Value;
            serial.PageNumberPosX = PageNumberPosX.Value;
            serial.PageNumberPosY = PageNumberPosY.Value;
            serial.PageNumberFontSize = PageNumberFontSize.Value;
            serial.BgmFadeOutMilliseconds = BgmFadeOutMilliseconds.Value;
            serial.MovieFadeOutMilliseconds = MovieFadeOutMilliseconds.Value;
            serial.CaptionMarginLeft = CaptionMarginLeft.Value;
            serial.CaptionMarginBottom = CaptionMarginBottom.Value;
            serial.FaceImageWidth = FaceImageWidth.Value;
            serial.CaptionFontSize = CaptionFontSize.Value;
            serial.NarrationVolume = NarrationVolume.Value;
            serial.DescriptionFontSize = DescriptionFontSize.Value;

            serial.CharacterHorizontalOffset = CharacterHorizontalOffset.Value;
            serial.CharacterVerticalOffset = CharacterVerticalOffset.Value;
            serial.ImageMouthAPath = ImageMouthAPath.Value.ActualPath.Value;
            serial.ImageMouthIPath = ImageMouthIPath.Value.ActualPath.Value;
            serial.ImageMouthUPath = ImageMouthUPath.Value.ActualPath.Value;
            serial.ImageMouthEPath = ImageMouthEPath.Value.ActualPath.Value;
            serial.ImageMouthOPath = ImageMouthOPath.Value.ActualPath.Value;
            serial.ImageMouthNPath = ImageMouthNPath.Value.ActualPath.Value;
            serial.ImageEyeOpenPath = ImageEyeOpenPath.Value.ActualPath.Value;
            serial.ImageEyeClosePath = ImageEyeClosePath.Value.ActualPath.Value;
            serial.ImageFaceBasePath = ImageFaceBasePath.Value.ActualPath.Value;
            serial.SlideBackgroundImagePath = SlideBackgroundImagePath.Value.ActualPath.Value;
            serial.ImageBodyPath = ImageBodyPath.Value.ActualPath.Value;
            serial.DefaultPageTurningAudioPath = DefaultPageTurningAudioPath.Value.ActualPath.Value;
            serial.DefaultSectionPageTurningAudioPath = DefaultSectionPageTurningAudioPath.Value.ActualPath.Value;
            serial.CaptionBackgroundOpacity = CaptionBackgroundOpacity.Value;
            return serial;
        }

        public PathViewModel GetDefaultPageTurningAudioPath(PageType pageType)
        {
            return pageType switch
            {
                PageType.Title or PageType.SectionHeader => 
                    DefaultSectionPageTurningAudioPath.Value.IsEmpty() ?
                                        DefaultPageTurningAudioPath.Value : DefaultSectionPageTurningAudioPath.Value,
                PageType.TitleAndBody => DefaultPageTurningAudioPath.Value,
                _ => throw new Exception(),
            };
        }

        public void UpdateDuration()
        {
            TimeSpan duration = new();
            foreach (var info in PageInfos)
            {
                duration += info.TotalDuration.Value;

                // とりあえずページ前後に固定で700msのブランクを入れてるのでそれを計上
                duration += TimeSpan.FromMilliseconds(700);
            }

            this.TotalDuration.Value = duration;
        }
        public void UpdateCharCount()
        {
            int count = 0;
            foreach (var info in PageInfos)
            {
                count += info.TotalCharCount.Value;
            }
            TotalCharCount.Value = count;
        }

        private PageInfoViewModel AddNewPage()
        {
            var newPage = new PageInfoViewModel(this);
            AddPageInfos(new[] { newPage });
            return newPage;
        }
        private void UpdateSlideBackground(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            SlideBackgroundImage.Value = ImageUtility.ConvertBitmapToBitmapSource(new System.Drawing.Bitmap(path));
        }
    }
}
