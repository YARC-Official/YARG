﻿using System.Collections.Generic;
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

        public abstract BaseEngine BaseEngine { get; }

        /// <summary>
        /// The player's input calibration, in seconds.
        /// </summary>
        /// <remarks>
        /// Be aware that this value is negated!
        /// Positive calibration settings will result in a negative number here.
        /// </remarks>
        public double InputCalibration => -Player.Profile.InputCalibrationSeconds;

        public abstract BaseStats Stats { get; }

        public abstract float[] StarMultiplierThresholds { get; }

        public abstract int[] StarScoreThresholds { get; protected set; }

        public int Score { get; protected set; }
        public int Combo { get; protected set; }

        public int NotesHit   { get; protected set; }
        public int TotalNotes { get; protected set; }

        protected bool IsFc;

        protected bool IsInitialized { get; private set; }

        private List<GameInput> _replayInputs;
        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        private int _replayInputIndex;

        protected override void GameplayAwake()
        {
            _replayInputs = new List<GameInput>();

            InputViewer = FindObjectOfType<BaseInputViewer>();

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

        protected abstract void UpdateVisuals(double time);

        public abstract void SetPracticeSection(uint start, uint end);

        public virtual void SetReplayTime(double time)
        {
            ResetVisuals();

            IsFc = true;

            _replayInputIndex = BaseEngine.ProcessUpToTime(time, ReplayInputs);
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
            inputTime += InputCalibration;

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
                    if (inputTime < input.Time)
                    {
                        break;
                    }
                    
                    BaseEngine.QueueInput(input);
                    InputViewer.OnInput(input);
                    
                    _replayInputIndex++;
                }
            }

            if (BaseEngine.IsInputQueued)
            {
                BaseEngine.UpdateEngine();
            }
            else
            {
                BaseEngine.UpdateEngine(time);
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
        }

        protected abstract bool InterceptInput(ref GameInput input);

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