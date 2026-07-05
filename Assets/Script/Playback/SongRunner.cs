using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Audio;
using YARG.Input;
using YARG.Settings;

namespace YARG.Playback
{
    // There are many design decisions for SongRunner which may seem confusing.
    // Here is an overview of everything and why it is done that way:
    //
    // # Time Clock
    //
    // The Unity input system's time clock is used as the primary time source, as opposed to audio
    // playback time, for various reasons:
    //
    // - It makes frame-independent inputs significantly easier to handle (if not outright possible
    //   in the first place) since input times aren't messed with whatsoever during playback
    //   (except to offset them relative to an absolute starting time).
    //
    // - It ensures timing is consistent throughout the song. Audio playback can be subject to
    //   various problems which could impact the playing experience very severely. The input system's
    //   timer has no such issues since it is based on a monotonic source.
    //
    // - It provides much higher precision than audio playback does. BASS is limited to a 5 ms
    //   update rate, which can cause visual stuttering or positional snapping/aliasing at framerates
    //   higher than 200 FPS. The input system's time, being monotonically-based, is determined on-demand
    //   and has a precision of around 100 microseconds (in my observations - Nate). A loop repeatedly
    //   querying the input system's time will produce a different value on every query, even within the
    //   same frame.
    //
    // - It makes it easy to allow times below 0 and beyond the audio's length. This is necessary
    //   for a variety of reasons:
    //   - Makes it possible to provide a small starting delay on songs, ensuring players have time
    //     to prepare on songs that have no delay between the start of the audio and their first note.
    //   - Makes song ending 100% reliable. The audio length reported by BASS is not reliably
    //     accurate: the final position reported in a song can be below the reported length.
    //     Additionally, while BASS has a song end event, even that has shown to be unreliable in
    //     certain scenarios, not firing when it should. Thus, the only reliable way to ensure the
    //     song ends is to have our own time source which can go beyond the audio length.
    //   - Makes it significantly easier to support song offsets, further detailed below.
    //
    // # Song Offset
    //
    // To support song offsets (`delay = 1234` in song.ini, `Offset = 1.234` in .chart), audio time
    // has an offset applied to conceptually shift the timeline for input time and the chart:
    // with an offset of 15 seconds, the 0-point for input time will be 15 seconds into audio
    // playback. Without applying the offset to audio time, this would cause a major discrepancy
    // between input and audio times, and make their relationship hard to reason about. So, to keep
    // the same basis for the two timelines, the audio position is offset such that, with the above
    // example of a 15 second offset, the position at which audio will start is -15 seconds.
    //
    // # Synchronization
    //
    // Audio is synchronized relative to the input system's timer, not the other way around. As
    // explained earlier, this is done to make it feasible to reason about an input's timing
    // relative to the song, in a framerate-independent manner (in addition to the timing stability
    // benefits also mentioned).
    //
    // Audio desync correction is performed by adjusting audio speed until it gradually falls back
    // in line. This produces little to no audible effect in BASS, its time stretching is well-suited
    // for this purpose. Seeking has also been considered for large desyncs, but is not implemented
    // currently.

    public class SongRunner : IDisposable
    {
        #region Times
        public const double SONG_START_DELAY = 2;
        private const double PLAYBACK_START_LATENCY_MARGIN = 0.025;

        /// <summary>
        /// The time into the song, accounting for song speed and audio calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value should be used for all interactions that are relative to the audio.
        /// Note that this is driven by input time, rather than audio time.
        /// Use <see cref="AudioPlaybackTime"/> if the actual audio time is required.
        /// </remarks>
        public double SongTime { get; private set; }

        /// <summary>
        /// The current visual time, accounting for song speed and video calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        public double VisualTime { get; private set; }

        /// <summary>
        /// The current input time, accounting for song speed and video calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value should be used for all interactions with inputs, engines, and replays.
        /// It should also be used for setting position, as all times are based off of input time.
        /// </remarks>
        public double InputTime { get; private set; }

        /// <summary>
        /// The playback position of the audio relative to gameplay.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value is for scenarios that <b>must</b> be tied to audio playback time,
        /// as opposed to input/visual time.
        /// In general, <see cref="SongTime"/> should be used instead where possible.
        /// </remarks>
        public double AudioTime => AudioPlaybackTime + SongOffset;

        /// <summary>
        /// The playback position of the audio relative to the audio file only.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value is for scenarios that <b>must</b> know the position into the audio file,
        /// as opposed to the gameplay song position.
        /// In general, <see cref="SongTime"/> should be used instead where possible.
        /// </remarks>
        public double AudioPlaybackTime { get; private set; }
        #endregion

        #region Offsets
        /// <summary>
        /// The audio calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive calibration settings will result in a negative number here.
        /// This value also takes video calibration into account, otherwise things will not sync up visually.
        /// </remarks>
        public double AudioCalibration { get; private set; }

        /// <summary>
        /// The video calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive calibration settings will result in a negative number here.
        /// </remarks>
        public double VideoCalibration { get; private set; }

        /// <summary>
        /// The song offset, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive offsets in the .ini or .chart will result in a negative number here.
        /// </remarks>
        public double SongOffset { get; }

        /// <summary>
        /// The input time that is considered to be 0.
        /// </summary>
        public double InputTimeOffset { get; private set; }
        #endregion

        #region Other state
        /// <summary>
        /// The requested playback speed of the song. Updated immediately by user speed changes.
        /// </summary>
        public float RequestedSongSpeed => _requestedSongSpeed;

