using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.ViewModels
{
    public abstract class CompositeDisposableBase : INotifyPropertyChanged
    {
        protected CompositeDisposable Disposable { get; } = new CompositeDisposable();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Subscribe<T>(IObservable<T> observable, Action<T> onNext)
        {
            observable.Subscribe(onNext).AddTo(Disposable);
        }

        protected void Subscribe(ReactiveCommand observable, Action onNext)
        {
            observable.Subscribe(onNext).AddTo(Disposable);
        }

        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = "")
            where T : IEquatable<T>
        {
            if (field.Equals(newValue))
            {
                return false;
            }

            field = newValue;
            NotifyPropertyChanged(propertyName);
            return true;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
