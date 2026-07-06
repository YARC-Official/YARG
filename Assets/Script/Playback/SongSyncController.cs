using System;
using System.Collections.Generic;
using System.Threading;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;

namespace YARG.Playback
{
    internal sealed class SongSyncController : IDisposable
    {
        private const double SYNC_DEADBAND_SECONDS = 0.0015;
        private const float SYNC_GAIN = 0.15f;
        private const float SYNC_CLAMP = 0.10f;

        private readonly StemMixer _mixer;
        private readonly object _syncLock;
        private readonly Func<StateSnapshot> _captureState;
        private readonly Action<double, bool> _activateScheduledSongSpeeds;
        private readonly Thread _syncThread;

        private volatile bool _disposed;
        private volatile float _syncSpeedAdjustment;
        private double _syncCorrectionSuppressedUntil = double.NegativeInfinity;
        private double _nextSyncSpeedChangeTime = double.NegativeInfinity;

        private readonly LinkedList<(double DurationMs, double ContributionMs)> _syncHistory = new();
        private double _syncHistoryRunningSum;
        private double _syncHistoryRunningDurationMs;

        private bool _justResumed;
        private bool _started;
        private double _syncAudioTime;
        private double _syncVisualTime;

        public float SyncSpeedAdjustment => _syncSpeedAdjustment;

        public double SyncAudioTime
        {
            get
            {
                lock (_syncLock)
                {
                    return _syncAudioTime;
                }
            }
        }

        public double SyncVisualTime
        {
            get
            {
                lock (_syncLock)
                {
                    return _syncVisualTime;
                }
            }
        }

        public double SyncDelta => SyncVisualTime - SyncAudioTime;

        public SongSyncController(
            StemMixer mixer,
            object syncLock,
            Func<StateSnapshot> captureState,
            Action<double, bool> activateScheduledSongSpeeds)
        {
            _mixer = mixer;
            _syncLock = syncLock;
            _captureState = captureState;
            _activateScheduledSongSpeeds = activateScheduledSongSpeeds;
            _syncThread = new Thread(SyncThread) { IsBackground = true };
        }

        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _syncThread.Start();
        }

        public void Dispose()
        {
            RequestStop();
            if (_syncThread.IsAlive && !_syncThread.Join(2000))
            {
                YargLogger.LogError("Timed out waiting for song sync thread to stop.");
            }
        }

        public void RequestStop()
        {
            _disposed = true;
        }

        public void Reset(float songSpeed)
        {
            lock (_syncLock)
            {
                _syncSpeedAdjustment = 0f;
                _justResumed = false;
                ResetSyncHistoryLocked();
            }

            _mixer.SetSpeed(songSpeed, true);
            SuppressCorrection();
        }

        public void ClearSpeedAdjustment()
        {
            lock (_syncLock)
            {
                _syncSpeedAdjustment = 0f;
            }
        }

        public void NotifyResumed()
        {
            lock (_syncLock)
            {
                _justResumed = true;
            }
        }

        public void SuppressCorrection()
        {
            double now = GetEstimatedCurrentInputTime();
            double playbackLatency = GetPlaybackStreamLatency();
            double tempoLatency = GetTempoStreamLatency();
            double latency = Math.Max(playbackLatency, tempoLatency);
            SuppressUntil(now + latency);
        }

        public void SuppressUntil(double inputSystemTime)
        {
            lock (_syncLock)
            {
                _syncCorrectionSuppressedUntil = Math.Max(_syncCorrectionSuppressedUntil, inputSystemTime);
                _nextSyncSpeedChangeTime = Math.Max(_nextSyncSpeedChangeTime, inputSystemTime);
            }
        }

        public void PreAlignResumeAudio(double inputTime, double audioCalibration, float songSpeed, double songOffset)
        {
            double playbackLatency = GetPlaybackStreamLatency();
            if (playbackLatency <= 0)
            {
                return;
            }

            double audioOffset = songOffset - (audioCalibration * songSpeed);
            double syncVisualTime = inputTime - audioOffset;
            double seekPosition = GetLatencyAlignedSeekPosition(syncVisualTime, playbackLatency, songSpeed);

            _mixer.SetPosition(seekPosition);

            YargLogger.LogFormatDebug(
                "Pre-aligned resume audio. Playback stream latency: {0:0.000000}, tempo stream latency: {1:0.000000}, " +
                "sync visual: {2:0.000000}, seek position: {3:0.000000}",
                playbackLatency, GetTempoStreamLatency(), syncVisualTime, seekPosition
            );
        }

