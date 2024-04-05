using System.Collections.Generic;
using PlasticBand.Haptics;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Input;
using YARG.Player;
using YARG.Settings;

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

        public abstract float[] StarMultiplierThresholds { get; protected set; }
        public abstract int[] StarScoreThresholds { get; protected set; }

        public abstract bool ShouldUpdateInputsOnResume { get; }

        public HitWindowSettings HitWindow { get; protected set; }

        public float Stars => BaseStats.Stars;

        public int Score => BaseStats.TotalScore;
        public int Combo => BaseStats.Combo;
        public int NotesHit => BaseStats.NotesHit;

        public int TotalNotes { get; protected set; }

        public bool IsFc { get; protected set; }
        public bool IsNewHighScore { get; protected set; }

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        private Dictionary<int, GameInput> LastInputs { get; } = new();
        private Dictionary<int, GameInput> InputsToSendOnResume { get; } = new();

        protected SyncTrack SyncTrack { get; private set; }

        protected bool IsInitialized { get; private set; }

        protected List<ISantrollerHaptics> SantrollerHaptics { get; private set; } = new();

        protected BaseInputViewer InputViewer { get; private set; }

        protected int  _lastCombo;
        protected bool _isStemMuted;

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
                YargLogger.LogFormatDebug("Initialized replay inputs with {0} inputs", _replayInputs.Count);
            }

            if (InputViewer != null)
            {
                InputViewer.SetColors(player.ColorProfile);
                InputViewer.ResetButtons();
            }

            IsInitialized = true;
        }

        protected abstract void ResetVisuals();

        public virtual void ResetPracticeSection()
        {
            _lastCombo = 0;

            IsFc = true;

            ResetVisuals();
        }

        public virtual void UpdateWithTimes(double inputTime)
        {
            if (!GameManager.Started || GameManager.Paused)
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

        // TODO Make this more generic
        public abstract void SetStemMuteState(bool muted);

        public virtual void SetStarPowerFX(bool active)
        {
            GameManager.ChangeStemReverbState(SongStem.Song, active);
        }

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
            Player.Bindings.DeviceRemoved += OnDeviceRemoved;
        }

        private void UnsubscribeFromInputEvents()
        {
            Player.Bindings.UnsubscribeFromGameplayInputs(Player.Profile.GameMode, OnGameInput);

            Player.Bindings.DeviceAdded -= OnDeviceAdded;
            Player.Bindings.DeviceRemoved -= OnDeviceRemoved;
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

            if (!GameManager.Paused && SettingsManager.Settings.PauseOnDeviceDisconnect.Value)
            {
                GameManager.SetPaused(true);
            }
        }

        public void SendInputsOnResume()
        {
            foreach (var originalInput in InputsToSendOnResume.Values)
            {
                var input = new GameInput(InputManager.CurrentInputTime, originalInput.Action, originalInput.Integer);
                OnGameInput(ref input);
            }

            InputsToSendOnResume.Clear();
        }

        protected void OnGameInput(ref GameInput input)
        {
            // Ignore completely if the song hasn't started yet
            if (!GameManager.Started)
                return;

            // Ignore while paused
            if (GameManager.Paused)
            {
                if (!ShouldUpdateInputsOnResume)
                {
                    return;
                }

                if (LastInputs.TryGetValue(input.Action, out var lastInput))
                {
                    if (lastInput.Button != input.Button)
                    {
                        InputsToSendOnResume[input.Action] = input;
                    }
                    else
                    {
                        InputsToSendOnResume.Remove(input.Action);
                    }
                }

                return;
            }

            LastInputs[input.Action] = input;

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
                GlobalAudioHandler.PlaySoundEffect(SfxSample.StarPowerAward);
            }
        }

        protected virtual void OnStarPowerStatus(bool active)
        {
            if (!GameManager.Paused)
            {
                GlobalAudioHandler.PlaySoundEffect(active
                    ? SfxSample.StarPowerDeploy
                    : SfxSample.StarPowerRelease);

                SetStarPowerFX(active);
            }

            GameManager.ChangeStarPowerStatus(active);

            foreach (var haptics in SantrollerHaptics)
            {
                haptics.SetStarPowerActive(active);
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
    }
}