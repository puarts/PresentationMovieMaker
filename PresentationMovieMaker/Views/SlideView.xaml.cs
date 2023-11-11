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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PresentationMovieMaker.Views
{
    /// <summary>
    /// SlideView.xaml の相互作用ロジック
    /// </summary>
    public partial class SlideView : UserControl
    {
        public SlideView()
        {
            InitializeComponent();
        }
        public void SetMediaVolume(double volume, int bufferIndex)
        {
            //if (bufferIndex == 0)
            //{
                this.mediaElement.Volume = volume;
            //}
            //else
            //{
            //    this.mediaElement2.Volume = volume;
            //}
        }



        public void PlayMediaElement(int bufferIndex)
        {
            //if (bufferIndex == 0)
            //{
                this.mediaElement.Play();
            //}
            //else
            //{
            //    this.mediaElement2.Play();
            //}
        }

        public void PauseMediaElement(int bufferIndex)
        {
            //if (bufferIndex == 0)
            //{
                this.mediaElement.Pause();
            //}
            //else
            //{
            //    this.mediaElement2.Pause();
            //}
        }

        public bool IsMediaEnded()
        {
            return this.mediaElement.Position == this.mediaElement.NaturalDuration;
        }

        public void SwapBuffer()
        {
            // この方法はダメだった(RemoveしてInsertするとレイアウトの更新が走って一時的に黒くなる
            var currentBufferElement = rootCanvas.Children[0];
            rootCanvas.Children.RemoveAt(0);
            rootCanvas.Children.Insert(1, currentBufferElement);
        }

        private void MediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            var mediaElem = (MediaElement)sender;
            mediaElem.ScrubbingEnabled = true;
            mediaElem.Play();
            if (mediaElem.CanPause)
            {
                mediaElem.Pause();
            }
            var vm = (MainWindowViewModel)this.DataContext;
            vm.MediaDucration = mediaElem.NaturalDuration;
            vm.IsMediaLoaded = true;
            //int bufferIndex = mediaElem == mediaElement ? 0 : 1;
            //vm.WriteLogLine($"MediaElement loaded: bufferIndex={bufferIndex}");
        }

        public void ResetVisibleChangedFlag()
        {
            _isMediaElementVisibleChanged = false;
        }

        public void WaitVisibleChanged()
        {
            while (!_isMediaElementVisibleChanged)
            {
                Thread.Sleep(30);
            }
        }

        private bool _isMediaElementVisibleChanged = false;
        private void mediaElement_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _isMediaElementVisibleChanged = true;
        }
    }
}
