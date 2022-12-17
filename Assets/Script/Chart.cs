using System;
using System.Collections.Generic;

namespace YARG {
	public class Chart {
		public List<List<NoteInfo>[]> allParts = null;
		public List<NoteInfo>[] guitar = new List<NoteInfo>[4];
		public List<NoteInfo>[] bass = new List<NoteInfo>[4];
		public List<NoteInfo>[] keys = new List<NoteInfo>[4];

		public List<EventInfo> events = null;

		public Chart() {
			allParts = new() {
				guitar, bass, keys
			};
		}

		public List<NoteInfo>[] GetChartByName(string name) {
			return name switch {
				"guitar" => guitar,
				"bass" => bass,
				"keys" => keys,
				_ => throw new InvalidOperationException($"Unsupported chart type `{name}`.")
			};
		}
	}
}