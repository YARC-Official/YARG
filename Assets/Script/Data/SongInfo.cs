using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.Data {
	[JsonObject(MemberSerialization.OptIn)]
	public class SongInfo {
		private static readonly Dictionary<string, int> DEFAULT_DIFFS = new() {
			{"guitar", -1},
			{"bass", -1},
			{"keys", -1},
			{"drums", -1},
			{"vocals", -1},
			{"guitar_real", -1},
			{"bass_real", -1},
			{"keys_real", -1},
			{"drums_real", -1},
			{"vocals_harm", -1},
		};

		public bool fetched;
		public bool errored;

		[JsonProperty]
		[JsonConverter(typeof(DirectoryInfoConverter))]
		public DirectoryInfo folder;

		public bool BassPedal2xExpertPlus {
			private set;
			get;
		}
		public bool Live {
			private set;
			get;
		}

		[JsonProperty("songName")]
		private string _songName;
		public string SongNameWithFlags {
			set {
				const string BASS_PEDAL_SUFFIX = " (2x bass pedal expert+)";
				BassPedal2xExpertPlus = value.ToLower().EndsWith(BASS_PEDAL_SUFFIX);
				if (BassPedal2xExpertPlus) {
					value = value[..^BASS_PEDAL_SUFFIX.Length];
				}

				const string LIVE_SUFFIX = " (live)";
				Live = value.ToLower().EndsWith(LIVE_SUFFIX);
				if (Live) {
					value = value[..^LIVE_SUFFIX.Length];
				}

				_songName = value;
			}
		}
		public string SongName {
			set => _songName = value;
			get => _songName;
		}
		public string SongNameNoParen => SongName.Replace("(", "").Replace(")", "");

		public bool WaveGroup {
			private set;
			get;
		}

		[JsonProperty("artist")]
		private string _artistName;
		public string ArtistName {
			set {
				const string WAVEGROUP = " (WaveGroup)";
				WaveGroup = value.EndsWith(WAVEGROUP);
				if (WaveGroup) {
					value = value[..^WAVEGROUP.Length];
				}

				_artistName = value;
			}
			get => _artistName;
		}

		/// <value>
		/// Used for JSON. Compresses flags (<see cref="BassPedal2xExpertPlus"/>, etc.) into a small string.
		/// </value>
		[JsonProperty("flags")]
		public string JsonFlags {
			get {
				string o = "";

				if (BassPedal2xExpertPlus) {
					o += "B";
				}
				if (Live) {
					o += "L";
				}
				if (WaveGroup) {
					o += "W";
				}

				return o;
			}
			set {
				string i = value;

				if (i.StartsWith('B')) {
					BassPedal2xExpertPlus = true;
					i = i.Remove(0, 1);
				} else {
					BassPedal2xExpertPlus = false;
				}
				if (i.StartsWith('L')) {
					Live = true;
					i = i.Remove(0, 1);
				} else {
					Live = false;
				}
				if (i.StartsWith('W')) {
					WaveGroup = true;
					// i = i.Remove(0, 1);
				} else {
					WaveGroup = false;
				}
			}
		}

		/// <value>
		/// Used for JSON. Compresses <see cref="partDifficulties"/> by getting rid of <c>-1</c>s.
		/// </value>
		[JsonProperty("diffs")]
		public Dictionary<string, int> JsonDiffs {
			get => partDifficulties.Where(i => i.Value != -1).ToDictionary(i => i.Key, i => i.Value);
			set {
				partDifficulties = new(DEFAULT_DIFFS);
				foreach (var kvp in value) {
					partDifficulties[kvp.Key] = kvp.Value;
				}
			}
		}

		[JsonProperty]
		public string source;
		[JsonProperty]
		public float? songLength;
		[JsonProperty]
		public float delay;

		public Dictionary<string, int> partDifficulties;

		public SongInfo(DirectoryInfo folder) {
			this.folder = folder;
			string dirName = folder.Name;

			var split = dirName.Split(" - ");
			if (split.Length == 2) {
				SongNameWithFlags = split[1];
				ArtistName = split[0];
			} else {
				SongNameWithFlags = dirName;
				ArtistName = "Unknown";
			}

			partDifficulties = new(DEFAULT_DIFFS);
		}
	}
}