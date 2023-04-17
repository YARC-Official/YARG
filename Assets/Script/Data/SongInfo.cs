using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.Data
{
	[JsonObject(MemberSerialization.OptIn)]
	public partial class SongInfo
	{
		public enum DrumType
		{
			FOUR_LANE,
			FIVE_LANE, // AKA GH
			UNKNOWN
		}

		private static readonly Dictionary<string, int> DEFAULT_DIFFS = new()
		{
			{ "guitar", -1 },
			{ "bass", -1 },
			{ "keys", -1 },
			{ "drums", -1 },
			{ "vocals", -1 },
			{ "realGuitar", -1 },
			{ "realBass", -1 },
			{ "realKeys", -1 },
			{ "realDrums", -1 },
			{ "harmVocals", -1 },
		};

		public bool fetched;

		[JsonProperty]
		[JsonConverter(typeof(DirectoryInfoConverter))]
		public DirectoryInfo folder;

		/// <summary>
		/// Used for cache.
		/// </summary>
		public string rootFolder;

		[JsonProperty("live")]
		public bool Live
		{
			private set;
			get;
		}

		[JsonProperty("songName")]
		private string _songName;
		public string SongNameWithFlags
		{
			set
			{
				// We don't care about this...
				const string BASS_PEDAL_EXPERT_SUFFIX = " (2x bass pedal expert+)";
				bool present = value.ToLower().EndsWith(BASS_PEDAL_EXPERT_SUFFIX);
				if (present)
				{
					value = value[..^BASS_PEDAL_EXPERT_SUFFIX.Length];
				}

				// Or this...
				const string BASS_PEDAL_SUFFIX = " (2x bass pedal)";
				present = value.ToLower().EndsWith(BASS_PEDAL_SUFFIX);
				if (present)
				{
					value = value[..^BASS_PEDAL_SUFFIX.Length];
				}

				// But we do care about this!
				const string LIVE_SUFFIX = " (live)";
				Live = value.ToLower().EndsWith(LIVE_SUFFIX);
				if (Live)
				{
					value = value[..^LIVE_SUFFIX.Length];
				}

				_songName = value;
			}
		}
		public string SongName
		{
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
		public Dictionary<string, int> JsonDiffs
		{
			get => partDifficulties.Where(i => i.Value != -1).ToDictionary(i => i.Key, i => i.Value);
			set
			{
				partDifficulties = new(DEFAULT_DIFFS);
				foreach (var kvp in value)
				{
					partDifficulties[kvp.Key] = kvp.Value;
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

		public Dictionary<string, int> partDifficulties;

		public SongInfo(DirectoryInfo folder, string rootFolder)
		{
			this.folder = folder;
			this.rootFolder = rootFolder;

			string dirName = folder.Name;

			var split = dirName.Split(" - ");
			if (split.Length == 2)
			{
				SongNameWithFlags = split[1];
				artistName = split[0];
			}
			else
			{
				SongNameWithFlags = dirName;
				artistName = "Unknown";
			}

			partDifficulties = new(DEFAULT_DIFFS);
		}

		public SongInfo Duplicate()
		{
			return (SongInfo)MemberwiseClone();
		}
	}
}