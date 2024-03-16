using System;
using System.Threading;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Core.Audio;
using YARG.Input;

namespace YARG.Playback
{
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
        /// Note that this is driven by visual time, rather than audio time.
        /// Use <see cref="AudioTime"/> if audio time is required.
        /// </remarks>
        public double SongTime => RealSongTime + AudioCalibration;

        /// <summary>
        /// The time into the song, accounting for song speed but <b>not</b> audio calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        public double RealSongTime { get; private set; }

        /// <summary>
        /// The time into the audio, accounting for song speed and audio calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value is for scenarios that *must* be driven by audio time instead of visual time.
        /// In general, <see cref="SongTime"/> should be used instead where possible.
        /// </remarks>
        public double AudioTime => RealAudioTime + AudioCalibration;

        /// <summary>
        /// The time into the audio, accounting for song speed but <b>not</b> audio calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        public double RealAudioTime { get; private set; }

        /// <summary>
        /// The current visual time, accounting for song speed and video calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        public double VisualTime => RealVisualTime + VideoCalibration;

        /// <summary>
        /// The current visual time, accounting for song speed but <b>not</b> video calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value should be used for all visual interactions, as video calibration should not delay visuals.
        /// It should also be used for setting position, otherwise the actual set position will be offset incorrectly.
        /// </remarks>
        public double RealVisualTime { get; private set; }

        /// <summary>
        /// The current input time, accounting for song speed and video calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        /// <remarks>
        /// This value should be used for all interactions with inputs, engines, and replays.
        /// </remarks>
        public double InputTime => RealInputTime + VideoCalibration;

        /// <summary>
        /// The current input time, accounting for song speed but <b>not</b> video calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        public double RealInputTime { get; private set; }

        /// <summary>
        /// The input time that is considered to be 0.
        /// Applied before song speed is factored in.
        /// </summary>
        public double InputTimeOffset { get; private set; }

        /// <summary>
        /// The base time added on to relative time to get the real current input time.
        /// Applied after song speed is.
        /// </summary>
        public double InputTimeBase { get; private set; }
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
        public double AudioCalibration { get; }

        /// <summary>
        /// The video calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive calibration settings will result in a negative number here.
        /// </remarks>
        public double VideoCalibration { get; }

        /// <summary>
        /// The song offset, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive offsets in the .ini or .chart will result in a negative number here.
        /// </remarks>
        public double SongOffset { get; }
        #endregion

        #region Other state
        /// <summary>
        /// The song speed as selected by the user.
        /// </summary>
        public float SelectedSongSpeed { get; private set; }

        /// <summary>
        /// The actual current playback speed of the song.
        /// </summary>
        /// <remarks>
        /// The audio may be sped up or slowed down in order to re-synchronize.
        /// This value takes that speed adjustment into account.
        /// </remarks>
        public float ActualSongSpeed => SelectedSongSpeed + _syncSpeedAdjustment;

        /// <summary>
        /// Whether or not the runner has been started.
        /// </summary>
        public bool Started { get; private set; }

        /// <summary>
        /// Whether or not the song is currently paused.
        /// </summary>
        public bool Paused => PendingPauses > 0;

        /// <summary>
        /// The number of pauses which are currently active.
        /// </summary>
        /// <remarks>
        /// The song runner keeps track of the number of pending pauses to prevent pausing in one place
        /// being overridden by resuming in another. For correct behavior, every call to <see cref="Pause"/>
        /// must be matched with a future call to <see cref="Resume"/>.
        /// </remarks>
        public int PendingPauses { get; private set; }

        /// <summary>
        /// The input time at which the song was paused.
        /// </summary>
        public double PauseStartTime { get; private set; }

        /// <summary>
        /// Whether or not to resume audio playback when <see cref="Resume"/> is called.
        /// </summary>
        private bool _playAudioOnResume = false;

