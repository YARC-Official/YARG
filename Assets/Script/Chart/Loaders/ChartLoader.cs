using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public abstract class ChartLoader<T> {
		public MoonSong.MoonInstrument Instrument { get; protected set; }
		public string InstrumentName { get; protected set; }
		public Difficulty MaxDifficulty { get; protected set; } = Difficulty.EXPERT;

		public abstract List<T> GetNotesFromChart(MoonSong song, Difficulty chart);

		public virtual List<EventInfo> GetEventsFromChart(MoonSong song)
		{
			var events = new List<EventInfo>();
			var chart = song.GetChart(Instrument, MoonSong.Difficulty.Expert);

			// Star Power
			foreach (var sp in chart.starPower) {
				if (sp.flags != Starpower.Flags.None) {
					continue;
				}

				events.Add(new EventInfo($"starpower_{InstrumentName}", (float) sp.time, (float) GetStarpowerLength(song, sp)));
			}

			// Solos
			for (int i = 0; i < chart.events.Count; i++) {
				var chartEvent = chart.events[i];
				if (chartEvent.eventName == "solo") {
					for (int k = i; k < chart.events.Count; k++) {
						var chartEvent2 = chart.events[k];
						if (chartEvent2.eventName == "soloend") {
							events.Add(new EventInfo($"solo_{InstrumentName}", (float) chartEvent.time, (float) (chartEvent2.time - chartEvent.time)));
							break;
						}
					}
				}
			}

			return events;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected double GetLength(MoonSong song, double startTime, uint tick, uint tickLength) {
			return song.TickToTime(tick + tickLength, song.resolution) - startTime;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected double GetNoteLength(MoonSong song, MoonNote note) {
			return GetLength(song, note.time, note.tick, note.length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected double GetStarpowerLength(MoonSong song, Starpower sp) {
			return GetLength(song, sp.time, sp.tick, sp.length - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected MoonChart GetChart(MoonSong song, Difficulty difficulty) {
			if (difficulty > Difficulty.EXPERT) {
				difficulty = Difficulty.EXPERT;
			} else if (difficulty < Difficulty.EASY) {
				difficulty = Difficulty.EASY;
			}

			return song.GetChart(Instrument, MoonSong.Difficulty.Easy - (int) difficulty);
		}
	}
}
