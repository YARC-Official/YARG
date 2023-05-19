namespace YARG.Song {
	public class ConSongEntry : ExtractedConSongEntry {
		// Location: the path to the CON file

		// mid file size and block offsets
		public uint MidiFileSize { get; set; }
		public uint[] MidiFileMemBlockOffsets { get; set; }

		// mogg file size and block offsets
		public uint MoggFileSize { get; set; }
		public uint[] MoggFileMemBlockOffsets { get; set; }

		// img file size and block offsets
		public uint ImageFileSize { get; set; }
		public uint[] ImageFileMemBlockOffsets { get; set; } = null;
	}
}