        /// <summary>
        /// Whether or not <see cref="InitializeSongTime"/> has been called yet..
        /// </summary>
        private bool _songTimeInitialized = false;
        #endregion

        #region Audio syncing
        private Thread _syncThread;

        private EventWaitHandle _finishedSyncing = new(true, EventResetMode.ManualReset);
        private volatile bool _runSync;
        private volatile bool _pauseSync;

        private volatile float _syncSpeedAdjustment;
        private volatile int _syncSpeedMultiplier;
        private volatile float _syncStartDelta;

        private readonly StemMixer _mixer;

        public float SyncSpeedAdjustment => _syncSpeedAdjustment;
        public int SyncSpeedMultiplier => _syncSpeedMultiplier;
        public float SyncStartDelta => _syncStartDelta;

        /// <summary>
        /// The instantaneous current audio time, used for audio synchronization.<br/>
        /// Accounts for song speed, audio calibration, and song offset.
        /// </summary>
        public double SyncSongTime => _mixer.GetPosition() + AudioCalibration + SongOffset;

        /// <summary>
        /// The instantaneous current visual time, used for audio synchronization.<br/>
        /// Accounts for song speed, but <b>not</b> video calibration.
        /// </summary>
        public double SyncVisualTime => GetRelativeInputTime(InputManager.CurrentInputTime);

        /// <summary>
        /// The instantaneous current visual time, used for audio synchronization.<br/>
        /// Accounts for song speed, but <b>not</b> video calibration.
        /// </summary>
        public double SyncDelta => SyncVisualTime - SyncSongTime;
        #endregion

        #region Seek debugging
        private bool _seeked;
        private double _previousRealSongTime = double.NaN;
        private double _previousRealAudioTime = double.NaN;
        private double _previousRealVisualTime = double.NaN;
        private double _previousRealInputTime = double.NaN;
        #endregion

        /// <summary>
        /// Creates a new song runner with the given speed and calibration values.
        /// </summary>
        /// <remarks>
        /// The created song runner will be in a partially initialized, unstarted state.
        /// Full initialization is done lazily to prevent timing issues from loading lag,
        /// the first call to <see cref="Update"/> will initialize and start the runner.
        /// <br/>
        /// Since the runner starts paused, anything that might potentially interact with it before
        /// starting must respect the paused state, otherwise incorrect behavior may happen.
        /// </remarks>
        /// <param name="songSpeed">
        /// The percentage song speed, where 1f == 100%.
        /// </param>
        /// <param name="audioCalibration">
        /// The audio calibration, in milliseconds.<br/>
        /// This value is negated and normalized to seconds for more intuitive usage in other code.
        /// <paramref name="videoCalibration"/> is also applied to keep things visually synced.
        /// </param>
        /// <param name="videoCalibration">
        /// The video calibration, in milliseconds.<br/>
        /// This value is negated and normalized to seconds for more intuitive usage in other code.
        /// </param>
        /// <param name="songOffset">
        /// The song offset, in seconds.<br/>
        /// This value is negated for more intuitive usage in other code.
        /// </param>
        public SongRunner(StemMixer mixer, float songSpeed = 1f, int audioCalibration = 0, int videoCalibration = 0,
            double songOffset = 0)
        {
            _mixer = mixer;
            SelectedSongSpeed = songSpeed;
            VideoCalibration = -videoCalibration / 1000.0;
            AudioCalibration = (-audioCalibration / 1000.0) - VideoCalibration;
            SongOffset = -songOffset;

            _syncThread = new Thread(SyncThread) { IsBackground = true };

            InitializeSongTime(SongOffset);
            // We need to re-initialize on the first call to Update()
            _songTimeInitialized = false;
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
            // Stop sync thread
            _runSync = false;

            if (disposing)
            {
                // Wait for sync thread to stop
                if (_syncThread is { IsAlive: true })
                {
                    _syncThread?.Join();
                }
                _syncThread = null;
            }
        }

