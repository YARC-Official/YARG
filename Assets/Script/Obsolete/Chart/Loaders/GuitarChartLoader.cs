using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart
{
    public class GuitarChartLoader : ChartLoader<NoteInfo>
    {
        public GuitarChartLoader(MoonSong.MoonInstrument instrument)
        {
            InstrumentName = instrument switch
            {
                MoonSong.MoonInstrument.Guitar     => "guitar",
                MoonSong.MoonInstrument.GuitarCoop => "guitarCoop",
                MoonSong.MoonInstrument.Rhythm     => "rhythm",
                MoonSong.MoonInstrument.Bass       => "bass",
                MoonSong.MoonInstrument.Keys       => "keys",
                _                                  => throw new Exception("Instrument not supported!")
            };

            Instrument = instrument;
        }

        public override List<NoteInfo> GetNotesFromChart(MoonSong song, Difficulty difficulty)
        {
            var notes = new List<NoteInfo>();
            var chart = GetChart(song, difficulty);

            foreach (var moonNote in chart.notes)
            {
                var note = new NoteInfo
                {
                    time = (float) moonNote.time,
                    length = (float) GetNoteLength(song, moonNote),
                    fret = moonNote.rawNote,
                    hopo = moonNote.type == MoonNote.MoonNoteType.Hopo,
                    tap = moonNote.type == MoonNote.MoonNoteType.Tap,
                };

                notes.Add(note);
            }

            return notes;
        }
    }
}