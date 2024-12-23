// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Extensions;

namespace MoonscraperChartEditor.Song
{
    internal class MoonSong
    {
        public uint resolution => syncTrack.Resolution;
        public uint hopoThreshold;

        // Charts
        private readonly MoonChart[] charts;

        public IReadOnlyList<MoonChart> Charts => charts;

        /// <summary>
        /// Read only list of song events.
        /// </summary>
        public List<MoonText> events { get; private set; } = new();
        /// <summary>
        /// Read only list of song sections.
        /// </summary>
        public List<MoonText> sections { get; private set; } = new();
        /// <summary>
        /// Read only list of venue events.
        /// </summary>
        public List<MoonVenue> venue { get; private set; } = new();

        public SyncTrack syncTrack { get; private set; }

        /// <summary>
        /// Default constructor for a new chart. Initialises all lists and adds locked bpm and timesignature objects.
        /// </summary>
        public MoonSong(uint resolution)
        {
            syncTrack = new(resolution);
            syncTrack.Tempos.Add(new TempoChange(120, 0, 0));
            syncTrack.TimeSignatures.Add(new TimeSignatureChange(4, 4, 0, 0));

            // Chart initialisation
            charts = new MoonChart[EnumExtensions<MoonInstrument>.Count * EnumExtensions<Difficulty>.Count];
            for (int i = 0; i < charts.Length; ++i)
            {
                var instrument = (MoonInstrument)(i / EnumExtensions<Difficulty>.Count);
                charts[i] = new MoonChart(this, instrument);
            }
        }

        public MoonChart GetChart(MoonInstrument instrument, Difficulty difficulty)
        {
            return charts[(int)instrument * EnumExtensions<Difficulty>.Count + (int)difficulty];
        }

        public bool ChartExistsForInstrument(MoonInstrument instrument)
        {
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                var chart = GetChart(instrument, difficulty);
                if (!chart.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }

        public bool DoesChartExist(MoonInstrument instrument, Difficulty difficulty)
        {
            return !GetChart(instrument, difficulty).IsEmpty;
        }

        public double TickToTime(uint tick)
        {
            return syncTrack.TickToTime(tick);
        }

        public double TickToTime(uint tick, TempoChange tempo)
        {
            return syncTrack.TickToTime(tick, tempo);
        }

        public uint TimeToTick(double time)
        {
            return syncTrack.TimeToTick(time);
        }

        public uint TimeToTick(double time, TempoChange tempo)
        {
            return syncTrack.TimeToTick(time, tempo);
        }

        public MoonText? GetPrevSection(uint position)
        {
            return MoonObjectHelper.GetPrevious(sections, position);
        }

        private void AddSyncEvent<TEvent>(List<TEvent> events, TEvent newEvent)
            where TEvent : SyncEvent
        {
            if (events.Count < 1)
            {
                events.Add(newEvent);
                return;
            }

            uint lastTick = events[^1].Tick;
            if (newEvent.Tick == lastTick)
            {
                // Replace
                events[^1] = newEvent;
            }
            else if (newEvent.Tick < lastTick)
            {
                throw new InvalidOperationException($"Out-of-order sync track event at tick {newEvent.Tick}!");
            }
            else
            {
                events.Add(newEvent);
            }
        }

        public void Add(Beatline beat)
        {
            AddSyncEvent(syncTrack.Beatlines, beat);
        }

        public void Add(TimeSignatureChange timeSig)
        {
            AddSyncEvent(syncTrack.TimeSignatures, timeSig);
        }

        public void Add(TempoChange bpm)
        {
            AddSyncEvent(syncTrack.Tempos, bpm);
        }

        public void Add(MoonText ev)
        {
            MoonObjectHelper.Insert(ev, events);
        }

        public void AddSection(MoonText section)
        {
            MoonObjectHelper.Insert(section, sections);
        }

        public void Add(MoonVenue venueEvent)
        {
            MoonObjectHelper.Insert(venueEvent, venue);
        }

        public bool Remove(MoonText ev)
        {
            return MoonObjectHelper.Remove(ev, events);
        }

        public bool RemoveSection(MoonText section)
        {
            return MoonObjectHelper.Remove(section, sections);
        }

        public bool Remove(MoonVenue venueEvent)
        {
            return MoonObjectHelper.Remove(venueEvent, venue);
        }

        public float ResolutionScaleRatio(uint targetResoltion)
        {
            return (float)targetResoltion / resolution;
        }

        public static MoonChart.GameMode InstrumentToChartGameMode(MoonInstrument instrument)
        {
            switch (instrument)
            {
                case MoonInstrument.Guitar:
                case MoonInstrument.GuitarCoop:
                case MoonInstrument.Bass:
                case MoonInstrument.Rhythm:
                case MoonInstrument.Keys:
                    return MoonChart.GameMode.Guitar;

                case MoonInstrument.Drums:
                    return MoonChart.GameMode.Drums;

                case MoonInstrument.GHLiveGuitar:
                case MoonInstrument.GHLiveBass:
                case MoonInstrument.GHLiveRhythm:
                case MoonInstrument.GHLiveCoop:
                    return MoonChart.GameMode.GHLGuitar;

                case MoonInstrument.ProGuitar_17Fret:
                case MoonInstrument.ProGuitar_22Fret:
                case MoonInstrument.ProBass_17Fret:
                case MoonInstrument.ProBass_22Fret:
                    return MoonChart.GameMode.ProGuitar;

                case MoonInstrument.ProKeys:
                    return MoonChart.GameMode.ProKeys;

                case MoonInstrument.Vocals:
                case MoonInstrument.Harmony1:
                case MoonInstrument.Harmony2:
                case MoonInstrument.Harmony3:
                    return MoonChart.GameMode.Vocals;

                default:
                    throw new NotImplementedException($"Unhandled instrument {instrument}!");
            }
        }

        public enum Difficulty
        {
            Expert = 0,
            Hard = 1,
            Medium = 2,
            Easy = 3
        }

        public enum MoonInstrument
        {
            Guitar,
            GuitarCoop,
            Bass,
            Rhythm,
            Keys,
            Drums,
            GHLiveGuitar,
            GHLiveBass,
            GHLiveRhythm,
            GHLiveCoop,
            ProGuitar_17Fret,
            ProGuitar_22Fret,
            ProBass_17Fret,
            ProBass_22Fret,
            ProKeys,
            Vocals,
            Harmony1,
            Harmony2,
            Harmony3,
        }

        public enum AudioInstrument
        {
            // Keep these in numerical order, there are a few places we're looping over these by casting to avoid GC allocs
            Song = 0,
            Guitar = 1,
            Bass = 2,
            Rhythm = 3,
            Drum = 4,
            Drums_2 = 5,
            Drums_3 = 6,
            Drums_4 = 7,
            Vocals = 8,
            Keys = 9,
            Crowd = 10
        }
    }

    internal class ReadOnlyList<T> : IList<T>, IEnumerable<T>
    {
        private readonly List<T> _realListHandle;
        public ReadOnlyList(List<T> realListHandle)
        {
            _realListHandle = realListHandle;
        }

        public T this[int index] { get { return _realListHandle[index]; } set { _realListHandle[index] = value; } }

        public int Count
        {
            get { return _realListHandle.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return _realListHandle.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {

        }

        public IEnumerator<T> GetEnumerator()
        {
            return _realListHandle.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _realListHandle.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _realListHandle.GetEnumerator();
        }

        public T[] ToArray()
        {
            return _realListHandle.ToArray();
        }
    }
}