        private void Start()
        {
            YargLogger.LogDebug("Starting song runner");

            // Re-initialize song times to avoid lag issues
            if (!_songTimeInitialized)
                InitializeSongTime(SongOffset);

            // Start sync thread
            _runSync = true;
            _syncThread.Start();
        }

        public void Update()
        {
            // Runner is lazy-started to avoid timing issues with lag
            if (!Started)
            {
                Start();
                Started = true;
            }

            if (Paused)
                return;

            // Update times
            UpdateInputTimes();

            // Check for unexpected backwards time jumps

            // Only check for greater-than here
            // BASS's update rate is too coarse for equals to never happen
            AssertTimeProgressionLenient(nameof(RealAudioTime), RealAudioTime, ref _previousRealAudioTime);

            // *Do* check for equals here, as input time not updating is a more serious issue
            AssertTimeProgression(nameof(RealSongTime), RealSongTime, ref _previousRealSongTime);
            AssertTimeProgression(nameof(RealVisualTime), RealVisualTime, ref _previousRealVisualTime);
            AssertTimeProgression(nameof(RealInputTime), RealInputTime, ref _previousRealInputTime);

            _seeked = false;
        }

        private void AssertTimeProgression(string name, double current, ref double previous)
        {
            if (previous >= current)
            {
                YargLogger.AssertFormat(_seeked, "Unexpected {0} seek backwards! Went from {1} to {2}", name, previous, current);
            }
            previous = current;
        }

        private void AssertTimeProgressionLenient(string name, double current, ref double previous)
        {
            if (previous > current)
            {
                YargLogger.AssertFormat(_seeked, "Unexpected {0} seek backwards! Went from {1} to {2}", name, previous, current);
            }
            previous = current;
        }

