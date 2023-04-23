// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using MoonscraperEngine;

namespace MoonscraperChartEditor.Song
{
    public class Song
    {
        // Song properties
        public Metadata metaData = new Metadata();
        //public INIParser iniProperties = new INIParser();

        public string name
        {
            get
            {
                return metaData.name;
            }
            set
            {
                metaData.name = value;
            }
        }
        public float resolution = SongConfig.STANDARD_BEAT_RESOLUTION;
        public float offset = 0;

        string[] audioLocations = new string[EnumX<AudioInstrument>.Count];

        public IO.ExportOptions defaultExportOptions
        {
            get
            {
                IO.ExportOptions exportOptions = default(IO.ExportOptions);

                exportOptions.forced = true;
                exportOptions.copyDownEmptyDifficulty = false;
                exportOptions.format = IO.ExportOptions.Format.Chart;
                exportOptions.targetResolution = this.resolution;
                exportOptions.tickOffset = 0;
                exportOptions.isGeneralSave = false;

                exportOptions.midiOptions = new IO.ExportOptions.MidiOptions()
                {
                    difficultyToUseGlobalTrackEvents = Difficulty.Expert,
                    rbFormat = IO.ExportOptions.MidiOptions.RBFormat.RB3,
                };

                return exportOptions;
            }
        }

        public float? manualLength = null;

        // Charts
        Chart[] charts;
        public List<Chart> unrecognisedCharts = new List<Chart>();

        public List<Event> _events;
        List<SyncTrack> _syncTrack;

        /// <summary>
        /// Read only list of song events.
        /// </summary>
        public SongObjectCache<Event> events { get; private set; }
        /// <summary>
        /// Read only list of song sections.
        /// </summary>
        public SongObjectCache<Section> sections { get; private set; }

        public ReadOnlyList<SyncTrack> syncTrack;
        public ReadOnlyList<Event> eventsAndSections;

        /// <summary>
        /// Read only list of a song's bpm changes.
        /// </summary>
        public SongObjectCache<BPM> bpms { get; private set; }
        /// <summary>
        /// Read only list of a song's time signature changes.
        /// </summary>
        public SongObjectCache<TimeSignature> timeSignatures { get; private set; }

        /// <summary>
        /// Default constructor for a new chart. Initialises all lists and adds locked bpm and timesignature objects.
        /// </summary>
        public Song()
        {
            _events = new List<Event>();
            _syncTrack = new List<SyncTrack>();

            eventsAndSections = new ReadOnlyList<Event>(_events);
            syncTrack = new ReadOnlyList<SyncTrack>(_syncTrack);

            events = new SongObjectCache<Event>();
            sections = new SongObjectCache<Section>();
            bpms = new SongObjectCache<BPM>();
            timeSignatures = new SongObjectCache<TimeSignature>();

            Add(new BPM());
            Add(new TimeSignature());

            // Chart initialisation
            int numberOfInstruments = EnumX<Instrument>.Count - 1;     // Don't count the "Unused" instrument
            charts = new Chart[numberOfInstruments * EnumX<Difficulty>.Count];

            for (int i = 0; i < charts.Length; ++i)
            {
                Instrument instrument = (Instrument)(i / EnumX<Difficulty>.Count);
                charts[i] = new Chart(this, instrument);
            }

            // Set the name of the chart
            foreach (Instrument instrument in EnumX<Instrument>.Values)
            {
                if (instrument == Instrument.Unrecognised)
                    continue;

                string instrumentName = string.Empty;
                switch (instrument)
                {
                    case (Instrument.Guitar):
                        instrumentName += "Guitar - ";
                        break;
                    case (Instrument.GuitarCoop):
                        instrumentName += "Guitar - Co-op - ";
                        break;
                    case (Instrument.Bass):
                        instrumentName += "Bass - ";
                        break;
                    case (Instrument.Rhythm):
                        instrumentName += "Rhythm - ";
                        break;
                    case (Instrument.Keys):
                        instrumentName += "Keys - ";
                        break;
                    case (Instrument.Drums):
                        instrumentName += "Drums - ";
                        break;
                    case (Instrument.GHLiveGuitar):
                        instrumentName += "GHLive Guitar - ";
                        break;
                    case (Instrument.GHLiveBass):
                        instrumentName += "GHLive Bass - ";
                        break;
                    case (Instrument.GHLiveRhythm):
                        instrumentName += "GHLive Rhythm - ";
                        break;
                    case (Instrument.GHLiveCoop):
                        instrumentName += "GHLive Co-op - ";
                        break;
                    default:
                        continue;
                }

                foreach (Difficulty difficulty in EnumX<Difficulty>.Values)
                {
                    GetChart(instrument, difficulty).name = instrumentName + difficulty.ToString();
                }
            }

            for (int i = 0; i < audioLocations.Length; ++i)
                audioLocations[i] = string.Empty;

            UpdateCache();
        }

