using PresentationMovieMaker.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PresentationMovieMaker.Views
{
    /// <summary>
    /// MovieSettingView.xaml の相互作用ロジック
    /// </summary>
    public partial class MovieSettingView : UserControl
    {
        //private ListBox? dragSource = null;

        public MovieSettingView()
        {
            InitializeComponent();
            Loaded += MovieSettingView_Loaded;
        }

        private void MovieSettingView_Loaded(object sender, RoutedEventArgs e)
        {
            //pageInfoListBox.PreviewMouseLeftButtonDown += ListBox_PreviewMouseLeftButtonDown;
        }

        private static object? GetDataFromListBox(ListBox source, Point point)
        {
            UIElement? element = source.InputHitTest(point) as UIElement;
            if (element != null)
            {
                object data = DependencyProperty.UnsetValue;
                while (data == DependencyProperty.UnsetValue)
                {
                    data = source.ItemContainerGenerator.ItemFromContainer(element);

                    if (data == DependencyProperty.UnsetValue)
                    {
                        element = VisualTreeHelper.GetParent(element) as UIElement;
                    }

                    if (element == source)
                    {
                        return null;
                    }
                }

                if (data != DependencyProperty.UnsetValue)
                {
                    return data;
                }
            }

            return null;
        }

        //private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        //{
        //    ListBox parent = (ListBox)sender;
        //    dragSource = parent;
        //    object? data = GetDataFromListBox(dragSource, e.GetPosition(parent));
        //    if (data != null)
        //    {
        //        DragDrop.DoDragDrop(parent, data, DragDropEffects.Move);
        //    }
        //    e.Handled = true;
        //}

        //private void ListBox_Drop(object sender, DragEventArgs e)
        //{
        //    ListBox parent = (ListBox)sender;
        //    var droppedData = e.Data.GetData(typeof(PageInfoViewModel)) as PageInfoViewModel;
        //    if (droppedData is null)
        //    {
        //        return;
        //    }

        //    var dropDestinationData = GetDataFromListBox(parent, e.GetPosition(parent)) as PageInfoViewModel;
        //    if (dropDestinationData is null)
        //    {
        //        return;
        //    }

        //    var viewModel = (MovieSettingViewModel)DataContext;
        //    int index = viewModel.PageInfos.IndexOf(droppedData);
        //    viewModel.MovePageInfo(index, viewModel.PageInfos.IndexOf(dropDestinationData));
        //}

        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            var mediaElement = (MediaElement)sender;
            mediaElement.ScrubbingEnabled = true;
            mediaElement.Play();
            mediaElement.Pause();
        }

        private void ListView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ListView & !e.Handled)
            {
                e.Handled = true;
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                var parent = ((Control)sender).Parent as UIElement;
                parent?.RaiseEvent(eventArg);
            }
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] filePaths = (string[])e.Data.GetData(DataFormats.FileDrop); // ドロップしたファイル名を全部取得する
                var vm = (MovieSettingViewModel)DataContext;
                vm.AddPageInfos(filePaths);
            }
        }

        private void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var mediaElem = (MediaElement)sender;
            var vm = (MovieSettingViewModel)DataContext;
            vm.Logger.WriteErrorLogLine($"メディアの読み込みに失敗しました。{mediaElem.Source}", e.ErrorException);
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}
