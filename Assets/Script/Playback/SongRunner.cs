using System;
using System.Threading;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Audio;
using YARG.Input;

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
        #endregion

        #region Audio syncing
        private Thread _syncThread;

        private bool _disposed;

        private volatile float _syncSpeedAdjustment;
        private volatile int _syncSpeedMultiplier;
        private volatile float _syncStartDelta;
        private volatile float _syncWorstDelta;

        private readonly StemMixer _mixer;

        public float SyncSpeedAdjustment => _syncSpeedAdjustment;
        public int SyncSpeedMultiplier => _syncSpeedMultiplier;
        public float SyncStartDelta => _syncStartDelta;
        public float SyncWorstDelta => _syncWorstDelta;

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
            int audioCalibrationMs,
            int videoCalibrationMs,
            double songOffset
        )
        {
            _mixer = mixer;
            SongSpeed = songSpeed;
            SongOffset = -songOffset;

            _syncThread = new Thread(SyncThread) { IsBackground = true };

            InitializeSongTime(startTime + SongOffset, startDelay);
            SetCalibration(audioCalibrationMs, videoCalibrationMs);
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

        private void SyncThread()
        {
            const double INITIAL_SYNC_THRESH = 0.015;
            const double ADJUST_SYNC_THRESH = 0.005;
            const float SPEED_ADJUSTMENT = 0.05f;

            for (; !_disposed; Thread.Sleep(1))
            {
                lock (_syncThread)
                {
                    double audioOffset = SongOffset - (AudioCalibration * SongSpeed);

                    SyncAudioTime = _mixer.GetPosition();
                    SyncVisualTime = GetRelativeInputTime(InputManager.CurrentInputTime) - audioOffset;

                    if (Paused || SyncVisualTime < 0 || SyncVisualTime >= _mixer.Length)
                    {
                        continue;
                    }

                    if (_mixer.IsPaused)
                    {
                        _mixer.Play(false);
                    }

                    if (SyncAudioTime >= _mixer.Length)
                    {
                        continue;
                    }

                    // Account for song speed
                    double initialThreshold = INITIAL_SYNC_THRESH * SongSpeed;
                    double adjustThreshold = ADJUST_SYNC_THRESH * SongSpeed;

                    // Check the difference between visual and audio times
                    double delta = SyncVisualTime - SyncAudioTime;
                    double deltaAbs = Math.Abs(delta);

                    // Don't sync if below the initial sync threshold, and we haven't adjusted the speed
                    if (_syncSpeedMultiplier == 0 && deltaAbs < initialThreshold)
                        continue;

                    // We're now syncing, determine how much to adjust the song speed by
                    int speedMultiplier = (int) Math.Round(delta / INITIAL_SYNC_THRESH);
                    if (speedMultiplier == 0)
                        speedMultiplier = delta > 0 ? 1 : -1;

                    // Only change speed when the multiplier changes
                    if (_syncSpeedMultiplier != speedMultiplier)
                    {
                        if (_syncSpeedMultiplier == 0)
                        {
                            _syncStartDelta = (float) delta;
                            _syncWorstDelta = _syncStartDelta;
                        }
                        else if (Math.Abs(delta) > Math.Abs(_syncWorstDelta))
                        {
                            _syncWorstDelta = (float) delta;
                        }

                        _syncSpeedMultiplier = speedMultiplier;

                        float adjustment = SPEED_ADJUSTMENT * speedMultiplier;
                        if (!Mathf.Approximately(adjustment, _syncSpeedAdjustment))
                        {
                            _syncSpeedAdjustment = adjustment;
                            _mixer.SetSpeed(RealSongSpeed, false);
                        }
                    }

                    // No change in speed, check if we're below the threshold
                    if (deltaAbs < adjustThreshold ||
                        // Also check if we overshot and passed 0
                        (delta > 0.0 && _syncStartDelta < 0.0) ||
                        (delta < 0.0 && _syncStartDelta > 0.0))
                    {
                        ResetSync();
                    }
                }
            }
        }

        private void ResetSync()
        {
            // Don't reset so that they're easier to see in real time
            // _syncStartDelta = 0;
            // _syncWorstDelta = 0;

            _syncSpeedMultiplier = 0;
            _syncSpeedAdjustment = 0f;
            _mixer.SetSpeed(RealSongSpeed, true);
        }

        public double GetRelativeInputTime(double timeFromInputSystem)
        {
            return (timeFromInputSystem - InputTimeOffset) * SongSpeed;
        }

        private void UpdateTimes()
        {
            InputTime = GetRelativeInputTime(InputManager.InputUpdateTime);
            SongTime = InputTime + (AudioCalibration * SongSpeed);
            VisualTime = InputTime + (VideoCalibration * SongSpeed);

            AudioPlaybackTime = _mixer.GetPosition();
        }

        private void SetInputBase(double songTime)
        {
            double previousOffset = InputTimeOffset;
            double previousInputTime = InputTime;
            double previousSongTime = SongTime;
            double previousVisualTime = VisualTime;

            InputTimeOffset = InputManager.InputUpdateTime - (songTime / SongSpeed);

            // Update input times
            UpdateTimes();

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

        private void SetInputBaseChecked(double inputBase)
        {
            double previousVisualTime = VisualTime;
            double previousInputTime = InputTime;

            SetInputBase(inputBase);

            // Speeds above 200% or so can cause inaccuracies greater than 1 ms
            double threshold = Math.Max(0.001 * SongSpeed, 0.0005);
            YargLogger.AssertFormat(Math.Abs(VisualTime - previousVisualTime) <= threshold,
                "Unexpected visual time change! Went from {0} to {1}, threshold {2}",
                previousVisualTime, VisualTime, threshold);
            YargLogger.AssertFormat(Math.Abs(InputTime - previousInputTime) <= threshold,
                "Unexpected input time change! Went from {0} to {1}, threshold {2}",
                previousInputTime, InputTime, threshold);
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
                // Set input/song time
                InitializeSongTime(time, delayTime);

                // Reset syncing before seeking to prevent speed adjustments from causing issues
                ResetSync();

                _mixer.Pause();
                // Audio seeking; cannot go negative
                double seekTime = time - (delayTime - AudioCalibration) * SongSpeed - SongOffset;
                if (seekTime < 0)
                {
                    seekTime = 0;
                    _mixer.SetPosition(seekTime);
                }
                else
                {
                    _mixer.SetPosition(seekTime);
                    if (!Paused)
                        _mixer.Play(true);
                }

                UpdateTimes();
                _seeked = true;
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

        public void SetCalibration(int audioMs, int videoMs)
        {
            AudioCalibration = audioMs / 1000.0;
            VideoCalibration = videoMs / 1000.0;
            SetInputBase(InputTime);
        }

        /// <summary>
        /// Pauses the song.
        /// </summary>
        public void Pause()
        {
            if (PauseOverridden)
            {
                _resumeAfterOverride = false;
                return;
            }

            if (Paused)
                return;

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
                return;

            Paused = false;
            SetInputBaseChecked(InputTime);

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

        public static float ClampSongSpeed(float speed)
        {
            // 10% - 5000%, we reserve 5% at the bottom so that audio syncing can still function.
            // BASS can go up to 5100%, but we round down since 5000% looks nicer (and it gives us a
            // good buffer for audio syncing in the upper extreme).
            return Math.Clamp(speed, 10 / 100f, 5000 / 100f);
        }
    }
}