        private void SyncThread()
        {
            double lastSampleTime = double.NaN;

            for (; !_disposed; Thread.Sleep(1))
            {
                var sample = CreateSyncTimingSample(ref lastSampleTime);
                _activateScheduledSongSpeeds(sample.InputSystemTime, false);

                var snapshot = CaptureSyncState();
                var timeline = BuildSyncTimeline(sample.InputSystemTime, snapshot);
                if (_disposed)
                {
                    break;
                }

                ClearStaleResumeStateBeforePreroll(timeline);
                TryStartOrAlignAudioAfterResume(ref sample, snapshot, ref timeline);

                if (!ShouldApplySyncCorrection(snapshot, timeline))
                {
                    PublishSyncTimes(timeline);
                    continue;
                }

                double streamDelayMs = Math.Max(1.0, timeline.TempoLatency * 1000.0);
                float targetAdjustment = CalculateSyncSpeedAdjustment(sample, timeline, streamDelayMs);
                if (_disposed)
                {
                    break;
                }

                _mixer.SetSpeed(snapshot.SongSpeed + targetAdjustment, false);
                RecordSyncAdjustmentHistory(sample.ElapsedMs, targetAdjustment, streamDelayMs);
                PublishSyncThreadState(timeline, targetAdjustment, sample.InputSystemTime);
            }
        }

        private static SyncTimingSample CreateSyncTimingSample(ref double lastSampleTime)
        {
            double inputSystemTime = GetEstimatedCurrentInputTime();
            double elapsedMs = 1.0;

            if (!double.IsNaN(lastSampleTime))
            {
                double sampleElapsedMs = (inputSystemTime - lastSampleTime) * 1000.0;
                bool sampleElapsedTimeIsValid = !double.IsNaN(sampleElapsedMs) &&
                    !double.IsInfinity(sampleElapsedMs) && sampleElapsedMs > 0;
                if (sampleElapsedTimeIsValid)
                {
                    elapsedMs = Math.Min(sampleElapsedMs, 100.0);
                }
            }

            lastSampleTime = inputSystemTime;
            return new SyncTimingSample(inputSystemTime, elapsedMs);
        }

        private StateSnapshot CaptureSyncState()
        {
            lock (_syncLock)
            {
                return _captureState();
            }
        }

        private SyncTimeline BuildSyncTimeline(double inputSystemTime, StateSnapshot snapshot)
        {
            double audioOffset = snapshot.SongOffset - (snapshot.AudioCalibration * snapshot.SongSpeed);
            double currentSongTime = (inputSystemTime - snapshot.InputTimeOffset) * snapshot.SongSpeed;
            double syncAudioTime = _mixer.GetSyncPosition();
            double syncVisualTime = currentSongTime - audioOffset;
            double playbackLatency = GetPlaybackStreamLatency();
            double tempoLatency = GetTempoStreamLatency();
            double preRollSongTime = playbackLatency * snapshot.SongSpeed;

            return new SyncTimeline(
                currentSongTime,
                audioOffset,
                syncAudioTime,
                syncVisualTime,
                playbackLatency,
                tempoLatency,
                preRollSongTime
            );
        }

        private void ClearStaleResumeStateBeforePreroll(SyncTimeline timeline)
        {
            lock (_syncLock)
            {
                if (_justResumed && timeline.SyncVisualTime < -timeline.PreRollSongTime)
                {
                    _justResumed = false;
                }
            }
        }

