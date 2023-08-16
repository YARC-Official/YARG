using System;
using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay
{
    public class BeatEventManager : MonoBehaviour
    {
        public readonly struct Info
        {
            /// <summary>
            /// Quarter notes would be <c>1f / 4f</c>, whole notes <c>1f</c>, etc.
            /// </summary>
            public readonly float Note;

            /// <summary>
            /// The offset of the event in seconds.
            /// </summary>
            public readonly float Offset;

            public Info(float note, float offset)
            {
                Note = note;
                Offset = offset;
            }
        }

        private class State
        {
            public readonly Info Info;

            public double LastTime;

            public int CurrentTimeSigIndex;
            public int NextTimeSigIndex = 1;

            public int CurrentTempoIndex;
            public int NextTempoIndex = 1;

            public State(Info info)
            {
                Info = info;
            }
        }

        private GameManager _gameManager;

        private readonly Dictionary<Action, State> _states = new();

        private void Awake()
        {
            _gameManager = GetComponent<GameManager>();
        }

        public void Subscribe(Action action, Info info)
        {
            _states.Add(action, new State(info));
        }

        public void Unsubscribe(Action action)
        {
            _states.Remove(action);
        }

        public void ResetTimers()
        {
            foreach (var (_, state) in _states)
            {
                state.LastTime = 0;

                state.CurrentTimeSigIndex = 0;
                state.NextTimeSigIndex = 1;

                state.CurrentTempoIndex = 0;
                state.NextTempoIndex = 1;
            }
        }

        private void Update()
        {
            // Skip while loading
            if (_gameManager.Chart is null) return;

            var sync = _gameManager.Chart.SyncTrack;

            foreach (var (action, state) in _states)
            {
                // TODO: I'm not really sure how to properly deal with offsets.
                var realTime = _gameManager.InputTime + state.Info.Offset;

                // Update the time signature indices
                var timeSigs = sync.TimeSignatures;
                while (state.NextTimeSigIndex < timeSigs.Count && timeSigs[state.NextTimeSigIndex].Time < realTime)
                {
                    state.CurrentTimeSigIndex++;
                    state.NextTimeSigIndex++;
                }

                // Get the time signature (important for beat length)
                var currentTimeSig = timeSigs[state.CurrentTimeSigIndex];

                // Update the tempo indices
                var tempos = sync.Tempos;
                while (state.NextTempoIndex < tempos.Count && tempos[state.NextTempoIndex].Time < realTime)
                {
                    state.CurrentTempoIndex++;
                    state.NextTempoIndex++;
                }

                // Get the seconds per measure
                var currentTempo = tempos[state.CurrentTempoIndex];
                float secondsPerWhole = currentTempo.SecondsPerBeat * (4f / currentTimeSig.Denominator) * 4f;

                // Get seconds per specified note
                float secondsPerNote = secondsPerWhole * state.Info.Note;

                // Call action
                if (state.LastTime + secondsPerNote <= realTime)
                {
                    action();
                    state.LastTime = _gameManager.SongTime;
                }
            }
        }
    }
}