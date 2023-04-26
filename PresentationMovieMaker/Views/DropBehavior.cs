using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace PresentationMovieMaker.Views
{
    public static class DropBehavior
    {
        private static readonly DependencyProperty DropEvent =
         DependencyProperty.RegisterAttached(
          nameof(DropEvent),
          typeof(ICommand),
          typeof(DropBehavior),
          new PropertyMetadata(null, new ExecuteCommandOnRoutedEvent(Selector.DropEvent).PropertyChangedHandler));

        public static void SetDropEvent(DependencyObject dependencyObject, ICommand value)
        {
            dependencyObject.SetValue(DropEvent, value);
        }

        public static ICommand GetDropEvent(DependencyObject dependencyObject)
        {
            return (ICommand)dependencyObject.GetValue(DropEvent);
        }
    }

    public static class PreviewDragOverBehavior
    {
        private static readonly DependencyProperty PreviewDragOverEvent =
         DependencyProperty.RegisterAttached(
          nameof(PreviewDragOverEvent),
          typeof(ICommand),
          typeof(PreviewDragOverBehavior),
          new PropertyMetadata(null, new ExecuteCommandOnRoutedEvent(Selector.PreviewDragOverEvent).PropertyChangedHandler));

        public static void SetPreviewDragOverEvent(DependencyObject dependencyObject, ICommand value)
        {
            dependencyObject.SetValue(PreviewDragOverEvent, value);
        }

        public static ICommand GetPreviewDragOverEvent(DependencyObject dependencyObject)
        {
            return (ICommand)dependencyObject.GetValue(PreviewDragOverEvent);
        }
    }
}
