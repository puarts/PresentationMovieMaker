using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PresentationMovieMaker.Views
{
    public static class SelectionChangedBehavior
    {
        private static readonly DependencyProperty SelectionChangedEvent =
         DependencyProperty.RegisterAttached(
          "SelectionChangedEvent",
          typeof(ICommand),
          typeof(SelectionChangedBehavior),
          new PropertyMetadata(null, new ExecuteCommandOnRoutedEvent(Selector.SelectionChangedEvent).PropertyChangedHandler));

        public static void SetSelectionChangedEvent(DependencyObject dependencyObject, ICommand value)
        {
            dependencyObject.SetValue(SelectionChangedEvent, value);
        }

        public static ICommand GetSelectionChangedEvent(DependencyObject dependencyObject)
        {
            return (ICommand)dependencyObject.GetValue(SelectionChangedEvent);
        }
    }
}
