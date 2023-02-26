using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace YARG.Data {
	[JsonObject(MemberSerialization.Fields)]
	public class SongScore {
		public DateTime lastPlayed;
		public int timesPlayed;

		public DiffPercent TotalHighestPercent => highestPercent.Count <= 0 ? default : highestPercent.Max(i => i.Value);
		public Dictionary<string, DiffPercent> highestPercent;
	}
}