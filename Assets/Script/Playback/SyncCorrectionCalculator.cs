using System;

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
    // stream delay, converted into a playback-rate adjustment, and capped by SYNC_CLAMP. Callers
    // feed the adjustment applied during each elapsed frame back into the next calculation, which
    // keeps the pending-correction history aligned with what playback actually received.
    internal sealed class SyncCorrectionCalculator
    {
        // Ignore sync errors smaller than 1.5 ms to avoid reacting to tiny clock fluctuations.
        private const double SYNC_DEADBAND_SECONDS = 0.0015;

        // Number of milliseconds over which we aim to recover from a timing error.
        // For example, recovering 10 ms over 100 ms requires running 10% faster.
        private const float CORRECTION_TIME_MS = 100f;

        // Correct at least 40% of the remaining error, even with a short stream delay.
        private const float MIN_GAIN = 0.4f;

        // Correct at most 85% of the remaining error to reduce overshoot.
        private const float MAX_GAIN = 0.85f;

        // Never adjust playback speed by more than 50% in either direction.
        private const float SYNC_CLAMP = 0.50f;

        private readonly SyncHistoryBuffer _syncHistory = new();

        /// <summary>
        /// Calculates the playback-speed change needed to reduce the current sync error.
        /// </summary>
        /// <param name="syncDeltaSeconds">How far playback is behind or ahead, in seconds.</param>
        /// <param name="elapsedMs">Time since the previous calculation, in milliseconds.</param>
        /// <param name="streamDelayMs">Time before a speed change reaches audio output, in milliseconds.</param>
        /// <param name="appliedAdjustment">Speed adjustment used during the elapsed time.</param>
        /// <returns>Speed adjustment to apply, where 0.1 means 10% faster.</returns>
        public float CalculateAdjustment(double syncDeltaSeconds, double elapsedMs, double streamDelayMs,
            float appliedAdjustment)
        {
            streamDelayMs = Math.Max(1.0, streamDelayMs);
            elapsedMs = Math.Clamp(elapsedMs, 1.0, 100.0);

            RecordAdjustment(elapsedMs, appliedAdjustment, streamDelayMs);

            double errorMs = syncDeltaSeconds * 1000.0 - _syncHistory.RunningContributionMs;
            return CalculateRateAdjustment(errorMs, streamDelayMs);
        }

        /// <summary>
        /// Records the current speed adjustment but requests no new sync correction.
        /// </summary>
        /// <param name="elapsedMs">Time since the previous calculation, in milliseconds.</param>
        /// <param name="streamDelayMs">Time before a speed change reaches audio output, in milliseconds.</param>
        /// <param name="appliedAdjustment">Speed adjustment used during the elapsed time.</param>
        /// <returns>Zero, to disable sync correction.</returns>
        public float SuppressAdjustment(double elapsedMs, double streamDelayMs, float appliedAdjustment)
        {
            streamDelayMs = Math.Max(1.0, streamDelayMs);
            elapsedMs = Math.Clamp(elapsedMs, 1.0, 100.0);

            RecordAdjustment(elapsedMs, appliedAdjustment, streamDelayMs);
            return 0f;
        }

        /// <summary>
        /// Clears remembered speed corrections. Call when playback restarts, seeks, or changes speed
        /// so corrections from the previous playback state do not affect the new one.
        /// </summary>
        public void Reset()
        {
            _syncHistory.Clear();
        }

        private static float CalculateRateAdjustment(double errorMs, double streamDelayMs)
        {
            if (Math.Abs(errorMs) < SYNC_DEADBAND_SECONDS * 1000.0)
            {
                return 0f;
            }

            float gain = Math.Clamp((float) streamDelayMs / CORRECTION_TIME_MS, MIN_GAIN, MAX_GAIN);
            float correctionMs = (float) errorMs * gain;

            return Math.Clamp(correctionMs / (float) streamDelayMs, -SYNC_CLAMP, SYNC_CLAMP);
        }

        private void RecordAdjustment(double elapsedMs, float adjustment, double streamDelayMs)
        {
            _syncHistory.Add(elapsedMs, adjustment * elapsedMs);
            _syncHistory.TrimToDuration(streamDelayMs);
        }

        private sealed class SyncHistoryBuffer
        {
            private const int CAPACITY = 4096;

            private readonly Entry[] _entries = new Entry[CAPACITY];
            private int _start;
            private int _count;
            private double _runningDurationMs;
            private double _runningContributionMs;

            public double RunningContributionMs => _runningContributionMs;

            public void Clear()
            {
                _start = 0;
                _count = 0;
                _runningDurationMs = 0.0;
                _runningContributionMs = 0.0;
            }

            public void Add(double durationMs, double contributionMs)
            {
                if (_count == _entries.Length)
                {
                    RemoveOldest();
                }

                _entries[GetIndex(_count)] = new Entry(durationMs, contributionMs);
                _count++;
                _runningDurationMs += durationMs;
                _runningContributionMs += contributionMs;
            }

            public void TrimToDuration(double targetDurationMs)
            {
                targetDurationMs = Math.Max(1.0, targetDurationMs);

                while (_count > 0 && _runningDurationMs > targetDurationMs)
                {
                    double excessDurationMs = _runningDurationMs - targetDurationMs;
                    ref Entry oldest = ref _entries[_start];
                    if (oldest.DurationMs <= excessDurationMs)
                    {
                        RemoveOldest();
                        continue;
                    }

                    double retainedDurationMs = oldest.DurationMs - excessDurationMs;
                    double removedRatio = excessDurationMs / oldest.DurationMs;
                    double removedContributionMs = oldest.ContributionMs * removedRatio;

                    oldest.DurationMs = retainedDurationMs;
                    oldest.ContributionMs -= removedContributionMs;
                    _runningDurationMs = targetDurationMs;
                    _runningContributionMs -= removedContributionMs;
                }
            }

            private void RemoveOldest()
            {
                Entry oldest = _entries[_start];
                _start = GetIndex(1);
                _count--;
                _runningDurationMs -= oldest.DurationMs;
                _runningContributionMs -= oldest.ContributionMs;
            }

            private int GetIndex(int offset) => (_start + offset) % _entries.Length;

            private struct Entry
            {
                public double DurationMs;
                public double ContributionMs;

                public Entry(double durationMs, double contributionMs)
                {
                    DurationMs = durationMs;
                    ContributionMs = contributionMs;
                }
            }
        }
    }
}
