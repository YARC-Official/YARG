using System.Collections.Generic;
using System.IO;

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
			"clap",
			"star",
			"star_gold",
		};

		public static readonly IList<double> SfxVolume = new[] {
			0.5,
			0.45,
			0.5,
			0.45,
			0.5,
			0.15,
			1.0,
			1.0,
		};

		public static ICollection<string> GetSupportedStems(string folder) {
			var stems = new List<string>();

			foreach (string filePath in Directory.GetFiles(folder)) {
				// Check if file format is supported
				if (!GameManager.AudioManager.SupportedFormats.Contains(Path.GetExtension(filePath).ToLowerInvariant())) {
					continue;
				}

				// Check if file is a valid stem
				if (!SupportedStems.Contains(Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant())) {
					continue;
				}

				stems.Add(filePath);
			}

			return stems;
		}
		
#pragma warning disable format

		public static SongStem GetStemFromName(string stem) {
			return stem.ToLowerInvariant() switch {
				"song"       => SongStem.Song,
				"guitar"     => SongStem.Guitar,
				"bass"       => SongStem.Bass,
				"rhythm"     => SongStem.Rhythm,
				"keys"       => SongStem.Keys,
				"vocals"     => SongStem.Vocals,
				"vocals_1"   => SongStem.Vocals1,
				"vocals_2"   => SongStem.Vocals2,
				"drums"      => SongStem.Drums,
				"drums_1"    => SongStem.Drums1,
				"drums_2"    => SongStem.Drums2,
				"drums_3"    => SongStem.Drums3,
				"drums_4"    => SongStem.Drums4,
				"crowd"      => SongStem.Crowd,
				// "preview" => SongStem.Preview,
				_            => SongStem.Song,
			};
		}

		public static SfxSample GetSfxFromName(string sfx) {
			return sfx.ToLowerInvariant() switch {
				"note_miss"         => SfxSample.NoteMiss,
				"starpower_award"   => SfxSample.StarPowerAward,
				"starpower_gain"    => SfxSample.StarPowerGain,
				"starpower_deploy"  => SfxSample.StarPowerDeploy,
				"starpower_release" => SfxSample.StarPowerRelease,
				"clap"              => SfxSample.Clap,
				"star"              => SfxSample.StarGain,
				"star_gold"              => SfxSample.StarGold,
				_                   => SfxSample.NoteMiss,
			};
		}
		
#pragma warning restore format
	}
}
