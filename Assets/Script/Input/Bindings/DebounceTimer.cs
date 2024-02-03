using System;
using System.Diagnostics;

namespace YARG.Input
{
    public class DebounceTimer<T>
    {
        public const long DEBOUNCE_TIME_MAX = 25;

        private Stopwatch _timer = new();
        private long _timeThreshold = 0;
        private T _postDebounceValue;

        /// <summary>
        /// The debounce time threshold, in milliseconds. Use 0 or less to disable debounce.
        /// </summary>
        public long TimeThreshold
        {
            get => _timeThreshold;
            // Limit debounce amount to 0-25 ms
            // Any larger and input registration will be very bad, the max will limit to 40 inputs per second
            // If someone needs a larger amount their controller is just busted lol
            set => _timeThreshold = Math.Clamp(value, 0, DEBOUNCE_TIME_MAX);
        }

        public bool IsEnabled => _timeThreshold > 0;
        public bool IsRunning => _timer.IsRunning;
        public bool HasElapsed => !_timer.IsRunning || _timer.ElapsedMilliseconds >= _timeThreshold;

        public void Start()
        {
            if (!IsEnabled)
                return;

            _timer.Start();
        }

        public T Stop()
        {
            _timer.Reset();
            return _postDebounceValue;
        }

        public T Restart()
        {
            var value = Stop();
            Start();
            return value;
        }

        public void Update(T value)
        {
            _postDebounceValue = value;
        }
    }
}