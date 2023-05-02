using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.Data {
	[JsonObject(MemberSerialization.OptIn)]
	public partial class SongInfo {
		// TODO: Move this
		public enum DrumType {
			FOUR_LANE,
			FIVE_LANE, // AKA GH
			UNKNOWN
		}

		public enum SongType {
			SONG_INI,   // CH/PS
			RB_CON_RAW, // CON rawfiles
			RB_CON      // unextracted CON
		}

		public bool fetched;

		[JsonProperty]
		public string mainFile;
		public string RootFolder => Path.GetDirectoryName(mainFile);

		[JsonProperty]
		public SongType songType;

		/// <summary>
		/// Used for cache.
		/// </summary>
		public string cacheRoot;

		[JsonProperty("live")]
		public bool Live {
			private set;
			get;
		}

		[JsonProperty("songName")]
		private string _songName;
		public string SongNameWithFlags {
			set {
				// We don't care about this...
				const string BASS_PEDAL_EXPERT_SUFFIX = " (2x bass pedal expert+)";
				bool present = value.ToLower().EndsWith(BASS_PEDAL_EXPERT_SUFFIX);
				if (present) {
					value = value[..^BASS_PEDAL_EXPERT_SUFFIX.Length];
				}

				// Or this...
				const string BASS_PEDAL_SUFFIX = " (2x bass pedal)";
				present = value.ToLower().EndsWith(BASS_PEDAL_SUFFIX);
				if (present) {
					value = value[..^BASS_PEDAL_SUFFIX.Length];
				}

				// But we do care about this!
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
		public string SongNameNoParen {
			get {
				if (string.IsNullOrEmpty(SongName)) {
					return "";
				}

				return SongName.Replace("(", "").Replace(")", "");
			}
		}

		/// <value>
		/// Used for JSON. Compresses <see cref="partDifficulties"/> by getting rid of <c>-1</c>s.
		/// </value>
		[JsonProperty("diffs")]
		public Dictionary<string, int> JsonDiffs {
			get {
				// Remove all non-existent difficulties
				var diffs = partDifficulties.Where(i => i.Value != -1);

				// Convert to dictionary with strings
				var dict = new Dictionary<string, int>();
				foreach (var kvp in diffs) {
					var key = kvp.Key.ToStringName();
					if (key == null) {
						continue;
					}

					dict.Add(key, kvp.Value);
				}

				return dict;
			}

			set {
				// Create empty dictionary
				partDifficulties = new();
				foreach (Instrument instrument in Enum.GetValues(typeof(Instrument))) {
					if (instrument == Instrument.INVALID) {
						continue;
					}

					partDifficulties.Add(instrument, -1);
				}

				// Fill in values
				foreach (var kvp in value) {
					partDifficulties[InstrumentHelper.FromStringName(kvp.Key)] = kvp.Value;
				}
			}
		}

		[JsonProperty]
		public string source;
		public string SourceFriendlyName => SourceToGameName(source);
		[JsonProperty]
		public float songLength;
		[JsonProperty]
		public float delay;
		[JsonProperty]
		public DrumType drumType;
		/// <value>
		/// The hopo frequency in ticks.<br/>
		/// Standardized here: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/5-Fret%20Guitar.md#note-mechanics
		/// </value>
		[JsonProperty]
		public int hopoFreq = 170;

		[JsonProperty]
		public string artistName;
		[JsonProperty]
		public string album;
		[JsonProperty]
		public string genre;
		[JsonProperty]
		public string charter;
		[JsonProperty]
		public string year;

		[JsonProperty]
		public string loadingPhrase;
		[JsonProperty]
		public string hash;

		/// <summary>
		/// .mogg data for CON files.
		/// </summary>
		[JsonProperty]
		public XboxMoggData moggInfo;
		/// <summary>
		/// .xbox_png data for CON files.
		/// </summary>
		[JsonProperty]
		public XboxImage imageInfo;

		public Dictionary<Instrument, int> partDifficulties;

		public SongInfo(string mainFile, string cacheRoot, SongType songType) {
			this.mainFile = mainFile;
			this.cacheRoot = cacheRoot;
			this.songType = songType;

			// Set difficulty defaults
			partDifficulties = new();
			foreach (Instrument instrument in Enum.GetValues(typeof(Instrument))) {
				if (instrument == Instrument.INVALID) {
					continue;
				}

				partDifficulties.Add(instrument, -1);
			}
		}

		public SongInfo Duplicate() {
			return (SongInfo) MemberwiseClone();
		}
	}
}