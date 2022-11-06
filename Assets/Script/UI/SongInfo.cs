using System.IO;

namespace YARG.UI {
	public class SongInfo {
		public DirectoryInfo folder;
		public bool fetched;
		public bool errored;

		public string songName;
		public string artistName;

		public float? songLength;

		public SongInfo(DirectoryInfo folder) {
			this.folder = folder;
			string dirName = folder.Name;

			var split = dirName.Split(" - ");
			if (split.Length == 2) {
				songName = split[1];
				artistName = split[0];
			} else {
				songName = dirName;
				artistName = "Unknown";
			}
		}
	}
}