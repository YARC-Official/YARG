using System;
using System.Collections.Generic;
using YARG.Data;

namespace YARG.Song {
	public abstract class SongEntry {
		
		public string CacheRoot { get; set; }
		
		public SongType SongType { get; set; }
		
		public DrumType DrumType { get; set; }
		
		public string Name { get; set; } = string.Empty;
		
		public string NameNoParenthesis => string.IsNullOrEmpty(Name) ? "" : Name.Replace("(", "").Replace(")", "");

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
		
		public double Delay { get; set; }
		
		public string LoadingPhrase { get; set; } = string.Empty;
		
		public int HopoThreshold { get; set; }
		public bool EighthNoteHopo { get; set; }
		public int MultiplierNote { get; set; }
		
		public string Source { get; set; } = string.Empty;

		public Dictionary<Instrument, int> PartDifficulties { get; } = new();
		
		public string Checksum  { get; set; }
		public string NotesFile { get; set; }
		public string Location  { get; set; }
		
	}

	public enum SongType {
		SongIni,
		RbConRaw,
		RbCon,
	}
	
	public enum DrumType {
		FourLane,
		FiveLane, // AKA GH
		Unknown
	}
}