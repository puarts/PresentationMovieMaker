using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.ViewModels
{
    public class MediaElementViewModel
    {
        public ReactiveProperty<string> ImagePath { get; } = new ReactiveProperty<string>();

        public ReactiveProperty<double> RotationAngle { get; } = new ReactiveProperty<double>();

        public ReactiveProperty<double> Opacity { get; } = new ReactiveProperty<double>(1.0);

        public ReactiveProperty<bool> Visiibility { get; } = new ReactiveProperty<bool>(false);
    }
}
