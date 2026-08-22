using System;
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
    //
    // SongRunner and its audio synchronizer run on Unity's main thread. This gives mixer transport
    // one owner, so synchronization cannot interleave with pause, seek, or speed-change operations.

    public class SongRunner : IDisposable
    {
        #region Times

        public const  double SONG_START_DELAY       = 2;
        private const double MAX_START_FRAME_LENGTH = 0.1;

        /// <summary>
        /// The time into the song, accounting for song speed and audio calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value should be used for all interactions that are relative to the audio.
        /// Note that this is driven by input time, rather than audio time.
        /// Use <see cref="AudioTime"/> if actual audio time is required.
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
        /// Current heard playback position relative to gameplay.
        /// </summary>
        /// <remarks>
        /// This value is for scenarios that <b>must</b> be tied to audio playback time,
        /// as opposed to input/visual time.
        /// In general, <see cref="SongTime"/> should be used instead where possible.
        /// </remarks>
        public double AudioTime => _mixer.GetPosition() - AudioCalibration + SongOffset;

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
        /// This is the sum of the chart-provided offset and <see cref="SongOffsetOverride"/>.
        /// </remarks>
        public double SongOffset { get; private set; }

        /// <summary>
        /// The chart/.ini-provided offset, in seconds. Fixed for the duration of the song.
        /// </summary>
        private double _chartSongOffset;

        /// <summary>
        /// A per-song offset override on top of <see cref="_chartSongOffset"/>, in seconds
        /// (not negated). This is the value edited live from the pause menu's "Specific Song
        /// Offset" and "Auto Calibrate Offset" options.
        /// </summary>
        public double SongOffsetOverride { get; private set; }

        /// <summary>
        /// The input time that is considered to be 0.
        /// </summary>
        public double InputTimeOffset { get; private set; }

        #endregion

        #region Other state

        /// <summary>
        /// The currently effective playback speed of the song.
        /// </summary>
        public float SongSpeed { get; private set; }

        /// <summary>
        /// The requested playback speed. The effective speed catches up after tempo-stream latency.
        /// </summary>
        private float _requestedSongSpeed;

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
        private bool PauseOverridden => _pauseOverrides > 0;

        private int  _pauseOverrides;
        private bool _resumeAfterOverride;

        private bool _pausedForFrameDebugger;

        private double _forceStartTime = double.NaN;
        private bool   _disposed;

        #endregion

        #region Rewind State

        private CancellationTokenSource _rewindSource;
        private Tween                   _rewindTween;

        #endregion

        #region Audio syncing

        private readonly StemMixer         _mixer;
        private readonly AudioSynchronizer _audioSynchronizer;

        public float DebugSyncAdjustment => _audioSynchronizer.EffectiveAdjustment;
        public float DebugSyncStartDelta => _audioSynchronizer.StartDelta;
        public float DebugSyncWorstDelta => _audioSynchronizer.WorstDelta;
        public double DebugRawControlSyncError => _audioSynchronizer.RawControlError;
        public double DebugControlSyncError => _audioSynchronizer.ControlError;

        /// <summary>
        /// Latest sampled difference between target time and mixer position currently being heard.
        /// </summary>
        public double SyncError => _audioSynchronizer.Error;

        /// <summary>
        /// Whether the audio synchronizer is currently applying a playback speed correction.
        /// </summary>
        public bool IsAudioSyncCorrectionActive => _audioSynchronizer.EffectiveAdjustment != 0f;

        #endregion

        #region Seek debugging

        private bool   _seeked;
        private double _previousInputTime    = double.MinValue;
        private double _inputSystemTimeFloor = double.NegativeInfinity;

        #endregion

        /// <summary>
        /// Creates a song runner at the given position and speed.
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
        /// <param name="mixer">The mixer used for song playback.</param>
        /// <param name="startTime">The initial gameplay time, in seconds.</param>
        /// <param name="startDelay">
        /// The delay before <paramref name="startTime"/>, in seconds of playback time.
        /// </param>
        /// <param name="songSpeed">The song speed, where 1f is 100%.</param>
        /// <param name="songOffset">
        /// The chart's audio offset, in seconds. This value is negated for internal use.
        /// </param>
        /// <param name="chartSongOffset">
        /// The chart's audio offset, in seconds. This value is negated for internal use.
        /// </param>
        /// <param name="songOffsetOverride">
        /// A per-song offset override on top of <paramref name="chartSongOffset"/>, in seconds
        /// (not negated), sourced from the user's recorded/manually-set song offset.
        /// </param>
        public SongRunner(
            StemMixer mixer,
            double startTime,
            double startDelay,
            float songSpeed,
            double chartSongOffset,
            double songOffsetOverride = 0
        )
        {
            _mixer = mixer;
            _audioSynchronizer = new AudioSynchronizer(mixer);
            SongSpeed = ClampSongSpeed(songSpeed);
            _requestedSongSpeed = SongSpeed;
            _chartSongOffset = chartSongOffset;
            SongOffsetOverride = songOffsetOverride;
            SongOffset = -(_chartSongOffset + SongOffsetOverride);
            InitializeSongTime(startTime + SongOffset, startDelay);
            UpdateCalibration();
        }

        /// <summary>
        /// Live-updates <see cref="SongOffsetOverride"/> (and therefore <see cref="SongOffset"/>)
        /// while preserving current gameplay time. Used by the pause menu's specific song offset
        /// setting and its auto-calibration.
        /// </summary>
        public void SetSongOffsetOverride(double songOffsetOverride)
        {
            SongOffsetOverride = songOffsetOverride;
            SongOffset = -(_chartSongOffset + SongOffsetOverride);
            AnchorTimeline(InputTime);
        }

        ~SongRunner()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _rewindSource?.Cancel();
            _rewindTween?.Kill();
            _rewindSource?.Dispose();
            _rewindSource = null;
            _rewindTween = null;
        }

        private void Start()
        {
            YargLogger.LogDebug("Starting song runner");

            // Re-initialize song times to avoid lag issues
            InitializeSongTime(InputTime, 0);
            double startInputTime = InputTime;
            PrepareAudioAt(startInputTime);
            PlayPreparedAudioAt(startInputTime);

            Started = true;
        }

        public void Update()
        {
            // Runner is lazy-started to avoid timing issues with lag
            if (!Started)
            {
                if (ShouldDelayStart())
                {
                    return;
                }

                Start();
            }

            UpdateFrameDebuggerPause();
            if (!Paused)
            {
                UpdatePlayback();
                ValidateInputTime();
            }
        }

        private bool ShouldDelayStart()
        {
            double currentTime = InputManager.CurrentInputTime;

            // Delay after a lagged starting frame, but only for one second at most.
            if (double.IsNaN(_forceStartTime))
            {
                _forceStartTime = currentTime + 1;
            }

            double frameLength = currentTime - InputManager.InputUpdateTime;

            return frameLength >= MAX_START_FRAME_LENGTH && currentTime < _forceStartTime;
        }

        private void UpdateFrameDebuggerPause()
        {
            if (_pausedForFrameDebugger == FrameDebugger.enabled)
            {
                return;
            }

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

        // Detect unexpected clock regressions without flagging deliberate seeks.
        private void ValidateInputTime()
        {
            YargLogger.AssertFormat(
                InputTime >= _previousInputTime || _seeked,
                "Unexpected time seek backwards! Went from {0} to {1} (delta: {2})",
                _previousInputTime,
                InputTime,
                InputTime - _previousInputTime
            );

            _previousInputTime = InputTime;
            _seeked = false;
        }

        /// <summary>
        /// Starts prepared mixer playback and anchors gameplay when playback is armed.
        /// </summary>
        private void PlayPreparedAudioAt(double inputTime)
        {
            _mixer.Play();

            // Priming and ChannelPlay happen after PrepareAudioAt's anchor. Exclude that work from
            // gameplay advancement so the target and mixer control clocks start at the same instant.
            AnchorTimeline(inputTime, InputManager.CurrentInputTime);
        }

        /// <summary>
        /// Converts an absolute input-system timestamp to gameplay input time.
        /// </summary>
        public double GetInputTime(double inputSystemTime)
        {
            return (inputSystemTime - InputTimeOffset) * SongSpeed;
        }

        /// <summary>
        /// Converts gameplay song time to a position in the audio file.
        /// </summary>
        public double GetAudioPlaybackTime(double songTime)
        {
            return songTime - SongOffset;
        }

        // Advance gameplay clocks from input-system time, then synchronize audio to them.
        private void UpdatePlayback()
        {
            // Re-anchoring can use a newer on-demand input timestamp than this frame's cached
            // timestamp. Hold the timeline at that anchor until the frame clock catches up.
            double inputSystemTime = Math.Max(InputManager.InputUpdateTime, _inputSystemTimeFloor);
            InputTime = GetInputTime(inputSystemTime);
            SongTime = InputTime + (AudioCalibration * SongSpeed);
            VisualTime = InputTime + (VideoCalibration * SongSpeed);

            // Project the frame's input timestamp with the same high-resolution clock used by the
            // mixer control timeline. This avoids measuring frame age as audio drift.
            double syncSystemTime = InputManager.CurrentInputTime;
            double syncInputTime = GetInputTime(syncSystemTime);
            double controlTargetTime = GetAudioPlaybackTime(syncInputTime);
            double heardTargetTime = controlTargetTime + (AudioCalibration * SongSpeed);
            _audioSynchronizer.Synchronize(controlTargetTime, heardTargetTime, SongSpeed, syncSystemTime);
        }

        /// <summary>
        /// Anchors gameplay time to the input timestamp captured for the current frame.
        /// </summary>
        private void AnchorTimeline(double inputTime)
        {
            AnchorTimeline(inputTime, InputManager.InputUpdateTime);
        }

        /// <summary>
        /// Anchors gameplay time to an absolute input-system timestamp and refreshes all timeline samples.
        /// </summary>
        private void AnchorTimeline(double inputTime, double inputSystemTime)
        {
            double previousOffset = InputTimeOffset;
            double previousInputTime = InputTime;
            double previousSongTime = SongTime;
            double previousVisualTime = VisualTime;

            InputTimeOffset = inputSystemTime - (inputTime / SongSpeed);
            _inputSystemTimeFloor = Math.Max(_inputSystemTimeFloor, inputSystemTime);

            InputTime = GetInputTime(inputSystemTime);
            SongTime = InputTime + (AudioCalibration * SongSpeed);
            VisualTime = InputTime + (VideoCalibration * SongSpeed);

            YargLogger.LogFormatDebug(
                "Set input time base.\n" +
                "Offset {0:0.000000} -> {1:0.000000}\n" +
                "Input time {2:0.000000} -> {3:0.000000}\n" +
                "Song time {4:0.000000} -> {5:0.000000}\n" +
                "Visual time {6:0.000000} -> {7:0.000000}",
                previousOffset, InputTimeOffset,
                previousInputTime, InputTime,
                previousSongTime, SongTime,
                previousVisualTime, VisualTime
            );
        }

        private void AnchorTimelineChecked(double inputTime)
        {
            double previousVisualTime = VisualTime;
            double previousInputTime = InputTime;

            AnchorTimeline(inputTime);

            // Speeds above 200% or so can cause inaccuracies greater than 1 ms
            double threshold = Math.Max(0.001 * SongSpeed, 0.0005);
            YargLogger.AssertFormat(Math.Abs(VisualTime - previousVisualTime) <= threshold,
                "Unexpected visual time change! Went from {0} to {1}, threshold {2}",
                previousVisualTime, VisualTime, threshold);
            YargLogger.AssertFormat(Math.Abs(InputTime - previousInputTime) <= threshold,
                "Unexpected input time change! Went from {0} to {1}, threshold {2}",
                previousInputTime, InputTime, threshold);
        }

        private void InitializeSongTime(double time, double songStartDelay)
        {
            // Account for song speed
            songStartDelay *= SongSpeed;

            // Seek time
            // Doesn't account for audio calibration for better audio syncing
            // since seeking is slightly delayed
            double seekTime = time - songStartDelay;

            // Set input offsets
            AnchorTimeline(seekTime);

            YargLogger.LogFormatDebug("Set song time to {0:0.000000} (delay: {1:0.000000}).\n" +
                "Seek time: {2:0.000000}, resulting song time: {3:0.000000}", time, songStartDelay, seekTime, SongTime);
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            SongSpeed = _requestedSongSpeed;

            // Set input/song time
            InitializeSongTime(time, delayTime);
            double seekInputTime = InputTime;

            PrepareAudioAt(seekInputTime);
            if (!Paused)
            {
                PlayPreparedAudioAt(seekInputTime);
            }

            _seeked = true;
        }

        public void SetSongSpeed(float speed)
        {
            SetSongSpeed(speed, rebuildPlayback: true);
        }

        private void SetSongSpeed(float speed, bool rebuildPlayback)
        {
            speed = ClampSongSpeed(speed);
            if (Mathf.Approximately(speed, _requestedSongSpeed))
            {
                return;
            }

            ApplySpeedChange(speed, rebuildPlayback);

            YargLogger.LogFormatDebug("Set song speed to {0:0.00}.\n"
                + "Song time: {1:0.000000}, visual time: {2:0.000000}, input time: {3:0.000000}",
                speed, SongTime, VisualTime, InputTime);
        }

        private void ApplySpeedChange(float speed, bool rebuildPlayback)
        {
            double inputTime = InputTime;
            _requestedSongSpeed = speed;
            SongSpeed = speed;

            if (!Started || Paused)
            {
                _audioSynchronizer.Reset(SongSpeed);
                AnchorTimelineChecked(inputTime);
                return;
            }

            if (!rebuildPlayback)
            {
                _audioSynchronizer.ChangeSongSpeed(SongSpeed);
                AnchorTimelineChecked(inputTime);
                return;
            }

            // BASS applies tempo changes after samples already buffered in the tempo stream.
            // Rebuild at the current position so gameplay and audible playback start the new
            // speed together instead of correcting the buffered transition in place.
            PrepareAudioAt(inputTime);
            PlayPreparedAudioAt(inputTime);
        }

        /// <summary>
        /// Changes requested playback speed by the given amount, where 1f is 100%.
        /// </summary>
        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(_requestedSongSpeed + deltaSpeed);

        /// <summary>
        /// Changes requested playback speed without rebuilding active playback.
        /// </summary>
        public void AdjustSongSpeedInPlace(float deltaSpeed) =>
            SetSongSpeed(_requestedSongSpeed + deltaSpeed, rebuildPlayback: false);

        /// <summary>
        /// Reloads audio and video calibration settings while preserving current gameplay time.
        /// </summary>
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

            // Tell the mixer which modeled output position should represent heard audio.
            _mixer.SetOutputLatency(AudioCalibration);
            AnchorTimeline(InputTime);
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

            if (Paused)
            {
                return;
            }

            _audioSynchronizer.Suspend(SongSpeed);
            Paused = true;
            _mixer.Pause();

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

            if (!Paused)
            {
                return;
            }

            // Settings can change from the pause menu.
            UpdateCalibration();
            double resumeInputTime = InputTime;
            PrepareAudioAt(resumeInputTime);
            Paused = false;
            PlayPreparedAudioAt(resumeInputTime);

            YargLogger.LogFormatDebug(
                "Resumed at song time {0:0.000000}, visual time {1:0.000000}, input time {2:0.000000}.",
                SongTime, VisualTime, InputTime
            );
        }

        /// <summary>
        /// Sets mixer playback at the given gameplay time and anchors the gameplay clock after
        /// the seek completes.
        /// </summary>
        private void PrepareAudioAt(double inputTime)
        {
            _mixer.Pause();
            _audioSynchronizer.Reset(SongSpeed);
            double audioTime = GetAudioPlaybackTime(inputTime);
            _mixer.SetPosition(audioTime);
            AnchorTimeline(inputTime, InputManager.CurrentInputTime);
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
            {
                Resume();
            }

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

            var targetRewindTime = SongTime - seconds;
            var targetVisualTime = targetRewindTime + (VideoCalibration - AudioCalibration) * SongSpeed;
            var targetResumeTime = overrideTargetTime ?? SongTime;

            _rewindTween = DOTween.To(() => VisualTime, x => VisualTime = x, targetVisualTime, 0.5f);

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

            SetSongTime(targetRewindTime - (AudioCalibration * SongSpeed), 0);
            Resume();

            var waitCanceled = await UniTask.WaitUntil(() => SongTime > targetResumeTime, cancellationToken: token)
                .SuppressCancellationThrow();

            if (waitCanceled || token.IsCancellationRequested)
            {
                return true;
            }

            return false;
        }

        private static float ClampSongSpeed(float speed)
        {
            // 10% - 5000%, we reserve 5% at the bottom so that audio syncing can still function.
            // BASS can go up to 5100%, but we round down since 5000% looks nicer (and it gives us a
            // good buffer for audio syncing in the upper extreme).
            return Math.Clamp(speed, 10 / 100f, 5000 / 100f);
        }
    }

    /// <summary>
    /// Keeps mixer playback aligned with gameplay time by applying bounded speed corrections.
    /// Corrections stop near song boundaries.
    /// </summary>
    public sealed class AudioSynchronizer
    {
        public enum SyncState
        {
            Idle,
            Correcting,
            Settling,
        }

        private const double SYNC_START_SECONDS      = 0.003;
        private const double SYNC_STOP_SECONDS       = 0.0015;
        private const double SETTLE_MARGIN_SECONDS   = 0.025;
        private const double MIN_CORRECTION_TIME_SECONDS = 0.100;
        private const double MAX_CORRECTION_TIME_SECONDS = 0.500;
        private const double CORRECTION_DELAY_MULTIPLIER  = 0.10;
        private const float  SYNC_CLAMP              = 2.00f;
        private const float  MIN_SYNC_SPEED_RATIO    = 0.50f;

        private readonly StemMixer _mixer;

        private float     Adjustment          { get; set; }
        private SyncState _state;
        public  SyncState State               => _state;
        private double    _settleUntil         = double.NegativeInfinity;
        public  float  EffectiveAdjustment { get; private set; }
        public  float  StartDelta          { get; private set; }
        public  float  WorstDelta          { get; private set; }
        /// <summary>
        /// Latest difference between calibrated target time and mixer position currently being heard.
        /// </summary>
        public  double Error               { get; private set; }
        /// <summary>
        /// Latest unfiltered difference between target time and delay-free control position.
        /// </summary>
        public  double RawControlError     { get; private set; }
        /// <summary>
        /// Control error used to drive playback correction.
        /// </summary>
        public  double ControlError        { get; private set; }

        public AudioSynchronizer(StemMixer mixer)
        {
            _mixer = mixer;
        }

        /// <summary>
        /// Samples mixer position and corrects its control-time error.
        /// </summary>
        /// <param name="controlTargetTime">Required audio-file position on the input clock.</param>
        /// <param name="heardTargetTime">
        /// Required heard audio-file position after applying audio calibration.
        /// </param>
        /// <param name="songSpeed">Requested playback speed before synchronization correction.</param>
        public void Synchronize(double controlTargetTime, double heardTargetTime, float songSpeed,
            double targetTimestamp)
        {
            SyncPosition position = _mixer.GetSyncPosition();
            double now = InputManager.CurrentInputTime;
            double targetAdvance = (now - targetTimestamp) * songSpeed;
            controlTargetTime += targetAdvance;
            heardTargetTime += targetAdvance;
            Error = heardTargetTime - position.Heard;
            double rawControlError = controlTargetTime - position.Control;

            if (_state == SyncState.Settling && now >= _settleUntil)
            {
                _state = SyncState.Idle;
            }

            RawControlError = rawControlError;
            ControlError = rawControlError;

            bool isWithinSongBounds =
                controlTargetTime >= 0 &&
                controlTargetTime < _mixer.Length &&
                position.Control < _mixer.Length;

            float adjustment = GetCorrectionAdjustment(ControlError, isWithinSongBounds, songSpeed);

            EffectiveAdjustment = adjustment;
            RecordCorrection(adjustment);
            ApplyAdjustment(songSpeed, adjustment);
        }

        private float GetCorrectionAdjustment(double controlError, bool isWithinSongBounds, float songSpeed)
        {
            if (!isWithinSongBounds || _state == SyncState.Settling)
            {
                return 0;
            }

            // While actively correcting, stay in correcting mode until error drops below stop threshold
            if (_state == SyncState.Correcting)
            {
                if (Math.Abs(controlError) < SYNC_STOP_SECONDS)
                {
                    return 0;
                }

                return CalculateCorrectionAdjustment(controlError, songSpeed);
            }

            // Require error to exceed start threshold before starting correction
            if (Math.Abs(controlError) < SYNC_START_SECONDS)
            {
                return 0;
            }

            return CalculateCorrectionAdjustment(controlError, songSpeed);
        }

        private float CalculateCorrectionAdjustment(double controlError, float songSpeed)
        {
            double scaledDelay = _mixer.GetTempoStreamLatency() * CORRECTION_DELAY_MULTIPLIER;
            double correctionTime = Math.Clamp(
                MIN_CORRECTION_TIME_SECONDS + scaledDelay,
                MIN_CORRECTION_TIME_SECONDS,
                MAX_CORRECTION_TIME_SECONDS);

            float minimumAdjustment = Math.Max(-SYNC_CLAMP,
                songSpeed * (MIN_SYNC_SPEED_RATIO - 1f));
            return Math.Clamp((float) (controlError / correctionTime), minimumAdjustment, SYNC_CLAMP);
        }

        /// <summary>
        /// Restores requested speed and rebuilds mixer speed state after a timeline discontinuity.
        /// </summary>
        public void Reset(float songSpeed)
        {
            Adjustment = 0f;
            EffectiveAdjustment = 0f;
            StartDelta = 0f;
            WorstDelta = 0f;
            Error = 0;
            RawControlError = 0;
            ControlError = 0;
            _state = SyncState.Idle;
            _settleUntil = double.NegativeInfinity;
            _mixer.SetPlaybackSpeed(songSpeed);
        }

        /// <summary>
        /// Removes active correction while playback synchronization is suspended.
        /// </summary>
        public void Suspend(float songSpeed)
        {
            EffectiveAdjustment = 0f;
            ApplyAdjustment(songSpeed, 0f);
        }

        /// <summary>
        /// Changes requested song speed while preserving active synchronization correction.
        /// </summary>
        public void ChangeSongSpeed(float songSpeed)
        {
            _state = Adjustment == 0f ? SyncState.Idle : SyncState.Correcting;
            _settleUntil = double.NegativeInfinity;
            _mixer.SetPlaybackSpeed(songSpeed, Adjustment, true);
        }

        private void RecordCorrection(float adjustment)
        {
            bool correctionStarting = _state == SyncState.Idle && adjustment != 0f;
            if (correctionStarting)
            {
                StartDelta = (float) ControlError;
                WorstDelta = StartDelta;
            }
            else if (adjustment != 0f && Math.Abs(ControlError) > Math.Abs(WorstDelta))
            {
                WorstDelta = (float) ControlError;
            }
        }

        private void ApplyAdjustment(float songSpeed, float adjustment)
        {
            if (Mathf.Approximately(adjustment, Adjustment))
            {
                return;
            }

            bool correctionEnding = _state == SyncState.Correcting && adjustment == 0f;
            Adjustment = adjustment;
            _mixer.SetPlaybackSpeed(songSpeed, adjustment, false);

            if (correctionEnding)
            {
                // Do not react to control error while the final base-speed command is still
                // buffered. Wait for measured output to include that command before correcting again.
                BeginSettling(InputManager.CurrentInputTime);
            }
            else if (adjustment != 0f)
            {
                _state = SyncState.Correcting;
            }
        }

        private void BeginSettling(double now)
        {
            _state = SyncState.Settling;
            _settleUntil = now + _mixer.GetTempoStreamLatency() + SETTLE_MARGIN_SECONDS;
        }

    }
}
