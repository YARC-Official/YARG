using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Chart;

namespace YARG.Data {
	public sealed class YargChart {

		private MoonSong _song;

		public List<List<NoteInfo>[]> allParts;
		
#pragma warning disable format

		private List<NoteInfo>[] guitar;
		public List<NoteInfo>[] Guitar {
			get => guitar ?? LoadArray(ref guitar, new GuitarChartLoader(), MoonSong.MoonInstrument.Guitar);
			set => guitar = value;
		}
		
		private List<NoteInfo>[] guitarCoop;
		public List<NoteInfo>[] GuitarCoop {
			get => guitarCoop ?? LoadArray(ref guitarCoop, new GuitarChartLoader(), MoonSong.MoonInstrument.GuitarCoop);
			set => guitarCoop = value;
		}
		private List<NoteInfo>[] rhythm;
		public List<NoteInfo>[] Rhythm {
			get => rhythm ?? LoadArray(ref rhythm, new GuitarChartLoader(), MoonSong.MoonInstrument.Rhythm);
			set => rhythm = value;
		}
		
		private List<NoteInfo>[] bass;
		public List<NoteInfo>[] Bass {
			get => bass ?? LoadArray(ref bass, new GuitarChartLoader(), MoonSong.MoonInstrument.Bass);
			set => bass = value;
		}
		
		private List<NoteInfo>[] keys;
		public List<NoteInfo>[] Keys {
			get => keys ?? LoadArray(ref keys, new GuitarChartLoader(), MoonSong.MoonInstrument.Keys);
			set => keys = value;
		}

		public  List<NoteInfo>[] RealGuitar { get; set; }

		public  List<NoteInfo>[] RealBass { get; set; }

		private List<NoteInfo>[] drums;
		public List<NoteInfo>[] Drums {
			get => drums ?? LoadArray(ref drums, new FourLaneDrumsChartLoader(pro: false), MoonSong.MoonInstrument.Drums, Difficulty.EXPERT_PLUS);
			set => drums = value;
		}

		private List<NoteInfo>[] realDrums;
		public List<NoteInfo>[] RealDrums {
			get => realDrums ?? LoadArray(ref realDrums, new FourLaneDrumsChartLoader(pro: true), MoonSong.MoonInstrument.Drums, Difficulty.EXPERT_PLUS, isPro: true);
			set => realDrums = value;
		}

		private List<NoteInfo>[] ghDrums;
		public List<NoteInfo>[] GhDrums {
			get => ghDrums ?? LoadArray(ref ghDrums, new FiveLaneDrumsChartLoader(), MoonSong.MoonInstrument.Drums, Difficulty.EXPERT_PLUS, isGh: true);
			set => ghDrums = value;
		}

#pragma warning restore format

		private List<MoonSong.MoonInstrument> _loadedEvents = new();

		public List<EventInfo> events = new();
		public List<Beat> beats = new();

		/// <summary>
		/// Lyrics to be displayed in the lyric view when no one is singing.
		/// </summary>
		public List<GenericLyricInfo> genericLyrics = new();

		/// <summary>
		/// Solo vocal lyrics.
		/// </summary>
		public List<LyricInfo> realLyrics = new();

		/// <summary>
		/// Harmony lyrics. Size 0 by default, should be set by the harmony lyric parser.
		/// </summary>
		public List<LyricInfo>[] harmLyrics = new List<LyricInfo>[0];

		public YargChart(MoonSong song) {
			_song = song;
		}

		public List<NoteInfo>[] GetChartByName(string name) {
			return name switch {
				"guitar" => Guitar,
				"guitarCoop" => GuitarCoop,
				"rhythm" => Rhythm,
				"bass" => Bass,
				"keys" => Keys,
				"realGuitar" => RealGuitar,
				"realBass" => RealBass,
				"drums" => Drums,
				"realDrums" => RealDrums,
				"ghDrums" => GhDrums,
				_ => throw new InvalidOperationException($"Unsupported chart type `{name}`.")
			};
		}

		public void InitializeArrays() {
			Guitar = CreateArray();
			GuitarCoop = CreateArray();
			Rhythm = CreateArray();
			Bass = CreateArray();
			Keys = CreateArray();
			RealGuitar = CreateArray();
			RealBass = CreateArray();
			Drums = CreateArray(5);
			RealDrums = CreateArray(5);
			GhDrums = CreateArray(5);

			allParts = new() {
				guitar, guitarCoop, rhythm, bass, keys, RealGuitar, RealBass, drums, realDrums, ghDrums
			};
		}

		private List<NoteInfo>[] LoadArray(ref List<NoteInfo>[] notes, ChartLoader<NoteInfo> loader, MoonSong.MoonInstrument instrument,
			Difficulty maxDifficulty = Difficulty.EXPERT, bool isPro = false, bool isGh = false) {
			notes = new List<NoteInfo>[(int) (maxDifficulty + 1)];
			for (Difficulty diff = Difficulty.EASY; diff <= maxDifficulty; diff++) {
				notes[(int) diff] = loader.GetNotesFromChart(_song, diff);
			}

			if (_loadedEvents.Contains(instrument)) {
				return notes;
			}

			var chart = _song.GetChart(instrument, MoonSong.Difficulty.Expert);
			foreach (var sp in chart.starPower) {
				string name = GetNameFromInstrument(instrument, isPro, isGh);

				float finishTime = (float) _song.TickToTime(sp.tick + sp.length - 1);

				events.Add(new EventInfo($"starpower_{name}", (float) sp.time, finishTime - (float) sp.time));
			}

			for (int i = 0; i < chart.events.Count; i++) {
				var chartEvent = chart.events[i];
				string name = GetNameFromInstrument(instrument, isPro, isGh);

				if (chartEvent.eventName == "solo") {
					for (int k = i; k < chart.events.Count; k++) {
						var chartEvent2 = chart.events[k];
						if (chartEvent2.eventName == "soloend") {
							events.Add(new EventInfo($"solo_{name}", (float) chartEvent.time, (float) (chartEvent2.time - chartEvent.time)));
							break;
						}
					}
				}
			}

			_loadedEvents.Add(instrument);
			events.Sort((e1, e2) => e1.time.CompareTo(e2.time));

			return notes;
		}

		private static List<NoteInfo>[] CreateArray(int length = 4) {
			var list = new List<NoteInfo>[length];
			for (int i = 0; i < length; i++) {
				list[i] = new();
			}

			return list;
		}

		private static string GetNameFromInstrument(MoonSong.MoonInstrument instrument, bool isPro, bool isGh) {
			return instrument switch {
				MoonSong.MoonInstrument.Guitar => "guitar",
				MoonSong.MoonInstrument.GuitarCoop => "guitarCoop",
				MoonSong.MoonInstrument.Rhythm => "rhythm",
				MoonSong.MoonInstrument.Bass => "bass",
				MoonSong.MoonInstrument.Keys => "keys",
				MoonSong.MoonInstrument.Drums => isPro ? "realDrums" : isGh ? "ghDrums" : "drums",
				_ => throw new Exception("Instrument not supported!")
			};
		}
	}
}