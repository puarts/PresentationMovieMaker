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
    /// PageInfoEditView.xaml の相互作用ロジック
    /// </summary>
    public partial class PageInfoEditView : UserControl
    {
        public PageInfoEditView()
        {
            InitializeComponent();
        }

        private void TextBox_Drop(object sender, DragEventArgs e)
        {
            var textbox = (TextBox)sender;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                textbox.Text = string.Empty; // TextBoxの中身をクリアする。
                string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop); // ドロップしたファイル名を全部取得する。
                if (filenames.Length > 0)
                {
                    var filename = filenames.First();
                    textbox.Text = filename;
                }
            }
        }

        private void TextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = e.Data.GetDataPresent(DataFormats.FileDrop);
        }

        private void PreviewMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            var mediaElement = (MediaElement)sender;
            mediaElement.ScrubbingEnabled = true;
            mediaElement.Play();
            mediaElement.Pause();
        }
    }
}