        private void TryStartOrAlignAudioAfterResume(
            ref SyncTimingSample sample,
            StateSnapshot snapshot,
            ref SyncTimeline timeline)
        {
            bool audioShouldStart = !snapshot.Paused && _mixer.IsPaused &&
                timeline.SyncVisualTime >= -timeline.PreRollSongTime &&
                timeline.SyncVisualTime < _mixer.Length;
            if (!audioShouldStart)
            {
                return;
            }

            bool justResumed;
            lock (_syncLock)
            {
                justResumed = _justResumed;
                _justResumed = false;
            }

            if (justResumed)
            {
                AlignAudioForResume(snapshot, timeline);
            }

            _mixer.Play();

            if (!justResumed)
            {
                timeline = BuildSyncTimeline(sample.InputSystemTime, snapshot);
                return;
            }

            double inputSystemTime = GetEstimatedCurrentInputTime();
            sample = new SyncTimingSample(inputSystemTime, sample.ElapsedMs);
            timeline = BuildSyncTimeline(inputSystemTime, snapshot);
        }

        private void AlignAudioForResume(StateSnapshot snapshot, SyncTimeline timeline)
        {
            double resumeCommandDelay = GetResumeCommandDelay();
            double resumeCommandInputTime = GetEstimatedCurrentInputTime();
            double adjustedSyncVisualTime =
                ((resumeCommandInputTime - snapshot.InputTimeOffset) * snapshot.SongSpeed) - timeline.AudioOffset;
            double seekPosition = GetLatencyAlignedSeekPosition(
                adjustedSyncVisualTime,
                timeline.PlaybackLatency,
                snapshot.SongSpeed
            );

            _mixer.SetPosition(seekPosition);

            YargLogger.LogFormatDebug(
                "Aligned resumed audio. Sync visual: {0:0.000000}, adjusted sync visual: {1:0.000000}, seek position: {2:0.000000}, " +
                "playback stream latency: {3:0.000000}, tempo stream latency: {4:0.000000}, resume command delay: {5:0.000000}",
                timeline.SyncVisualTime, adjustedSyncVisualTime, seekPosition, timeline.PlaybackLatency,
                timeline.TempoLatency, resumeCommandDelay
            );
        }

        private static double GetResumeCommandDelay()
        {
            double frameStart = InputManager.InputUpdateCpuTime;
            if (frameStart <= 0)
            {
                return 0;
            }

            return Math.Max(0, GetCurrentCpuTime() - frameStart);
        }

        private bool ShouldApplySyncCorrection(StateSnapshot snapshot, SyncTimeline timeline)
        {
            return !snapshot.Paused &&
                timeline.SyncVisualTime >= 0 &&
                timeline.SyncVisualTime < _mixer.Length &&
                timeline.SyncAudioTime < _mixer.Length;
        }

        private void PublishSyncTimes(SyncTimeline timeline)
        {
            lock (_syncLock)
            {
                _syncAudioTime = timeline.SyncAudioTime;
                _syncVisualTime = timeline.SyncVisualTime;
            }
        }

        private float CalculateSyncSpeedAdjustment(SyncTimingSample sample, SyncTimeline timeline, double streamDelayMs)
        {
            double audioInputTime = timeline.SyncAudioTime + timeline.AudioOffset;
            double inputSyncDelta = timeline.CurrentSongTime - audioInputTime;
            bool correctionIsSuppressed;

            lock (_syncLock)
            {
                correctionIsSuppressed = sample.InputSystemTime < _syncCorrectionSuppressedUntil;
            }

            bool withinDeadband = Math.Abs(inputSyncDelta) < SYNC_DEADBAND_SECONDS;
            if (correctionIsSuppressed || withinDeadband)
            {
                return 0f;
            }

            double historyContributionMs;
            lock (_syncLock)
            {
                TrimSyncHistory(streamDelayMs);
                historyContributionMs = _syncHistoryRunningSum;
            }

            double errorMs = (inputSyncDelta * 1000.0) - historyContributionMs;
            float dynamicGain = SYNC_GAIN / (float) streamDelayMs;
            float targetAdjustment = (float) (dynamicGain * errorMs);
            return Math.Clamp(targetAdjustment, -SYNC_CLAMP, SYNC_CLAMP);
        }

        private void RecordSyncAdjustmentHistory(double elapsedMs, float targetAdjustment, double streamDelayMs)
        {
            double contributionMs = targetAdjustment * elapsedMs;

            lock (_syncLock)
            {
                _syncHistory.AddLast((elapsedMs, contributionMs));
                _syncHistoryRunningSum += contributionMs;
                _syncHistoryRunningDurationMs += elapsedMs;
                TrimSyncHistory(streamDelayMs);
            }
        }