        /// <summary>
        /// The effective gameplay/reference speed of the song.
        /// </summary>
        public float EffectiveSongSpeed => _effectiveSongSpeed;

        /// <summary>
        /// The effective gameplay/reference speed of the song.
        /// </summary>
        public float SongSpeed => _effectiveSongSpeed;

        /// <summary>
        /// The actual current playback speed of the song around the effective reference speed.
        /// </summary>
        /// <remarks>
        /// The audio may be sped up or slowed down in order to re-synchronize.
        /// This value takes that speed adjustment into account.
        /// </remarks>
        public float RealSongSpeed => _effectiveSongSpeed + _syncSpeedAdjustment;

        private float _requestedSongSpeed;
        private float _effectiveSongSpeed;
        private float _commandedAudioBaseSpeed;
        private readonly Queue<(double EffectiveTime, float Speed)> _songSpeedSchedule = new();

        /// <summary>
        /// Whether or not the runner has been started.
        /// </summary>
        public bool Started { get; private set; }

        /// <summary>
        /// Whether or not the song is currently paused.
        /// </summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// Whether or not the song's pause state is currently overridden.
        /// </summary>
        public bool PauseOverridden => _pauseOverrides > 0;

        private int _pauseOverrides;
        private bool _resumeAfterOverride;

        private bool _pausedForFrameDebugger;

        private double _forceStartTime = double.NaN;
        private double _minimumUpdateInputSystemTime = double.NegativeInfinity;
        #endregion

        #region Rewind State
        private CancellationTokenSource _rewindSource;
        private Tween                   _rewindTween;
        #endregion

        #region Audio syncing
        private Thread _syncThread;
        private readonly object _syncLock = new();

        private volatile bool _disposed;

        private volatile float _syncSpeedAdjustment;
        private volatile float _syncCommandedSpeedAdjustment;
        private volatile int   _syncSpeedMultiplier;
        private volatile float _syncStartDelta;
        private volatile float _syncWorstDelta;
        private volatile float _syncSmoothedDrift = float.NaN;
        private bool _syncRecoveryActive;
        private double _syncCorrectionSuppressedUntil = double.NegativeInfinity;
        private double _nextSyncSpeedChangeTime = double.NegativeInfinity;

        #region PLL state variables
        private double _pllModelPosition;

        private double _pllFilteredMismatch;
        private double _pllControlError;
        private double _pllRawMismatch;
        private double _pllStreamDelay;
        private double _pllAudioDelayDistance;
        private double _pllReferenceLeadDistance;
        private readonly List<(double Time, double Speed)> _pllSpeedHistory = new();
        private bool _pllInitialized;
        private double _pllLastTime = double.NaN;
        private double _pllLastRawAudioTime = double.NaN;
        #endregion

        #region Simplified Sync state variables
        private readonly LinkedList<(double DurationMs, double ContributionMs)> _syncHistory = new();
        private double _syncHistoryRunningSum;
        private double _syncHistoryRunningDurationMs;
        private const float SYNC_GAIN = 0.5f;
        private const float SYNC_CLAMP = 0.10f;
        #endregion

        private bool _justResumed;

        private readonly StemMixer _mixer;

        private string _lastSyncLandingOperation = "None";
        private double _lastSyncLandingDelta = double.NaN;

        public float SyncSpeedAdjustment => _syncSpeedAdjustment;
        public float SyncCommandedSpeedAdjustment => _syncCommandedSpeedAdjustment;
        public int SyncSpeedMultiplier => _syncSpeedMultiplier;
        public float SyncStartDelta => _syncStartDelta;
        public float SyncWorstDelta => _syncWorstDelta;
        public string LastSyncLandingOperation { get { lock (_syncLock) return _lastSyncLandingOperation; } }
        public double LastSyncLandingDelta { get { lock (_syncLock) return _lastSyncLandingDelta; } }
        public double EstimatedOutputLatency => GetPlaybackLatency();
        public double CommandLatency => GetTempoLatency();
        public double PllControlError { get { lock (_syncLock) return _pllControlError; } }
        public double PllRawMismatch { get { lock (_syncLock) return _pllRawMismatch; } }
        public double PllFilteredMismatch { get { lock (_syncLock) return _pllFilteredMismatch; } }
        public double PllStreamDelay { get { lock (_syncLock) return _pllStreamDelay; } }
        public double PllAudioDelayDistance { get { lock (_syncLock) return _pllAudioDelayDistance; } }
        public double PllReferenceLeadDistance { get { lock (_syncLock) return _pllReferenceLeadDistance; } }
        public double SyncSuppressionRemaining
        {
            get
            {
                double suppressedUntil;
                lock (_syncLock)
                {
                    suppressedUntil = _syncCorrectionSuppressedUntil;
                }
                return Math.Max(0.0, suppressedUntil - GetEstimatedCurrentInputTime());
            }
        }

        /// <summary>
        /// The audio time used by audio synchronization.<br/>
        /// Accounts for song speed, audio calibration, and song offset.
        /// </summary>
        public double SyncAudioTime { get; private set; }

        /// <summary>
        /// The visual time used by audio synchronization.<br/>
        /// Accounts for song speed, but <b>not</b> video calibration.
        /// </summary>
        public double SyncVisualTime { get; private set; }

        /// <summary>
        /// The difference between the visual and audio times shown in the debug panel.
        /// </summary>
        public double AudioVisualDelta => VisualTime - AudioTime;

