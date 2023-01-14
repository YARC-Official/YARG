using System;
using System.Collections.Generic;

namespace YARG.Data {
	public class Chart {
		public List<List<NoteInfo>[]> allParts;
		public List<NoteInfo>[] guitar = new List<NoteInfo>[4];
		public List<NoteInfo>[] bass = new List<NoteInfo>[4];
		public List<NoteInfo>[] keys = new List<NoteInfo>[4];
		public List<NoteInfo>[] realGuitar = new List<NoteInfo>[4];
		public List<NoteInfo>[] realBass = new List<NoteInfo>[4];

		public List<EventInfo> events;
		public List<float> beats;

		public List<GenericLyricInfo> genericLyrics;

		public Chart() {
			allParts = new() {
				guitar, bass, keys, realGuitar, realBass
			};
		}

		public List<NoteInfo>[] GetChartByName(string name) {
			return name switch {
				"guitar" => guitar,
				"bass" => bass,
				"keys" => keys,
				"realGuitar" => realGuitar,
				"realBass" => realBass,
				_ => throw new InvalidOperationException($"Unsupported chart type `{name}`.")
			};
		}
	}
}