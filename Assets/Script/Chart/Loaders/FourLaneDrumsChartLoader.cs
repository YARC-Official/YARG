using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public class FourLaneDrumsChartLoader : ChartLoader<NoteInfo> {
		private bool _proDrums;

		public FourLaneDrumsChartLoader(bool pro) {
			_proDrums = pro;
		}

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
					hopo = _proDrums && moonNote.type == MoonNote.MoonNoteType.Cymbal
				};

				notes.Add(note);
			}

			// TODO: Need to handle playing 5-lane charts on 4-lane
			return notes;
		}

		private int MoonDrumNoteToPad(MoonNote note) {
			return note.drumPad switch {
				MoonNote.DrumPad.Kick   => 4,
				MoonNote.DrumPad.Red    => 0,
				MoonNote.DrumPad.Yellow => 1,
				MoonNote.DrumPad.Blue   => 2,
				MoonNote.DrumPad.Orange => 3, // Moonscraper internally uses Orange for 4-lane green
				MoonNote.DrumPad.Green  => 3, // Turn 5-lane green into 4-lane green
				_                       => -1
			};
		}
	}
}