        /// <summary>
        /// The difference between the song/input clock and actual audio playback.
        /// </summary>
        public double AudioSyncDelta => SongTime - AudioTime;

        /// <summary>
        /// The difference between the visual and audio times used by audio synchronization.
        /// </summary>
        public double SyncDelta => SyncVisualTime - SyncAudioTime;
        #endregion

        #region Seek debugging
        private bool _seeked;
        private double _previousInputTime = double.MinValue;
        #endregion

        /// <summary>
        /// Creates a new song runner with the given speed and calibration values.
        /// </summary>
        /// <remarks>
        /// The created song runner will be in an unstarted state. Upon calling <see cref="Update"/>,
        /// the runner will attempt to start and re-initialize its time values, to adjust for loading
        /// lag. If the current frame took too long to process before the update started, then starting
        /// will be skipped and attempted again next frame.
        /// <br/>
        /// Since the runner starts paused, anything that might potentially interact with it before
        /// starting must respect the paused state, otherwise incorrect behavior may happen.
        /// </remarks>
        /// <param name="songSpeed">
        /// The percentage song speed, where 1f == 100%.
        /// </param>
        /// <param name="audioCalibrationMs">
        /// The audio calibration, in milliseconds.<br/>
        /// This value is negated and normalized to seconds for more intuitive usage in other code.
        /// <paramref name="videoCalibrationMs"/> is also applied to keep things visually synced.
        /// </param>
        /// <param name="videoCalibrationMs">
        /// The video calibration, in milliseconds.<br/>
        /// This value is negated and normalized to seconds for more intuitive usage in other code.
        /// </param>
        /// <param name="songOffset">
        /// The song offset, in seconds.<br/>
        /// This value is negated for more intuitive usage in other code.
        /// </param>
        public SongRunner(
            StemMixer mixer,
            double startTime,
            double startDelay,
            float songSpeed,
            double songOffset
        )
        {
            _mixer = mixer;
            _requestedSongSpeed = ClampSongSpeed(songSpeed);
            _effectiveSongSpeed = _requestedSongSpeed;
            _commandedAudioBaseSpeed = _effectiveSongSpeed;
            SongOffset = -songOffset;

            _syncThread = new Thread(SyncThread) { IsBackground = true };

            InitializeSongTime(startTime + SongOffset, GetLatencyAdjustedStartDelay(startDelay));
            UpdateCalibration();
        }

