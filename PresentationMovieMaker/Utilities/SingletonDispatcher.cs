using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PresentationMovieMaker.Utilities
{
    public class SingletonDispatcher
    {
        private static Dispatcher? _dispatcher;
        public static SingletonDispatcher Instance { get; } = new SingletonDispatcher();

        public static void Initialize()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        public static void Invoke(Action callback)
        {
            if (_dispatcher is null)
            {
                throw new Exception($"{nameof(SingletonDispatcher)} is not initialized");
            }

            _dispatcher.Invoke(callback);
        }

        public static DispatcherOperation InvokeAsync(Action callback)
        {
            if (_dispatcher is null)
            {
                throw new Exception($"{nameof(SingletonDispatcher)} is not initialized");
            }
            return _dispatcher.InvokeAsync(callback);
        }
    }
}
