﻿// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

//#define TIMING_DEBUG

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace MoonscraperChartEditor.Song
{
    public class Chart
    {
        Song _song;
        List<ChartObject> _chartObjects;
        int _note_count;
        GameMode _gameMode;
        public string name = string.Empty;

        /// <summary>
        /// Read only list of notes.
        /// </summary>
        public SongObjectCache<Note> notes { get; private set; }
        /// <summary>
        /// Read only list of starpower.
        /// </summary>
        public SongObjectCache<Starpower> starPower { get; private set; }
        /// <summary>
        /// Read only list of drum rolls.
        /// </summary>
        public SongObjectCache<DrumRoll> drumRoll { get; private set; }
        /// <summary>
        /// Read only list of local events.
        /// </summary>
        public SongObjectCache<ChartEvent> events { get; private set; }
        /// <summary>
        /// The song this chart is connected to.
        /// </summary>
        public Song song { get { return _song; } }
        /// <summary>
        /// The game mode the chart is designed for
        /// </summary>
        public GameMode gameMode { get { return _gameMode; } }

        /// <summary>
        /// Read only list containing all chart notes, starpower, drumRoll and events.
        /// </summary>
        public ReadOnlyList<ChartObject> chartObjects;

        /// <summary>
        /// The total amount of notes in the chart, counting chord (notes sharing the same tick position) as a single note.
        /// </summary>
        public int note_count { get { return _note_count; } }

        /// <summary>
        /// Creates a new chart object.
        /// </summary>
        /// <param name="song">The song to associate this chart with.</param>
        /// <param name="name">The name of the chart (easy single, expert double guitar, etc.</param>
        public Chart(Song song, GameMode gameMode, string name = "")
        {
            _song = song;
            _chartObjects = new List<ChartObject>();
            chartObjects = new ReadOnlyList<ChartObject>(_chartObjects);
            _gameMode = gameMode;

            notes = new SongObjectCache<Note>();
            starPower = new SongObjectCache<Starpower>();
            drumRoll = new SongObjectCache<DrumRoll>();
            events = new SongObjectCache<ChartEvent>();

            _note_count = 0;

            this.name = name;
        }

        public Chart(Song song, Song.Instrument instrument, string name = "") : this(song, Song.InstumentToChartGameMode(instrument), name)
        {
        }

        public Chart(Chart chart, Song song)
        {
            _song = song;
            name = chart.name;
            _gameMode = chart.gameMode;

            _chartObjects = new List<ChartObject>();
            _chartObjects.AddRange(chart._chartObjects);

            chartObjects = new ReadOnlyList<ChartObject>(_chartObjects);

            this.name = chart.name;
        }

        /// <summary>
        /// Updates all read-only values and the total note count.
        /// </summary>
        public void UpdateCache()
        {
            Song.UpdateCacheList(notes, _chartObjects);
            Song.UpdateCacheList(starPower, _chartObjects);
            Song.UpdateCacheList(drumRoll, _chartObjects);
            Song.UpdateCacheList(events, _chartObjects);

            _note_count = GetNoteCount();
        }

        int GetNoteCount()
        {
            if (notes.Count > 0)
            {
                int count = 1;

                uint previousPos = notes[0].tick;
                for (int i = 1; i < notes.Count; ++i)
                {
                    if (notes[i].tick > previousPos)
                    {
                        ++count;
                        previousPos = notes[i].tick;
                    }
                }

                return count;
            }
            else
                return 0;
        }

        public void SetCapacity(int size)
        {
            if (size > _chartObjects.Capacity)
                _chartObjects.Capacity = size;
        }

        public void Clear()
        {
            _chartObjects.Clear();
        }

        /// <summary>
        /// Adds a series of chart objects (note, starpower, drumRoll and/or chart events) into the chart.
        /// </summary>
        /// <param name="chartObjects">Items to add.</param>
        public void Add(ChartObject[] chartObjects)
        {
            foreach (ChartObject chartObject in chartObjects)
            {
                Add(chartObject, false);
            }

            UpdateCache();
        }

        /// <summary>
        /// Adds a chart object (note, starpower, drumRoll and/or chart event) into the chart.
        /// </summary>
        /// <param name="chartObject">The item to add</param>
        /// <param name="update">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when adding multiple objects as it increases performance dramatically.</param>
        public int Add(ChartObject chartObject, bool update = true)
        {
            chartObject.chart = this;
            chartObject.song = this._song;

            int pos = SongObjectHelper.Insert(chartObject, _chartObjects);

            if (update)
                UpdateCache();

            return pos;
        }

        /// <summary>
        /// Removes a series of chart objects (note, starpower, drumRoll and/or chart events) from the chart.
        /// </summary>
        /// <param name="chartObjects">Items to add.</param>
        public void Remove(ChartObject[] chartObjects)
        {
            foreach (ChartObject chartObject in chartObjects)
            {
                Remove(chartObject, false);
            }

            UpdateCache();
        }

        /// <summary>
        /// Removes a chart object (note, starpower, drumRoll and/or chart event) from the chart.
        /// </summary>
        /// <param name="chartObject">Item to add.</param>
        /// <param name="update">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when removing multiple objects as it increases performance dramatically.</param>
        /// <returns>Returns whether the removal was successful or not (item may not have been found if false).</returns>
        public bool Remove(ChartObject chartObject, bool update = true)
        {
            bool success = SongObjectHelper.Remove(chartObject, _chartObjects);

            if (success)
            {
                chartObject.chart = null;
                chartObject.song = null;
            }

            if (update)
                UpdateCache();

            return success;
        }

        public enum GameMode
        {
            Guitar,
            Drums,
            GHLGuitar,

            Unrecognised,
        }
    }
}
