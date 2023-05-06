using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public class FiveLaneDrumsChartLoader : ChartLoader<NoteInfo> {
		public override List<NoteInfo> GetNotesFromChart(MoonSong song, Difficulty difficulty) {
			var notes = new List<NoteInfo>();
			bool doubleBass = false;
			if (difficulty == Difficulty.EXPERT_PLUS) {
				difficulty = Difficulty.EXPERT;
				doubleBass = true;
			}
			var chart = song.GetChart(MoonSong.MoonInstrument.Drums, MoonSong.Difficulty.Easy - (int) difficulty);

			foreach (var moonNote in chart.notes) {
				// Ignore double-kicks if not Expert+
				if (!doubleBass && (moonNote.flags & MoonNote.Flags.DoubleKick) != 0)
					continue;

				// Convert note value
				int pad = MoonDrumNoteToPad(moonNote);
				if (pad == -1)
					continue;

				// Length of the note in realtime
				double timeLength = song.TickToTime(moonNote.tick + moonNote.length, song.resolution) - moonNote.time;

				var note = new NoteInfo {
					time = (float) moonNote.time,
					length = (float) timeLength,
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