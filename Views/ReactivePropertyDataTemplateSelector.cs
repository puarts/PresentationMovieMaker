using PresentationMovieMaker.ViewModels;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PresentationMovieMaker.Views
{
    public class ReactivePropertyDataTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is IPropertyViewModel prop)
            {
                if (prop.ValueType == typeof(string))
                {
                    var property = (PropertyViewModel<string>)prop;
                }
            }


            return null;
        }
    }
}
