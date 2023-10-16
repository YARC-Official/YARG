using System;
using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Playback
{
    public class BeatEventHandler
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

        private int _currentTimeSigIndex = 0;
        private int _nextTimeSigIndex = 1;

        private readonly Dictionary<Action, State> _states = new();
        private readonly List<Action> _removeStates = new();
        private readonly List<(Action, State)> _addStates = new();

        public BeatEventHandler(SyncTrack sync)
        {
            _sync = sync;
        }

        public void Subscribe(Action action, Info info)
        {
            _addStates.Add((action, new State(info)));
        }

        public void Unsubscribe(Action action)
        {
            _removeStates.Add(action);
        }

        public void ResetTimers()
        {
            foreach (var state in _states.Values)
            {
                state.LastTick = 0;
            }

            _currentTimeSigIndex = 0;
            _nextTimeSigIndex = 1;
        }

        public void Update(double songTime)
        {
            // Skip until in the chart
            if (songTime < 0) return;

            // Update the time signature indices
            var timeSigs = _sync.TimeSignatures;
            while (_nextTimeSigIndex < timeSigs.Count && timeSigs[_nextTimeSigIndex].Time < songTime)
            {
                _currentTimeSigIndex++;
                _nextTimeSigIndex++;
            }

            // Get the time signature
            var currentTimeSig = timeSigs[_currentTimeSigIndex];

            // Add and remove states from the _state list outside the main loop to prevent enumeration errors.
            // (aka removing things from the list while it loops)
            foreach (var (action, state) in _addStates)
            {
                _states.Add(action, state);
            }
            _addStates.Clear();

            foreach (var action in _removeStates)
            {
                _states.Remove(action);
            }
            _removeStates.Clear();

            // Update per action now
            foreach (var (action, state) in _states)
            {
                // Get the ticks per whole note so we can use it to get the ticks per note.
                // DO NOT use measures here, as a "quarter note" represents a quarter of
                // a whole and NOT a quarter of a measure.
                uint ticksPerWholeNote = (uint) (_sync.Resolution * (4.0 / currentTimeSig.Denominator) * 4.0);
                uint ticksPerNote = (uint) (ticksPerWholeNote * state.Info.Note);

                // Call action
                bool actionDone = false;
                while (_sync.TickToTime(state.LastTick + ticksPerNote) <= songTime + state.Info.Offset)
                {
                    state.LastTick += ticksPerNote;
                    if (!actionDone)
                    {
                        action();
                        actionDone = true;
                    }
                }
            }
        }
    }
}
