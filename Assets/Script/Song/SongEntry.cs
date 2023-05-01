using System;

namespace YARG.Song {
	public abstract class SongEntry {
		
		public SongType Type { get; set; }
		
		public string Name { get; set; } = string.Empty;
		public string Artist { get; set; } = string.Empty;
		public string Charter { get; set; } = string.Empty;
		
		public string Album { get; set; } = string.Empty;
		public int AlbumTrack { get; set; }
		
		public int PlaylistTrack { get; set; }
		
		public string Genre { get; set; } = string.Empty;
		public string Year  { get; set; } = string.Empty;

		public int SongLength { get; set; }
		public TimeSpan SongLengthTimeSpan => TimeSpan.FromMilliseconds(SongLength);

		public int PreviewStart { get; set; }
		public TimeSpan PreviewStartTimeSpan => TimeSpan.FromMilliseconds(PreviewStart);
		
		public int PreviewEnd { get; set; }
		public TimeSpan PreviewEndTimeSpan => TimeSpan.FromMilliseconds(PreviewEnd);
		
		public string LoadingPhrase { get; set; } = string.Empty;
		
		public int HopoThreshold { get; set; }
		public bool EighthNoteHopo { get; set; }
		public int MultiplierNote { get; set; }
		
		public string Icon { get; set; } = string.Empty;
		
		public string Checksum  { get; set; }
		public string NotesFile { get; set; }
		public string Location  { get; set; }
		
	}

	public enum SongType {
		SongIni,
		RbConRaw,
		RbCon,
	}
}