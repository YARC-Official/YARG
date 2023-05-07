using System.Collections.Generic;
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

		// songs.dta content exclusive to a RB CON
		public string ShortName { get; set; }
		public bool IsMaster { get; set; }
		public int SongID { get; set; }
		public int VocalParts { get; set; }
		public int AnimTempo { get; set; }
		public string VocalPercussionBank { get; set; }
		public int VocalSongScrollSpeed { get; set; }
		public int SongRating { get; set; }
		public bool VocalGender { get; set; } //true for male, false for female
		public bool HasAlbumArt { get; set; }
		public bool IsFake { get; set; }
		public int VocalTonicNote { get; set; }
		public bool SongTonality { get; set; }
		public int TuningOffsetCents { get; set; }
		public int[] RealGuitarTuning { get; set; }
		public int[] RealBassTuning { get; set; }

		// .mogg info
		public string MoggPath { get; set; }
		public int MoggHeader { get; set; }
		public int MoggAddressAudioOffset { get; set; }
		public long MoggAudioLength { get; set; }

		public Dictionary<SongStem, int[]> StemMaps { get; set; } = new();
		public float[,] MatrixRatios { get; set; }

		// image info
		public string ImagePath { get; set; } = string.Empty;

	}
}