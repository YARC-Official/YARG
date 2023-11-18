using System;
using System.Threading;
using UnityEngine;
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
        /// </remarks>
        public double SongTime => RealSongTime + AudioCalibration;

        /// <summary>
        /// The time into the song, accounting for song speed but <b>not</b> audio calibration.<br/>
        /// This is updated every frame while not paused.
        /// </summary>
        public double RealSongTime { get; private set; }

        /// <summary>
        /// The instantaneous current audio time, used for audio synchronization.<br/>
        /// Accounts for song speed, audio calibration, and song offset.
        /// </summary>
        public double SyncSongTime => GlobalVariables.AudioManager.CurrentPositionD + AudioCalibration + SongOffset;

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
        /// The instantaneous current visual time, used for audio synchronization.<br/>
        /// Accounts for song speed, but <b>not</b> video calibration.
        /// </summary>
        public double SyncVisualTime => GetRelativeInputTime(InputManager.CurrentInputTime);

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
        #endregion

        #region Audio syncing
        private Thread _syncThread;

        private EventWaitHandle _finishedSyncing = new(true, EventResetMode.ManualReset);
        private volatile bool _runSync;
        private volatile bool _pauseSync;

        private volatile float _syncSpeedAdjustment;
        private volatile int _syncSpeedMultiplier;
        private volatile float _syncStartDelta;

        public float SyncSpeedAdjustment => _syncSpeedAdjustment;
        public int SyncSpeedMultiplier => _syncSpeedMultiplier;
        public float SyncStartDelta => _syncStartDelta;
        #endregion

        #region Seek debugging
        private bool _seeked;
        private double _previousRealSongTime = double.NaN;
        private double _previousRealVisualTime = double.NaN;
        private double _previousRealInputTime = double.NaN;
        #endregion

        /// <summary>
        /// Creates a new song runner with the given speed and calibration values.
        /// </summary>
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
        public SongRunner(float songSpeed = 1f, int audioCalibration = 0, int videoCalibration = 0,
            double songOffset = 0)
        {
            SelectedSongSpeed = songSpeed;
            VideoCalibration = -videoCalibration / 1000.0;
            AudioCalibration = (-audioCalibration / 1000.0) - VideoCalibration;
            SongOffset = -songOffset;

            // Initialize times
            InitializeSongTime(SongOffset);
            GlobalVariables.AudioManager.SetPosition(0);

            // Start sync thread
            _runSync = true;
            _syncThread = new Thread(SyncThread) { IsBackground = true };
            _syncThread.Start();
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
                _syncThread?.Join();
                _syncThread = null;
            }
        }

        public void Update()
        {
            // Update input/visual time
            RealInputTime = GetRelativeInputTime(InputManager.InputUpdateTime);
            RealVisualTime = GetRelativeInputTime(InputManager.GameUpdateTime);

            // Calculate song time
            if (RealSongTime < SongOffset)
            {
                // Drive song time using visual time until it's time to start the audio
                RealSongTime = RealVisualTime - AudioCalibration;
                if (RealSongTime >= SongOffset)
                {
                    // Start audio
                    GlobalVariables.AudioManager.Play();
                    // Seek to calculated time to keep everything in sync
                    GlobalVariables.AudioManager.SetPosition(RealSongTime - SongOffset);
                }
            }
            else
            {
                RealSongTime = GlobalVariables.AudioManager.CurrentPositionD + SongOffset;
            }

            // Check for unexpected backwards time jumps

            // Only check for greater-than here
            // BASS's update rate is too coarse for equals to never happen
            if (_previousRealSongTime > RealSongTime)
            {
                Debug.Assert(_seeked, $"Unexpected audio seek backwards! Went from {_previousRealSongTime} to {RealSongTime}");
            }
            _previousRealSongTime = RealSongTime;

            // *Do* check for equals here, as input time not updating is a more serious issue
            if (_previousRealVisualTime >= RealVisualTime)
            {
                Debug.Assert(_seeked, $"Unexpected visual seek backwards! Went from {_previousRealVisualTime} to {RealVisualTime}");
            }
            _previousRealVisualTime = RealVisualTime;

            if (_previousRealInputTime >= RealInputTime)
            {
                Debug.Assert(_seeked, $"Unexpected input seek backwards! Went from {_previousRealInputTime} to {RealInputTime}");
            }
            _previousRealInputTime = RealInputTime;

            _seeked = false;
        }

        private void SyncThread()
        {
            const double INITIAL_SYNC_THRESH = 0.015;
            const double ADJUST_SYNC_THRESH = 0.005;
            const float SPEED_ADJUSTMENT = 0.05f;

            for (; _runSync; _finishedSyncing.Set(), Thread.Sleep(5))
            {
                if (!GlobalVariables.AudioManager.IsPlaying || _pauseSync)
                    continue;

                _finishedSyncing.Reset();

                // Account for song speed
                double initialThreshold = INITIAL_SYNC_THRESH * SelectedSongSpeed;
                double adjustThreshold = ADJUST_SYNC_THRESH * SelectedSongSpeed;

                // Check the difference between visual and audio times
                float delta = (float) (SyncVisualTime - SyncSongTime);
                float deltaAbs = Math.Abs(delta);
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
                        _syncStartDelta = delta;

                    _syncSpeedMultiplier = speedMultiplier;

                    float adjustment = SPEED_ADJUSTMENT * speedMultiplier;
                    if (!Mathf.Approximately(adjustment, _syncSpeedAdjustment))
                    {
                        _syncSpeedAdjustment = adjustment;
                        GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);
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
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);
        }

        public double GetRelativeInputTime(double timeFromInputSystem)
        {
            return InputTimeBase + ((timeFromInputSystem - InputTimeOffset) * SelectedSongSpeed);
        }

        public double GetCalibratedRelativeInputTime(double timeFromInputSystem)
        {
            return GetRelativeInputTime(timeFromInputSystem) + VideoCalibration;
        }

        private void SetInputBase(double inputBase)
        {
            double previousBase = InputTimeBase;
            double previousOffset = InputTimeOffset;
            double previousInputTime = InputTime;
            double previousVisualTime = VisualTime;

            InputTimeBase = inputBase;
            InputTimeOffset = InputManager.GameUpdateTime;

            // Update input/visual time
            RealInputTime = GetRelativeInputTime(InputManager.InputUpdateTime);
            RealVisualTime = GetRelativeInputTime(InputManager.GameUpdateTime);

#if UNITY_EDITOR
            Debug.Log($"Set input time base. New base: {InputTimeBase:0.000000}, new offset: {InputTimeOffset:0.000000}, new input time: {InputTime:0.000000}, new visual time: {VisualTime:0.000000}\n"
                + $"Old base: {previousBase:0.000000}, old offset: {previousOffset:0.000000}, old input time: {previousInputTime:0.000000}, old input time: {previousVisualTime:0.000000}");
#endif
        }

        private void InitializeSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            // Account for song speed
            delayTime *= SelectedSongSpeed;
            // Additionally delay by audio calibration to keep delays consistent relative to when the audio starts
            delayTime -= AudioCalibration;

            // Seek time
            // Doesn't account for audio calibration for better audio syncing
            // since seeking is slightly delayed
            double seekTime = time - delayTime;

            // Set input offsets
            SetInputBase(seekTime);

            // Set audio/song time, factoring in audio calibration
            RealSongTime = seekTime - AudioCalibration;

            // Previously audio calibration was handled on input time, as it consistently started out synced
            // within 50 ms (within 5 ms a majority of the time)
            // But it makes more sense to apply it to audio instead, and the small initial desync it
            // may cause *really* isn't that big of a deal, as it's also usually within 50 ms,
            // and the sync code handles it quickly anyways
            //
            // SetInputBase(seekTime + AudioCalibration);
            // RealSongTime = seekTime;

