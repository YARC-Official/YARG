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

    public class SongRunner : IDisposable, ISongSyncStateProvider
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
        /// The current gameplay input time, accounting for song speed.<br/>
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
        /// Positive calibration settings are stored as positive seconds.
        /// Song time is calculated as input time plus this value scaled by song speed.
        /// </remarks>
        public double AudioCalibration { get; private set; }

        /// <summary>
        /// The video calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Positive calibration settings are stored as positive seconds.
        /// Visual time is calculated as input time plus this value scaled by song speed.
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
        public float SongSpeed => _effectiveSongSpeed;

        /// <summary>
        /// The actual current playback speed of the song around the effective reference speed.
        /// </summary>
        /// <remarks>
        /// The audio may be sped up or slowed down in order to re-synchronize.
        /// This value takes that speed adjustment into account.
        /// </remarks>
        public float RealSongSpeed => _effectiveSongSpeed + SyncSpeedAdjustment;

        private float _requestedSongSpeed;
        private float _effectiveSongSpeed;
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
        private readonly object _timingStateLock = new();
        private bool _disposed;

        private readonly StemMixer _mixer;
        private readonly SongSyncController _syncController;

        public float SyncSpeedAdjustment => _syncController.SyncSpeedAdjustment;

        /// <summary>
        /// The audio time used by audio synchronization.<br/>
        /// Accounts for song speed, audio calibration, and song offset.
        /// </summary>
        public double SyncAudioTime => _syncController.SyncAudioTime;

        /// <summary>
        /// The visual time used by audio synchronization.<br/>
        /// Accounts for song speed, but <b>not</b> video calibration.
        /// </summary>
        public double SyncVisualTime => _syncController.SyncVisualTime;

        /// <summary>
        /// The difference between the visual and audio times used by audio synchronization.
        /// </summary>
        public double SyncDelta => _syncController.SyncDelta;
        #endregion

        #region Seek debugging
        private bool _seeked;
        private double _previousInputTime = double.MinValue;
        #endregion

        /// <summary>
        /// Creates a new song runner with the given mixer, starting timeline, speed, and song offset.
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
        /// <param name="songOffset">
        /// The song offset, in seconds.<br/>
        /// This value is negated so audio file time can be converted to gameplay song time by adding it.
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
            SongOffset = -songOffset;

            _syncController = new SongSyncController(
                _mixer,
                this,
                (inputSystemTime, updateTimes) => ActivateScheduledSongSpeeds(inputSystemTime, updateTimes)
            );

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
                    _syncController.Dispose();
                }
                else
                {
                    _syncController.RequestStop();
                }
            }
        }

        private void Start()
        {
            YargLogger.LogDebug("Starting song runner");
 
            // Re-initialize song times to avoid lag issues
            InitializeSongTime(InputTime, 0);
 
            _syncController.Start();
            Started = true;
        }

        public void Update()
        {
            if (!TryStartAfterLoadingLag())
            {
                return;
            }

            UpdateFrameDebuggerPauseOverride();

            double updateInputSystemTime = GetUpdateInputSystemTime();
            ActivateScheduledSongSpeeds(updateInputSystemTime, true);

            if (Paused)
            {
                return;
            }

            UpdateTimes(updateInputSystemTime);
            AssertTimeDidNotJumpBackwards();
        }

        private bool TryStartAfterLoadingLag()
        {
            if (Started)
            {
                return true;
            }

            // Lazy start avoids anchoring song time to a frame that stalled during loading.
            // Force start after one second so pathological load spikes cannot block forever.
            if (double.IsNaN(_forceStartTime))
            {
                _forceStartTime = InputManager.CurrentInputTime + 1;
            }

            double currentTime = InputManager.CurrentInputTime;
            double currentFrameLength = currentTime - InputManager.InputUpdateTime;
            bool startingFrameLagged = currentFrameLength >= 0.1f;
            if (startingFrameLagged && currentTime < _forceStartTime)
            {
                return false;
            }

            Start();
            return true;
        }

        private void UpdateFrameDebuggerPauseOverride()
        {
            // Unity frame debugger halts rendering while input time keeps advancing.
            // Pause through normal override path so resume restores prior pause intent.
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

        private void AssertTimeDidNotJumpBackwards()
        {
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

        private double GetPlaybackStreamLatency()
        {
            return SanitizeLatency(_mixer.GetPlaybackStreamLatency());
        }

        private double GetTempoStreamLatency()
        {
            return SanitizeLatency(_mixer.GetTempoStreamLatency());
        }

        private bool ActivateScheduledSongSpeeds(double nowInputSystemTime, bool updateTimes)
        {
            bool changed = false;

            lock (_timingStateLock)
            {
                while (_songSpeedSchedule.Count > 0 && _songSpeedSchedule.Peek().EffectiveTime <= nowInputSystemTime)
                {
                    var command = _songSpeedSchedule.Dequeue();
                    float nextSpeed = ClampSongSpeed(command.Speed);
                    PreserveInputTimeAcrossSpeedChange(command.EffectiveTime, nextSpeed);
                    changed = true;
                }
            }

            if (changed && updateTimes)
            {
                UpdateTimes(nowInputSystemTime);
            }

            return changed;
        }

        private void PreserveInputTimeAcrossSpeedChange(double inputSystemTime, float newSpeed)
        {
            double currentInputAtChange = (inputSystemTime - InputTimeOffset) * _effectiveSongSpeed;
            _effectiveSongSpeed = newSpeed;
            InputTimeOffset = inputSystemTime - (currentInputAtChange / _effectiveSongSpeed);
        }

        SongSyncState ISongSyncStateProvider.ReadSongSyncState()
        {
            lock (_timingStateLock)
            {
                return new SongSyncState(
                    _effectiveSongSpeed,
                    SongOffset,
                    AudioCalibration,
                    InputTimeOffset,
                    Paused
                );
            }
        }

        private void ResetSync()
        {
            _syncController.Reset(SongSpeed);
        }

        private void PreAlignResumeAudio()
        {
            _syncController.PreAlignResumeAudio(InputTime, AudioCalibration, SongSpeed, SongOffset);
        }

        private void SuppressSyncCorrection()
        {
            _syncController.SuppressCorrection();
        }

        private double GetLatencyAdjustedStartDelay(double requestedDelay)
        {
            double playbackLatency = GetPlaybackStreamLatency();
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
            lock (_timingStateLock)
            {
                return (timeFromInputSystem - InputTimeOffset) * SongSpeed;
            }
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
            lock (_timingStateLock)
            {
                InputTime = (inputSystemTime - InputTimeOffset) * SongSpeed;
                SongTime = InputTime + (AudioCalibration * SongSpeed);
                VisualTime = InputTime + (VideoCalibration * SongSpeed);
            }

            AudioPlaybackTime = Math.Max(0, _mixer.GetPosition());
        }

        private void SetInputBase(double songTime)
        {
            SetInputBase(songTime, InputManager.InputUpdateTime);
        }

        private void SetInputBase(double songTime, double inputSystemTime)
        {
            // InputTime = (inputSystemTime - InputTimeOffset) * SongSpeed.
            // Song/visual times add calibration after the shared input timeline is established.
            double previousOffset;
            double previousInputTime;
            double previousSongTime;
            double previousVisualTime;
            double newOffset;
            double newInputTime;
            double newSongTime;
            double newVisualTime;

            lock (_timingStateLock)
            {
                previousOffset = InputTimeOffset;
                previousInputTime = InputTime;
                previousSongTime = SongTime;
                previousVisualTime = VisualTime;

                InputTimeOffset = inputSystemTime - (songTime / SongSpeed);
                InputTime = (inputSystemTime - InputTimeOffset) * SongSpeed;
                SongTime = InputTime + (AudioCalibration * SongSpeed);
                VisualTime = InputTime + (VideoCalibration * SongSpeed);

                newOffset = InputTimeOffset;
                newInputTime = InputTime;
                newSongTime = SongTime;
                newVisualTime = VisualTime;
            }

            AudioPlaybackTime = Math.Max(0, _mixer.GetPosition());

            YargLogger.LogFormatDebug(
                "Set input time base.\n" +
                "Clock time: {0:0.000000}\n" +
                "Offset {1:0.000000} -> {2:0.000000}\n" +
                "Input time {3:0.000000} -> {4:0.000000}\n" +
                "Song time {5:0.000000} -> {6:0.000000}\n" +
                "Visual time {7:0.000000} -> {8:0.000000}",
                inputSystemTime,
                previousOffset, newOffset,
                previousInputTime, newInputTime,
                previousSongTime, newSongTime,
                previousVisualTime, newVisualTime
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
            double playbackLatency = GetPlaybackStreamLatency();
            double tempoLatency = GetTempoStreamLatency();

            ResetEffectiveSpeedToRequestedForSeek();
            InitializeSongTime(time, delayTime);

            double seekTime = CalculateSeekAudioFileTime(time, delayTime, playbackLatency);
            bool canStartAudio = seekTime >= 0;
            seekTime = Math.Max(0, seekTime);

            SeekMixerForSongTime(seekTime, canStartAudio);

            UpdateTimes();
            _seeked = true;

            YargLogger.LogFormatDebug(
                "Set song time with latency budget.\n" +
                "Requested delay: {0:0.000000}, effective delay: {1:0.000000}, playback latency: {2:0.000000}, " +
                "tempo latency: {3:0.000000}, raw audio: {4:0.000000}, seek time: {5:0.000000}",
                requestedDelay, delayTime, playbackLatency, tempoLatency, _mixer.GetPosition(), seekTime
            );
        }

        private void ResetEffectiveSpeedToRequestedForSeek()
        {
            lock (_timingStateLock)
            {
                if (!Mathf.Approximately(_effectiveSongSpeed, _requestedSongSpeed))
                {
                    _effectiveSongSpeed = _requestedSongSpeed;
                    _songSpeedSchedule.Clear();
                }
            }
        }

        private double CalculateSeekAudioFileTime(double songTime, double delayTime, double playbackLatency)
        {
            // Input/song timeline may be negative during lead-in. Mixer file position cannot, so caller
            // clamps negative result to zero and waits until timeline reaches audible range before playing.
            return songTime - (delayTime - playbackLatency - AudioCalibration) * SongSpeed - SongOffset;
        }

        private void SeekMixerForSongTime(double seekTime, bool canStartAudio)
        {
            ResetSync();

            _mixer.Pause();
            _mixer.SetPosition(seekTime);

            if (ShouldPlayAfterSeek(canStartAudio))
            {
                _mixer.Play();
            }
        }

        private bool ShouldPlayAfterSeek(bool canStartAudio)
        {
            lock (_timingStateLock)
            {
                return !Paused && canStartAudio;
            }
        }

        public void SetSongSpeed(float speed)
        {
            speed = ClampSongSpeed(speed);
            double nowInputSystemTime = GetEstimatedCurrentInputTime();
            double streamDelay = GetTempoStreamLatency();
            double effectiveTime;
            bool immediate;
            float previousRequested;
            float previousEffective;

            lock (_timingStateLock)
            {
                previousRequested = _requestedSongSpeed;
                previousEffective = _effectiveSongSpeed;

                if (Mathf.Approximately(speed, _requestedSongSpeed) && _songSpeedSchedule.Count == 0)
                {
                    return;
                }

                _requestedSongSpeed = speed;

                immediate = ShouldApplySpeedChangeImmediately(streamDelay);
                if (immediate)
                {
                    ApplyImmediateSongSpeedChange(nowInputSystemTime, speed);
                    effectiveTime = nowInputSystemTime;
                }
                else
                {
                    effectiveTime = ScheduleSongSpeedChange(nowInputSystemTime, streamDelay, speed);
                }

            }

            _syncController.ClearSpeedAdjustment();
            _syncController.SuppressUntil(effectiveTime);
            _mixer.SetSpeed(speed, true);

            if (immediate)
            {
                UpdateTimes(nowInputSystemTime);
            }

            YargLogger.LogFormatDebug(
                "Set song speed. Requested {0:0.00} -> {1:0.00}, effective {2:0.00} -> {3:0.00}, " +
                "activation: {4:0.000000}, stream delay: {5:0.000000}.\n" +
                "Song time: {6:0.000000}, visual time: {7:0.000000}, input time: {8:0.000000}",
                previousRequested, speed, previousEffective, SongSpeed, effectiveTime, streamDelay,
                SongTime, VisualTime, InputTime);
        }

        private bool ShouldApplySpeedChangeImmediately(double streamDelay)
        {
            return !Started || Paused || streamDelay <= 0.0;
        }

        private void ApplyImmediateSongSpeedChange(double inputSystemTime, float speed)
        {
            _songSpeedSchedule.Clear();
            PreserveInputTimeAcrossSpeedChange(inputSystemTime, speed);
        }

        private double ScheduleSongSpeedChange(double inputSystemTime, double streamDelay, float speed)
        {
            // Effective gameplay speed waits for the tempo stream latency so gameplay timeline and
            // audible BASS tempo shift cross at the same perceived time.
            double effectiveTime = inputSystemTime + streamDelay;
            _songSpeedSchedule.Enqueue((effectiveTime, speed));
            return effectiveTime;
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

            double inputTime;
            lock (_timingStateLock)
            {
                AudioCalibration = audioCalibrationMs / 1000.0;
                VideoCalibration = videoCalibrationMs / 1000.0;
                inputTime = InputTime;
            }

            SetInputBase(inputTime);
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

            lock (_timingStateLock)
            {
                if (Paused)
                {
                    return;
                }

                Paused = true;
            }

            _mixer.Pause();
            ResetSync();

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

            lock (_timingStateLock)
            {
                if (!Paused)
                {
                    return;
                }
            }

            UpdateCalibration();
            SetResumeInputBase();
            ResetSync();
            PreAlignResumeAudio();

            _syncController.NotifyResumed();

            lock (_timingStateLock)
            {
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