        public Song(Song song) : this()
        {
            metaData = new Metadata(song.metaData);
            offset = song.offset;
            resolution = song.resolution;

            _events.Clear();
            _syncTrack.Clear();

            _events.AddRange(song._events);
            _syncTrack.AddRange(song._syncTrack);

            manualLength = song.manualLength;

            charts = new Chart[song.charts.Length];
            for (int i = 0; i < charts.Length; ++i)
            {
                charts[i] = new Chart(song.charts[i], this);
            }

            for (int i = 0; i < audioLocations.Length; ++i)
            {
                audioLocations[i] = song.audioLocations[i];
            }

            // iniProperties = new INIParser();
            // iniProperties.OpenFromString(string.Empty);
            // iniProperties.WriteValue(song.iniProperties);
        }

        ~Song()
        {
        }

        public Chart GetChart(Instrument instrument, Difficulty difficulty)
        {
            try
            {
                return charts[(int)instrument * EnumX<Difficulty>.Count + (int)difficulty];
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return charts[0];
            }
        }

        public bool ChartExistsForInstrument(Instrument instrument)
        {
            foreach (Difficulty difficulty in EnumX<Difficulty>.Values)
            {
                var chart = GetChart(instrument, difficulty);
                if (chart.chartObjects.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a time value into a tick position value. May be inaccurate due to interger rounding.
        /// </summary>
        /// <param name="time">The time (in seconds) to convert.</param>
        /// <param name="resolution">Ticks per beat, usually provided from the resolution song of a Song class.</param>
        /// <returns>Returns the calculated tick position.</returns>
        public uint TimeToTick(float time, float resolution)
        {
            if (time < 0)
                time = 0;

            uint position = 0;

            BPM prevBPM = bpms[0];

            // Search for the last bpm
            for (int i = 0; i < bpms.Count; ++i)
            {
                BPM bpmInfo = bpms[i];
                if (bpmInfo.assignedTime >= time)
                    break;
                else
                    prevBPM = bpmInfo;
            }

            position = prevBPM.tick;
            position += TickFunctions.TimeToDis(prevBPM.assignedTime, time, resolution, prevBPM.value / 1000.0f);

            return position;
        }



        /// <summary>
        /// Finds the value of the first bpm that appears before or on the specified tick position.
        /// </summary>
        /// <param name="position">The tick position</param>
        /// <returns>Returns the value of the bpm that was found.</returns>
        public BPM GetPrevBPM(uint position)
        {
            return SongObjectHelper.GetPrevious(bpms, position);
        }

        /// <summary>
        /// Finds the value of the first time signature that appears before the specified tick position.
        /// </summary>
        /// <param name="position">The tick position</param>
        /// <returns>Returns the value of the time signature that was found.</returns>
        public TimeSignature GetPrevTS(uint position)
        {
            return SongObjectHelper.GetPrevious(timeSignatures, position);
        }

        public Section GetPrevSection(uint position)
        {
            return SongObjectHelper.GetPrevious(sections, position);
        }

        /// <summary>
        /// Converts a tick position into the time it will appear in the song.
        /// </summary>
        /// <param name="position">Tick position.</param>
        /// <returns>Returns the time in seconds.</returns>
        public float TickToTime(uint position)
        {
            return TickToTime(position, this.resolution);
        }

        /// <summary>
        /// Converts a tick position into the time it will appear in the song.
        /// </summary>
        /// <param name="position">Tick position.</param>
        /// <param name="resolution">Ticks per beat, usually provided from the resolution song of a Song class.</param>
        /// <returns>Returns the time in seconds.</returns>
        public float TickToTime(uint position, float resolution)
        {
            int previousBPMPos = SongObjectHelper.FindClosestPosition(position, bpms);
            if (bpms[previousBPMPos].tick > position)
                --previousBPMPos;

            BPM prevBPM = bpms[previousBPMPos];
            float time = prevBPM.assignedTime;
            time += (float)TickFunctions.DisToTime(prevBPM.tick, position, resolution, prevBPM.value / 1000.0f);

            return time;
        }

        /// <summary>
        /// Adds a synctrack object (bpm or time signature) into the song.
        /// </summary>
        /// <param name="syncTrackObject">Item to add.</param>
        /// <param name="autoUpdate">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when adding multiple objects as it increases performance dramatically.</param>
        public void Add(SyncTrack syncTrackObject, bool autoUpdate = true)
        {
            syncTrackObject.song = this;
            SongObjectHelper.Insert(syncTrackObject, _syncTrack);

            if (autoUpdate)
                UpdateCache();
        }

        /// <summary>
        /// Removes a synctrack object (bpm or time signature) from the song.
        /// </summary>
        /// <param name="autoUpdate">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when removing multiple objects as it increases performance dramatically.</param>
        /// <returns>Returns whether the removal was successful or not (item may not have been found if false).</returns>
        public bool Remove(SyncTrack syncTrackObject, bool autoUpdate = true)
        {
            bool success = false;

            if (syncTrackObject.tick > 0)
            {
                success = SongObjectHelper.Remove(syncTrackObject, _syncTrack);
            }

            if (success)
            {
                syncTrackObject.song = null;
            }

            if (autoUpdate)
                UpdateCache();

            return success;
        }

        /// <summary>
        /// Adds an event object (section or event) into the song.
        /// </summary>
        /// <param name="syncTrackObject">Item to add.</param>
        /// <param name="autoUpdate">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when adding multiple objects as it increases performance dramatically.</param>
        public void Add(Event eventObject, bool autoUpdate = true)
        {
            eventObject.song = this;
            SongObjectHelper.Insert(eventObject, _events);

            if (autoUpdate)
                UpdateCache();
        }

        /// <summary>
        /// Removes an event object (section or event) from the song.
        /// </summary>
        /// <param name="autoUpdate">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when removing multiple objects as it increases performance dramatically.</param>
        /// <returns>Returns whether the removal was successful or not (item may not have been found if false).</returns>
        public bool Remove(Event eventObject, bool autoUpdate = true)
        {
            bool success = false;
            success = SongObjectHelper.Remove(eventObject, _events);

            if (success)
            {
                eventObject.song = null;
            }

            if (autoUpdate)
                UpdateCache();

            return success;
        }

        public static void UpdateCacheList<T, U>(SongObjectCache<T> cache, List<U> objectsToCache)
            where U : SongObject
            where T : U
        {
            var cacheObjectList = cache.EditCache();
            cacheObjectList.Clear();

            foreach (U objectToCache in objectsToCache)
            {
                if (objectToCache.GetType() == typeof(T))
                {
                    cacheObjectList.Add(objectToCache as T);
                }
            }
        }

        /// <summary>
        /// Updates all read-only values and bpm assigned time values. 
        /// </summary>
        public void UpdateCache()
        {
            UpdateCacheList(sections, _events);
            UpdateCacheList(events, _events);
            UpdateCacheList(bpms, _syncTrack);
            UpdateCacheList(timeSignatures, _syncTrack);

            UpdateBPMTimeValues();
        }

        public void UpdateAllChartCaches()
        {
            foreach (Chart chart in charts)
                chart.UpdateCache();
        }

        /// <summary>
        /// Dramatically speeds up calculations of songs with lots of bpm changes.
        /// </summary>
        void UpdateBPMTimeValues()
        {
            /*
             * Essentially just an optimised version of this, as this was n^2 and bad
             * foreach (BPM bpm in bpms)
             * {
             *     bpm.assignedTime = LiveTickToTime(bpm.tick, resolution);
             * }
            */

            double time = 0;
            BPM prevBPM = bpms[0];
            prevBPM.assignedTime = 0;

            foreach (BPM bpm in bpms)
            {
                time += TickFunctions.DisToTime(prevBPM.tick, bpm.tick, resolution, prevBPM.value / 1000.0f);
                bpm.assignedTime = (float)time;
                prevBPM = bpm;
            }
        }

        public float LiveTickToTime(uint position, float resolution)
        {
            return LiveTickToTime(position, resolution, bpms[0], _syncTrack);
        }

        public static float LiveTickToTime(uint position, float resolution, BPM initialBpm, IList<SyncTrack> synctrack)
        {
            double time = 0;
            BPM prevBPM = initialBpm;

            foreach (SyncTrack syncTrack in synctrack)
            {
                BPM bpmInfo = syncTrack as BPM;

                if (bpmInfo == null)
                    continue;

                if (bpmInfo.tick > position)
                {
                    break;
                }
                else
                {
                    time += TickFunctions.DisToTime(prevBPM.tick, bpmInfo.tick, resolution, prevBPM.value / 1000.0f);
                    prevBPM = bpmInfo;
                }
            }

            time += TickFunctions.DisToTime(prevBPM.tick, position, resolution, prevBPM.value / 1000.0f);

            return (float)time;
        }

        public float ResolutionScaleRatio(float targetResoltion)
        {
            return (targetResoltion / (float)resolution);
        }

        public string GetAudioName(AudioInstrument audio)
        {
            return Path.GetFileName(audioLocations[(int)audio]);
        }

        public string GetAudioLocation(AudioInstrument audio)
        {
            return audioLocations[(int)audio];
        }

        public void SetAudioLocation(AudioInstrument audio, string path)
        {
            if (File.Exists(path))
                audioLocations[(int)audio] = Path.GetFullPath(path);
            else if (string.IsNullOrEmpty(path))
                audioLocations[(int)audio] = string.Empty;
        }

        public static Chart.GameMode InstumentToChartGameMode(Instrument instrument)
        {
            switch (instrument)
            {
                case (Instrument.Guitar):
                case (Instrument.GuitarCoop):
                case (Instrument.Bass):
                case (Instrument.Rhythm):
                case (Instrument.Keys):
                    return Chart.GameMode.Guitar;

                case (Instrument.Drums):
                    return Chart.GameMode.Drums;

                case (Instrument.GHLiveGuitar):
                case (Instrument.GHLiveBass):
                case (Instrument.GHLiveRhythm):
                case (Instrument.GHLiveCoop):
                    return Chart.GameMode.GHLGuitar;

                default:
                    break;
            }

            return Chart.GameMode.Unrecognised;
        }

        public enum Difficulty
        {
            Expert = 0,
            Hard = 1,
            Medium = 2,
            Easy = 3
        }

        public enum Instrument
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
            Unrecognised = 99,
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

    public class SongObjectCache<T> : IList<T>, IEnumerable<T> where T : SongObject
    {
        List<T> cache = new List<T>();

        public T this[int index] { get { return cache[index]; } set { cache[index] = value; } }

        public int Count
        {
            get { return cache.Count; }
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
            return cache.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
	        
        }

        public IEnumerator<T> GetEnumerator()
        {
            return cache.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return cache.IndexOf(item);
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
            return cache.GetEnumerator();
        }

        public List<T> EditCache()
        {
            return cache;
        }

        public T[] ToArray()
        {
            return cache.ToArray();
        }
    }

    public class ReadOnlyList<T> : IList<T>, IEnumerable<T>
    {
        List<T> _realListHandle;
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
