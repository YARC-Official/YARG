using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YARG {
	public static class AudioHelpers {
   
		public static readonly IList<string> SupportedStems = new[] {
			"song",
			"guitar",
			"bass",
			"rhythm",
			"keys",
			"vocals",
			"vocals_1",
			"vocals_2",
			"drums",
			"drums_1",
			"drums_2",
			"drums_3",
			"drums_4",
			"crowd",
			// "preview"
		};

		public static readonly IList<string> SfxPaths = new[] {
			"note_miss",
			"starpower_award",
			"starpower_gain",
			"starpower_deploy",
			"starpower_release",
			"clap"
		};

		public static readonly IList<double> SfxVolume = new[] {
			0.5,
			0.3,
			0.5,
			0.3,
			0.4,
			0.1,
		};

		public static IEnumerable<string> GetSupportedStems(string folder) {
			var stems = new List<string>();
			
			foreach (string filePath in Directory.GetFiles(folder)) {
				// Check if file format is supported
				if (!GameManager.AudioManager.SupportedFormats.Contains(Path.GetExtension(filePath).ToLower()))
					continue;

				// Check if file is a valid stem
				if (!SupportedStems.Contains(Path.GetFileNameWithoutExtension(filePath).ToLower()))
					continue;
				
				stems.Add(filePath);
			}

			return stems;
		}
		
		public static SongStem GetStemFromName(string stem) {
			switch (stem.ToLower()) {
				case "song":
					return SongStem.Song;
				case "guitar":
					return SongStem.Guitar;
				case "bass":
					return SongStem.Bass;
				case "rhythm":
					return SongStem.Rhythm;
				case "keys":
					return SongStem.Keys;
				case "vocals":
					return SongStem.Vocals;
				case "vocals_1":
					return SongStem.Vocals1;
				case "vocals_2":
					return SongStem.Vocals2;
				case "drums":
					return SongStem.Drums;
				case "drums_1":
					return SongStem.Drums1;
				case "drums_2":
					return SongStem.Drums2;
				case "drums_3":
					return SongStem.Drums3;
				case "drums_4":
					return SongStem.Drums4;
				case "crowd":
					return SongStem.Crowd;
				// case "preview":
				// 	return SongStems.Preview;
				default:
					return SongStem.Song;
			}
		}
	
		public static SfxSample GetSfxFromName(string sfx) {
			switch (sfx.ToLower()) {
				case "note_miss":
					return SfxSample.NoteMiss;
				case "starpower_award":
					return SfxSample.StarPowerAward;
				case "starpower_gain":
					return SfxSample.StarPowerGain;
				case "starpower_deploy":
					return SfxSample.StarPowerDeploy;
				case "starpower_release":
					return SfxSample.StarPowerRelease;
				case "clap":
					return SfxSample.Clap;
				default:
					return SfxSample.NoteMiss;
			}
		}
		
	}
}
