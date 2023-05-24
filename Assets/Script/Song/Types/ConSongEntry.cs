using XboxSTFS;

namespace YARG.Song {
	public class ConSongEntry : ExtractedConSongEntry {
		// Location: the path to the CON file

		// relevant CON file listings
		public FileListing FLMidi { get; set; }
		public FileListing FLMogg { get; set; }
		public FileListing FLImg { get; set; }
		
	}
}