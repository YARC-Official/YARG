using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YARG.Data {
	[JsonObject(MemberSerialization.Fields)]
	public class SongScore {
		public DateTime lastPlayed;
		public int timesPlayed;

		public Dictionary<string, DiffPercent> highestPercent;

		public KeyValuePair<string, DiffPercent> GetHighestPercent() {
			KeyValuePair<string, DiffPercent> highest = default;

			foreach (var kvp in highestPercent) {
				if (kvp.Value > highest.Value) {
					highest = kvp;
				}
			}

			return highest;
		}
	}
}