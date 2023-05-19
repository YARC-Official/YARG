namespace YARG.Song {
	public class IniSongEntry : SongEntry {

		public string Playlist { get; set; } = string.Empty;
		public string SubPlaylist { get; set; } = string.Empty;

		public bool IsModChart { get; set; }
		public bool HasLyrics { get; set; }

		public int VideoStartOffset { get; set; }

	}
}