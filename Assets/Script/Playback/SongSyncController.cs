using System;
using System.Threading;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Playback
{
    internal sealed class SongSyncController : IDisposable
    {
        private readonly StemMixer _mixer;
        private readonly ISongSyncStateProvider _stateProvider;
        private readonly IInputClock _inputClock;
        private readonly object _stateLock = new();
        private readonly Thread _syncThread;
        private readonly SyncCorrectionCalculator _syncCalculator = new();

        private volatile bool _disposed;
        private volatile float _syncSpeedAdjustment;
        private double _syncCorrectionSuppressedUntil = double.NegativeInfinity;

        private bool _started;
        private double _syncAudioTime;
        private double _targetAudioPosition;

        public float SyncSpeedAdjustment => _syncSpeedAdjustment;

        public double SyncAudioTime
        {
            get
            {
                lock (_stateLock)
                {
                    return _syncAudioTime;
                }
            }
        }

        public double TargetAudioPosition
        {
            get
            {
                lock (_stateLock)
                {
                    return _targetAudioPosition;
                }
            }
        }

        public double SyncDelta => TargetAudioPosition - SyncAudioTime;

        public SongSyncController(
            StemMixer mixer,
            ISongSyncStateProvider stateProvider,
            IInputClock inputClock)
        {
            _mixer = mixer;
            _stateProvider = stateProvider;
            _inputClock = inputClock;
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
            lock (_stateLock)
            {
                _syncSpeedAdjustment = 0f;
                _syncCalculator.Reset();
            }

            _mixer.SetSpeed(songSpeed, true);
            SuppressCorrection();
        }

        public void ClearSpeedAdjustment()
        {
            lock (_stateLock)
            {
                _syncSpeedAdjustment = 0f;
            }
        }

        public void SuppressCorrection()
        {
            double now = _inputClock.InstantTime;
            var latency = _mixer.GetStreamLatency();
            SuppressUntil(now + latency.TempoStream);
        }

        public void SuppressUntil(double inputSystemTime)
        {
            lock (_stateLock)
            {
                _syncCorrectionSuppressedUntil = Math.Max(_syncCorrectionSuppressedUntil, inputSystemTime);
            }
        }

        private void SyncThread()
        {
            double lastSampleTime = double.NaN;

            for (; !_disposed; Thread.Sleep(1))
            {
                var sample = CreateSyncTimingSample(ref lastSampleTime);
                var snapshot = _stateProvider.ReadSongSyncState(sample.InputSystemTime);
                var timeline = BuildSyncTimeline(snapshot);
                if (_disposed)
                {
                    break;
                }

                TryStartAudio(ref sample, ref snapshot, ref timeline);

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
                PublishSyncThreadState(timeline, targetAdjustment);
            }
        }

        private SyncTimingSample CreateSyncTimingSample(ref double lastSampleTime)
        {
            double inputSystemTime = _inputClock.InstantTime;
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

        private SyncTimeline BuildSyncTimeline(SongSyncState snapshot)
        {
            double syncAudioTime = _mixer.GetPosition();
            var latency = _mixer.GetStreamLatency();
            double preRollSongTime = latency.PlaybackStream * snapshot.SongSpeed;

            return new SyncTimeline(
                syncAudioTime,
                snapshot.TargetAudioPosition,
                latency.TempoStream,
                preRollSongTime
            );
        }

        private void TryStartAudio(
            ref SyncTimingSample sample,
            ref SongSyncState snapshot,
            ref SyncTimeline timeline)
        {
            bool audioShouldStart = !snapshot.Paused && _mixer.IsPaused &&
                timeline.TargetAudioPosition >= -timeline.PreRollSongTime &&
                timeline.TargetAudioPosition < _mixer.Length;
            if (!audioShouldStart)
            {
                return;
            }

            _mixer.Play();

            double inputSystemTime = _inputClock.InstantTime;
            sample = new SyncTimingSample(inputSystemTime, sample.ElapsedMs);
            snapshot = _stateProvider.ReadSongSyncState(inputSystemTime);
            timeline = BuildSyncTimeline(snapshot);
        }

        private bool ShouldApplySyncCorrection(SongSyncState snapshot, SyncTimeline timeline)
        {
            return !snapshot.Paused &&
                timeline.TargetAudioPosition >= 0 &&
                timeline.TargetAudioPosition < _mixer.Length &&
                timeline.SyncAudioTime < _mixer.Length;
        }

        private void PublishSyncTimes(SyncTimeline timeline)
        {
            lock (_stateLock)
            {
                _syncAudioTime = timeline.SyncAudioTime;
                _targetAudioPosition = timeline.TargetAudioPosition;
            }
        }

        private float CalculateSyncSpeedAdjustment(SyncTimingSample sample, SyncTimeline timeline, double streamDelayMs)
        {
            lock (_stateLock)
            {
                bool correctionIsSuppressed = sample.InputSystemTime < _syncCorrectionSuppressedUntil;
                double syncDeltaSeconds = timeline.TargetAudioPosition - timeline.SyncAudioTime;

                return _syncCalculator.CalculateAdjustment(
                    syncDeltaSeconds,
                    sample.ElapsedMs,
                    streamDelayMs,
                    correctionIsSuppressed);
            }
        }

        private void PublishSyncThreadState(SyncTimeline timeline, float targetAdjustment)
        {
            lock (_stateLock)
            {
                _syncAudioTime = timeline.SyncAudioTime;
                _targetAudioPosition = timeline.TargetAudioPosition;
                _syncSpeedAdjustment = targetAdjustment;
            }
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

        private readonly struct SyncTimeline
        {
            public readonly double SyncAudioTime;
            public readonly double TargetAudioPosition;
            public readonly double TempoLatency;
            public readonly double PreRollSongTime;

            public SyncTimeline(
                double syncAudioTime,
                double targetAudioPosition,
                double tempoLatency,
                double preRollSongTime)
            {
                SyncAudioTime = syncAudioTime;
                TargetAudioPosition = targetAudioPosition;
                TempoLatency = tempoLatency;
                PreRollSongTime = preRollSongTime;
            }
        }
    }
}
