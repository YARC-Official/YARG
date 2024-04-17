using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Integration;

namespace YARG.Playback
{
    public enum TempoMapEventMode
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
        private interface IBeatAction
        {
            void Update(double songTime, SyncTrack sync);
            void Reset();
        }

        private class TempoMapAction : IBeatAction
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
            private readonly TempoMapEventMode _mode;

            private int _eventsHandled = -1;

            private int _tempoIndex;
            private int _timeSigIndex;

            public TempoMapAction(Action action, float beatRate, double offset, TempoMapEventMode mode)
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
                {
                    return;
                }

                var tempos = sync.Tempos;
                var timeSigs = sync.TimeSignatures;

                // Progress tempo map
                while (_tempoIndex + 1 < tempos.Count && tempos[_tempoIndex + 1].Time < songTime) _tempoIndex++;

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
                    TempoMapEventMode.Beat => timeSig.GetBeatProgress(songTime, sync, tempo),
                    TempoMapEventMode.Quarter => timeSig.GetQuarterNoteProgress(songTime, sync, tempo),
                    TempoMapEventMode.Measure => timeSig.GetMeasureProgress(songTime, sync, tempo),
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

        private class BeatlineAction : IBeatAction
        {
            private readonly double _offset;
            private readonly Action<Beatline> _action;

            private int _beatlineIndex;

            public BeatlineAction(Action<Beatline> action, double offset)
            {
                _action = action;
                _offset = offset;
            }

            public void Reset()
            {
                _beatlineIndex = 0;
            }

            public void Update(double songTime, SyncTrack sync)
            {
                // Apply offset up-front
                songTime -= _offset;
                if (songTime < 0)
                {
                    return;
                }

                var beatlines = sync.Beatlines;
                while (_beatlineIndex + 1 < beatlines.Count && beatlines[_beatlineIndex + 1].Time < songTime)
                {
                    // Deliberately call with each beatline
                    // Since there are multiple kinds of beatline, we don't want to
                    // end up skipping events that rely on specific types
                    _action(beatlines[++_beatlineIndex]);
                }
            }
        }

        private readonly SyncTrack _sync;

        // private int _tempoIndex;
        // private int _timeSigIndex;

        private readonly Dictionary<Delegate, IBeatAction> _states = new();

        // Necessary to allow adding or removing subscriptions within event handlers
        private readonly List<(bool, Delegate, IBeatAction)> _changeStates = new();

        public BeatEventHandler(SyncTrack sync)
        {
            _sync = sync;
        }

        /// <summary>
        /// Subscribes to a beat event using the tempo map as the driver.
        /// </summary>
        /// <param name="action">The action to be called when the beat occurs.</param>
        /// <param name="beatRate">The beat rate to use for the event (see <see cref="TempoMapEventMode"/>).</param>
        /// <param name="offset">The constant offset to use for the event.</param>
        /// <param name="mode">The tempo map mode to use for the event.</param>
        public void Subscribe(Action action, float beatRate, double offset = 0,
            TempoMapEventMode mode = TempoMapEventMode.Beat)
        {
            _changeStates.Add((true, action, new TempoMapAction(action, beatRate, offset, mode)));
        }

        /// <summary>
        /// Subscribes to a beat event using beatlines as the driver.
        /// </summary>
        /// <param name="action">The action to be called when the beat occurs.</param>
        /// <param name="offset">The constant offset to use for the event.</param>
        public void Subscribe(Action<Beatline> action, double offset = 0)
        {
            _changeStates.Add((true, action, new BeatlineAction(action, offset)));
        }

        public void Unsubscribe(Action action)
        {
            _changeStates.Add((false, action, null));
        }

        public void Unsubscribe(Action<Beatline> action)
        {
            _changeStates.Add((false, action, null));
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
            foreach (var (add, action, state) in _changeStates)
            {
                if (add)
                {
                    if (!_states.TryAdd(action, state))
                    {
                        YargLogger.LogWarning("A beat event handler with the same action has already been added!");
                    }
                }
                else
                {
                    _states.Remove(action);
                }
            }

            _changeStates.Clear();

            // Skip until in the chart
            if (songTime < 0)
            {
                return;
            }

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
