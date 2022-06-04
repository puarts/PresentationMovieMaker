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
    public class ExecuteCommandOnRoutedEvent
    {
        private readonly RoutedEvent routed;

        private DependencyProperty? property;

        public ExecuteCommandOnRoutedEvent(RoutedEvent @event)
        {
            routed = @event;
        }

        private void ManageEventHandlers(DependencyObject sender, object oldValue, object newValue)
        {
            var element = sender as UIElement;

            if (element == null)
            {
                return;
            }

            if (oldValue != null)
            {
                element.RemoveHandler(routed, new RoutedEventHandler(CommandEventHandler));
            }

            if (newValue != null)
            {
                element.AddHandler(routed, new RoutedEventHandler(CommandEventHandler));
            }
        }

        private void CommandEventHandler(object sender, RoutedEventArgs e)
        {
            var dp = sender as DependencyObject;
            if (dp == null)
            {
                return;
            }

            var command = dp.GetValue(property) as ICommand;

            if (command == null)
            {
                return;
            }

            if (command.CanExecute(e))
            {
                command.Execute(e);
            }
        }

        public void PropertyChangedHandler(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            property = e.Property;
            var oldValue = e.OldValue;
            var newValue = e.NewValue;
            ManageEventHandlers(sender, oldValue, newValue);
        }
    }

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
