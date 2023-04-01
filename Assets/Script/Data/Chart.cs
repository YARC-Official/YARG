using System;
using System.Collections.Generic;

namespace YARG.Data {
	public sealed class Chart {
		public List<List<NoteInfo>[]> allParts;
		
#pragma warning disable format
		
		public List<NoteInfo>[] guitar     = CreateArray();
		public List<NoteInfo>[] bass       = CreateArray();
		public List<NoteInfo>[] keys       = CreateArray();
		public List<NoteInfo>[] realGuitar = CreateArray();
		public List<NoteInfo>[] realBass   = CreateArray();
		
		public List<NoteInfo>[] drums      = CreateArray(5);
		public List<NoteInfo>[] ghDrums    = CreateArray(5);

#pragma warning restore format

		public List<EventInfo> events = new();
		public List<float> beats = new();

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

		public Chart() {
			allParts = new() {
				guitar, bass, keys, realGuitar, realBass, drums, ghDrums
			};
		}

		public List<NoteInfo>[] GetChartByName(string name) {
			return name switch {
				"guitar" => guitar,
				"bass" => bass,
				"keys" => keys,
				"realGuitar" => realGuitar,
				"realBass" => realBass,
				"drums" or "realDrums" => drums,
				"ghDrums" => ghDrums,
				_ => throw new InvalidOperationException($"Unsupported chart type `{name}`.")
			};
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