using System;
using System.Collections.Generic;

namespace YARG.Data {
	public class Chart {
		public List<List<NoteInfo>[]> allParts;
		public List<NoteInfo>[] guitar = new List<NoteInfo>[] { new(), new(), new(), new() };
		public List<NoteInfo>[] bass = new List<NoteInfo>[] { new(), new(), new(), new() };
		public List<NoteInfo>[] keys = new List<NoteInfo>[] { new(), new(), new(), new() };
		public List<NoteInfo>[] realGuitar = new List<NoteInfo>[] { new(), new(), new(), new() };
		public List<NoteInfo>[] realBass = new List<NoteInfo>[] { new(), new(), new(), new() };

		public List<EventInfo> events = new();
		public List<float> beats = new();

		public List<GenericLyricInfo> genericLyrics = new();

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