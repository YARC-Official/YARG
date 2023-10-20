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
        /// <remarks>
        /// This value should be used for all visual interactions, as video calibration should not delay visuals.
        /// It should also be used for setting position, otherwise the actual set position will be offset incorrectly.
        /// </remarks>
        public double RealInputTime { get; private set; }

        /// <summary>
        /// The instantaneous current input time, used for audio synchronization.<br/>
        /// Accounts for song speed, but <b>not</b> video calibration.
        /// </summary>
        public double SyncInputTime => GetRelativeInputTime(InputManager.CurrentInputTime);

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
        public bool Paused { get; private set; }

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
        private double _previousInputTime = double.NaN;
        #endregion

        public SongRunner(float songSpeed = 1f, double audioCalibration = 0, double videoCalibration = 0,
            double songOffset = 0)
        {
            SelectedSongSpeed = songSpeed;
            AudioCalibration = audioCalibration;
            VideoCalibration = videoCalibration;
            SongOffset = songOffset;

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
            // Update input time
            RealInputTime = GetRelativeInputTime(InputManager.CurrentUpdateTime);

            // Calculate song time
            if (RealSongTime < SongOffset)
            {
                // Drive song time using input time until it's time to start the audio
                RealSongTime = RealInputTime - AudioCalibration;
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
            if (_previousInputTime > InputTime)
            {
                Debug.Assert(_seeked, $"Unexpected input seek backwards! Went from {_previousInputTime} to {InputTime}");
            }
            _previousInputTime = InputTime;

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

                // Check the difference between input and audio times
                float delta = (float) (SyncInputTime - SyncSongTime);
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
            double previousTime = InputTime;

            InputTimeBase = inputBase;
            InputTimeOffset = InputManager.CurrentUpdateTime;

            // Update input time
            RealInputTime = GetRelativeInputTime(InputManager.CurrentUpdateTime);

#if UNITY_EDITOR
            Debug.Log($"Set input time base. New base: {InputTimeBase:0.000000}, new offset: {InputTimeOffset:0.000000}, new input time: {InputTime:0.000000}\n"
                + $"Old base: {previousBase:0.000000}, old offset: {previousOffset:0.000000}, old input time: {previousTime:0.000000}");
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
            SetInputBase(InputTime);

            _pauseSync = false;

#if UNITY_EDITOR
            Debug.Log($"Set song speed to {speed:0.00}.\n"
                + $"Input time: {InputTime:0.000000}, song time: {SongTime:0.000000}");
#endif
        }

        public void AdjustSongSpeed(float deltaSpeed) => SetSongSpeed(SelectedSongSpeed + deltaSpeed);

        public void Pause()
        {
            if (Paused) return;
            Paused = true;

            PauseStartTime = RealInputTime;
            GlobalVariables.AudioManager.Pause();

#if UNITY_EDITOR
            Debug.Log($"Paused at song time {SongTime:0.000000} (real: {RealSongTime:0.000000}), input time {InputTime:0.000000} (real: {RealInputTime:0.000000}).");
#endif
        }

        public void Resume(bool inputCompensation = true)
        {
            if (!Paused) return;

            Paused = false;

            if (inputCompensation)
            {
                SetInputBase(PauseStartTime);
            }

            if (RealSongTime >= SongOffset)
            {
                GlobalVariables.AudioManager.Play();
            }

#if UNITY_EDITOR
            Debug.Log($"Resumed at song time {SongTime:0.000000} (real: {RealSongTime:0.000000}), input time {InputTime:0.000000} (real: {RealInputTime:0.000000}).");
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
                pauseTime = RealInputTime;

            PauseStartTime = pauseTime;
        }
    }
}