using PresentationMovieMaker.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PresentationMovieMaker.Views
{
    /// <summary>
    /// PlayWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class PlayWindow : Window
    {
        public PlayWindow()
        {
            InitializeComponent();
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            var viewModel = (MainWindowViewModel)this.DataContext;
            //if (sizeInfo.WidthChanged)
            //{
            //    viewModel.PlayWindowWidth.Value = sizeInfo.NewSize.Width;
            //    viewModel.UpdateHeight();
            //}
            //if (sizeInfo.HeightChanged)
            //{
            //    viewModel.PlayWindowHeight.Value = sizeInfo.NewSize.Height;
            //    viewModel.UpdateWidth();
            //}
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            var vm = (MainWindowViewModel)this.DataContext;
            vm.CancelPlaying();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MenuItemClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
