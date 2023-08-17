using System;
using UnityEngine;
using YARG.Input;
using YARG.Settings;

namespace YARG.Gameplay
{
    public partial class GameManager : MonoBehaviour
    {
        private const double SONG_START_DELAY = 2;

        /// <summary>
        /// The time into the song, accounting for song speed and calibration.<br/>
        /// This is updated every frame while the game is not paused.
        /// </summary>
        public double SongTime => RealSongTime + AudioCalibration;

        /// <summary>
        /// The time into the song, accounting for song speed but <b>not</b> calibration.<br/>
        /// This is updated every frame while the game is not paused.
        /// </summary>
        public double RealSongTime { get; private set; }

        /// <summary>
        /// The current input time, accounting for song speed and calibration.<br/>
        /// This is updated every frame while the game is not paused.
        /// </summary>
        public double InputTime => RealInputTime + AudioCalibration;

        /// <summary>
        /// The current input time, accounting for song speed but <b>not</b> calibration.<br/>
        /// This is updated every frame while the game is not paused.
        /// </summary>
        // Uses the selected song speed and not the actual song speed,
        // audio is synced to the inputs and not vice versa
        public double RealInputTime { get; private set; }

        /// <summary>
        /// The input time that is considered to be 0.
        /// Applied before song speed is factored in.
        /// </summary>
        public static double InputTimeOffset { get; private set; }

        /// <summary>
        /// The base time added on to relative time to get the real current input time.
        /// Applied after song speed is.
        /// </summary>
        public static double InputTimeBase { get; private set; }

        /// <summary>
        /// The audio calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive calibration settings will result in a negative number here.
        /// </remarks>
        public double AudioCalibration => -SettingsManager.Settings.AudioCalibration.Data / 1000.0;

        /// <summary>
        /// The song offset, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive offsets in the .ini or .chart will result in a negative number here.
        /// </remarks>
        public double SongOffset => -Song.SongOffset;

        private float _syncSpeedAdjustment = 0f;
        private int _syncSpeedMultiplier = 0;
        private double _syncStartDelta;

        private void InitializeTime()
        {
            // Initialize times
            InitializeSongTime(SongOffset);
            GlobalVariables.AudioManager.SetPosition(0);
        }

        public double GetRelativeInputTime(double timeFromInputSystem)
        {
            return InputTimeBase + ((timeFromInputSystem - InputTimeOffset) * SelectedSongSpeed);
        }

        private void SetInputBase(double inputBase)
        {
            InputTimeBase = inputBase;
            InputTimeOffset = InputManager.CurrentUpdateTime;

            // Update input time
            RealInputTime = GetRelativeInputTime(InputManager.CurrentUpdateTime);
        }

        private void UpdateTimes()
        {
            // Update input time
            RealInputTime = GetRelativeInputTime(InputManager.CurrentUpdateTime);

            // Calculate song time
            if (RealSongTime < SongOffset)
            {
                // Drive song time using input time until it's time to start the audio
                RealSongTime = RealInputTime;
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
                // Sync if needed
                SyncAudio();
            }
        }

        private void SyncAudio()
        {
            const double INITIAL_SYNC_THRESH = 0.015;
            const double ADJUST_SYNC_THRESH = 0.005;
            const float SPEED_ADJUSTMENT = 0.05f;

            double inputTime = InputTime;
            double audioTime = SongTime;

            // Account for song speed
            double initialThreshold = INITIAL_SYNC_THRESH * SelectedSongSpeed;
            double adjustThreshold = ADJUST_SYNC_THRESH * SelectedSongSpeed;

            // Check the difference between input and audio times
            double delta = inputTime - audioTime;
            double deltaAbs = Math.Abs(delta);
            // Don't sync if below the initial sync threshold, and we haven't adjusted the speed
            if (_syncSpeedMultiplier == 0 && deltaAbs < initialThreshold)
                return;

            // We're now syncing, determine how much to adjust the song speed by
            int speedMultiplier = (int)Math.Round(delta / initialThreshold);
            if (speedMultiplier == 0)
                speedMultiplier = delta > 0 ? 1 : -1;

            // Only change speed when the multiplier changes
            if (_syncSpeedMultiplier != speedMultiplier)
            {
                if (_syncSpeedMultiplier == 0)
                {
                    _syncStartDelta = delta;
                }

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
                _syncStartDelta = 0;
                _syncSpeedMultiplier = 0;
                _syncSpeedAdjustment = 0f;
                GlobalVariables.AudioManager.SetSpeed(ActualSongSpeed);
            }
        }

        private void InitializeSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            // Account for song speed
            delayTime *= SelectedSongSpeed;

            // Seek time
            // Doesn't account for audio calibration for better audio syncing
            // since seeking is slightly delayed
            double seekTime = time - delayTime;

            // Set input offsets, factoring out audio calibration
            // Doing audio calibration here seems to work the best,
            // consistently starts out synced within ~50 ms (within 5 ms a majority of the time)
            SetInputBase(seekTime + AudioCalibration);

            // Set audio/song time
            RealSongTime = seekTime;

#if UNITY_EDITOR
            Debug.Log($"Set song time to {time:0.000000} (delay: {delayTime:0.000000}).\n" +
                $"Seek time: {seekTime:0.000000}, song time: {SongTime:0.000000}, input time: {InputTime:0.000000} " +
                $"(base: {InputTimeBase:0.000000}, offset: {InputTimeOffset:0.000000}, absolute: {InputManager.CurrentUpdateTime:0.000000})");
#endif
        }

        public void SetSongTime(double time, double delayTime = SONG_START_DELAY)
        {
            // Set input/song time
            InitializeSongTime(time, delayTime);

            // Audio seeking; cannot go negative
            double seekTime = RealSongTime;
            if (seekTime < 0) seekTime = 0;
            GlobalVariables.AudioManager.SetPosition(seekTime);

            // Reset beat events
            BeatEventManager.ResetTimers();
        }
    }
}