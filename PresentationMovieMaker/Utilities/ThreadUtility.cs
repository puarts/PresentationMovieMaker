using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PresentationMovieMaker.Utilities
{
    public static class ThreadUtility
    {
        public static void Wait(int waitMilliseconds, CancellationToken? ct, int intervalMilliseconds = 10, Action? callback = null)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (stopwatch.ElapsedMilliseconds < waitMilliseconds)
            {
                callback?.Invoke();
                ct?.ThrowIfCancellationRequested();
                Thread.Sleep(intervalMilliseconds);
            }
        }
    }
}
