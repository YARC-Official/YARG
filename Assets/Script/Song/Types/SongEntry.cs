using System;
using System.Collections.Generic;
using YARG.Data;

namespace YARG.Song {
	public enum SongType {
		SongIni,
		ExtractedRbCon,
		RbCon,
	}

	public enum DrumType {
		FourLane,
		FiveLane,
		Unknown
	}

	public abstract class SongEntry {

		public string CacheRoot { get; set; }

		public SongType SongType { get; set; }

		public DrumType DrumType { get; set; }

		public string Name { get; set; } = string.Empty;

		public string NameNoParenthesis => string.IsNullOrEmpty(Name) ? "" : Name.Replace("(", "").Replace(")", "");

		public string Artist { get; set; } = string.Empty;
		public string Charter { get; set; } = string.Empty;
		public bool IsMaster { get; set; }

		public string Album { get; set; } = string.Empty;
		public int AlbumTrack { get; set; }

		public int PlaylistTrack { get; set; }

		public string Genre { get; set; } = string.Empty;
		public string Year { get; set; } = string.Empty;

		public int SongLength { get; set; }
		public TimeSpan SongLengthTimeSpan => TimeSpan.FromMilliseconds(SongLength);

		public int PreviewStart { get; set; }
		public TimeSpan PreviewStartTimeSpan => TimeSpan.FromMilliseconds(PreviewStart);

		public int PreviewEnd { get; set; }
		public TimeSpan PreviewEndTimeSpan => TimeSpan.FromMilliseconds(PreviewEnd);

		public double Delay { get; set; }

		public string LoadingPhrase { get; set; } = string.Empty;

		public int HopoThreshold { get; set; } = 170;
		public bool EighthNoteHopo { get; set; }
		public int MultiplierNote { get; set; }

		public string Source { get; set; } = string.Empty;

		public Dictionary<Instrument, int> PartDifficulties { get; } = new();
		public int BandDifficulty { get; set; }

		public ulong AvailableParts { get; set; }
		public int VocalParts { get; set; }

		public string Checksum { get; set; }
		public string NotesFile { get; set; }
		public string Location { get; set; }

		public bool HasInstrument(Instrument instrument) {
			// FL is my favourite hexadecimal number
			long instrumentBits = 0xFL << (int) instrument * 4;
			return (AvailableParts & (ulong) instrumentBits) != 0;
		}

		public bool HasPart(Instrument instrument, Difficulty difficulty) {
			long instrumentBits = 0x1L << (int)instrument * 4 + (int)difficulty;
			return (AvailableParts & (ulong) instrumentBits) != 0;
		}
	}
}