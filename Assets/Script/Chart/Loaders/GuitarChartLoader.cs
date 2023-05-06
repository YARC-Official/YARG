using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public class GuitarChartLoader : ChartLoader<NoteInfo> {
		public GuitarChartLoader(MoonSong.MoonInstrument instrument) {
			InstrumentName = instrument switch {
				MoonSong.MoonInstrument.Guitar => "guitar",
				MoonSong.MoonInstrument.GuitarCoop => "guitarCoop",
				MoonSong.MoonInstrument.Rhythm => "rhythm",
				MoonSong.MoonInstrument.Bass => "bass",
				MoonSong.MoonInstrument.Keys => "keys",
				_ => throw new Exception("Instrument not supported!")
			};

			Instrument = instrument;
		}

		public override List<NoteInfo> GetNotesFromChart(MoonSong song, Difficulty difficulty) {
			var notes = new List<NoteInfo>();
			if (difficulty == Difficulty.EXPERT_PLUS) {
				difficulty = Difficulty.EXPERT;
			}
			var chart = song.GetChart(Instrument, MoonSong.Difficulty.Easy - (int) difficulty);

			foreach (var moonNote in chart.notes) {
				// Length of the note in realtime
				double timeLength = song.TickToTime(moonNote.tick + moonNote.length, song.resolution) - moonNote.time;

				var note = new NoteInfo {
					time = (float)moonNote.time,
					length = (float)timeLength,
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