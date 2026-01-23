using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;

namespace YARG.Helpers
{
    public class Timer
    {
        private CancellationTokenSource _cts;
        private bool                    IsRunning => _cts != null;

        public void Start(float intervalSeconds, Action onTick)
        {
            if (IsRunning)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            UniTaskAsyncEnumerable.Timer(
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(intervalSeconds)
                )
                .ForEachAsync(_ => onTick?.Invoke(), _cts.Token)
                .Forget();
        }

        public void Stop()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}