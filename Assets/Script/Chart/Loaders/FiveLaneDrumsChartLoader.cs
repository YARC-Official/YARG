using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

		public override List<EventInfo> GetEventsFromChart(MoonSong song) {
			var events = base.GetEventsFromChart(song);
			var chart = GetChart(song, Difficulty.EXPERT);

			// SP activations
			// Not typically present on 5-lane, but if they're there we'll take 'em!
			foreach (var sp in chart.starPower) {
				if ((sp.flags & Starpower.Flags.ProDrums_Activation) == 0) {
					continue;
				}

				events.Add(new EventInfo($"fill_{InstrumentName}", (float) sp.time, (float) GetDrumFillLength(song, sp)));
			}

			return events;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected double GetDrumFillLength(MoonSong song, Starpower sp) {
			return GetLength(song, sp.time, sp.tick, sp.length);
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