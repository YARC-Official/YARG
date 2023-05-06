using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public class FiveLaneDrumsChartLoader : ChartLoader<NoteInfo> {
		public FiveLaneDrumsChartLoader() {
			Instrument = MoonSong.MoonInstrument.Drums;
			InstrumentName = "ghDrums";
			MaxDifficulty = Difficulty.EXPERT_PLUS;
		}

		public override List<NoteInfo> GetNotesFromChart(MoonSong song, Difficulty difficulty) {
			var notes = new List<NoteInfo>();
			var chart = GetChart(song, difficulty);
			bool doubleBass = difficulty == Difficulty.EXPERT_PLUS;

			foreach (var moonNote in chart.notes) {
				// Ignore double-kicks if not Expert+
				if (!doubleBass && (moonNote.flags & MoonNote.Flags.DoubleKick) != 0)
					continue;

				// Convert note value
				int pad = MoonDrumNoteToPad(moonNote);
				if (pad == -1)
					continue;

				var note = new NoteInfo {
					time = (float) moonNote.time,
					length = (float) GetNoteLength(song, moonNote),
					fret = pad,
					hopo = moonNote.type == MoonNote.MoonNoteType.Cymbal
				};

				notes.Add(note);
			}

			// TODO: Need to handle playing 4-lane charts on 5-lane
			return notes;
		}

		private int MoonDrumNoteToPad(MoonNote note) {
			return note.drumPad switch {
				MoonNote.DrumPad.Kick   => 5,
				MoonNote.DrumPad.Red    => 0,
				MoonNote.DrumPad.Yellow => 1,
				MoonNote.DrumPad.Blue   => 2,
				MoonNote.DrumPad.Orange => 3,
				MoonNote.DrumPad.Green  => 4,
				_                       => -1
			};
		}
	}
}