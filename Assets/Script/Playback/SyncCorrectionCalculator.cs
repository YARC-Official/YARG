using System;
using YARG.Helpers;

namespace YARG.Playback
{
    // Keeps playback synchronized by turning the difference between the expected and actual song
    // position into a temporary playback-speed adjustment. Speed changes do not take effect
    // immediately: they must first pass through the buffered tempo stream. The stream delay is the
    // time between sending a speed change and that change reaching playback. Each adjustment is
    // therefore recorded for that length of time, along with how much timing error it should
    // correct. That pending correction is subtracted from the latest measured error so the
    // controller does not repeatedly react to work that is already in progress.
    //
    // In simplified terms:
    //   pending correction = sum(applied speed adjustment * elapsed time)
    //   remaining error = measured timing error - pending correction
    //   new speed adjustment = remaining error * gain / stream delay
    // For example, running 10% faster for 20 ms contributes 2 ms of pending correction.
    //
    // Once pending work is removed, errors below the deadband are ignored to prevent constant
    // speed changes from tiny clock fluctuations. Larger errors are multiplied by a gain based on
    // stream delay, converted into a playback-rate adjustment, and capped by SYNC_CLAMP. Tiny
    // changes to the requested adjustment are retained until they cross the update threshold,
    // avoiding needless tempo-stream updates while keeping pending-correction history aligned with
    // the adjustment requested from playback.
    internal sealed class SyncCorrectionCalculator
    {
        // Ignore sync errors smaller than 1.5 ms to avoid reacting to tiny clock fluctuations.
        private const double SYNC_DEADBAND_SECONDS = 0.0015;

        // Number of seconds over which we aim to recover from a timing error.
        // For example, recovering 10 ms over 100 ms requires running 10% faster.
        private const float CORRECTION_TIME_SECONDS = 0.1f;

        private const double MIN_LATENCY_SECONDS = 0.001;

        // Correct at least 40% of the remaining error, even with a short stream delay.
        private const float MIN_GAIN = 0.4f;

        // Correct at most 85% of the remaining error to reduce overshoot.
        private const float MAX_GAIN = 0.85f;

        // Never adjust playback speed by more than 50% in either direction.
        private const float SYNC_CLAMP = 0.50f;

        private const float SPEED_UPDATE_THRESHOLD = 0.0005f;

        private readonly SyncHistoryBuffer _syncHistory = new();
        private float _currentAdjustment;

        public float EffectiveAdjustment => _syncHistory.EffectiveAdjustment;

        /// <summary>
        /// Updates buffered correction history and returns the playback-speed change to request.
        /// </summary>
        /// <param name="syncDeltaSeconds">How far playback is behind or ahead, in seconds.</param>
        /// <param name="currentTime">Current input-clock time, in seconds.</param>
        /// <param name="latency">Time before a speed change reaches audio output, in seconds.</param>
        /// <param name="allowCorrection">Whether a new sync correction may be requested.</param>
        /// <returns>Speed adjustment to apply, where 0.1 means 10% faster.</returns>
        public float Update(double syncDeltaSeconds, double currentTime, double latency, bool allowCorrection)
        {
            latency = Math.Max(MIN_LATENCY_SECONDS, latency);
            _syncHistory.Update(currentTime, latency, _currentAdjustment);

            float adjustment = allowCorrection ? CalculateAdjustment(syncDeltaSeconds, latency) : 0f;
            if (allowCorrection && adjustment != 0f &&
                Math.Abs(adjustment - _currentAdjustment) < SPEED_UPDATE_THRESHOLD)
            {
                return _currentAdjustment;
            }

            _currentAdjustment = adjustment;
            _syncHistory.RecordChange(currentTime, _currentAdjustment);
            return _currentAdjustment;
        }

        private float CalculateAdjustment(double syncDeltaSeconds, double latency)
        {
            double errorSeconds = syncDeltaSeconds - _syncHistory.RunningContributionSeconds;
            if (Math.Abs(errorSeconds) < SYNC_DEADBAND_SECONDS)
            {
                return 0f;
            }

            float gain = Math.Clamp((float) latency / CORRECTION_TIME_SECONDS, MIN_GAIN, MAX_GAIN);
            float correctionSeconds = (float) errorSeconds * gain;
            return Math.Clamp(correctionSeconds / (float) latency, -SYNC_CLAMP, SYNC_CLAMP);
        }

        /// <summary>
        /// Clears remembered speed corrections. Call when playback restarts, seeks, or changes speed
        /// so corrections from the previous playback state do not affect the new one.
        /// </summary>
        public void Reset()
        {
            _syncHistory.Clear();
            _currentAdjustment = 0f;
        }

        /// <summary>
        /// Remembers when requested playback-speed adjustments change while they pass through the
        /// buffered audio stream. Pending correction is the integral of those adjustments over the
        /// stream-delay window. Absolute timestamps make history independent of update cadence.
        /// </summary>
        private sealed class SyncHistoryBuffer
        {
            // Covers five seconds even if the requested adjustment changes every millisecond.
            private readonly RingBuffer<Entry> _entries = new(5012);
            private double _historyStartTime = double.NaN;

            public double RunningContributionSeconds { get; private set; }
            public float EffectiveAdjustment { get; private set; }

            public void Clear()
            {
                _entries.Clear();
                _historyStartTime = double.NaN;
                RunningContributionSeconds = 0.0;
                EffectiveAdjustment = 0f;
            }

            public void RecordChange(double timestamp, float adjustment)
            {
                if (_entries.Count == 0)
                {
                    _historyStartTime = timestamp;
                    _entries.Add(new Entry(timestamp, adjustment));
                    return;
                }

                int latestIndex = _entries.Count - 1;
                var latest = _entries[latestIndex];
                if (latest.Timestamp == timestamp)
                {
                    _entries[latestIndex] = new Entry(timestamp, adjustment);
                }
                else if (latest.Adjustment != adjustment)
                {
                    _entries.Add(new Entry(timestamp, adjustment));
                }
            }

            public void Update(double currentTime, double latency, float currentAdjustment)
            {
                if (_entries.Count == 0)
                {
                    RecordChange(currentTime, currentAdjustment);
                }

                double cutoffTime = currentTime - latency;
                while (_entries.Count > 1 && _entries[1].Timestamp <= cutoffTime)
                {
                    _entries.RemoveOldest();
                }

                bool historyReachesCutoff = _historyStartTime <= cutoffTime;
                EffectiveAdjustment = historyReachesCutoff ? _entries[0].Adjustment : 0f;

                double intervalStart = Math.Max(cutoffTime, _historyStartTime);
                float adjustment = _entries[0].Adjustment;
                double contributionSeconds = 0.0;
                for (int i = 1; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    if (entry.Timestamp > intervalStart)
                    {
                        contributionSeconds += adjustment * (entry.Timestamp - intervalStart);
                    }

                    intervalStart = Math.Max(intervalStart, entry.Timestamp);
                    adjustment = entry.Adjustment;
                }

                contributionSeconds += adjustment * (currentTime - intervalStart);
                RunningContributionSeconds = contributionSeconds;
            }

            private readonly struct Entry
            {
                public readonly double Timestamp;
                public readonly float Adjustment;

                public Entry(double timestamp, float adjustment)
                {
                    Timestamp = timestamp;
                    Adjustment = adjustment;
                }
            }
        }
    }
}
