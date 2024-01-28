using System;
using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Playback
{
    public class BeatEventHandler
    {
        private class BeatAction
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

            /// <summary>
            /// The action to call at the set beat rate.
            /// </summary>
            public readonly Action Action;

            private uint _lastTick;

            private int _tempoIndex;
            private int _timeSigIndex;

            public BeatAction(float beatRate, double offset, Action action)
            {
                BeatRate = beatRate;
                Offset = offset;
                Action = action;
            }

            public void Reset()
            {
                _lastTick = 0;
                _tempoIndex = 0;
                _timeSigIndex = 0;
            }

            public void Update(double songTime, SyncTrack sync)
            {
                // Apply offset up-front
                songTime -= Offset;

                var tempos = sync.Tempos;
                var timeSigs = sync.TimeSignatures;

                // Determine end tick so we know when to stop updating
                while (_tempoIndex + 1 < tempos.Count && tempos[_tempoIndex + 1].Time < songTime)
                    _tempoIndex++;

                uint endTick = sync.TimeToTick(songTime, tempos[_tempoIndex]);

                // We need to process tempo map info individually for each event, or else
                // it's possible for a later tempo/time signature to be used too early
                bool actionDone = false;
                while (true)
                {
                    var currentTimeSig = timeSigs[_timeSigIndex];

                    uint ticksPerBeat = currentTimeSig.GetTicksPerBeat(sync);
                    uint ticksPerEvent = (uint) (ticksPerBeat * BeatRate);
                    uint currentTick = _lastTick + ticksPerEvent;

                    if (currentTick > endTick)
                        break;
                    _lastTick = currentTick;

                    if (!actionDone)
                    {
                        Action();
                        actionDone = true;
                    }

                    while (_timeSigIndex + 1 < _timeSigIndex && timeSigs[_timeSigIndex + 1].Tick < currentTick)
                        _timeSigIndex++;
                }
            }
        }

        private readonly SyncTrack _sync;

        // private int _tempoIndex;
        // private int _timeSigIndex;

        private readonly Dictionary<Action, BeatAction> _states = new();
        private readonly List<Action> _removeStates = new();
        private readonly List<(Action, BeatAction)> _addStates = new();

        public BeatEventHandler(SyncTrack sync)
        {
            _sync = sync;
        }

        /// <summary>
        /// Subscribes to a beat event.
        /// </summary>
        /// <param name="action">The action to be called when the beat occurs.</param>
        /// <param name="beatRate">See <see cref="BeatAction.BeatRate"/>.</param>
        /// <param name="offset">See <see cref="BeatAction.Offset"/>.</param>
        public void Subscribe(Action action, float beatRate, double offset = 0)
        {
            _addStates.Add((action, new BeatAction(beatRate, offset, action)));
        }

        public void Unsubscribe(Action action)
        {
            _removeStates.Add(action);
        }

        public void ResetTimers()
        {
            foreach (var state in _states.Values)
            {
                state.Reset();
            }

            // _tempoIndex = 0;
            // _timeSigIndex = 0;
        }

        public void Update(double songTime)
        {
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

            // Skip until in the chart
            if (songTime < 0) return;

#if false // Not necessary currently, but might be in the future
            // Update the current sync track info
            var tempos = _sync.Tempos;
            var timeSigs = _sync.TimeSignatures;

            while (_tempoIndex + 1 < tempos.Count && tempos[_tempoIndex + 1].Time < songTime)
                _tempoIndex++;
            while (_timeSigIndex + 1 < timeSigs.Count && timeSigs[_timeSigIndex + 1].Time < songTime)
                _timeSigIndex++;

            var finalTempo = tempos[_tempoIndex];
            var finalTimeSig = timeSigs[_timeSigIndex];
#endif

            // Update actions
            foreach (var state in _states.Values)
            {
                state.Update(songTime, _sync);
            }
        }
    }
}
