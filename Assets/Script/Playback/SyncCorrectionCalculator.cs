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

        // Number of milliseconds over which we aim to recover from a timing error.
        // For example, recovering 10 ms over 100 ms requires running 10% faster.
        private const float CORRECTION_TIME_MS = 100f;

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
        /// Calculates the playback-speed change needed to reduce the current sync error.
        /// </summary>
        /// <param name="syncDeltaSeconds">How far playback is behind or ahead, in seconds.</param>
        /// <param name="elapsedMs">Time since the previous calculation, in milliseconds.</param>
        /// <param name="streamDelayMs">Time before a speed change reaches audio output, in milliseconds.</param>
        /// <returns>Speed adjustment to apply, where 0.1 means 10% faster.</returns>
        public float CalculateAdjustment(double syncDeltaSeconds, double elapsedMs, double streamDelayMs)
        {
            streamDelayMs = Math.Max(1.0, streamDelayMs);
            elapsedMs = Math.Clamp(elapsedMs, 1.0, 100.0);

            RecordAdjustment(elapsedMs, streamDelayMs);

            double errorMs = syncDeltaSeconds * 1000.0 - _syncHistory.RunningContributionMs;
            float adjustment = 0f;
            if (Math.Abs(errorMs) >= SYNC_DEADBAND_SECONDS * 1000.0)
            {
                float gain = Math.Clamp((float) streamDelayMs / CORRECTION_TIME_MS, MIN_GAIN, MAX_GAIN);
                float correctionMs = (float) errorMs * gain;
                adjustment = Math.Clamp(correctionMs / (float) streamDelayMs, -SYNC_CLAMP, SYNC_CLAMP);
            }

            if (adjustment != 0f && Math.Abs(adjustment - _currentAdjustment) < SPEED_UPDATE_THRESHOLD)
            {
                return _currentAdjustment;
            }

            _currentAdjustment = adjustment;
            return _currentAdjustment;
        }

        /// <summary>
        /// Records the current speed adjustment but requests no new sync correction.
        /// </summary>
        /// <param name="elapsedMs">Time since the previous calculation, in milliseconds.</param>
        /// <param name="streamDelayMs">Time before a speed change reaches audio output, in milliseconds.</param>
        /// <returns>Zero, to disable sync correction.</returns>
        public float SuppressAdjustment(double elapsedMs, double streamDelayMs)
        {
            streamDelayMs = Math.Max(1.0, streamDelayMs);
            elapsedMs = Math.Clamp(elapsedMs, 1.0, 100.0);

            RecordAdjustment(elapsedMs, streamDelayMs);
            _currentAdjustment = 0f;
            return _currentAdjustment;
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

        private void RecordAdjustment(double elapsedMs, double streamDelayMs)
        {
            _syncHistory.Add(elapsedMs, _currentAdjustment * elapsedMs);
            _syncHistory.TrimToDuration(streamDelayMs);
        }

        /// <summary>
        /// Remembers recent playback-speed adjustments while they pass through the buffered audio
        /// stream. Each entry records how long an adjustment was active and how much timing error it
        /// should correct. The running total represents correction already requested but not yet
        /// reflected in playback, preventing the sync controller from correcting the same error
        /// again. History older than the current stream delay is removed, including part of an entry
        /// when the cutoff falls between updates.
        /// </summary>
        private sealed class SyncHistoryBuffer
        {
            // 500 entries cover the maximum 5000 ms delay at the 10 ms update cadence.
            private readonly RingBuffer<Entry> _entries = new(5012);
            private double _runningDurationMs;

            public double RunningContributionMs { get; private set; }
            public float EffectiveAdjustment { get; private set; }

            public void Clear()
            {
                _entries.Clear();
                _runningDurationMs = 0.0;
                RunningContributionMs = 0.0;
                EffectiveAdjustment = 0f;
            }

            public void Add(double durationMs, double contributionMs)
            {
                _entries.Add(new Entry(durationMs, contributionMs));
                _runningDurationMs += durationMs;
                RunningContributionMs += contributionMs;
            }

            public void TrimToDuration(double targetDurationMs)
            {
                targetDurationMs = Math.Max(1.0, targetDurationMs);

                while (_entries.Count > 0 && _runningDurationMs > targetDurationMs)
                {
                    double excessMs = _runningDurationMs - targetDurationMs;
                    var oldest = _entries[0];

                    if (oldest.DurationMs <= excessMs)
                    {
                        _entries.RemoveOldest();
                        _runningDurationMs -= oldest.DurationMs;
                        RunningContributionMs -= oldest.ContributionMs;
                        continue;
                    }

                    double removedRatio = excessMs / oldest.DurationMs;
                    double removedContributionMs = oldest.ContributionMs * removedRatio;

                    _entries[0] = new Entry(
                        oldest.DurationMs - excessMs,
                        oldest.ContributionMs - removedContributionMs);

                    _runningDurationMs = targetDurationMs;
                    RunningContributionMs -= removedContributionMs;
                }

                // Oldest retained adjustment is reaching output now. Until history spans the
                // stream delay, requested changes have not reached output yet.
                EffectiveAdjustment = _entries.Count > 0 && _runningDurationMs >= targetDurationMs
                    ? (float) (_entries[0].ContributionMs / _entries[0].DurationMs)
                    : 0f;
            }

            private readonly struct Entry
            {
                public readonly double DurationMs;
                public readonly double ContributionMs;

                public Entry(double durationMs, double contributionMs)
                {
                    DurationMs = durationMs;
                    ContributionMs = contributionMs;
                }
            }
        }
    }
}
