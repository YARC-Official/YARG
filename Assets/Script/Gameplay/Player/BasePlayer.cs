using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public abstract class BasePlayer : GameplayBehaviour
    {
        protected SyncTrack SyncTrack { get; private set; }

        private List<GameInput> _replayInputs;

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

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

        public abstract BaseStats Stats { get; }

        public abstract float[] StarMultiplierThresholds { get; }

        public abstract int[] StarScoreThresholds { get; protected set; }

        public int Score { get; protected set; }
        public int Combo { get; protected set; }

        public int NotesHit   { get; protected set; }
        public int TotalNotes { get; protected set; }

        protected bool IsFc;

        protected bool IsInitialized { get; private set; }

        protected override void GameplayAwake()
        {
            _replayInputs = new List<GameInput>();

            IsFc = true;
        }

        protected void Start()
        {
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

        protected abstract void UpdateInputs(double time);

        protected abstract void UpdateVisuals(double time);

        public abstract void SetPracticeSection(uint start, uint end);

        public abstract void SetReplayTime(double time);

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

        protected void AddReplayInput(GameInput input)
        {
            _replayInputs.Add(input);
        }

        protected abstract void SubscribeToInputEvents();
        protected abstract void UnsubscribeFromInputEvents();
    }
}