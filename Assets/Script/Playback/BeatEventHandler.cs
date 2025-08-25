using System;
using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Playback
{
    public enum BeatEventType
    {
        /// <summary>
        /// Fire events relative to all beatlines (measure, strong, weak).
        /// </summary>
        WeakBeat,

        /// <summary>
        /// Fire events relative to strong beatlines (including measure beatlines).
        /// </summary>
        StrongBeat,

        /// <summary>
        /// Fire events relative to beats calculated from the time signature only.
        /// </summary>
        /// <remarks>
        /// Note that this has no direct correspondence to beatlines, as they can be authored manually.
        /// </remarks>
        DenominatorBeat,

        /// <summary>
        /// Fire events relative to quarter notes (absolute).
        /// </summary>
        /// <remarks>
        /// 1 = 1/4th note, 2 = 1/2nd note, 0.5 = 1/8th note, etc.
        /// Time signature does not affect this rate.
        /// <br/>
        /// This event type is not intended to be used at its default rate;
        /// it's moreso useful for firing at specific smaller steps like 1/16th or 1/32nd.
        /// </remarks>
        QuarterNote,

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

    public class BeatEvent
    {
        /// <summary>
        /// The event type to track the progress of.
        /// </summary>
        private readonly BeatEventType _mode;

        /// <summary>
        /// The number of times this event should occur per unit of progress.
        /// </summary>
        private readonly float _rate;

        /// <summary>
        /// The offset of the event in seconds.
        /// </summary>
        /// <remarks>
        /// 0.05 = 50 ms late, -0.025 = 25 ms early.
        /// </remarks>
        private readonly double _offset;

        public event Action Action;

        public double CurrentProgress { get; private set; }
        public double CurrentPercentage => CurrentProgress % 1;
        public int CurrentCount => (int) CurrentProgress;

        // This field being initialized to -1 ensures that the event fires at the start of the song
        private int _lastCount = -1;

        public BeatEvent(BeatEventType mode, float division, double offset)
        {
            _mode = mode;
            _rate = 1 / division;
            _offset = offset;
        }

        public void Update(double time, SyncTrack sync)
        {
            // Apply offset up-front
            time -= _offset;
            if (time < 0)
            {
                return;
            }

            // Determine progress
            uint quarterTick = sync.TimeToTick(time);
            double progress = _mode switch
            {
                BeatEventType.WeakBeat => sync.GetWeakBeatPosition(quarterTick),
                BeatEventType.StrongBeat => sync.GetStrongBeatPosition(quarterTick),
                BeatEventType.DenominatorBeat => sync.GetDenominatorBeatPosition(quarterTick),
                BeatEventType.QuarterNote => sync.GetQuarterNotePosition(quarterTick),
                BeatEventType.Measure => sync.GetMeasurePosition(quarterTick),
                _ => throw new NotImplementedException($"Unhandled beat event mode {_mode}!")
            };

            CurrentProgress = progress * _rate;
            if (CurrentCount != _lastCount)
            {
                Action?.Invoke();
                _lastCount = CurrentCount;
            }
        }

        public void Reset()
        {
            _lastCount = -1;
        }
    }

    public class BeatEventController
    {
        private struct BeatEventKey
        {
            public BeatEventType mode;
            public float division;
            public double offset;

            public readonly override int GetHashCode()
            {
                return HashCode.Combine(mode, division, offset);
            }
        }

        public BeatEvent WeakBeat { get; }
        public BeatEvent StrongBeat { get; }
        public BeatEvent DenominatorBeat { get; }
        public BeatEvent QuarterNote { get; }
        public BeatEvent Measure { get; }

        private readonly Dictionary<BeatEventKey, BeatEvent> _events = new();
        private readonly Dictionary<Action, BeatEvent> _eventsByAction = new();

        // Necessary to allow adding or removing subscriptions within event handlers
        private readonly List<(BeatEventKey key, Action action)> _addEvents = new();
        private readonly List<Action> _removeEvents = new();

        public BeatEventController()
        {
            BeatEvent MakeBeatEvent(BeatEventType mode)
            {
                var key = new BeatEventKey()
                {
                    mode = mode,
                    division = 1,
                    offset = 0,
                };

                var ev = new BeatEvent(key.mode, key.division, key.offset);
                _events.Add(key, ev);
                return ev;
            }

            WeakBeat = MakeBeatEvent(BeatEventType.WeakBeat);
            StrongBeat = MakeBeatEvent(BeatEventType.StrongBeat);
            DenominatorBeat = MakeBeatEvent(BeatEventType.DenominatorBeat);
            QuarterNote = MakeBeatEvent(BeatEventType.QuarterNote);
            Measure = MakeBeatEvent(BeatEventType.Measure);
        }

        /// <summary>
        /// Subscribes to a beat event using the tempo map as the driver.
        /// </summary>
        /// <param name="action">The action to be called when the beat occurs.</param>
        /// <param name="mode">The tempo map event type to use for the event.</param>
        /// <param name="division">The division at which events should be fired, relative to the units of <paramref name="mode"/>.</param>
        /// <param name="offset">The constant offset to use for the event.</param>
        public void Subscribe(Action action, BeatEventType mode, float division = 1, double offset = 0)
        {
            var key = new BeatEventKey()
            {
                mode = mode,
                division = division,
                offset = offset,
            };

            _addEvents.Add((key, action));
        }

        public void Unsubscribe(Action action)
        {
            _removeEvents.Add(action);
        }

        public BeatEvent GetEventForAction(Action action)
        {
            return _eventsByAction[action];
        }

        public void Update(double time, SyncTrack sync)
        {
            foreach (var (key, action) in _addEvents)
            {
                if (!_events.TryGetValue(key, out var ev))
                {
                    ev = new(key.mode, key.division, key.offset);
                    _events.Add(key, ev);
                }

                ev.Action += action;
                _eventsByAction[action] = ev;
            }

            foreach (var action in _removeEvents)
            {
                if (_eventsByAction.Remove(action, out var ev))
                {
                    ev.Action -= action;
                }
            }

            _addEvents.Clear();
            _removeEvents.Clear();

            // Skip until in the chart
            if (time < 0)
            {
                return;
            }

            // Update actions
            foreach (var state in _events.Values)
            {
                state.Update(time, sync);
            }
        }

        public void Reset()
        {
            foreach (var state in _events.Values)
            {
                state.Reset();
            }
        }
    }

    public class BeatEventHandler
    {
        private readonly SyncTrack _sync;

        public BeatEventController Audio { get; } = new();
        public BeatEventController Visual { get; } = new();

        public BeatEventHandler(SyncTrack sync)
        {
            _sync = sync;
        }

        public void Update(double songTime, double visualTime)
        {
            Audio.Update(songTime, _sync);
            Visual.Update(visualTime, _sync);
        }

        public void Reset()
        {
            Audio.Reset();
            Visual.Reset();
        }
    }
}
