using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    // Does not use GameplayBehaviour, as players are handled specially in GameManager
    public abstract class BasePlayer : MonoBehaviour
    {
        protected GameManager GameManager { get; private set; }

        protected SyncTrack SyncTrack { get; private set; }

        public YargPlayer Player { get; private set; }

        public float NoteSpeed
        {
            get
            {
                if (GameManager.IsPractice && GameManager.SelectedSongSpeed < 1)
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

        protected BaseInputViewer InputViewer { get; private set; }

        public abstract BaseEngine BaseEngine { get; }

        public abstract BaseStats Stats { get; }

        public abstract float[] StarMultiplierThresholds { get; }

        public abstract int[] StarScoreThresholds { get; protected set; }

        public HitWindowSettings HitWindow { get; protected set; }

        public int Score => Stats.Score;
        public int Combo => Stats.Score;

        public int NotesHit   { get; protected set; }
        public int TotalNotes { get; protected set; }

        public bool IsFc { get; protected set; } = true;

        protected bool IsInitialized { get; private set; }

        private List<GameInput> _replayInputs = new();
        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs;

        private int _replayInputIndex;

        private void Awake()
        {
            InputViewer = FindObjectOfType<BaseInputViewer>();
            GameManager = FindObjectOfType<GameManager>();
            if (GameManager == null)
            {
                Debug.LogWarning($"Player object {gameObject.name} was instantiated outside of the gameplay scene!");
                Destroy(gameObject);
                return;
            }

            // Ensure proper initialization if the song already started
            // (dunno why that would happen with the players, but just in case)
            if (GameManager.IsSongStarted)
            {
                PlayerStart();
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

        protected virtual void OnDestroy()
        {
            if (!GameManager.IsReplay)
            {
                UnsubscribeFromInputEvents();
            }
        }

        public void PlayerStart()
        {
            if (!GameManager.IsReplay)
            {
                SubscribeToInputEvents();
            }
        }

        public void PlayerUpdate()
        {
            UpdateInputs(GameManager.InputTime);
            UpdateVisuals(GameManager.VisualTime);
        }

        protected abstract void UpdateVisuals(double time);
        protected abstract void ResetVisuals();

        public abstract void SetPracticeSection(uint start, uint end);
        public abstract void ResetPracticeSection();

        public virtual void SetReplayTime(double time)
        {
            ResetVisuals();

            IsFc = true;

            _replayInputIndex = BaseEngine.ProcessUpToTime(time, _replayInputs);
            UpdateVisuals(time);
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
                while (_replayInputIndex < _replayInputs.Count)
                {
                    var input = _replayInputs[_replayInputIndex];

                    // Current input does not meet the time requirement
                    if (time < input.Time)
                    {
                        break;
                    }

                    BaseEngine.QueueInput(input);

                    if (InputViewer != null)
                    {
                        InputViewer.OnInput(input);
                    }

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
        }

        private void UnsubscribeFromInputEvents()
        {
            Player.Bindings.UnsubscribeFromGameplayInputs(Player.Profile.GameMode, OnGameInput);
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

            BaseEngine.QueueInput(input);
            _replayInputs.Add(input);

            if (InputViewer != null)
            {
                InputViewer.OnInput(input);
            }
        }

        protected abstract bool InterceptInput(ref GameInput input);

        protected virtual void OnStarPowerPhraseHit()
        {
            if (!GameManager.Paused)
            {
                GlobalVariables.AudioManager.PlaySoundEffect(SfxSample.StarPowerAward);
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