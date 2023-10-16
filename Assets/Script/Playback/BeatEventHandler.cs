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
            /// The number of beats for the rate at which this event should occur at.
            /// This value is relative to the current time signature's denominator.
            /// </summary>
            /// <remarks>
            /// 1 = every beat, 2 = every 2 beats, 0.5 = every beat and every half-beat, etc.
            /// A time signature of x/4 will make events occur relative to 1/4th steps,
            /// x/8 will make events occur relative to 1/8th steps, etc.
            /// </remarks>
            public readonly float BeatRate;

            /// <summary>
            /// The offset of the event in seconds.
            /// This is a constant offset for every firing of the event,
            /// where positive values delay the event, and negative values make it early.
            /// </summary>
            /// <remarks>
            /// 0.05 = 50 ms late, -0.025 = 25 ms early.
            /// </remarks>
            public readonly double Offset;

            public Info(float rate, double offset = 0)
            {
                BeatRate = rate;
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
                // Get the ticks per denominator beat so we can use it to determine when to call the action.
                uint ticksPerBeat = (uint) (_sync.Resolution * (4.0 / currentTimeSig.Denominator));
                uint ticksPerEvent = (uint) (ticksPerBeat * state.Info.BeatRate);

                // Call action
                bool actionDone = false;
                while (_sync.TickToTime(state.LastTick + ticksPerEvent) <= songTime + state.Info.Offset)
                {
                    state.LastTick += ticksPerEvent;
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
