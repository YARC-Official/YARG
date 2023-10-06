using System;
using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public class BeatEventManager : GameplayBehaviour
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

            public uint LastTick;

            public State(Info info)
            {
                Info = info;
            }
        }

        private SyncTrack _sync;

        private int _currentTimeSigIndex;
        private int _nextTimeSigIndex = 1;

        private readonly Dictionary<Action, State> _states = new();

        public void Subscribe(Action action, Info info)
        {
            _states.Add(new State(info), state);
        }

        public void Unsubscribe(Action action)
        {
            _states.Remove(action);
        }

        public void ResetTimers()
        {
            foreach (var (_, state) in _states)
            {
                state.LastTick = 0;
            }

            _currentTimeSigIndex = 0;
            _nextTimeSigIndex = 1;
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _sync = GameManager.Chart.SyncTrack;
        }

        private void Update()
        {
            // Skip until in the chart
            if (GameManager.SongTime < 0) return;

            // Update the time signature indices
            var timeSigs = _sync.TimeSignatures;
            while (_nextTimeSigIndex < timeSigs.Count && timeSigs[_nextTimeSigIndex].Time < GameManager.InputTime)
            {
                _currentTimeSigIndex++;
                _nextTimeSigIndex++;
            }

            // Get the time signature
            var currentTimeSig = timeSigs[_currentTimeSigIndex];

            // Update per action now
            foreach (var (action, state) in _states)
            {
                // Get the ticks per whole note so we can use it to get the ticks per note.
                // DO NOT use measures here, as a "quarter note" represents a quarter of
                // a whole and NOT a quarter of a measure.
                uint ticksPerWholeNote = (uint) (_sync.Resolution * ((double) 4 / currentTimeSig.Denominator) * 4);
                uint ticksPerNote = (uint) (ticksPerWholeNote * state.Info.Note);

                // Call action
                bool actionDone = false;
                while (_sync.TickToTime(state.LastTick + ticksPerNote) <= GameManager.SongTime + state.Info.Offset)
                {
                    state.LastTick += ticksPerNote;
                    if (actionDone) continue;
                    action();
                    actionDone = true;
                }
            }
        }
    }
}
