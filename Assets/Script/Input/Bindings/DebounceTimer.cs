using System;
using System.Diagnostics;

namespace YARG.Input
{
    public class DebounceTimer
    {
        public const long DEBOUNCE_TIME_MAX = 25;

        private Stopwatch _timer = new();
        private long _timeThreshold = 0;
        private float _postDebounceValue;

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

        public bool Enabled => TimeThreshold > 0;
        public bool HasElapsed => !_timer.IsRunning || _timer.ElapsedMilliseconds >= TimeThreshold;

        public float Value { get; private set; }

        public void Start()
        {
            if (!Enabled)
                return;

            _timer.Start();
        }

        public void Reset()
        {
            _timer.Reset();
            Value = _postDebounceValue;
        }

        public void Restart()
        {
            Reset();
            Start();
        }

        public void Update(float value)
        {
            _postDebounceValue = value;
        }
    }
}