        private void SyncThread()
        {
            const double INITIAL_SYNC_THRESH = 0.015;
            const double ADJUST_SYNC_THRESH = 0.005;
            const float SPEED_ADJUSTMENT = 0.05f;

            // Wait until it's time to start the audio
            for (; _runSync; Thread.Yield())
            {
                if (_pauseSync)
                    continue;

                // Song time is driven using visual time when the audio isn't running
                // Playback buffer needs to be accounted for to prevent backwards seeking
                double currentTime = SyncVisualTime - AudioCalibration + AudioManager.Instance.PlaybackBufferLength;
                if (currentTime >= SongOffset)
                {
                    // Start audio
                    _mixer.Play();
                    break;
                }
            }

            for (; _runSync; _finishedSyncing.Set(), Thread.Sleep(5))
            {
                if (!_mixer.IsPlaying || _pauseSync)
                    continue;

                _finishedSyncing.Reset();

                // Account for song speed
                double initialThreshold = INITIAL_SYNC_THRESH * SelectedSongSpeed;
                double adjustThreshold = ADJUST_SYNC_THRESH * SelectedSongSpeed;

                // Check the difference between visual and audio times
                double delta = SyncDelta;
                double deltaAbs = Math.Abs(delta);
                // Don't sync if below the initial sync threshold, and we haven't adjusted the speed
                if (_syncSpeedMultiplier == 0 && deltaAbs < initialThreshold)
                    continue;

                // We're now syncing, determine how much to adjust the song speed by
                int speedMultiplier = (int) Math.Round(delta / initialThreshold);
                if (speedMultiplier == 0)
                    speedMultiplier = delta > 0 ? 1 : -1;

                // Only change speed when the multiplier changes
                if (_syncSpeedMultiplier != speedMultiplier)
                {
                    if (_syncSpeedMultiplier == 0)
                        _syncStartDelta = (float) delta;

                    _syncSpeedMultiplier = speedMultiplier;

                    float adjustment = SPEED_ADJUSTMENT * speedMultiplier;
                    if (!Mathf.Approximately(adjustment, _syncSpeedAdjustment))
                    {
                        _syncSpeedAdjustment = adjustment;
                        _mixer.SetSpeed(ActualSongSpeed);
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

        private void ResetSync()
        {
            _syncStartDelta = 0;
            _syncSpeedMultiplier = 0;
            _syncSpeedAdjustment = 0f;
            _mixer.SetSpeed(ActualSongSpeed);
        }

        public double GetRelativeInputTime(double timeFromInputSystem)
        {
            return InputTimeBase + ((timeFromInputSystem - InputTimeOffset) * SelectedSongSpeed);
        }

        public double GetCalibratedRelativeInputTime(double timeFromInputSystem)
        {
            return GetRelativeInputTime(timeFromInputSystem) + VideoCalibration;
        }

        private void UpdateInputTimes()
        {
            // Update times
            RealInputTime = GetRelativeInputTime(InputManager.InputUpdateTime);
            RealVisualTime = GetRelativeInputTime(InputManager.GameUpdateTime);
            RealAudioTime = _mixer.GetPosition() + SongOffset;
            // We use visual time for song time due to an apparent bug in BASS
            // where it will sometimes not fire the song end event when the audio ends
            // Using visual time guarantees a reliable timing source, and therefore song end timing
            RealSongTime = RealVisualTime - AudioCalibration;
        }

        private void SetInputBase(double inputBase)
        {
            double previousBase = InputTimeBase;
            double previousOffset = InputTimeOffset;
            double previousInputTime = InputTime;
            double previousVisualTime = VisualTime;

            InputTimeBase = inputBase;
            InputTimeOffset = InputManager.InputUpdateTime;

            // Update input times
            UpdateInputTimes();

            YargLogger.LogFormatDebug("Set input time base.\nNew base: {0:0.000000}, new offset: {1:0.000000}, new visual time: {2:0.000000}, new input time: {3:0.000000}\n"
                + "Old base: {4:0.000000}, old offset: {5:0.000000}, old visual time: {6:0.000000}, old input time: {7:0.000000}",
                InputTimeBase, InputTimeOffset, VisualTime, InputTime, previousBase,
                previousOffset, previousVisualTime, previousInputTime);
        }

        private void SetInputBaseChecked(double inputBase)
        {
            double previousVisualTime = VisualTime;
            double previousInputTime = InputTime;

            SetInputBase(inputBase);

            // Speeds above 200% or so can cause inaccuracies greater than 1 ms
            double threshold = Math.Max(0.001 * SelectedSongSpeed, 0.0005);
            YargLogger.AssertFormat(Math.Abs(VisualTime - previousVisualTime) <= threshold,
                "Unexpected visual time change! Went from {0} to {1}, threshold {2}",
                previousVisualTime, VisualTime, threshold);
            YargLogger.AssertFormat(Math.Abs(InputTime - previousInputTime) <= threshold,
                "Unexpected visual time change! Went from {0} to {1}, threshold {2}",
                previousInputTime, InputTime, threshold);
        }

        private void InitializeSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            // Account for song speed
            delayTime *= SelectedSongSpeed;

            // Seek time
            // Doesn't account for audio calibration for better audio syncing
            // since seeking is slightly delayed
            double seekTime = time - delayTime;

            // Set input offsets
            SetInputBase(seekTime);

            // Previously audio calibration was handled on input time, as it consistently started out synced
            // within 50 ms (within 5 ms a majority of the time)
            // But it makes more sense to apply it to audio instead, and the small initial desync it
            // may cause *really* isn't that big of a deal, as it's also usually within 50 ms,
            // and the sync code handles it quickly anyways
            //
            // SetInputBase(seekTime + AudioCalibration);
            // RealSongTime = seekTime;

            YargLogger.LogFormatDebug("Set song time to {0:0.000000} (delay: {1:0.000000}).\n" +
                "Seek time: {2:0.000000}, resulting song time: {3:0.000000}", time, delayTime, seekTime, SongTime);

            _songTimeInitialized = true;
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            _pauseSync = true;
            _finishedSyncing.WaitOne();

            // Set input/song time
            InitializeSongTime(time, delayTime);

            // Reset syncing before seeking to prevent speed adjustments from causing issues
            ResetSync();

            // Audio seeking; cannot go negative
            double seekTime = RealSongTime;
            if (seekTime < 0) seekTime = 0;
            _mixer.SetPosition(seekTime);

            _pauseSync = false;
            _seeked = true;
        }

        public void SetSongSpeed(float speed)
        {
            _pauseSync = true;
            _finishedSyncing.WaitOne();

            // 10% - 4995%, we reserve 5% so that audio syncing can still function
            speed = Math.Clamp(speed, 10 / 100f, 4995 / 100f);

            // Set speed; save old for input offset compensation
            SelectedSongSpeed = speed;

            // Set based on the actual song speed, so as to not break resyncing
            _mixer.SetSpeed(ActualSongSpeed);

            // Adjust input offset, otherwise input time will desync
            // TODO: Pressing and holding left or right in practice will
            // cause time to progress much slower than it should
            SetInputBaseChecked(RealInputTime);

            _pauseSync = false;

            YargLogger.LogFormatDebug("Set song speed to {0:0.00}.\n"
                + "Song time: {1:0.000000}, visual time: {2:0.000000}, input time: {3:0.000000}", speed,
                SongTime, VisualTime, InputTime);
        }

        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(SelectedSongSpeed + deltaSpeed);

        /// <summary>
        /// Pauses the song.
        /// </summary>
        /// <remarks>
        /// The song runner keeps track of the number of pending pauses to prevent pausing in one place
        /// being overridden by resuming in another. For correct behavior, every call to <see cref="Pause"/>
        /// must be matched with a future call to <see cref="Resume"/>.
        /// </remarks>
        public void Pause()
        {
            if (PendingPauses++ > 0) return;

            // Visual time is used for pause time since it's closer to when
            // the song runner is actually being updated; the asserts in Update get hit otherwise
            PauseStartTime = RealVisualTime;
            _playAudioOnResume = _mixer.IsPlaying;
            _pauseSync = true;
            _mixer.Pause();

            YargLogger.LogFormatDebug("Paused at song time {0:0.000000} (real: {1:0.000000}), visual time {2:0.000000} " +
                "(real: {3:0.000000}), input time {4:0.000000} (real: {5:0.000000}).",
                SongTime, RealSongTime, VisualTime, RealVisualTime, InputTime, RealInputTime);
        }

        /// <summary>
        /// Resumes the song.
        /// </summary>
        /// <remarks>
        /// The song runner keeps track of the number of pending pauses to prevent pausing in one place
        /// being overridden by resuming in another. For correct behavior, every call to <see cref="Resume"/>
        /// must be matched with a previous call to <see cref="Pause"/>.
        /// </remarks>
        public void Resume(bool inputCompensation = true)
        {
            if (PendingPauses < 1 || --PendingPauses > 0) return;

            if (inputCompensation)
            {
                SetInputBaseChecked(PauseStartTime);
            }

            if (_playAudioOnResume)
            {
                _mixer.Play();
            }

            _pauseSync = false;

            YargLogger.LogFormatDebug("Resumed at song time {0:0.000000} (real: {1:0.000000}), visual time {2:0.000000} " +
                "(real: {3:0.000000}), input time {4:0.000000} (real: {5:0.000000}).",
                SongTime, RealSongTime, VisualTime, RealVisualTime, InputTime, RealInputTime);
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

        public void OverridePauseTime(double pauseTime = -1)
        {
            if (!Paused)
            {
                return;
            }

            // Visual time is used for pause time since it's closer to when
            // the song runner is actually being updated; the asserts in Update get hit otherwise
            if (pauseTime < 0)
                pauseTime = RealVisualTime;

            PauseStartTime = pauseTime;
        }
    }
}