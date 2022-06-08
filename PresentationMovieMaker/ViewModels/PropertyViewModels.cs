using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.ViewModels
{
    public interface IPropertyViewModel
    {
        string Name { get; }
        Type ValueType { get; }
    }

    public interface IPropertyViewModel<T>
    {
        string Name { get; }

        T Value { get; set; }
    }

    public class PropertyViewModel<T> : ReactiveProperty<T>, IPropertyViewModel<T>, IPropertyViewModel
    {
        public PropertyViewModel(string name)
        {
            Name = name;
        }

        public PropertyViewModel(string name, T value)
            : base(value)
        {
            Name = name;
        }

        public string Name { get; } = string.Empty;
        public Type ValueType { get => typeof(T); }
    }

    public class PathPropertyViewModel : PropertyViewModel<PathViewModel>
    {
        public PathPropertyViewModel(string name)
            : base(name, new PathViewModel())
        {
        }
    }

    public class StringPropertyViewModel : PropertyViewModel<string>
    {
        public StringPropertyViewModel(string name)
            : base(name)
        {
        }
    }

    public class IntPropertyViewModel : PropertyViewModel<int>
    {
        public IntPropertyViewModel(string name)
            : base(name)
        {
        }
    }

    public class DoublePropertyViewModel : PropertyViewModel<double>
    {
        public DoublePropertyViewModel(string name)
            : base(name)
        {
        }
    }
    public class BoolPropertyViewModel : PropertyViewModel<bool>
    {
        public BoolPropertyViewModel(string name)
            : base(name)
        {
        }
    }
}
