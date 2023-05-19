using System.Collections.Generic;
using YARG.Serialization;

namespace YARG.Song {
	public class ExtractedConSongEntry : SongEntry {

		// songs.dta content exclusive to a RB CON
		// NOTE: none of these are written to a cache
		// make sure once these variables start getting used for YARG,
		// that they get cached a la CacheHelpers.cs
		public string ShortName { get; set; }
		public int SongID { get; set; }
		public int AnimTempo { get; set; }
		public string VocalPercussionBank { get; set; }
		public int VocalSongScrollSpeed { get; set; }
		public int SongRating { get; set; } // 1 = FF; 2 = SR; 3 = M; 4 = NR
		public bool VocalGender { get; set; } //true for male, false for female
		public bool HasAlbumArt { get; set; }
		public bool IsFake { get; set; }
		public int VocalTonicNote { get; set; }
		public bool SongTonality { get; set; } // 0 = major, 1 = minor
		public int TuningOffsetCents { get; set; }

		// _update.mid info, if it exists
		public bool DiscUpdate { get; set; } = false;
		public string UpdateMidiPath { get; set; } = string.Empty;

		// pro upgrade info, if it exists
		public SongProUpgrade SongUpgrade { get; set; } = new();
		public int[] RealGuitarTuning { get; set; }
		public int[] RealBassTuning { get; set; }

		// .mogg info
		public bool UsingUpdateMogg { get; set; } = false;
		public string MoggPath { get; set; }
		public int MoggHeader { get; set; }
		public int MoggAddressAudioOffset { get; set; }
		public long MoggAudioLength { get; set; }

		public Dictionary<SongStem, int[]> StemMaps { get; set; } = new();
		public float[,] MatrixRatios { get; set; }

		// image info
		public bool AlternatePath { get; set; } = false;
		public string ImagePath { get; set; } = string.Empty;

	}
}