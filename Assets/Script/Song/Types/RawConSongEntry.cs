using YARG.Serialization;

namespace YARG.Song {
	public class ExtractedConSongEntry : SongEntry {

		/// <summary>
		/// .mogg data for CON files.
		/// </summary>
		public XboxMoggData MoggInfo { get; set; }
		/// <summary>
		/// .xbox_png data for CON files.
		/// </summary>
		public XboxImage ImageInfo { get; set; }

	}
}