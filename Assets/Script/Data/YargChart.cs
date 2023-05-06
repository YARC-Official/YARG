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
			get => guitar ??= LoadArray(ChartLoader.GuitarLoader);
			set => guitar = value;
		}
		
		private List<NoteInfo>[] guitarCoop;
		public List<NoteInfo>[] GuitarCoop {
			get => guitarCoop ??= LoadArray(ChartLoader.GuitarCoopLoader);
			set => guitarCoop = value;
		}
		private List<NoteInfo>[] rhythm;
		public List<NoteInfo>[] Rhythm {
			get => rhythm ??= LoadArray(ChartLoader.RhythmLoader);
			set => rhythm = value;
		}
		
		private List<NoteInfo>[] bass;
		public List<NoteInfo>[] Bass {
			get => bass ??= LoadArray(ChartLoader.BassLoader);
			set => bass = value;
		}
		
		private List<NoteInfo>[] keys;
		public List<NoteInfo>[] Keys {
			get => keys ??= LoadArray(ChartLoader.KeysLoader);
			set => keys = value;
		}

		public  List<NoteInfo>[] RealGuitar { get; set; }

		public  List<NoteInfo>[] RealBass { get; set; }

		private List<NoteInfo>[] drums;
		public List<NoteInfo>[] Drums {
			get => drums ??= LoadArray(ChartLoader.DrumsLoader);
			set => drums = value;
		}

		private List<NoteInfo>[] realDrums;
		public List<NoteInfo>[] RealDrums {
			get => realDrums ??= LoadArray(ChartLoader.ProDrumsLoader);
			set => realDrums = value;
		}

		private List<NoteInfo>[] ghDrums;
		public List<NoteInfo>[] GhDrums {
			get => ghDrums ??= LoadArray(ChartLoader.FiveLaneDrumsLoader);
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

		private List<NoteInfo>[] LoadArray(ChartLoader<NoteInfo> loader) {
			var maxDifficulty = loader.MaxDifficulty;
			var instrument = loader.Instrument;
			string instrumentName = loader.InstrumentName;

			var notes = new List<NoteInfo>[(int) (maxDifficulty + 1)];
			for (Difficulty diff = Difficulty.EASY; diff <= maxDifficulty; diff++) {
				notes[(int) diff] = loader.GetNotesFromChart(_song, diff);
			}

			if (_loadedEvents.Contains(instrument)) {
				return notes;
			}

			events.AddRange(loader.GetEventsFromChart(_song));
			events.Sort((e1, e2) => e1.time.CompareTo(e2.time));
			_loadedEvents.Add(instrument);

			return notes;
		}

		private static List<NoteInfo>[] CreateArray(int length = 4) {
			var list = new List<NoteInfo>[length];
			for (int i = 0; i < length; i++) {
				list[i] = new();
			}

			return list;
		}
	}
}