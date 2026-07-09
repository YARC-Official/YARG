using System;

namespace YARG.Playback
{
    // Syncs audio to the target song time within +/-SYNC_DEADBAND_SECONDS (+/-1.5 ms).
    // Positive drift means audio is behind the target, so speed up. Negative drift means slow down.
    // Drift inside the deadband is ignored to avoid tiny speed changes and jitter.
    // Error = clock delta minus correction already applied in recent stream window.
    // Adjustment = proportional playback-rate nudge to erase remaining error over streamDelayMs, clamped.
    // recentCorrectionMs = integrated timing gain/loss from adjustments over last streamDelayMs.
    internal sealed class SyncCorrectionCalculator
    {
        // Ignore drift smaller than 1.5 ms.  Prevents constant tempo changes, which harm audio quality
        private const double SYNC_DEADBAND_SECONDS = 0.0015;

        // Delay where correction gain reaches MAX_GAIN.
        private const float GAIN_DELAY_MS = 100f;

        // Correction gain bounds. Gain is fraction of remaining error corrected per sample.
        private const float MIN_GAIN = 0.4f;
        private const float MAX_GAIN = 0.85f;

        // Max timing correction speed. Caps large errorMs spikes before converting to playback-rate correction.
        private const float MAX_SLEW_MS_PER_SEC = 2500f;

        // Max playback-rate correction. 0.10 means +/-10% speed delta.
        // Caps playback-rate correction so large errorMs spikes cannot cause wild speed changes.
        private const float SYNC_CLAMP = 0.50f;

        private readonly SyncHistoryBuffer _syncHistory = new();

        /// <summary>
        /// Returns speed change adjustment needed to reduce drift, excluding adjustments not yet reflected in
        /// mixer position due to latency.
        /// </summary>
        /// <param name="syncDeltaSeconds">Target audio time minus mixer position, in seconds.</param>
        /// <param name="elapsedMs">Time since previous sync sample, in milliseconds.</param>
        /// <param name="streamDelayMs">Mixer latency window, in milliseconds.</param>
        public float CalculateAdjustment(
            double syncDeltaSeconds,
            double elapsedMs,
            double streamDelayMs)
        {
            _syncHistory.TrimToDuration(streamDelayMs);
            float adjustment = CalculateTargetAdjustment(syncDeltaSeconds, elapsedMs, streamDelayMs);
            RecordHistory(elapsedMs, adjustment, streamDelayMs);
            return adjustment;
        }

        /// <summary>
        /// Outputs zero correction while recent timeline changes are still moving through the mixer latency window.
        /// Measured drift may reflect stale audio state during this time, but correction history still needs to age.
        /// </summary>
        /// <param name="elapsedMs">Time since previous sync sample, in milliseconds.</param>
        /// <param name="streamDelayMs">Mixer latency window, in milliseconds.</param>
        public float SuppressAdjustment(double elapsedMs, double streamDelayMs)
        {
            _syncHistory.TrimToDuration(streamDelayMs);
            RecordHistory(elapsedMs, 0f, streamDelayMs);
            return 0f;
        }

        /// <summary>
        /// Clears recent correction history after discontinuous timeline changes, such as song reset or seek.
        /// Old playback-rate adjustments no longer describe pending mixer latency after the timeline jumps.
        /// </summary>
        public void Reset()
        {
            _syncHistory.Clear();
        }

        private float CalculateTargetAdjustment(double syncDeltaSeconds, double elapsedMs, double streamDelayMs)
        {
            double errorMs = (syncDeltaSeconds * 1000.0) - _syncHistory.RunningContributionMs;
            bool withinDeadband = Math.Abs(errorMs) < SYNC_DEADBAND_SECONDS * 1000.0;
            if (withinDeadband || elapsedMs <= 0.0 || streamDelayMs <= 0.0)
            {
                return 0f;
            }

            float stepMs = CalculateSyncStepMs((float) errorMs, (float) streamDelayMs);
            float targetAdjustment = stepMs / (float) streamDelayMs;
            return Math.Clamp(targetAdjustment, -SYNC_CLAMP, SYNC_CLAMP);
        }

        private static float CalculateSyncStepMs(float errorMs, float streamDelayMs)
        {
            float gain = Math.Clamp(streamDelayMs / GAIN_DELAY_MS, MIN_GAIN, MAX_GAIN);
            float stepMs = errorMs * gain;

            float maxStepMs = MAX_SLEW_MS_PER_SEC * streamDelayMs / 1000f;
            return Math.Clamp(stepMs, -maxStepMs, maxStepMs);
        }

        private void RecordHistory(double elapsedMs, float targetAdjustment, double streamDelayMs)
        {
            double contributionMs = targetAdjustment * elapsedMs;

            _syncHistory.Add(elapsedMs, contributionMs);
            _syncHistory.TrimToDuration(streamDelayMs);
        }

        private sealed class SyncHistoryBuffer
        {
            private const int SYNC_HISTORY_CAPACITY = 4096;

            private readonly SyncHistoryEntry[] _entries = new SyncHistoryEntry[SYNC_HISTORY_CAPACITY];
            private int _start;
            private int _count;
            private double _runningContributionMs;
            private double _runningDurationMs;

            public double RunningContributionMs => _runningContributionMs;

            public void Clear()
            {
                _start = 0;
                _count = 0;
                _runningContributionMs = 0.0;
                _runningDurationMs = 0.0;
            }

            public void Add(double durationMs, double contributionMs)
            {
                if (_count == _entries.Length)
                {
                    RemoveOldestEntry(_entries[_start]);
                }

                int index = GetIndex(_count);
                _entries[index] = new SyncHistoryEntry(durationMs, contributionMs);
                _count++;
                _runningContributionMs += contributionMs;
                _runningDurationMs += durationMs;
            }

            public void TrimToDuration(double targetDurationMs)
            {
                while (_runningDurationMs > targetDurationMs && _count > 0)
                {
                    double excessDurationMs = _runningDurationMs - targetDurationMs;
                    ref var oldest = ref _entries[_start];
                    if (oldest.DurationMs <= excessDurationMs)
                    {
                        RemoveOldestEntry(oldest);
                        continue;
                    }

                    double remainingDurationMs = oldest.DurationMs - excessDurationMs;
                    double consumedRatio = excessDurationMs / oldest.DurationMs;
                    double removedContributionMs = oldest.ContributionMs * consumedRatio;

                    oldest.DurationMs = remainingDurationMs;
                    oldest.ContributionMs -= removedContributionMs;
                    _runningDurationMs = targetDurationMs;
                    _runningContributionMs -= removedContributionMs;
                }
            }

            private int GetIndex(int offset)
            {
                return (_start + offset) % _entries.Length;
            }

            private void RemoveOldestEntry(SyncHistoryEntry oldest)
            {
                _start = GetIndex(1);
                _count--;
                _runningDurationMs -= oldest.DurationMs;
                _runningContributionMs -= oldest.ContributionMs;
            }

            private struct SyncHistoryEntry
            {
                public double DurationMs;
                public double ContributionMs;

                public SyncHistoryEntry(double durationMs, double contributionMs)
                {
                    DurationMs = durationMs;
                    ContributionMs = contributionMs;
                }
            }
        }
    }
}
