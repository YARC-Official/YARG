using System.IO;

namespace YARG.UI {
	public class SongInfo {
		public DirectoryInfo folder;
		public bool fetched;
		public bool errored;

		public bool BassPedal2xExpertPlus {
			private set;
			get;
		}
		public bool Live {
			private set;
			get;
		}

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