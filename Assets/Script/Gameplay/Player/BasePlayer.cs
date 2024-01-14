using System.Collections.Generic;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public abstract class BasePlayer : GameplayBehaviour
    {
        public YargPlayer Player { get; private set; }

        public float NoteSpeed
        {
            get
            {
                if (GameManager.SelectedSongSpeed < 1)
                {
                    return Player.Profile.NoteSpeed;
                }

                return Player.Profile.NoteSpeed / GameManager.SelectedSongSpeed;
            }
        }

        /// <summary>
        /// The player's input calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive calibration settings will result in a negative number here.
        /// </remarks>
        public double InputCalibration => -Player.Profile.InputCalibrationSeconds;

        public abstract BaseEngine BaseEngine { get; }

        public BaseStats BaseStats => BaseEngine.BaseStats;
        public BaseEngineParameters BaseParameters => BaseEngine.BaseParameters;

        public abstract float[] StarMultiplierThresholds { get; }
        public abstract int[] StarScoreThresholds { get; protected set; }

        public HitWindowSettings HitWindow { get; protected set; }

        public int Score => BaseStats.Score;
        public int Combo => BaseStats.Combo;
        public int NotesHit => BaseStats.NotesHit;

        public int TotalNotes { get; protected set; }

        public bool IsFc { get; protected set; }
        public bool IsNewHighScore { get; protected set; }

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        protected SyncTrack SyncTrack { get; private set; }

        protected bool IsInitialized { get; private set; }

        protected List<ISantrollerHaptics> SantrollerHaptics { get; private set; } = new();

        protected BaseInputViewer InputViewer { get; private set; }

        private List<GameInput> _replayInputs;

        private int _replayInputIndex;

        protected override void GameplayAwake()
        {
            _replayInputs = new List<GameInput>();

            InputViewer = FindObjectOfType<BaseInputViewer>();

            IsFc = true;
        }

        protected void Start()
        {
            if (Player.Bindings is not null)
            {
                SantrollerHaptics = Player.Bindings.GetDevicesByType<ISantrollerHaptics>();
            }

            if (!GameManager.IsReplay)
            {
                SubscribeToInputEvents();
            }
        }

        protected void Initialize(int index, YargPlayer player, SongChart chart)
        {
            if (IsInitialized) return;

            Player = player;

            SyncTrack = chart.SyncTrack;

            if (GameManager.IsReplay)
            {
                _replayInputs = new List<GameInput>(GameManager.Replay.Frames[index].Inputs);
                Debug.Log("Initialized replay inputs with " + _replayInputs.Count + " inputs");
            }

            if (InputViewer != null)
            {
                InputViewer.SetColors(player.ColorProfile);
                InputViewer.ResetButtons();
            }

            IsInitialized = true;
        }

        protected abstract void ResetVisuals();
        public abstract void ResetPracticeSection();

        public virtual void UpdateWithTimes(double inputTime)
        {
            if (GameManager.Paused)
            {
                return;
            }

            UpdateInputs(inputTime);
            UpdateVisualsWithTimes(inputTime);
        }

        protected virtual void UpdateVisualsWithTimes(double inputTime)
        {
            UpdateVisuals(inputTime);
        }

        protected abstract void UpdateVisuals(double time);

        public abstract void SetPracticeSection(uint start, uint end);

        public virtual void SetReplayTime(double time)
        {
            _replayInputIndex = BaseEngine.ProcessUpToTime(time, ReplayInputs);

            IsFc = true;

            ResetVisuals();
            UpdateVisualsWithTimes(time);
        }

        protected virtual void FinishDestruction()
        {
        }

        protected override void GameplayDestroy()
        {
            if (!GameManager.IsReplay)
            {
                UnsubscribeFromInputEvents();
            }

            FinishDestruction();
        }

        protected virtual void UpdateInputs(double time)
        {
            // Apply input offset
            // Video offset is already accounted for
            time += InputCalibration;

            if (Player.Profile.IsBot)
            {
                BaseEngine.UpdateBot(time);
                return;
            }

            if (GameManager.IsReplay)
            {
                while (_replayInputIndex < ReplayInputs.Count)
                {
                    var input = ReplayInputs[_replayInputIndex];

                    // Current input does not meet the time requirement
                    if (time < input.Time)
                    {
                        break;
                    }

                    BaseEngine.QueueInput(ref input);
                    OnInputQueued(input);

                    _replayInputIndex++;
                }
            }

            if (BaseEngine.IsInputQueued)
            {
                BaseEngine.UpdateEngineInputs();
            }
            else
            {
                BaseEngine.UpdateEngineToTime(time);
            }
        }

        private void SubscribeToInputEvents()
        {
            Player.Bindings.SubscribeToGameplayInputs(Player.Profile.GameMode, OnGameInput);
            Player.Bindings.DeviceAdded += OnDeviceAdded;
        }

        private void UnsubscribeFromInputEvents()
        {
            Player.Bindings.UnsubscribeFromGameplayInputs(Player.Profile.GameMode, OnGameInput);
            Player.Bindings.DeviceAdded -= OnDeviceRemoved;
        }

        private void OnDeviceAdded(InputDevice device)
        {
            if (device is ISantrollerHaptics haptics)
            {
                SantrollerHaptics.Add(haptics);
            }
        }

        private void OnDeviceRemoved(InputDevice device)
        {
            if (device is ISantrollerHaptics haptics)
            {
                SantrollerHaptics.Remove(haptics);
            }
        }

        protected void OnGameInput(ref GameInput input)
        {
            // Ignore while paused
            if (GameManager.Paused) return;

            double adjustedTime = GameManager.GetCalibratedRelativeInputTime(input.Time);
            // Apply input offset
            adjustedTime += InputCalibration;
            input = new(adjustedTime, input.Action, input.Integer);

            // Allow the input to be explicitly ignored before processing it
            if (InterceptInput(ref input)) return;

            BaseEngine.QueueInput(ref input);
            OnInputQueued(input);
            _replayInputs.Add(input);
        }

        protected virtual void OnStarPowerPhraseHit()
        {
            if (!GameManager.Paused)
            {
                GlobalVariables.AudioManager.PlaySoundEffect(SfxSample.StarPowerAward);
            }

            foreach (var haptics in SantrollerHaptics)
            {
                haptics.SetStarPowerFill((float) BaseStats.StarPowerAmount);
            }
        }

        protected virtual void OnStarPowerStatus(bool status)
        {
            if (!GameManager.Paused)
            {
                GlobalVariables.AudioManager.PlaySoundEffect(status
                    ? SfxSample.StarPowerDeploy
                    : SfxSample.StarPowerRelease);
            }

            foreach (var haptics in SantrollerHaptics)
            {
                haptics.SetStarPowerActive(status);
            }
        }

        protected abstract bool InterceptInput(ref GameInput input);

        protected virtual void OnInputQueued(GameInput input)
        {
            if (InputViewer != null)
            {
                InputViewer.OnInput(input);
            }
        }

        protected static int[] PopulateStarScoreThresholds(float[] multiplierThresh, int baseScore)
        {
            var starScoreThresh = new int[multiplierThresh.Length];

            for (int i = 0; i < multiplierThresh.Length; i++)
            {
                starScoreThresh[i] = Mathf.FloorToInt(baseScore * multiplierThresh[i]);
            }

            return starScoreThresh;
        }

        public double GetStarsPercent()
        {
            double stars = 0;

            for (int i = 0; i < StarScoreThresholds.Length; i++)
            {
                // Skip until we reach the progressing threshold
                if (Score > StarScoreThresholds[i])
                {
                    if (i == StarScoreThresholds.Length - 1)
                    {
                        stars += 6f;
                    }

                    continue;
                }

                // Otherwise, get the progress.
                // There is at least this amount of stars.
                stars += i;

                // Then, we just gotta get the progress into the next star.
                int bound = i != 0 ? StarScoreThresholds[i - 1] : 0;
                stars += (double) (Score - bound) / (StarScoreThresholds[i] - bound);

                break;
            }

            return stars;
        }
    }
}