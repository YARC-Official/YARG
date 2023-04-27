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
			get => drums;// ?? LoadArray(new DrumsChartLoader(false), MoonSong.MoonInstrument.Drums, 4);
			set => drums = value;
		}
		private List<NoteInfo>[] realDrums;
		public List<NoteInfo>[] RealDrums {
			get => realDrums;// ?? LoadArray(new DrumsChartLoader(true), MoonSong.MoonInstrument.Drums, 4);
			set => realDrums = value;
		}
		
		private List<NoteInfo>[] ghDrums;
		public List<NoteInfo>[] GhDrums {
			get => ghDrums;// ?? LoadArray(new DrumsChartLoader(false), MoonSong.MoonInstrument.Drums, 4);
			set => ghDrums = value;
		}

#pragma warning restore format

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

		private List<NoteInfo>[] LoadArray(ref List<NoteInfo>[] notes, IChartLoader<NoteInfo> loader, MoonSong.MoonInstrument instrument, int length = 4) {
			notes = new List<NoteInfo>[length];
			for (int i = 0; i < length; i++) {
				notes[i] = loader.GetNotesFromChart(_song, _song.GetChart(instrument, (MoonSong.Difficulty) length - 1 - i));
			}

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