using System;

namespace YARG.Playback
{
    // Speed corrections are delayed by the tempo stream. Keep the corrections sent during
    // that delay in the error calculation so the controller does not react to its own stale work.
    internal sealed class SyncCorrectionCalculator
    {
        private const double SYNC_DEADBAND_SECONDS = 0.0015;

        // Correct a larger fraction of the error as the stream delay grows, without making
        // short-delay corrections unnecessarily aggressive.
        private const float GAIN_DELAY_MS = 100f;
        private const float MIN_GAIN = 0.4f;
        private const float MAX_GAIN = 0.85f;

        // Limit the correction represented by one latency window.
        private const float MAX_SLEW_MS_PER_SEC = 2500f;
        private const float SYNC_CLAMP = 0.50f;

        private readonly SyncHistoryBuffer _syncHistory = new();

        public float CalculateAdjustment(double syncDeltaSeconds, double elapsedMs, double streamDelayMs,
            float appliedAdjustment)
        {
            streamDelayMs = Math.Max(1.0, streamDelayMs);
            elapsedMs = Math.Clamp(elapsedMs, 1.0, 100.0);

            RecordAdjustment(elapsedMs, appliedAdjustment, streamDelayMs);

            double errorMs = syncDeltaSeconds * 1000.0 - _syncHistory.RunningContributionMs;
            return CalculateRateAdjustment(errorMs, streamDelayMs);
        }

        public float SuppressAdjustment(double elapsedMs, double streamDelayMs, float appliedAdjustment)
        {
            streamDelayMs = Math.Max(1.0, streamDelayMs);
            elapsedMs = Math.Clamp(elapsedMs, 1.0, 100.0);

            RecordAdjustment(elapsedMs, appliedAdjustment, streamDelayMs);
            return 0f;
        }

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

            float gain = Math.Clamp((float) streamDelayMs / GAIN_DELAY_MS, MIN_GAIN, MAX_GAIN);
            float correctionMs = (float) errorMs * gain;
            float maxCorrectionMs = MAX_SLEW_MS_PER_SEC * (float) streamDelayMs / 1000f;
            correctionMs = Math.Clamp(correctionMs, -maxCorrectionMs, maxCorrectionMs);

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
