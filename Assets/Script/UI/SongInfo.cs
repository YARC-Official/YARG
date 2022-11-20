using System.IO;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.UI {
	[JsonObject(MemberSerialization.Fields)]
	public class SongInfo {
		[JsonConverter(typeof(DirectoryInfoConverter))]
		public DirectoryInfo folder;
		[JsonIgnore]
		public bool fetched;
		[JsonIgnore]
		public bool errored;

		[field: JsonProperty("bassPedal2xExpertPlus")]
		public bool BassPedal2xExpertPlus {
			private set;
			get;
		}
		[field: JsonProperty("live")]
		public bool Live {
			private set;
			get;
		}

		[JsonProperty("songName")]
		private string _songName;
		public string SongName {
			set {
				const string BASS_PEDAL_SUFFIX = " (2x Bass Pedal Expert+)";
				BassPedal2xExpertPlus = value.EndsWith(BASS_PEDAL_SUFFIX);
				if (BassPedal2xExpertPlus) {
					value = value[..^BASS_PEDAL_SUFFIX.Length];
				}

				const string LIVE_SUFFIX = " (Live)";
				Live = value.EndsWith(LIVE_SUFFIX);
				if (Live) {
					value = value[..^LIVE_SUFFIX.Length];
				}

				_songName = value;
			}
			get => _songName;
		}
		public string SongNameNoParen => SongName.Replace("(", "").Replace(")", "");

		public string artistName;

		public float? songLength;

		public SongInfo(DirectoryInfo folder) {
			this.folder = folder;
			string dirName = folder.Name;

			var split = dirName.Split(" - ");
			if (split.Length == 2) {
				SongName = split[1];
				artistName = split[0];
			} else {
				SongName = dirName;
				artistName = "Unknown";
			}
		}
	}
}