        ~SongRunner()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool SyncThreadStopped => _syncThread == null || !_syncThread.IsAlive;

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    Thread syncThread = _syncThread;
                    if (syncThread.IsAlive && !syncThread.Join(2000))
                    {
                        YargLogger.LogError("Timed out waiting for song sync thread to stop. Skipping join to avoid editor freeze.");
                        return;
                    }
                    _syncThread = null;
                }
            }
        }

        private void Start()
        {
            YargLogger.LogDebug("Starting song runner");
 
            // Re-initialize song times to avoid lag issues
            InitializeSongTime(InputTime, 0);
            ResetSyncEstimate();
 
            _syncThread.Start();
            Started = true;
        }

        public void Update()
        {
            // Runner is lazy-started to avoid timing issues with lag
            if (!Started)
            {
                // Hack: delay if the starting frame lagged

                // Only delay a maximum of one second
                if (double.IsNaN(_forceStartTime))
                {
                    _forceStartTime = InputManager.CurrentInputTime + 1;
                }

                double currentTime = InputManager.CurrentInputTime;
                double currentFrameLength = currentTime - InputManager.InputUpdateTime;
                if (currentFrameLength >= 0.1f && currentTime < _forceStartTime)
                {
                    return;
                }

                Start();
            }

            // Hack: don't update while in the frame debugger
            if (_pausedForFrameDebugger != FrameDebugger.enabled)
            {
                _pausedForFrameDebugger = FrameDebugger.enabled;
                if (_pausedForFrameDebugger)
                {
                    OverridePause();
                }
                else
                {
                    OverrideResume();
                }
            }

            double updateInputSystemTime = GetUpdateInputSystemTime();
            ActivateScheduledSongSpeeds(updateInputSystemTime, true);

            if (Paused)
                return;

            // Update times
            UpdateTimes(updateInputSystemTime);

            // Check for unexpected backwards time jumps
            YargLogger.AssertFormat(
                InputTime >= _previousInputTime || _seeked,
                "Unexpected time seek backwards! Went from {0} to {1} (delta: {2})",
                _previousInputTime, InputTime, InputTime - _previousInputTime
            );
            _previousInputTime = InputTime;

            _seeked = false;
        }

        private static double SanitizeLatency(double latency)
        {
            if (double.IsNaN(latency) || double.IsInfinity(latency) || latency < 0)
            {
                return 0;
            }

            return latency;
        }

        private double GetPlaybackLatency()
        {
            return SanitizeLatency(_mixer.GetPlaybackLatency());
        }

        private double GetTempoLatency()
        {
            return SanitizeLatency(_mixer.GetTempoLatency());
        }

        private static void PruneSpeedHistory(List<(double Time, double Speed)> history, double windowStart)
        {
            int removeCount = 0;
            while (removeCount + 1 < history.Count && history[removeCount + 1].Time <= windowStart)
            {
                removeCount++;
            }

            if (removeCount > 0)
            {
                history.RemoveRange(0, removeCount);
            }
        }

        private static double IntegrateSpeedHistory(
            List<(double Time, double Speed)> history,
            double fallbackSpeed,
            double startTime,
            double endTime)
        {
            if (endTime <= startTime)
            {
                return 0.0;
            }

            if (history.Count == 0)
            {
                return (endTime - startTime) * fallbackSpeed;
            }

            double total = 0.0;
            double currentTime = startTime;
            double currentSpeed = fallbackSpeed;
            int index = 0;

            while (index < history.Count && history[index].Time <= startTime)
            {
                currentSpeed = history[index].Speed;
                index++;
            }

            while (index < history.Count && history[index].Time < endTime)
            {
                var sample = history[index];
                if (sample.Time > currentTime)
                {
                    total += (sample.Time - currentTime) * currentSpeed;
                    currentTime = sample.Time;
                }

                currentSpeed = sample.Speed;
                index++;
            }

            if (endTime > currentTime)
            {
                total += (endTime - currentTime) * currentSpeed;
            }

            return total;
        }

        private bool ActivateScheduledSongSpeeds(double nowInputSystemTime, bool updateTimes)
        {
            bool changed = false;

            lock (_syncLock)
            {
                while (_songSpeedSchedule.Count > 0 && _songSpeedSchedule.Peek().EffectiveTime <= nowInputSystemTime)
                {
                    var command = _songSpeedSchedule.Dequeue();
                    float nextSpeed = ClampSongSpeed(command.Speed);
                    double activationTime = command.EffectiveTime;
                    double currentInputAtActivation = (activationTime - InputTimeOffset) * _effectiveSongSpeed;

                    _effectiveSongSpeed = nextSpeed;
                    InputTimeOffset = activationTime - (currentInputAtActivation / _effectiveSongSpeed);
                    changed = true;
                }
            }

            if (changed && updateTimes)
            {
                UpdateTimes(nowInputSystemTime);
            }

            return changed;
        }

        private void SyncThread()
        {
            var dtSamples = new List<double>();
            double lastSampleTime = double.NaN;

            for (; !_disposed; Thread.Sleep(1))
            {
                double currentInputTime = GetEstimatedCurrentInputTime();
                double actualDtMs = 1.0;

                if (!double.IsNaN(lastSampleTime))
                {
                    double sampleDtMs = (currentInputTime - lastSampleTime) * 1000.0;
                    if (!double.IsNaN(sampleDtMs) && !double.IsInfinity(sampleDtMs) && sampleDtMs > 0)
                    {
                        actualDtMs = Math.Min(sampleDtMs, 100.0);
                    }
                    if (dtSamples.Count < 1000)
                    {
                        dtSamples.Add(sampleDtMs);
                    }
                    else if (dtSamples.Count == 1000)
                    {
                        double sum = 0;
                        double min = double.MaxValue;
                        double max = double.MinValue;
                        foreach (var s in dtSamples)
                        {
                            sum += s;
                            if (s < min)
                            {
                                min = s;
                            }
                            if (s > max)
                            {
                                max = s;
                            }
                        }
                        double avg = sum / dtSamples.Count;
                        YargLogger.LogInfo($"SyncThread sleep timing over 1000 samples: Avg = {avg:F3}ms, Min = {min:F3}ms, Max = {max:F3}ms");
                        dtSamples.Add(0);
                    }
                }
                lastSampleTime = currentInputTime;

                ActivateScheduledSongSpeeds(currentInputTime, false);

                double songSpeed;
                double songOffset;
                double audioCalibration;
                double inputTimeOffset;
                double syncCorrectionSuppressedUntil;
                bool paused;

                lock (_syncLock)
                {
                    songSpeed = _effectiveSongSpeed;
                    songOffset = SongOffset;
                    audioCalibration = AudioCalibration;
                    inputTimeOffset = InputTimeOffset;
                    syncCorrectionSuppressedUntil = _syncCorrectionSuppressedUntil;
                    paused = Paused;
                }

                double audioOffset = songOffset - (audioCalibration * songSpeed);
                double currentSongTime = (currentInputTime - inputTimeOffset) * songSpeed;
                double rawAudioTime = _mixer.GetPosition();
                double syncAudioTime = _mixer.GetSyncPosition();
                double syncVisualTime = currentSongTime - audioOffset;
                double playbackLatency = GetPlaybackLatency();
                double tempoLatency = GetTempoLatency();
                if (_disposed)
                {
                    break;
                }
                double preRollSongTime = playbackLatency * songSpeed;

                lock (_syncLock)
                {
                    if (_justResumed && syncVisualTime < -preRollSongTime)
                    {
                        _justResumed = false;
                    }
                }

                if (!paused && _mixer.IsPaused &&
                    syncVisualTime >= -preRollSongTime && syncVisualTime < _mixer.Length)
                {
                    double delay = 0;
                    bool justResumed;
                    lock (_syncLock)
                    {
                        justResumed = _justResumed;
                        _justResumed = false;
                    }

                    double landingExpectedSyncAudioTime = double.NaN;
                    if (justResumed)
                    {
                        double frameStart = InputManager.InputUpdateCpuTime;
                        if (frameStart > 0)
                        {
                            double now = (double) System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
                            delay = Math.Max(0, now - frameStart);
                        }

                        double resumeCommandInputTime = GetEstimatedCurrentInputTime();
                        double adjustedSyncVisualTime = ((resumeCommandInputTime - inputTimeOffset) * songSpeed) - audioOffset;
                        double seekPosition = GetLatencyAlignedSeekPosition(adjustedSyncVisualTime, playbackLatency, songSpeed);
                        landingExpectedSyncAudioTime = seekPosition;
                        _mixer.SetPosition(seekPosition);

                        YargLogger.LogFormatDebug(
                            "Aligned resumed audio. Sync visual: {0:0.000000}, adjusted sync visual: {1:0.000000}, seek position: {2:0.000000}, " +
                            "playback latency: {3:0.000000}, tempo latency: {4:0.000000}, resume command delay: {5:0.000000}",
                            syncVisualTime, adjustedSyncVisualTime, seekPosition, playbackLatency, tempoLatency, delay
                        );
                    }

                    _mixer.Play();
                    rawAudioTime = _mixer.GetPosition();
                    syncAudioTime = _mixer.GetSyncPosition();

                    if (justResumed)
                    {
                        currentInputTime = GetEstimatedCurrentInputTime();
                        currentSongTime = (currentInputTime - inputTimeOffset) * songSpeed;
                        syncVisualTime = currentSongTime - audioOffset;
                        syncAudioTime = _mixer.GetSyncPosition();
                        RecordAudioInputSyncLanding("Unpause", landingExpectedSyncAudioTime, syncAudioTime);
                    }
                }

                if (paused || syncVisualTime < 0 || syncVisualTime >= _mixer.Length || syncAudioTime >= _mixer.Length)
                {
                    lock (_syncLock)
                    {
                        SyncAudioTime = syncAudioTime;
                        SyncVisualTime = syncVisualTime;
                    }
                    continue;
                }

                double streamDelay = GetTempoLatency();
                double streamDelayMs = Math.Max(1.0, streamDelay * 1000.0);
                TrimSyncHistory(streamDelayMs);

                double audioInputTime = syncAudioTime + audioOffset;
                double inputSyncDelta = currentSongTime - audioInputTime;

                const double DEADBAND = 0.0015;
                bool withinDeadband = Math.Abs(inputSyncDelta) < DEADBAND;

                float targetAdjustment = 0f;
                double err_ms = 0.0;
                if (currentInputTime >= syncCorrectionSuppressedUntil && !withinDeadband)
                {
                    err_ms = (inputSyncDelta * 1000.0) - _syncHistoryRunningSum;
                    float dynamicK = SYNC_GAIN / (float) streamDelayMs;
                    targetAdjustment = (float)(dynamicK * err_ms);
                    targetAdjustment = Math.Clamp(targetAdjustment, -SYNC_CLAMP, SYNC_CLAMP);
                }

                if (_disposed)
                {
                    break;
                }

                // Update BASS immediately every millisecond
                _mixer.SetSpeed((float)(songSpeed + targetAdjustment), false);

                // Update the history queue using real sync-loop elapsed time.
                double contributionMs = targetAdjustment * actualDtMs;
                _syncHistory.AddLast((actualDtMs, contributionMs));
                _syncHistoryRunningSum += contributionMs;
                _syncHistoryRunningDurationMs += actualDtMs;
                TrimSyncHistory(streamDelayMs);

                int previousSpeedMultiplier = _syncSpeedMultiplier;
                int speedMultiplier = 0;
                if (inputSyncDelta > DEADBAND)
                {
                    speedMultiplier = 1;
                }
                else if (inputSyncDelta < -DEADBAND)
                {
                    speedMultiplier = -1;
                }

                float startDelta = 0f;
                float worstDelta = 0f;
                if (speedMultiplier != 0)
                {
                    startDelta = _syncStartDelta;
                    worstDelta = _syncWorstDelta;
                    if (previousSpeedMultiplier != speedMultiplier)
                    {
                        startDelta = (float) inputSyncDelta;
                        worstDelta = startDelta;
                    }
                    else if (Math.Abs(inputSyncDelta) > Math.Abs(worstDelta))
                    {
                        worstDelta = (float) inputSyncDelta;
                    }
                }

                const double ALPHA_MISMATCH = 0.05;
                _pllFilteredMismatch = (ALPHA_MISMATCH * inputSyncDelta) + ((1.0 - ALPHA_MISMATCH) * _pllFilteredMismatch);

                lock (_syncLock)
                {
                    SyncAudioTime = syncAudioTime;
                    SyncVisualTime = syncVisualTime;
                    _syncSmoothedDrift = (float) _pllFilteredMismatch;
                    _syncSpeedAdjustment = targetAdjustment;
                    _syncCommandedSpeedAdjustment = targetAdjustment;
                    _commandedAudioBaseSpeed = (float) songSpeed;
                    _syncSpeedMultiplier = speedMultiplier;
                    _syncStartDelta = startDelta;
                    _syncWorstDelta = worstDelta;
                    _syncRecoveryActive = speedMultiplier != 0;
                    _syncCorrectionSuppressedUntil = syncCorrectionSuppressedUntil;
                    _nextSyncSpeedChangeTime = currentInputTime;
                    _pllControlError = err_ms / 1000.0;
                    _pllRawMismatch = inputSyncDelta;
                    _pllStreamDelay = streamDelay;
                    _pllAudioDelayDistance = _syncHistoryRunningSum / 1000.0;
                    _pllReferenceLeadDistance = songSpeed * streamDelay;
                }
            }
        }

        private void RecordAudioInputSyncLanding(string operation, double expectedSyncAudioTime, double actualSyncAudioTime)
        {
            lock (_syncLock)
            {
                _lastSyncLandingOperation = operation;
                _lastSyncLandingDelta = expectedSyncAudioTime - actualSyncAudioTime;
            }
        }

        private void ResetSync()
        {
            lock (_syncLock)
            {
                _syncSpeedMultiplier = 0;
                _syncSpeedAdjustment = 0f;
                _syncCommandedSpeedAdjustment = 0f;
                _commandedAudioBaseSpeed = _effectiveSongSpeed;
                _syncSmoothedDrift = float.NaN;
                _syncRecoveryActive = false;
                _justResumed = false;

                _pllInitialized = false;

                _pllFilteredMismatch = 0.0;
                _pllControlError = 0.0;
                _pllRawMismatch = 0.0;
                _pllStreamDelay = 0.0;
                _pllAudioDelayDistance = 0.0;
                _pllReferenceLeadDistance = 0.0;
                _pllSpeedHistory.Clear();
                _pllLastTime = double.NaN;
                _pllLastRawAudioTime = double.NaN;

                _syncHistory.Clear();
                _syncHistoryRunningSum = 0.0;
                _syncHistoryRunningDurationMs = 0.0;
            }

            _mixer.SetSpeed(RealSongSpeed, true);
            SuppressSyncCorrection();
        }

        private void ResetSyncEstimate()
        {
            _syncSmoothedDrift = float.NaN;
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

        private void PreAlignResumeAudio()
        {
            double playbackLatency = GetPlaybackLatency();
            if (playbackLatency <= 0)
            {
                return;
            }

            double audioOffset = SongOffset - (AudioCalibration * SongSpeed);
            double syncVisualTime = InputTime - audioOffset;
            double seekPosition = GetLatencyAlignedSeekPosition(syncVisualTime, playbackLatency, SongSpeed);

            _mixer.SetPosition(seekPosition);

            YargLogger.LogFormatDebug(
                "Pre-aligned resume audio. Playback latency: {0:0.000000}, tempo latency: {1:0.000000}, " +
                "sync visual: {2:0.000000}, seek position: {3:0.000000}",
                playbackLatency, GetTempoLatency(), syncVisualTime, seekPosition
            );
        }

        private void SuppressSyncCorrection()
        {
            double now = GetEstimatedCurrentInputTime();
            double playbackLatency = GetPlaybackLatency();
            double tempoLatency = GetTempoLatency();
            double latency = Math.Max(playbackLatency, tempoLatency);
            _syncCorrectionSuppressedUntil = now + latency;
            _nextSyncSpeedChangeTime = Math.Max(_nextSyncSpeedChangeTime, _syncCorrectionSuppressedUntil);
        }

        public double GetLatencyAdjustedStartDelay(double requestedDelay)
        {
            double playbackLatency = GetPlaybackLatency();
            return Math.Max(requestedDelay, playbackLatency + PLAYBACK_START_LATENCY_MARGIN);
        }

        public double GetLatencyLeadInSongTime(double requestedDelay = 0)
        {
            return GetLatencyAdjustedStartDelay(requestedDelay) * SongSpeed;
        }

        public float GetLatencyAdjustedRewindDuration(float minimumDuration)
        {
            return (float) Math.Max(minimumDuration, GetLatencyAdjustedStartDelay(0));
        }

        public double GetRelativeInputTime(double timeFromInputSystem)
        {
            return (timeFromInputSystem - InputTimeOffset) * SongSpeed;
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

        private double GetUpdateInputSystemTime()
        {
            double inputUpdateTime = InputManager.InputUpdateTime;
            if (inputUpdateTime >= _minimumUpdateInputSystemTime)
            {
                _minimumUpdateInputSystemTime = double.NegativeInfinity;
                return inputUpdateTime;
            }

            return _minimumUpdateInputSystemTime;
        }

        private void UpdateTimes()
        {
            UpdateTimes(GetUpdateInputSystemTime());
        }

        private void UpdateTimes(double inputSystemTime)
        {
            InputTime = GetRelativeInputTime(inputSystemTime);
            SongTime = InputTime + (AudioCalibration * SongSpeed);
            VisualTime = InputTime + (VideoCalibration * SongSpeed);

            AudioPlaybackTime = Math.Max(0, _mixer.GetSyncPosition());
        }

        private void SetInputBase(double songTime)
        {
            SetInputBase(songTime, InputManager.InputUpdateTime);
        }

        private void SetInputBase(double songTime, double inputSystemTime)
        {
            double previousOffset = InputTimeOffset;
            double previousInputTime = InputTime;
            double previousSongTime = SongTime;
            double previousVisualTime = VisualTime;

            InputTimeOffset = inputSystemTime - (songTime / SongSpeed);

            // Update input times
            UpdateTimes(inputSystemTime);

            YargLogger.LogFormatDebug(
                "Set input time base.\n" +
                "Clock time: {0:0.000000}\n" +
                "Offset {1:0.000000} -> {2:0.000000}\n" +
                "Input time {3:0.000000} -> {4:0.000000}\n" +
                "Song time {5:0.000000} -> {6:0.000000}\n" +
                "Visual time {7:0.000000} -> {8:0.000000}",
                inputSystemTime,
                previousOffset, InputTimeOffset,
                previousInputTime, InputTime,
                previousSongTime, SongTime,
                previousVisualTime, VisualTime
            );
        }

        private void SetInputBaseChecked(double inputBase)
        {
            SetInputBaseChecked(inputBase, InputManager.InputUpdateTime);
        }

        private void SetInputBaseChecked(double inputBase, double inputSystemTime)
        {
            double previousVisualTime = VisualTime;
            double previousInputTime = InputTime;

            SetInputBase(inputBase, inputSystemTime);

            // Speeds above 200% or so can cause inaccuracies greater than 1 ms
            double threshold = Math.Max(0.001 * SongSpeed, 0.0005);
            YargLogger.AssertFormat(Math.Abs(VisualTime - previousVisualTime) <= threshold,
                "Unexpected visual time change! Went from {0} to {1}, threshold {2}",
                previousVisualTime, VisualTime, threshold);
            YargLogger.AssertFormat(Math.Abs(InputTime - previousInputTime) <= threshold,
                "Unexpected input time change! Went from {0} to {1}, threshold {2}",
                previousInputTime, InputTime, threshold);
        }

        private void SetResumeInputBase()
        {
            double resumeInputSystemTime = GetEstimatedCurrentInputTime();
            _minimumUpdateInputSystemTime = Math.Max(_minimumUpdateInputSystemTime, resumeInputSystemTime);
            SetInputBaseChecked(InputTime, resumeInputSystemTime);
        }

        private void InitializeSongTime(double time, double delayTime)
        {
            // Account for song speed
            delayTime *= SongSpeed;

            // Seek time
            // Doesn't account for audio calibration for better audio syncing
            // since seeking is slightly delayed
            double seekTime = time - delayTime;

            // Set input offsets
            SetInputBase(seekTime);

            YargLogger.LogFormatDebug("Set song time to {0:0.000000} (delay: {1:0.000000}).\n" +
                "Seek time: {2:0.000000}, resulting song time: {3:0.000000}", time, delayTime, seekTime, SongTime);
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            double requestedDelay = delayTime;
            delayTime = GetLatencyAdjustedStartDelay(delayTime);
            double playbackLatency = GetPlaybackLatency();
            double tempoLatency = GetTempoLatency();
            double seekTime;
            bool canStartAudio;

            lock (_syncLock)
            {
                if (!Mathf.Approximately(_effectiveSongSpeed, _requestedSongSpeed))
                {
                    _effectiveSongSpeed = _requestedSongSpeed;
                    _commandedAudioBaseSpeed = _effectiveSongSpeed;
                    _songSpeedSchedule.Clear();
                }
            }

            // Set input/song time
            InitializeSongTime(time, delayTime);
            ResetSyncEstimate();

            // Audio seeking; cannot go negative
            seekTime = time - (delayTime - playbackLatency - AudioCalibration) * SongSpeed - SongOffset;
            canStartAudio = seekTime >= 0;
            if (seekTime < 0)
            {
                seekTime = 0;
            }

            // Reset syncing before seeking to prevent speed adjustments from causing issues
            ResetSync();

            _mixer.Pause();
            double landingExpectedSyncAudioTime = seekTime;
            _mixer.SetPosition(seekTime);

            bool shouldPlay;
            lock (_syncLock)
            {
                shouldPlay = !Paused && canStartAudio;
            }

            if (shouldPlay)
            {
                _mixer.Play();
            }

            UpdateTimes();
            _seeked = true;

            double landingSyncAudioTime = _mixer.GetSyncPosition();
            RecordAudioInputSyncLanding("Seek", landingExpectedSyncAudioTime, landingSyncAudioTime);

            YargLogger.LogFormatDebug(
                "Set song time with latency budget.\n" +
                "Requested delay: {0:0.000000}, effective delay: {1:0.000000}, playback latency: {2:0.000000}, " +
                "tempo latency: {3:0.000000}, raw audio: {4:0.000000}, sync audio: {5:0.000000}, seek time: {6:0.000000}",
                requestedDelay, delayTime, playbackLatency, tempoLatency, _mixer.GetPosition(), _mixer.GetSyncPosition(), seekTime
            );
        }

        public void SetSongSpeed(float speed)
        {
            speed = ClampSongSpeed(speed);
            double now = GetEstimatedCurrentInputTime();
            double streamDelay = GetTempoLatency();
            double effectiveTime;
            bool immediate;
            float previousRequested;
            float previousEffective;

            lock (_syncLock)
            {
                previousRequested = _requestedSongSpeed;
                previousEffective = _effectiveSongSpeed;

                if (Mathf.Approximately(speed, _requestedSongSpeed) && _songSpeedSchedule.Count == 0)
                {
                    return;
                }

                _requestedSongSpeed = speed;
                _syncSpeedAdjustment = 0f;
                _syncCommandedSpeedAdjustment = 0f;
                _commandedAudioBaseSpeed = speed;

                immediate = !Started || Paused || streamDelay <= 0.0;
                if (immediate)
                {
                    _songSpeedSchedule.Clear();
                    double currentInputAtChange = (now - InputTimeOffset) * _effectiveSongSpeed;
                    _effectiveSongSpeed = speed;
                    InputTimeOffset = now - (currentInputAtChange / _effectiveSongSpeed);
                    effectiveTime = now;
                }
                else
                {
                    effectiveTime = now + streamDelay;
                    _songSpeedSchedule.Enqueue((effectiveTime, speed));
                }

                _syncCorrectionSuppressedUntil = Math.Max(_syncCorrectionSuppressedUntil, effectiveTime);
                _nextSyncSpeedChangeTime = Math.Max(_nextSyncSpeedChangeTime, effectiveTime);
            }

            _mixer.SetSpeed(speed, true);

            if (immediate)
            {
                UpdateTimes(now);
            }

            YargLogger.LogFormatDebug(
                "Set song speed. Requested {0:0.00} -> {1:0.00}, effective {2:0.00} -> {3:0.00}, " +
                "activation: {4:0.000000}, stream delay: {5:0.000000}.\n" +
                "Song time: {6:0.000000}, visual time: {7:0.000000}, input time: {8:0.000000}",
                previousRequested, speed, previousEffective, SongSpeed, effectiveTime, streamDelay,
                SongTime, VisualTime, InputTime);
        }

        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(RequestedSongSpeed + deltaSpeed);

        public void UpdateCalibration()
        {
            int videoCalibrationMs = SettingsManager.Settings.VideoCalibration.Value;
            int audioCalibrationMs = SettingsManager.Settings.AudioCalibration.Value;
            if (SettingsManager.Settings.AccountForHardwareLatency.Value)
            {
                audioCalibrationMs += GlobalAudioHandler.PlaybackLatency;
            }

            AudioCalibration = audioCalibrationMs / 1000.0;
            VideoCalibration = videoCalibrationMs / 1000.0;
            SetInputBase(InputTime);
        }

        /// <summary>
        /// Pauses the song.
        /// </summary>
        public void Pause()
        {
            // Ensure previous rewind tasks are dead
            _rewindSource?.Cancel();
            _rewindTween?.Kill();
            _rewindTween = null;

            if (PauseOverridden)
            {
                _resumeAfterOverride = false;
                return;
            }

            lock (_syncLock)
            {
                if (Paused)
                    return;

                Paused = true;
            }

            _mixer.Pause();
            ResetSync();
            ResetSyncEstimate();

            YargLogger.LogFormatDebug(
                "Paused at song time {0:0.000000}, visual time {1:0.000000}, input time {2:0.000000}.",
                SongTime, VisualTime, InputTime
            );
        }

        /// <summary>
        /// Resumes the song.
        /// </summary>
        public void Resume()
        {
            if (PauseOverridden)
            {
                _resumeAfterOverride = true;
                return;
            }

            lock (_syncLock)
            {
                if (!Paused)
                {
                    return;
                }
            }

            UpdateCalibration();
            SetResumeInputBase();
            ResetSync();
            ResetSyncEstimate();
            PreAlignResumeAudio();

            lock (_syncLock)
            {
                _justResumed = true;
                Paused = false;
            }

            YargLogger.LogFormatDebug(
                "Resumed at song time {0:0.000000}, visual time {1:0.000000}, input time {2:0.000000}.",
                SongTime, VisualTime, InputTime
            );
        }

        public void SetPaused(bool paused)
        {
            if (paused)
            {
                Pause();
            }
            else
            {
                Resume();
            }
        }

        /// <summary>
        /// Forces the song to be paused until <see cref="OverrideResume"/> is called,
        /// for long-running operations that must be completed before resuming.
        /// </summary>
        public void OverridePause()
        {
            if (!PauseOverridden)
            {
                Pause();
                _resumeAfterOverride = true;
            }

            _pauseOverrides++;
        }

        /// <summary>
        /// Removes the forced pause set by an <see cref="OverridePause"/> call.
        /// </summary>
        /// <returns>
        /// Whether or not the song was resumed. A pause that occurs during the override
        /// will take precedence, and prevent a resume from occurring here.
        /// </returns>
        public bool OverrideResume()
        {
            _pauseOverrides--;
            if (PauseOverridden)
            {
                return false;
            }

            if (_resumeAfterOverride)
                Resume();

            return !Paused;
        }

        public async UniTask<bool> RewindAndResume(double seconds, double? overrideTargetTime = null)
        {
            // We can only do this when paused
            if (!Paused)
            {
                return false;
            }

            _rewindSource?.Cancel();
            _rewindSource?.Dispose();
            _rewindSource = new CancellationTokenSource();
            var token = _rewindSource.Token;

            double resumeDelay = GetLatencyAdjustedStartDelay(0);
            double leadInSongTime = resumeDelay * SongSpeed;
            var targetRewindTime = SongTime - seconds;
            var targetVisualTime = targetRewindTime + (VideoCalibration - AudioCalibration) * SongSpeed - leadInSongTime;
            var targetResumeTime = overrideTargetTime ?? SongTime;
            float rewindDuration = GetLatencyAdjustedRewindDuration(0.5f);

            _rewindTween = DOTween.To(() => VisualTime, x => VisualTime = x, targetVisualTime, rewindDuration);

            var rewindCanceled = await _rewindTween
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(token)
                .SuppressCancellationThrow();

            if (rewindCanceled || token.IsCancellationRequested)
            {
                _rewindTween?.Kill();
                _rewindTween = null;
                return true;
            }

            SetSongTime(targetRewindTime - (AudioCalibration * SongSpeed), resumeDelay);
            Resume();


            var waitCanceled = await UniTask.WaitUntil(() => SongTime > targetResumeTime, cancellationToken: token)
                .SuppressCancellationThrow();

            if (waitCanceled || token.IsCancellationRequested)
            {
                return true;
            }

            return false;
        }

        public static float ClampSongSpeed(float speed)
        {
            // 10% - 5000%, we reserve 5% at the bottom so that audio syncing can still function.
            // BASS can go up to 5100%, but we round down since 5000% looks nicer (and it gives us a
            // good buffer for audio syncing in the upper extreme).
            return Math.Clamp(speed, 10 / 100f, 5000 / 100f);
        }
    }
}
