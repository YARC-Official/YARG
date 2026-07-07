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

        /// <summary>
        /// The duration to use for rewind animations, in seconds.
        /// </summary>
        public const float REWIND_DURATION = 0.5f;

        /// <summary>
        /// The time into the song, accounting for song speed and audio calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value should be used for all interactions that are relative to the audio.
        /// Note that this is driven by input time, rather than audio time.
        /// </remarks>
        public double SongTime => _timeline.Current.SongTime;

        /// <summary>
        /// The current visual time, accounting for song speed and video calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        public double VisualTime => _visualTimeOverride ?? _timeline.Current.VisualTime;

        /// <summary>
        /// The current gameplay input time, accounting for song speed.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value should be used for all interactions with inputs, engines, and replays.
        /// It should also be used for setting position, as all times are based off of input time.
        /// </remarks>
        public double InputTime => _timeline.Current.InputTime;

        #endregion

        #region Offsets
        /// <summary>
        /// The audio calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Positive calibration settings are stored as positive seconds.
        /// Song time is calculated as input time plus this value scaled by song speed.
        /// </remarks>
        public double AudioCalibration => _timeline.AudioCalibration;

        /// <summary>
        /// The video calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Positive calibration settings are stored as positive seconds.
        /// Visual time is calculated as input time plus this value scaled by song speed.
        /// </remarks>
        public double VideoCalibration => _timeline.VideoCalibration;

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
        public double InputTimeOffset => _timeline.Current.InputTimeOffset;
        #endregion

        #region Other state
        /// <summary>
        /// The requested playback speed of the song. Updated immediately by user speed changes.
        /// </summary>
        public float RequestedSongSpeed { get; private set; }

        /// <summary>
        /// The effective gameplay/reference speed of the song. There is an estimated delay between when song speed is requested
        /// and when it becomes effective. Note that while this is mathematically exact for input timeline calculations,
        /// the heard audio sync may be slightly off due to inaccurate latency estimation.
        /// </summary>
        public float SongSpeed => _timeline.Current.SongSpeed;

        /// <summary>
        /// Whether or not the runner has been started.
        /// </summary>
        public bool Started { get; private set; }

        /// <summary>
        /// Whether or not the song is currently paused.
        /// </summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// Whether or not the song is currently actively playing.
        /// </summary>
        private bool IsPlaying => Started && !Paused;

        /// <summary>
        /// Whether or not the song's pause state is currently overridden.
        /// </summary>
        private bool PauseOverridden => _pauseOverrides > 0;

        private readonly Queue<(double EffectiveTime, float Speed)> _gameplaySpeedSchedule = new();
        private readonly IInputClock _inputClock;
        private readonly SongTimeline _timeline;

        private int _pauseOverrides;
        private bool _resumeAfterOverride;

        private bool _wasFrameDebuggerEnabled;

        private double? _startDeadline;
        #endregion

        #region Rewind State
        private CancellationTokenSource _rewindSource;
        private Tween                   _rewindTween;
        private double?                 _visualTimeOverride;
        #endregion

        #region Audio syncing
        private readonly object _timingStateLock = new();
        private bool _disposed;

        private readonly StemMixer _mixer;
        private readonly SongSyncController _syncController;

        public float SyncSpeedAdjustment => _syncController.SyncSpeedAdjustment;

        /// <summary>
        /// The difference between the visual and audio times used by audio synchronization.
        /// </summary>
        public double SyncDelta => _syncController.SyncDelta;
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
        /// <param name="mixer"></param>
        public SongRunner(
            StemMixer mixer,
            double startTime,
            double startDelay,
            float songSpeed,
            double songOffset
        )
        {
            _mixer = mixer;
            RequestedSongSpeed = ClampSongSpeed(songSpeed);
            _inputClock = new UnityInputClock();
            _timeline = new SongTimeline(_inputClock, RequestedSongSpeed, 0.0, 0.0);
            SongOffset = -songOffset;

            _syncController = new SongSyncController(
                _mixer,
                this,
                inputSystemTime => ApplyScheduledSpeedChanges(inputSystemTime)
            );

            SetTimelinePosition(targetInputTime: startTime + SongOffset, startDelaySeconds: startDelay);
            UpdateCalibration();
        }

        ~SongRunner()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases resources used by the song runner and stops audio synchronization.
        /// </summary>
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
            SetTimelinePosition(InputTime, 0);
            _syncController.Start();
            Started = true;
        }

        /// <summary>
        /// Updates the runner state for the current frame.
        /// </summary>
        /// <remarks>
        /// Starts playback when loading lag has settled, applies scheduled speed changes,
        /// handles frame debugger pause overrides, and advances all timeline values while unpaused.
        /// </remarks>
        public void Update()
        {
            //Start on the first non-lagging frame.  Early frames tend to lag because of all the objects being created
            if (!Started)
            {
                if (IsFrameLagging())
                {
                    return;
                }
                Start();
            }

            HandleFrameDebugger();
            ApplyScheduledSpeedChanges(_inputClock.FrameTime);

            if (Paused)
            {
                return;
            }

            double previousInputTime = InputTime;
            var snapshot = _timeline.TickFrame();
            AssertTimeDidNotJumpBackwards(previousInputTime, snapshot.InputTime);
        }

        private bool IsFrameLagging()
        {
            if (!_startDeadline.HasValue)
            {
                const double maxWaitTimeSeconds = 1.0;
                _startDeadline = InputManager.CurrentInputTime + maxWaitTimeSeconds;
            }

            double currentTime = InputManager.CurrentInputTime;
            double currentFrameLength = currentTime - _inputClock.FrameTime;
            bool startingFrameLagged = currentFrameLength >= 0.1f;

            // Wait for lag to settle, but only until our start deadline is reached
            return startingFrameLagged && currentTime < _startDeadline.Value;
        }

        /// <summary>
        /// Automatically pauses or resumes the song runner when the Unity Frame Debugger is toggled.
        /// </summary>
        private void HandleFrameDebugger()
        {
            bool isDebuggerEnabled = FrameDebugger.enabled;
            if (_wasFrameDebuggerEnabled == isDebuggerEnabled)
            {
                return;
            }
            _wasFrameDebuggerEnabled = isDebuggerEnabled;

            if (isDebuggerEnabled)
            {
                // When Unity's Frame Debugger is enabled, rendering freezes but system/input time
                // keeps advancing. If we didn't pause, the song time would suddenly jump forward
                // by several seconds when the debugger is closed.
                //
                // We use OverridePause() instead of Pause() so that we don't overwrite the user's
                // actual pause intent (e.g., if the user already had the game paused, it should stay paused).
                OverridePause();
            }
            else
            {
                OverrideResume();
            }
        }

        private void AssertTimeDidNotJumpBackwards(double previousTime, double currentTime)
        {
            YargLogger.AssertFormat(
                currentTime >= previousTime,
                "Unexpected time seek backwards! Went from {0} to {1} (delta: {2})",
                previousTime, currentTime, currentTime - previousTime
            );
        }

        private double GetPlaybackStreamLatency() => _mixer.GetPlaybackStreamLatency();
        private double GetTempoStreamLatency()    => _mixer.GetTempoStreamLatency();

        /// <summary>
        /// Activates scheduled speed changes for the track, gameplay, and inputs once their predicted latency delay
        /// has passed.  This syncs up track speed changes with heard audio speed changes
        /// </summary>
        private void ApplyScheduledSpeedChanges(double nowInputSystemTime)
        {
            lock (_timingStateLock)
            {
                while (_gameplaySpeedSchedule.Count > 0 && _gameplaySpeedSchedule.Peek().EffectiveTime <= nowInputSystemTime)
                {
                    var command = _gameplaySpeedSchedule.Dequeue();
                    ApplyGameplaySpeedChange(command.Speed, command.EffectiveTime);
                }
            }
        }

        /// <summary>
        /// Updates the active gameplay speed and shifts the input offset to keep the input timeline
        /// perfectly continuous. While mathematically exact, heard audio sync might be slightly off
        /// due to inaccurate latency estimate
        /// </summary>
        private void ApplyGameplaySpeedChange(float newSpeed, double inputSystemTime)
        {
            // Use the scheduled/effective time rather than the current system time
            // to ensure the transition is jitter-free, regardless of when this frame executes.
            _timeline.ApplySpeedChange(newSpeed, inputSystemTime);
        }

        SongSyncState ISongSyncStateProvider.ReadSongSyncState()
        {
            lock (_timingStateLock)
            {
                var snapshot = _timeline.Current;
                return new SongSyncState(
                    snapshot.SongSpeed,
                    SongOffset,
                    AudioCalibration,
                    snapshot.InputTimeOffset,
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

        /// <summary>
        /// Gets the visual time that rewind visuals should target, accounting for playback latency.
        /// </summary>
        /// <param name="seconds">The number of seconds to rewind from the current visual time.</param>
        /// <returns>The latency-adjusted visual rewind target.</returns>
        public double GetRewindVisualTime(double seconds)
        {
            return VisualTime - seconds - (GetPlaybackStreamLatency() * SongSpeed);
        }

        /// <summary>
        /// Converts an input-system timestamp to song-relative input time.
        /// </summary>
        /// <param name="timeFromInputSystem">The absolute timestamp from the input system, in seconds.</param>
        /// <returns>The corresponding input time relative to the song timeline.</returns>
        public double GetRelativeInputTime(double timeFromInputSystem)
        {
            lock (_timingStateLock)
            {
                return _timeline.ConvertInputSystemTime(timeFromInputSystem);
            }
        }

        private void ReanchorTimelineForResume()
        {
            double previousVisualTime = VisualTime;
            double previousInputTime = InputTime;

            _timeline.AnchorAtInstant(previousInputTime);

            // Speeds above 200% or so can cause inaccuracies greater than 1 ms
            double threshold = Math.Max(0.001 * SongSpeed, 0.0005);
            YargLogger.AssertFormat(Math.Abs(VisualTime - previousVisualTime) <= threshold,
                "Unexpected visual time change! Went from {0} to {1}, threshold {2}",
                previousVisualTime, VisualTime, threshold);
            YargLogger.AssertFormat(Math.Abs(InputTime - previousInputTime) <= threshold,
                "Unexpected input time change! Went from {0} to {1}, threshold {2}",
                previousInputTime, InputTime, threshold);
        }

        private void SetTimelinePosition(double targetInputTime, double startDelaySeconds)
        {
            _visualTimeOverride = null;

            // Account for song speed.
            double leadInSongTime = startDelaySeconds * SongSpeed;
            double anchoredInputTime = targetInputTime - leadInSongTime;
            _timeline.AnchorAtFrame(anchoredInputTime);
        }

        /// <summary>
        /// Seeks the song timeline and audio playback to the specified song time.
        /// </summary>
        /// <param name="time">The target song time, in seconds.</param>
        /// <param name="delayTime">The requested lead-in delay, in seconds.</param>
        /// <remarks>
        /// Clamped to a minimum of the playback latency to preserve audio sync.
        /// </remarks>
        public void SetSongTime(double time, double delayTime)
        {
            double requestedDelay = delayTime;
            double playbackLatency = GetPlaybackStreamLatency();
            double tempoLatency = GetTempoStreamLatency();
            double effectiveDelay = Math.Max(delayTime, playbackLatency);

            //Apply last song speed change immediately
            lock (_timingStateLock)
            {
                _timeline.ApplySpeedChangeInstant(RequestedSongSpeed);
                _gameplaySpeedSchedule.Clear();
            }

            SetTimelinePosition(targetInputTime: time, startDelaySeconds: effectiveDelay);
            double seekTime = CalculateSeekAudioFileTime(time, effectiveDelay, playbackLatency);
            SeekMixer(seekTime);
            _timeline.TickFrame();

            YargLogger.LogFormatDebug(
                "Set song time with latency budget.\n" +
                "Requested delay: {0:0.000000}, effective delay: {1:0.000000}, playback latency: {2:0.000000}, " +
                "tempo latency: {3:0.000000}, raw audio: {4:0.000000}, seek time: {5:0.000000}",
                requestedDelay, effectiveDelay, playbackLatency, tempoLatency, _mixer.GetPosition(), seekTime
            );
        }

        private double CalculateSeekAudioFileTime(double songTime, double delayTime, double playbackLatency)
        {
            // Input/song timeline may be negative during lead-in. Mixer file position cannot, so caller
            // clamps negative result to zero and waits until timeline reaches audible range before playing.
            double leadIn = delayTime * SongSpeed;
            double latencyOffset = playbackLatency * SongSpeed;
            double calibrationOffset = AudioCalibration * SongSpeed;
            return songTime - SongOffset - leadIn + latencyOffset + calibrationOffset;
        }

        private void SeekMixer(double seekTime)
        {
            ResetSync();
            bool canStartAudio = seekTime >= 0;
            var postSeekState = !Paused && canStartAudio ? PostSeekState.Play : PostSeekState.Pause;
            _mixer.Seek(Math.Max(0, seekTime), postSeekState);
        }

        /// <summary>
        /// Sets the requested song playback speed.
        /// </summary>
        /// <param name="speed">The requested speed multiplier, where 1f is 100% speed.</param>
        /// <remarks>
        /// The value is clamped by <see cref="ClampSongSpeed"/>. While running, the effective gameplay
        /// speed may be delayed to align with audio tempo stream latency.
        /// </remarks>
        public void SetSongSpeed(float speed)
        {
            speed = ClampSongSpeed(speed);
            double nowInputSystemTime = _inputClock.InstantTime;

            //If we are paused, buffers will be flushed, so no latency
            double latency = IsPlaying ? GetTempoStreamLatency() : 0.0;
            double effectiveTime;

            lock (_timingStateLock)
            {
                if (Mathf.Approximately(speed, RequestedSongSpeed) && _gameplaySpeedSchedule.Count == 0)
                {
                    return;
                }

                RequestedSongSpeed = speed;
                effectiveTime = ScheduleGameplaySpeedChange(nowInputSystemTime, latency, speed);
            }

            _syncController.ClearSpeedAdjustment();
            _syncController.SuppressUntil(effectiveTime);
            _mixer.SetSpeed(speed, true);
            if (!IsPlaying)
            {
                _timeline.TickAt(nowInputSystemTime);
            }
        }

        private double ScheduleGameplaySpeedChange(double inputSystemTime, double streamDelay, float speed)
        {
            if (streamDelay <= 0.0)
            {
                _gameplaySpeedSchedule.Clear();
                ApplyGameplaySpeedChange(speed, inputSystemTime);
                return inputSystemTime;
            }

            // Effective gameplay speed waits for the tempo stream latency so gameplay timeline and
            // audible BASS tempo shift cross at the same perceived time.
            double effectiveTime = inputSystemTime + streamDelay;
            _gameplaySpeedSchedule.Enqueue((effectiveTime, speed));
            return effectiveTime;
        }

        /// <summary>
        /// Adjusts the requested song playback speed by the specified delta.
        /// </summary>
        /// <param name="deltaSpeed">The speed multiplier delta to add to <see cref="RequestedSongSpeed"/>.</param>
        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(RequestedSongSpeed + deltaSpeed);

        /// <summary>
        /// Reloads audio and video calibration from settings and preserves current input time.
        /// </summary>
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
                inputTime = InputTime;
            }

            _timeline.SetCalibrationAndAnchorAtFrame(
                audioCalibrationMs / 1000.0,
                videoCalibrationMs / 1000.0,
                inputTime
            );
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
            _visualTimeOverride = null;

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
            ReanchorTimelineForResume();
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

        /// <summary>
        /// Rewinds visual playback while paused, seeks backwards, resumes, and waits until the original target time is reached.
        /// </summary>
        /// <param name="seconds">The number of seconds to rewind from the current song time.</param>
        /// <param name="overrideTargetTime">Optional song time to wait for instead of the current song time.</param>
        /// <returns>
        /// <c>true</c> if the rewind was canceled or could not complete; <c>false</c> if playback resumed and reached the target time.
        /// </returns>
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
            var targetVisualTime = GetRewindVisualTime(seconds);
            var targetResumeTime = overrideTargetTime ?? SongTime;
            float rewindDuration = REWIND_DURATION;

            _visualTimeOverride = VisualTime;
            _rewindTween = DOTween.To(
                () => _visualTimeOverride ?? VisualTime,
                x => _visualTimeOverride = x,
                targetVisualTime,
                rewindDuration
            );

            var rewindCanceled = await _rewindTween
                .AsyncWaitForCompletion()
                .AsUniTask()
                .AttachExternalCancellation(token)
                .SuppressCancellationThrow();

            if (rewindCanceled || token.IsCancellationRequested)
            {
                _rewindTween?.Kill();
                _rewindTween = null;
                _visualTimeOverride = null;
                return true;
            }

            SetSongTime(targetRewindTime - (AudioCalibration * SongSpeed), 0);
            Resume();


            var waitCanceled = await UniTask.WaitUntil(() => SongTime > targetResumeTime, cancellationToken: token)
                .SuppressCancellationThrow();

            if (waitCanceled || token.IsCancellationRequested)
            {
                _visualTimeOverride = null;
                return true;
            }

            _visualTimeOverride = null;
            return false;
        }

        /// <summary>
        /// Clamps a song speed multiplier to the supported playback range.
        /// </summary>
        /// <param name="speed">The requested speed multiplier, where 1f is 100% speed.</param>
        /// <returns>The clamped speed multiplier.</returns>
        private static float ClampSongSpeed(float speed)
        {
            // 10% - 5000%, we reserve 5% at the bottom so that audio syncing can still function.
            // BASS can go up to 5100%, but we round down since 5000% looks nicer (and it gives us a
            // good buffer for audio syncing in the upper extreme).
            return Math.Clamp(speed, 10 / 100f, 5000 / 100f);
        }
    }
}
