using System;
using System.Collections.Generic;

namespace YARG.Playback
{
    internal sealed class SyncCorrectionCalculator
    {
        private const double SYNC_DEADBAND_SECONDS = 0.0015;
        private const float SYNC_GAIN = 0.3f;
        private const float SYNC_CLAMP = 0.10f;

        private readonly LinkedList<(double DurationMs, double ContributionMs)> _syncHistory = new();
        private double _syncHistoryRunningSum;
        private double _syncHistoryRunningDurationMs;

        public float CalculateAdjustment(
            double syncDeltaSeconds,
            double elapsedMs,
            double streamDelayMs,
            bool correctionIsSuppressed)
        {
            streamDelayMs = Math.Max(1.0, streamDelayMs);

            TrimHistory(streamDelayMs);

            float adjustment = CalculateTargetAdjustment(syncDeltaSeconds, streamDelayMs, correctionIsSuppressed);
            RecordHistory(elapsedMs, adjustment, streamDelayMs);
            return adjustment;
        }

        public void Reset()
        {
            _syncHistory.Clear();
            _syncHistoryRunningSum = 0.0;
            _syncHistoryRunningDurationMs = 0.0;
        }

        private float CalculateTargetAdjustment(
            double syncDeltaSeconds,
            double streamDelayMs,
            bool correctionIsSuppressed)
        {
            bool withinDeadband = Math.Abs(syncDeltaSeconds) < SYNC_DEADBAND_SECONDS;
            if (correctionIsSuppressed || withinDeadband)
            {
                return 0f;
            }

            double errorMs = (syncDeltaSeconds * 1000.0) - _syncHistoryRunningSum;
            float dynamicGain = SYNC_GAIN / (float) streamDelayMs;
            float targetAdjustment = (float) (dynamicGain * errorMs);
            return Math.Clamp(targetAdjustment, -SYNC_CLAMP, SYNC_CLAMP);
        }

        private void RecordHistory(double elapsedMs, float targetAdjustment, double streamDelayMs)
        {
            double contributionMs = targetAdjustment * elapsedMs;

            _syncHistory.AddLast((elapsedMs, contributionMs));
            _syncHistoryRunningSum += contributionMs;
            _syncHistoryRunningDurationMs += elapsedMs;
            TrimHistory(streamDelayMs);
        }

        private void TrimHistory(double targetDurationMs)
        {
            targetDurationMs = Math.Max(1.0, targetDurationMs);

            while (_syncHistoryRunningDurationMs > targetDurationMs && _syncHistory.First != null)
            {
                double excessDurationMs = _syncHistoryRunningDurationMs - targetDurationMs;
                var oldest = _syncHistory.First.Value;
                if (oldest.DurationMs <= excessDurationMs)
                {
                    _syncHistory.RemoveFirst();
                    _syncHistoryRunningDurationMs -= oldest.DurationMs;
                    _syncHistoryRunningSum -= oldest.ContributionMs;
                    continue;
                }

                double remainingDurationMs = oldest.DurationMs - excessDurationMs;
                double remainingRatio = remainingDurationMs / oldest.DurationMs;
                double remainingContributionMs = oldest.ContributionMs * remainingRatio;

                _syncHistory.First.Value = (remainingDurationMs, remainingContributionMs);
                _syncHistoryRunningDurationMs = targetDurationMs;
                _syncHistoryRunningSum -= oldest.ContributionMs - remainingContributionMs;
            }
        }
    }
}