#if UNITY_EDITOR
            Debug.Log($"Set song time to {time:0.000000} (delay: {delayTime:0.000000}).\n" +
                $"Seek time: {seekTime:0.000000}, song time: {SongTime:0.000000}");
#endif
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
            GlobalVariables.AudioManager.SetPosition(seekTime);

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
            GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);

            // Adjust input offset, otherwise input time will desync
            SetInputBase(VisualTime);

            _pauseSync = false;

#if UNITY_EDITOR
            Debug.Log($"Set song speed to {speed:0.00}.\n"
                + $"Input time: {VisualTime:0.000000}, song time: {SongTime:0.000000}");
#endif
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

            PauseStartTime = RealVisualTime;
            GlobalVariables.AudioManager.Pause();

#if UNITY_EDITOR
            Debug.Log($"Paused at song time {SongTime:0.000000} (real: {RealSongTime:0.000000}), input time {VisualTime:0.000000} (real: {RealVisualTime:0.000000}).");
#endif
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
                SetInputBase(PauseStartTime);
            }

            if (RealSongTime >= SongOffset)
            {
                GlobalVariables.AudioManager.Play();
            }

#if UNITY_EDITOR
            Debug.Log($"Resumed at song time {SongTime:0.000000} (real: {RealSongTime:0.000000}), input time {VisualTime:0.000000} (real: {RealVisualTime:0.000000}).");
#endif
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

            if (pauseTime < 0)
                pauseTime = RealVisualTime;

            PauseStartTime = pauseTime;
        }
    }
}