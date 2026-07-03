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
        /// The set playback speed of the song.
        /// </summary>
        public float SongSpeed { get; private set; }

        /// <summary>
        /// The actual current playback speed of the song.
        /// </summary>
        /// <remarks>
        /// The audio may be sped up or slowed down in order to re-synchronize.
        /// This value takes that speed adjustment into account.
        /// </remarks>
        public float RealSongSpeed => SongSpeed + _syncSpeedAdjustment;

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
        private readonly struct SyncSpeedCommand
        {
            public readonly double EffectiveTime;
            public readonly float  Adjustment;

            public SyncSpeedCommand(double effectiveTime, float adjustment)
            {
                EffectiveTime = effectiveTime;
                Adjustment = adjustment;
            }
        }

        private Thread _syncThread;

        private bool _disposed;

        private readonly Queue<SyncSpeedCommand> _pendingSyncSpeedCommands = new();
        private volatile float _syncSpeedAdjustment;
        private volatile float _syncCommandedSpeedAdjustment;
        private volatile int   _syncSpeedMultiplier;
        private volatile float _syncStartDelta;
        private volatile float _syncWorstDelta;
        private volatile float _syncSmoothedDrift = float.NaN;
        private bool _syncRecoveryActive;
        private double _syncCorrectionSuppressedUntil = double.NegativeInfinity;
        private double _nextSyncSpeedChangeTime = double.NegativeInfinity;

        private bool _justResumed;

        private readonly StemMixer _mixer;

        public float SyncSpeedAdjustment => _syncSpeedAdjustment;
        public int SyncSpeedMultiplier => _syncSpeedMultiplier;
        public float SyncStartDelta => _syncStartDelta;
        public float SyncWorstDelta => _syncWorstDelta;
        public double EstimatedOutputLatency => _mixer.GetAudibleSyncLatency();
        public double CommandLatency => _mixer.GetCommandLatency();

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
            SongSpeed = songSpeed;
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

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    if (_syncThread.IsAlive)
                    {
                        _syncThread.Join();
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

            if (Paused)
                return;

            // Update times
            UpdateTimes();

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

        private double GetAudibleSyncLatency()
        {
            return SanitizeLatency(_mixer.GetAudibleSyncLatency());
        }

        private double GetCommandLatency()
        {
            return SanitizeLatency(_mixer.GetCommandLatency());
        }

        private double GetStartLatency()
        {
            return SanitizeLatency(_mixer.GetStartLatency());
        }

        private double GetPausedResumeLatency()
        {
            return SanitizeLatency(_mixer.GetPausedResumeLatency());
        }

        private static double GetResumeLatency(double audibleSyncLatency, double startLatency)
        {
            return Math.Max(audibleSyncLatency, startLatency);
        }

        private static double GetResumeLatency(double audibleSyncLatency, double startLatency, double pausedResumeLatency)
        {
            return Math.Max(GetResumeLatency(audibleSyncLatency, startLatency), pausedResumeLatency);
        }

        private void ApplyAudibleSyncSpeedCommands(double currentTime)
        {
            while (_pendingSyncSpeedCommands.Count > 0 &&
                _pendingSyncSpeedCommands.Peek().EffectiveTime <= currentTime)
            {
                var command = _pendingSyncSpeedCommands.Dequeue();
                _syncSpeedAdjustment = command.Adjustment;
            }
        }

        private static float GetScheduledSpeedCorrection(
            float currentAdjustment,
            List<SyncSpeedCommand> pendingCommands,
            double startTime,
            double endTime)
        {
            if (endTime <= startTime)
            {
                return 0f;
            }

            double correction = 0;
            double previousTime = startTime;
            float adjustment = currentAdjustment;

            foreach (var command in pendingCommands)
            {
                if (command.EffectiveTime <= startTime)
                {
                    adjustment = command.Adjustment;
                    continue;
                }

                if (command.EffectiveTime >= endTime)
                {
                    break;
                }

                correction += adjustment * (command.EffectiveTime - previousTime);
                previousTime = command.EffectiveTime;
                adjustment = command.Adjustment;
            }

            correction += adjustment * (endTime - previousTime);
            return (float) correction;
        }

        private void SyncThread()
        {
            for (; !_disposed; Thread.Sleep(1))
            {
                double songSpeed;
                double songOffset;
                double audioCalibration;
                double inputTimeOffset;
                double syncCorrectionSuppressedUntil;
                double nextSyncSpeedChangeTime;
                bool paused;

                lock (_syncThread)
                {
                    songSpeed = SongSpeed;
                    songOffset = SongOffset;
                    audioCalibration = AudioCalibration;
                    inputTimeOffset = InputTimeOffset;
                    syncCorrectionSuppressedUntil = _syncCorrectionSuppressedUntil;
                    nextSyncSpeedChangeTime = _nextSyncSpeedChangeTime;
                    paused = Paused;
                }

                double currentInputTime = GetEstimatedCurrentInputTime();
                double audioOffset = songOffset - (audioCalibration * songSpeed);
                double currentSongTime = (currentInputTime - inputTimeOffset) * songSpeed;
                double rawAudioTime = _mixer.GetPosition();
                double syncAudioTime = _mixer.GetSyncPosition();
                double syncVisualTime = currentSongTime - audioOffset;
                double audibleSyncLatency = GetAudibleSyncLatency();
                double commandLatency = GetCommandLatency();
                double startLatency = GetStartLatency();
                double pausedResumeLatency = GetPausedResumeLatency();
                double resumeLatency = GetResumeLatency(audibleSyncLatency, startLatency, pausedResumeLatency);
                double preRollSongTime = resumeLatency * songSpeed;

                // Reset justResumed if we are still in the lead-in
                lock (_syncThread)
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
                    lock (_syncThread)
                    {
                        justResumed = _justResumed;
                        _justResumed = false;
                    }

                    if (justResumed)
                    {
                        double frameStart = InputManager.FrameStartCpuTime;
                        if (frameStart > 0)
                        {
                            double now = (double) System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
                            delay = Math.Max(0, now - frameStart);
                        }

                        // Align to the input clock, not the render frame. InputState.currentTime can be stale
                        // between input updates, so extrapolate from the last input timestamp using CPU time.
                        double resumeCommandInputTime = GetEstimatedCurrentInputTime();
                        double adjustedSyncVisualTime = ((resumeCommandInputTime - inputTimeOffset) * songSpeed) - audioOffset;
                        double seekPosition = GetLatencyAlignedSeekPosition(adjustedSyncVisualTime, resumeLatency, songSpeed);
                        _mixer.SetPosition(seekPosition);

                        YargLogger.LogFormatDebug(
                            "Aligned resumed audio. Sync visual: {0:0.000000}, adjusted sync visual: {1:0.000000}, seek position: {2:0.000000}, " +
                            "audible latency: {3:0.000000}, start latency: {4:0.000000}, paused resume latency: {5:0.000000}, " +
                            "resume latency: {6:0.000000}, command latency: {7:0.000000}, resume command delay: {8:0.000000}",
                            syncVisualTime, adjustedSyncVisualTime, seekPosition, audibleSyncLatency, startLatency, pausedResumeLatency,
                            resumeLatency, commandLatency, delay
                        );
                    }

                    _mixer.Play();
                    rawAudioTime = _mixer.GetPosition();
                    syncAudioTime = _mixer.GetSyncPosition();
                }

                if (paused || syncVisualTime < 0 || syncVisualTime >= _mixer.Length)
                {
                    lock (_syncThread)
                    {
                        SyncAudioTime = syncAudioTime;
                        SyncVisualTime = syncVisualTime;
                    }
                    continue;
                }

                if (syncAudioTime >= _mixer.Length)
                {
                    lock (_syncThread)
                    {
                        SyncAudioTime = syncAudioTime;
                        SyncVisualTime = syncVisualTime;
                    }
                    continue;
                }

                double delta = syncVisualTime - syncAudioTime;

                float smoothedDrift;
                float speedAdjustment;
                float commandedSpeedAdjustment;
                int speedMultiplierState;
                float startDelta;
                float worstDelta;
                bool recoveryActive;
                List<SyncSpeedCommand> pendingSpeedCommands;

                lock (_syncThread)
                {
                    ApplyAudibleSyncSpeedCommands(currentInputTime);
                    smoothedDrift = _syncSmoothedDrift;
                    speedAdjustment = _syncSpeedAdjustment;
                    commandedSpeedAdjustment = _syncCommandedSpeedAdjustment;
                    speedMultiplierState = _syncSpeedMultiplier;
                    startDelta = _syncStartDelta;
                    worstDelta = _syncWorstDelta;
                    recoveryActive = _syncRecoveryActive;
                    pendingSpeedCommands = new List<SyncSpeedCommand>(_pendingSyncSpeedCommands);
                }

                // Smooth the drift over a few frames (Low-Pass Filter)
                if (float.IsNaN(smoothedDrift))
                {
                    smoothedDrift = (float) delta;
                }
                else
                {
                    smoothedDrift = Mathf.Lerp(smoothedDrift, (float) delta, 0.15f);
                }

                // General sync correction. Small drift uses gentle PLL trimming; larger step errors
                // use faster catch-up. The current speed adjustment keeps affecting audible audio until
                // the next command exits the output buffer, so predict drift at that effect time.
                const float RECOVERY_ENTER_BAND = 0.003f;
                const float RECOVERY_ENTER_CONFIRM_BAND = 0.002f;
                const float RECOVERY_EXIT_BAND = 0.001f;
                const float RECOVERY_TIME = 0.5f;
                const float MAX_RECOVERY_ADJUSTMENT = 0.03f;
                const double MIN_SYNC_COMMAND_INTERVAL = 0.1;

                float targetAdjustment;
                bool previousRecoveryActive = recoveryActive;
                double commandEffectTime = currentInputTime + commandLatency;
                float scheduledCorrection = GetScheduledSpeedCorrection(
                    speedAdjustment,
                    pendingSpeedCommands,
                    currentInputTime,
                    commandEffectTime
                );
                float predictedDrift = (float) delta - scheduledCorrection;
                float predictedSmoothedDrift = smoothedDrift - scheduledCorrection;
                if (currentInputTime < syncCorrectionSuppressedUntil)
                {
                    targetAdjustment = 0f;
                    predictedDrift = 0f;
                    predictedSmoothedDrift = float.NaN;
                    smoothedDrift = float.NaN;
                    speedMultiplierState = 0;
                    recoveryActive = false;
                }
                else
                {
                    float absolutePredictedDrift = Math.Abs(predictedDrift);
                    float absolutePredictedSmoothedDrift = Math.Abs(predictedSmoothedDrift);
                    bool rawDriftExceedsRecoveryBand = absolutePredictedDrift > RECOVERY_ENTER_BAND;
                    bool smoothedDriftExceedsRecoveryBand = absolutePredictedSmoothedDrift > RECOVERY_ENTER_BAND;
                    bool smoothedDriftConfirmsRawDrift = absolutePredictedSmoothedDrift > RECOVERY_ENTER_CONFIRM_BAND;

                    if (recoveryActive)
                    {
                        recoveryActive = absolutePredictedDrift > RECOVERY_EXIT_BAND;
                    }
                    else
                    {
                        recoveryActive = smoothedDriftExceedsRecoveryBand ||
                            (rawDriftExceedsRecoveryBand && smoothedDriftConfirmsRawDrift);
                    }

                    if (recoveryActive)
                    {
                        targetAdjustment = Mathf.Clamp(
                            predictedDrift / RECOVERY_TIME,
                            -MAX_RECOVERY_ADJUSTMENT,
                            MAX_RECOVERY_ADJUSTMENT
                        );
                    }
                    else
                    {
                        targetAdjustment = 0f;
                    }
                }

                if (previousRecoveryActive != recoveryActive)
                {
                    YargLogger.LogDebug(
                        $"Sync recovery {(recoveryActive ? "started" : "stopped")}. " +
                        $"Sync thread delta: {delta * 1000.0:0.000}ms, predicted drift: {predictedDrift * 1000.0f:0.000}ms, " +
                        $"smoothed drift: {smoothedDrift * 1000.0f:0.000}ms, audible latency: {audibleSyncLatency * 1000.0:0.000}ms, " +
                        $"command latency: {commandLatency * 1000.0:0.000}ms, raw audio: {rawAudioTime:0.000000}, " +
                        $"sync audio: {syncAudioTime:0.000000}, sync visual: {syncVisualTime:0.000000}, " +
                        $"adjustment: {targetAdjustment:0.000000}."
                    );

                    if (!recoveryActive)
                    {
                        smoothedDrift = float.NaN;
                    }
                }

                // Update debug/status variables using hysteresis to prevent boundary oscillation
                float speedStateDrift = predictedSmoothedDrift;
                int speedMultiplier = speedMultiplierState;
                if (speedMultiplierState == 0)
                {
                    if (speedStateDrift > 0.003f)
                    {
                        speedMultiplier = 1;
                    }
                    else if (speedStateDrift < -0.003f)
                    {
                        speedMultiplier = -1;
                    }
                }
                else if (speedMultiplierState == 1)
                {
                    if (speedStateDrift < 0.0015f)
                    {
                        speedMultiplier = 0;
                    }
                }
                else if (speedMultiplierState == -1)
                {
                    if (speedStateDrift > -0.0015f)
                    {
                        speedMultiplier = 0;
                    }
                }

                if (speedMultiplierState != speedMultiplier)
                {
                    int previousSpeedMultiplier = speedMultiplierState;
                    if (speedMultiplierState == 0)
                    {
                        startDelta = (float) delta;
                        worstDelta = startDelta;
                    }
                    speedMultiplierState = speedMultiplier;

                    YargLogger.LogDebug(
                        $"Sync speed multiplier {previousSpeedMultiplier} -> {speedMultiplier}. " +
                        $"Delta: {delta * 1000.0:0.000}ms, smoothed drift: {smoothedDrift * 1000.0:0.000}ms, " +
                        $"predicted drift: {predictedDrift * 1000.0f:0.000}ms, " +
                        $"predicted smoothed drift: {predictedSmoothedDrift * 1000.0f:0.000}ms, " +
                        $"raw audio: {rawAudioTime:0.000000}, sync audio: {syncAudioTime:0.000000}, " +
                        $"sync visual: {syncVisualTime:0.000000}, audible latency: {audibleSyncLatency:0.000000}, " +
                        $"command latency: {commandLatency:0.000000}, input: {currentSongTime:0.000000}, " +
                        $"real speed: {songSpeed + targetAdjustment:0.000000}."
                    );
                }

                if (speedMultiplierState != 0 && Math.Abs(delta) > Math.Abs(worstDelta))
                {
                    worstDelta = (float) delta;
                }

                if (!Mathf.Approximately(targetAdjustment, commandedSpeedAdjustment) &&
                    currentInputTime >= nextSyncSpeedChangeTime)
                {
                    commandedSpeedAdjustment = targetAdjustment;
                    _mixer.SetSpeed((float) (songSpeed + targetAdjustment), false);
                    double effectiveTime = currentInputTime + commandLatency;
                    lock (_syncThread)
                    {
                        _pendingSyncSpeedCommands.Enqueue(new SyncSpeedCommand(effectiveTime, targetAdjustment));
                        _syncCommandedSpeedAdjustment = commandedSpeedAdjustment;
                    }
                    nextSyncSpeedChangeTime = currentInputTime + MIN_SYNC_COMMAND_INTERVAL;
                }

                // Write everything back under the lock
                lock (_syncThread)
                {
                    SyncAudioTime = syncAudioTime;
                    SyncVisualTime = syncVisualTime;
                    _syncSmoothedDrift = smoothedDrift;
                    _syncSpeedAdjustment = speedAdjustment;
                    _syncCommandedSpeedAdjustment = commandedSpeedAdjustment;
                    _syncSpeedMultiplier = speedMultiplierState;
                    _syncRecoveryActive = recoveryActive;
                    _syncStartDelta = startDelta;
                    _syncWorstDelta = worstDelta;
                    _syncCorrectionSuppressedUntil = syncCorrectionSuppressedUntil;
                    _nextSyncSpeedChangeTime = nextSyncSpeedChangeTime;
                }
            }
        }

        private void ResetSync()
        {
            lock (_syncThread)
            {
                _syncSpeedMultiplier = 0;
                _syncSpeedAdjustment = 0f;
                _syncCommandedSpeedAdjustment = 0f;
                _pendingSyncSpeedCommands.Clear();
                _syncSmoothedDrift = float.NaN;
                _syncRecoveryActive = false;
                _justResumed = false;
            }

            _mixer.SetSpeed(RealSongSpeed, true);
            SuppressSyncCorrection();
        }

        private void ResetSyncEstimate()
        {
            _syncSmoothedDrift = float.NaN;
        }

        private double GetLatencyAlignedSeekPosition(double syncVisualTime, double syncLatency, double songSpeed)
        {
            return Math.Clamp(syncVisualTime + (syncLatency * songSpeed), 0, _mixer.Length);
        }

        private void PreAlignResumeAudio()
        {
            double audibleSyncLatency = GetAudibleSyncLatency();
            double startLatency = GetStartLatency();
            double pausedResumeLatency = GetPausedResumeLatency();
            double resumeLatency = GetResumeLatency(audibleSyncLatency, startLatency, pausedResumeLatency);
            if (resumeLatency <= 0)
            {
                return;
            }

            double audioOffset = SongOffset - (AudioCalibration * SongSpeed);
            double syncVisualTime = InputTime - audioOffset;
            double seekPosition = GetLatencyAlignedSeekPosition(syncVisualTime, resumeLatency, SongSpeed);

            _mixer.SetPosition(seekPosition);

            YargLogger.LogFormatDebug(
                "Pre-aligned resume audio. Resume latency: {0:0.000000}, audible sync latency: {1:0.000000}, " +
                "start latency: {2:0.000000}, paused resume latency: {3:0.000000}, command latency: {4:0.000000}, " +
                "sync visual: {5:0.000000}, seek position: {6:0.000000}",
                resumeLatency, audibleSyncLatency, startLatency, pausedResumeLatency, GetCommandLatency(), syncVisualTime, seekPosition
            );
        }

        private void SuppressSyncCorrection()
        {
            double now = GetEstimatedCurrentInputTime();
            double commandLatency = GetCommandLatency();
            double resumeLatency = GetResumeLatency(GetAudibleSyncLatency(), GetStartLatency(), GetPausedResumeLatency());
            double latency = Math.Max(resumeLatency, commandLatency);
            _syncCorrectionSuppressedUntil = now + latency;
            _nextSyncSpeedChangeTime = Math.Max(_nextSyncSpeedChangeTime, _syncCorrectionSuppressedUntil);
        }

        public double GetLatencyAdjustedStartDelay(double requestedDelay)
        {
            double startLatency = GetStartLatency();
            return Math.Max(requestedDelay, startLatency + PLAYBACK_START_LATENCY_MARGIN);
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
            double currentInputTime = InputManager.CurrentInputTime;
            double inputUpdateCpuTime = InputManager.InputUpdateCpuTime;
            if (inputUpdateCpuTime <= 0)
            {
                return currentInputTime;
            }

            double elapsed = Math.Max(0, GetCurrentCpuTime() - inputUpdateCpuTime);
            double estimatedInputTime = InputManager.InputUpdateTime + elapsed;
            return Math.Max(currentInputTime, estimatedInputTime);
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
            lock (_syncThread)
            {
                double requestedDelay = delayTime;
                delayTime = GetLatencyAdjustedStartDelay(delayTime);
                double audibleSyncLatency = GetAudibleSyncLatency();
                double commandLatency = GetCommandLatency();
                double startLatency = GetStartLatency();
                double pausedResumeLatency = GetPausedResumeLatency();
                double resumeLatency = GetResumeLatency(audibleSyncLatency, startLatency, pausedResumeLatency);

                // Set input/song time
                InitializeSongTime(time, delayTime);
                ResetSyncEstimate();

                // Reset syncing before seeking to prevent speed adjustments from causing issues
                ResetSync();

                _mixer.Pause();
                // Audio seeking; cannot go negative
                double seekTime = time - (delayTime - resumeLatency - AudioCalibration) * SongSpeed - SongOffset;
                bool canStartAudio = seekTime >= 0;
                if (seekTime < 0)
                {
                    seekTime = 0;
                    _mixer.SetPosition(seekTime);
                }
                else
                {
                    _mixer.SetPosition(seekTime);
                }

                if (!Paused && canStartAudio)
                {
                    _mixer.Play();
                }

                UpdateTimes();
                _seeked = true;

                YargLogger.LogFormatDebug(
                    "Set song time with latency budget.\n" +
                    "Requested delay: {0:0.000000}, effective delay: {1:0.000000}, resume latency: {2:0.000000}, " +
                    "audible latency: {3:0.000000}, start latency: {4:0.000000}, paused resume latency: {5:0.000000}, " +
                    "command latency: {6:0.000000}, raw audio: {7:0.000000}, sync audio: {8:0.000000}, seek time: {9:0.000000}",
                    requestedDelay, delayTime, resumeLatency, audibleSyncLatency, startLatency, pausedResumeLatency,
                    commandLatency, _mixer.GetPosition(), _mixer.GetSyncPosition(), seekTime
                );
            }
        }

        public void SetSongSpeed(float speed)
        {
            lock (_syncThread)
            {
                speed = ClampSongSpeed(speed);

                // Set speed; save old for input offset compensation
                SongSpeed = speed;

                // Set based on the actual song speed, so as to not break resyncing
                _mixer.SetSpeed(RealSongSpeed, true);
                SuppressSyncCorrection();

                // Adjust input offset, otherwise input time will desync
                // TODO: Pressing and holding left or right in practice will
                // cause time to progress much slower than it should
                SetInputBaseChecked(InputTime);
            }

            YargLogger.LogFormatDebug("Set song speed to {0:0.00}.\n"
                + "Song time: {1:0.000000}, visual time: {2:0.000000}, input time: {3:0.000000}", speed,
                SongTime, VisualTime, InputTime);
        }

        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(SongSpeed + deltaSpeed);

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

            lock (_syncThread)
            {
                if (Paused)
                    return;

                Paused = true;
                _mixer.Pause();
                ResetSync();
                ResetSyncEstimate();
            }

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

            lock (_syncThread)
            {
                if (!Paused)
                {
                    return;
                }

                UpdateCalibration();
                SetResumeInputBase();
                ResetSync();
                ResetSyncEstimate();
                PreAlignResumeAudio();
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
