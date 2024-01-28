using System;
using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Playback
{
    public enum BeatEventMode
    {
        /// <summary>
        /// Fire events relative to beats (relative to time signature).
        /// </summary>
        /// <remarks>
        /// 1 = every beat, 2 = every 2 beats, 0.5 = every beat and every half-beat, etc.
        /// A time signature of x/4 will make events occur relative to 1/4th steps,
        /// x/8 will make events occur relative to 1/8th steps, etc.
        /// </remarks>
        Beat,

        /// <summary>
        /// Fire events relative to quarter notes (absolute).
        /// </summary>
        /// <remarks>
        /// 1 = 1/4th note, 2 = 1/2nd note, 0.5 = 1/8th note, etc.
        /// Time signature does not affect this rate.
        /// </remarks>
        Quarter,

        /// <summary>
        /// Fire events relative to measures.
        /// </summary>
        /// <remarks>
        /// 1 = 1 measure, 2 = 2 measures, 0.5 = half-measure, etc.
        /// With a rate of 1, a time signature of 4/4 will make events occur every 4 quarter notes,
        /// 6/8 will make events occur every 6 eighth notes, etc.
        /// </remarks>
        Measure,
    }

    public class BeatEventHandler
    {
        private class BeatAction
        {
            /// <summary>
            /// The number of beats for the rate at which this event should occur at.
            /// </summary>
            private readonly float _beatRate;

            /// <summary>
            /// The offset of the event in seconds.
            /// </summary>
            /// <remarks>
            /// 0.05 = 50 ms late, -0.025 = 25 ms early.
            /// </remarks>
            private readonly double _offset;

            /// <summary>
            /// The action to call at the set beat rate.
            /// </summary>
            private readonly Action _action;

            /// <summary>
            /// The action to call at the set beat rate.
            /// </summary>
            private readonly BeatEventMode _mode;

            private int _eventsHandled = -1;

            private int _tempoIndex;
            private int _timeSigIndex;

            public BeatAction(float beatRate, double offset, Action action, BeatEventMode mode)
            {
                _beatRate = beatRate;
                _offset = offset;
                _action = action;
                _mode = mode;
            }

            public void Reset()
            {
                _eventsHandled = -1;
                _tempoIndex = 0;
                _timeSigIndex = 0;
            }

            public void Update(double songTime, SyncTrack sync)
            {
                // Apply offset up-front
                songTime -= _offset;
                if (songTime < 0)
                    return;

                var tempos = sync.Tempos;
                var timeSigs = sync.TimeSignatures;

                // Progress tempo map
                while (_tempoIndex + 1 < tempos.Count && tempos[_tempoIndex + 1].Time < songTime)
                    _tempoIndex++;

                while (_timeSigIndex + 1 < timeSigs.Count && timeSigs[_timeSigIndex + 1].Time < songTime)
                {
                    _timeSigIndex++;
                    // Reset beats for each new time signature
                    _eventsHandled = -1;
                }

                // Progress beat count
                var timeSig = timeSigs[_timeSigIndex];
                var tempo = tempos[_tempoIndex];
                double progress = _mode switch
                {
                    BeatEventMode.Beat => timeSig.GetBeatProgress(songTime, sync, tempo),
                    BeatEventMode.Quarter => timeSig.GetQuarterNoteProgress(songTime, sync, tempo),
                    BeatEventMode.Measure => timeSig.GetMeasureProgress(songTime, sync, tempo),
                    _ => throw new NotImplementedException($"Unhandled beat event mode {_mode}!")
                };

                int eventCount = (int) (progress / _beatRate);
                if (eventCount > _eventsHandled)
                {
                    _eventsHandled = eventCount;
                    _action();
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
        /// <param name="beatRate">See <see cref="BeatAction._beatRate"/>.</param>
        /// <param name="offset">See <see cref="BeatAction._offset"/>.</param>
        public void Subscribe(Action action, float beatRate, double offset = 0, BeatEventMode mode = BeatEventMode.Beat)
        {
            _addStates.Add((action, new BeatAction(beatRate, offset, action, mode)));
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