        private void PublishSyncThreadState(SyncTimeline timeline, float targetAdjustment, double inputSystemTime)
        {
            lock (_syncLock)
            {
                _syncAudioTime = timeline.SyncAudioTime;
                _syncVisualTime = timeline.SyncVisualTime;
                _syncSpeedAdjustment = targetAdjustment;
                _nextSyncSpeedChangeTime = inputSystemTime;
            }
        }

        private void ResetSyncHistoryLocked()
        {
            _syncHistory.Clear();
            _syncHistoryRunningSum = 0.0;
            _syncHistoryRunningDurationMs = 0.0;
        }

        private void TrimSyncHistory(double targetDurationMs)
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

        private double GetLatencyAlignedSeekPosition(double syncVisualTime, double syncLatency, double songSpeed)
        {
            return Math.Clamp(syncVisualTime + (syncLatency * songSpeed), 0, _mixer.Length);
        }

        private static double SanitizeLatency(double latency)
        {
            if (double.IsNaN(latency) || double.IsInfinity(latency) || latency < 0)
            {
                return 0;
            }

            return latency;
        }

        private double GetPlaybackStreamLatency()
        {
            return SanitizeLatency(_mixer.GetPlaybackStreamLatency());
        }

        private double GetTempoStreamLatency()
        {
            return SanitizeLatency(_mixer.GetTempoStreamLatency());
        }

        private static double GetCurrentCpuTime()
        {
            return (double) System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        }

        private static double GetEstimatedCurrentInputTime()
        {
            double inputUpdateTime;
            double inputUpdateCpuTime;
            double inputUpdateCpuTimeCheck;

            int retries = 0;
            do
            {
                inputUpdateCpuTime = InputManager.InputUpdateCpuTime;
                inputUpdateTime = InputManager.InputUpdateTime;
                inputUpdateCpuTimeCheck = InputManager.InputUpdateCpuTime;
                retries++;
            } while (inputUpdateCpuTime != inputUpdateCpuTimeCheck && retries < 10);

            if (inputUpdateCpuTime <= 0)
            {
                return inputUpdateTime;
            }

            double elapsed = Math.Max(0, GetCurrentCpuTime() - inputUpdateCpuTime);
            return inputUpdateTime + elapsed;
        }

        private readonly struct SyncTimingSample
        {
            public readonly double InputSystemTime;
            public readonly double ElapsedMs;

            public SyncTimingSample(double inputSystemTime, double elapsedMs)
            {
                InputSystemTime = inputSystemTime;
                ElapsedMs = elapsedMs;
            }
        }

        public readonly struct StateSnapshot
        {
            public readonly float SongSpeed;
            public readonly double SongOffset;
            public readonly double AudioCalibration;
            public readonly double InputTimeOffset;
            public readonly bool Paused;

            public StateSnapshot(
                float songSpeed,
                double songOffset,
                double audioCalibration,
                double inputTimeOffset,
                bool paused)
            {
                SongSpeed = songSpeed;
                SongOffset = songOffset;
                AudioCalibration = audioCalibration;
                InputTimeOffset = inputTimeOffset;
                Paused = paused;
            }
        }

        private readonly struct SyncTimeline
        {
            public readonly double CurrentSongTime;
            public readonly double AudioOffset;
            public readonly double SyncAudioTime;
            public readonly double SyncVisualTime;
            public readonly double PlaybackLatency;
            public readonly double TempoLatency;
            public readonly double PreRollSongTime;

            public SyncTimeline(
                double currentSongTime,
                double audioOffset,
                double syncAudioTime,
                double syncVisualTime,
                double playbackLatency,
                double tempoLatency,
                double preRollSongTime)
            {
                CurrentSongTime = currentSongTime;
                AudioOffset = audioOffset;
                SyncAudioTime = syncAudioTime;
                SyncVisualTime = syncVisualTime;
                PlaybackLatency = playbackLatency;
                TempoLatency = tempoLatency;
                PreRollSongTime = preRollSongTime;
            }
        }
    }
}
