using PresentationMovieMaker.Utilities;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.ViewModels;

public class ImageSequenceViewModel : CompositeDisposableBase
{
    public ImageSequenceViewModel()
        : this(null)
    {
    }

    public ImageSequenceViewModel(string? source)
    {
        _ = AddPathCommand.Subscribe(() =>
        {
            _ = SingletonDispatcher.InvokeAsync(() =>
            {
                AddDefaultPathViewModel();
            });
        }).AddTo(Disposer);

        _ = RemovePathCommand.Subscribe(() =>
        {
            _ = SingletonDispatcher.InvokeAsync(() =>
            {
                ImagePaths.RemoveAt(ImagePaths.Count - 1);
            });
        }).AddTo(Disposer);

        _ = ImagePaths.CollectionChangedAsObservable().Subscribe(x =>
        {
            MaxIndex.Value = ImagePaths.Count - 1;
        }).AddTo(Disposer);

        SetPathsFromString(source);
    }

    //public ReactiveProperty<PathViewModel> SelectedPath { get; } = new ReactiveProperty<PathViewModel>();

    public PathViewModel CurrentImagePath => ImagePaths[CurrentIndex.Value];

    public ObservableCollection<PathViewModel> ImagePaths { get; } = [];
    public ReactiveProperty<int> CurrentIndex { get; } = new();

    public ReactiveProperty<int> MaxIndex { get; } = new();

    public ReactiveCommand AddPathCommand { get; } = new();
    public ReactiveCommand RemovePathCommand { get; } = new();

    private static string Delim = ";";

    public string ConvertPathsToString()
    {
        return string.Join(Delim, ImagePaths.Select(x => x.ActualPath.Value));
    }

    public void SetPathsFromString(string? source)
    {
        ImagePaths.Clear();
        CurrentIndex.Value = 0;
        if (source is not null)
        {
            foreach (var path in source.Split(Delim))
            {
                ImagePaths.Add(new PathViewModel(path));
            }
        }

        if (!ImagePaths.Any())
        {
            // 空のパスを追加
            AddDefaultPathViewModel();
        }
    }

    private void AddDefaultPathViewModel()
    {
        ImagePaths.Add(new PathViewModel());
    }
}
