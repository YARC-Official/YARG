using System;

namespace YARG.Input
{
    public class DebounceTimer<T>
    {
        public const long DEBOUNCE_TIME_MAX = 25;

        // double.MinValue is used to simplify HasElapsed,
        // as it should result in true when not enabled
        private double _startTime = double.MinValue;
        private double _timeThreshold = 0;
        private T _postDebounceValue;

        /// <summary>
        /// The debounce time threshold, in milliseconds. Use 0 or less to disable debounce.
        /// </summary>
        public long TimeThreshold
        {
            get => (long) (_timeThreshold * 1000);
            // Limit debounce amount to 0-25 ms
            // Any larger and input registration will be very bad, the max will limit to 40 inputs per second
            // If someone needs a larger amount their controller is just busted lol
            set => _timeThreshold = Math.Clamp(value, 0, DEBOUNCE_TIME_MAX) / 1000.0;
        }

        public bool IsEnabled => _timeThreshold > 0;
        public bool IsRunning => _startTime != double.MinValue;

        public void Start(double time)
        {
            if (!IsEnabled)
                return;

            _startTime = time;
        }

        public T Stop()
        {
            _startTime = double.MinValue;
            return _postDebounceValue;
        }

        public T Restart(double time)
        {
            var value = Stop();
            Start(time);
            return value;
        }

        public bool HasElapsed(double time)
            => time >= (_startTime + _timeThreshold);

        public void UpdateValue(T value)
        {
            _postDebounceValue = value;
        }
    }
}