using System;
using System.Threading;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Playback
{
    /// <summary>
    /// Runs a background control loop that keeps mixer playback aligned with input-clock song time.
    /// It starts audio at the scheduled time, then makes small temporary speed changes to correct drift.
    /// </summary>
    internal sealed class SongSyncController : IDisposable
    {
        // Check often enough to respond to drift without busy-spinning a CPU core.
        private const int SYNC_TICK_MS = 1;

        private readonly StemMixer _mixer;
        private readonly ISongSyncStateProvider _stateProvider;
        private readonly IInputClock _inputClock;
        private readonly object _stateLock = new();
        private readonly Thread _syncThread;
        private readonly SyncCorrectionCalculator _syncCalculator = new();

        private readonly ManualResetEventSlim _stopRequested = new();

        private float _debugSyncSpeedAdjustment;
        private double _syncCorrectionSuppressedUntil = double.NegativeInfinity;

        private bool _started;
        private double _debugSyncAudioTime;
        private double _debugTargetAudioPosition;

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

        /// <summary>
        /// Starts sync loop. Safe to call more than once; only first call creates thread work.
        /// </summary>
        public void Start()
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _syncThread.Start();
        }

        /// <summary>
        /// Stops sync loop and waits up to two seconds for background thread to exit.
        /// </summary>
        public void Dispose()
        {
            RequestStop();
            if (_syncThread.IsAlive && !_syncThread.Join(2000))
            {
                YargLogger.LogError("Timed out waiting for song sync thread to stop.");
            }

            _stopRequested.Dispose();
        }

        /// <summary>
        /// Requests sync loop stop without waiting for it. Use <see cref="Dispose"/> to wait for shutdown.
        /// </summary>
        public void RequestStop()
        {
            _stopRequested.Set();
        }

        /// <summary>
        /// Resets correction history after a timeline discontinuity, such as a seek or song restart.
        /// Restores requested song speed, then waits for current mixer latency to clear before correcting again.
        /// </summary>
        public void Reset(float songSpeed)
        {
            lock (_stateLock)
            {
                _syncCalculator.Reset();
            }

            Volatile.Write(ref _debugSyncSpeedAdjustment, 0f);
            _mixer.SetSpeed(songSpeed, true);
            SuppressCorrection();
        }

        /// <summary>
        /// Gets latest target-minus-mixer position delta and temporary speed adjustment for debug display.
        /// </summary>
        internal void GetDebugState(out double syncDelta, out float speedAdjustment)
        {
            double targetAudioPosition = Volatile.Read(ref _debugTargetAudioPosition);
            double syncAudioTime = Volatile.Read(ref _debugSyncAudioTime);

            syncDelta = targetAudioPosition - syncAudioTime;
            speedAdjustment = Volatile.Read(ref _debugSyncSpeedAdjustment);
        }

        private void SuppressCorrection()
        {
            double now = _inputClock.InstantTime;
            var latency = _mixer.GetStreamLatency();
            SuppressUntil(now + latency.TempoStream);
        }

        /// <summary>
        /// Disables new speed corrections until given input-clock time while stale mixer state catches up.
        /// Existing suppression is never shortened.
        /// </summary>
        public void SuppressUntil(double inputSystemTime)
        {
            lock (_stateLock)
            {
                _syncCorrectionSuppressedUntil = Math.Max(_syncCorrectionSuppressedUntil, inputSystemTime);
            }
        }

        // Mixer position lags speed changes by its stream latency. The calculator tracks that pending
        // effect, so this loop does not react to its own corrections as though they were fresh drift.
        private void SyncThread()
        {
            double lastSampleTime = _inputClock.InstantTime;
            while (!_stopRequested.Wait(SYNC_TICK_MS))
            {
                var frame = ReadSyncFrame(lastSampleTime);
                if (TryStartAudio(frame))
                {
                    frame = ReadSyncFrame(lastSampleTime);
                }

                lastSampleTime = frame.InputSystemTime;
                if (!ShouldApplySyncCorrection(frame))
                {
                    PublishDebugSyncState(frame, 0f);
                    continue;
                }

                double streamDelayMs = Math.Max(1.0, frame.TempoLatency * 1000.0);
                float targetAdjustment = CalculateSyncSpeedAdjustment(frame, streamDelayMs);

                _mixer.SetSpeed(frame.SongSpeed + targetAdjustment, false);
                PublishDebugSyncState(frame, targetAdjustment);
            }
        }

        private double CalculateElapsedMs(double lastSampleTime, double inputSystemTime)
        {
            double sampleElapsedMs = (inputSystemTime - lastSampleTime) * 1000.0;
            return sampleElapsedMs > 0.0 ? Math.Min(sampleElapsedMs, 100.0) : 1.0;
        }

        private SyncFrame ReadSyncFrame(double lastSampleTime)
        {
            double inputSystemTime = _inputClock.InstantTime;
            double elapsedMs = CalculateElapsedMs(lastSampleTime, inputSystemTime);
            var state = _stateProvider.ReadSongSyncState(inputSystemTime);
            double syncAudioTime = _mixer.GetPosition();
            var latency = _mixer.GetStreamLatency();
            double playbackLatencySongTime = latency.PlaybackStream * state.SongSpeed;

            return new SyncFrame(
                inputSystemTime,
                elapsedMs,
                state.SongSpeed,
                state.TargetAudioPosition,
                state.Paused,
                syncAudioTime,
                latency.TempoStream,
                playbackLatencySongTime
            );
        }

        // Playback may begin slightly before song time zero: output latency delays audible audio until zero.
        private bool TryStartAudio(SyncFrame frame)
        {
            bool audioShouldStart = !frame.Paused && _mixer.IsPaused &&
                frame.TargetAudioPosition >= -frame.PlaybackLatencySongTime &&
                frame.TargetAudioPosition < _mixer.Length;
            if (!audioShouldStart)
            {
                return false;
            }

            _mixer.Play();
            return true;
        }

        private bool ShouldApplySyncCorrection(SyncFrame frame)
        {
            return !frame.Paused &&
                frame.TargetAudioPosition >= 0 &&
                frame.TargetAudioPosition < _mixer.Length &&
                frame.SyncAudioTime < _mixer.Length;
        }

        private float CalculateSyncSpeedAdjustment(SyncFrame frame, double streamDelayMs)
        {
            lock (_stateLock)
            {
                bool suppressCorrection = frame.InputSystemTime < _syncCorrectionSuppressedUntil;
                if (suppressCorrection)
                {
                    return _syncCalculator.SuppressAdjustment(frame.ElapsedMs, streamDelayMs);
                }

                double syncDeltaSeconds = frame.TargetAudioPosition - frame.SyncAudioTime;
                return _syncCalculator.CalculateAdjustment(
                    syncDeltaSeconds,
                    frame.ElapsedMs,
                    streamDelayMs);
            }
        }

        private void PublishDebugSyncState(SyncFrame frame, float speedAdjustment)
        {
            Volatile.Write(ref _debugSyncAudioTime, frame.SyncAudioTime);
            Volatile.Write(ref _debugTargetAudioPosition, frame.TargetAudioPosition);
            Volatile.Write(ref _debugSyncSpeedAdjustment, speedAdjustment);
        }

        /// <summary>
        /// Immutable snapshot collected once per sync tick, keeping mixer and timeline reads consistent within it.
        /// </summary>
        private readonly struct SyncFrame
        {
            public readonly double InputSystemTime;
            public readonly double ElapsedMs;
            public readonly float SongSpeed;
            public readonly double TargetAudioPosition;
            public readonly bool Paused;
            public readonly double SyncAudioTime;
            public readonly double TempoLatency;
            public readonly double PlaybackLatencySongTime;

            public SyncFrame(
                double inputSystemTime,
                double elapsedMs,
                float songSpeed,
                double targetAudioPosition,
                bool paused,
                double syncAudioTime,
                double tempoLatency,
                double playbackLatencySongTime)
            {
                InputSystemTime = inputSystemTime;
                ElapsedMs = elapsedMs;
                SongSpeed = songSpeed;
                TargetAudioPosition = targetAudioPosition;
                Paused = paused;
                SyncAudioTime = syncAudioTime;
                TempoLatency = tempoLatency;
                PlaybackLatencySongTime = playbackLatencySongTime;
            }
        }
    }
}
