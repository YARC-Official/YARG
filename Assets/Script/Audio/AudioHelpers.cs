using System.Collections.Generic;
using System.IO;
using YARG.Data;
using Random = UnityEngine.Random;

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
			"menu_navigation",
			"menu_select_gtr_1",
			"menu_select_gtr_2",
			"menu_select_gtr_3",
			"menu_select_gtr_4",
			"menu_back_gtr_1",
			"menu_back_gtr_2",
			"menu_back_gtr_3",
			"menu_back_gtr_4",
			"menu_select_bass_1",
			"menu_select_bass_2",
			"menu_select_bass_3",
			"menu_select_bass_4",
			"menu_back_bass_1",
			"menu_back_bass_2",
			"menu_back_bass_3",
			"menu_back_bass_4",
			"menu_select_keys_1",
			"menu_select_keys_2",
			"menu_select_keys_3",
			"menu_select_keys_4",
			"menu_back_keys_1",
			"menu_back_keys_2",
			"menu_back_keys_3",
			"menu_back_keys_4",
			"menu_select_drums_1",
			"menu_select_drums_2",
			"menu_select_drums_3",
			"menu_select_drums_4",
			"menu_back_drums_1",
			"menu_back_drums_2",
			"menu_back_drums_3",
			"menu_back_drums_4",
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
			0.5,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
			Constants.SELECT_BACK_SFX_VOLUME,
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
				"note_miss"           => SfxSample.NoteMiss,
				"starpower_award"     => SfxSample.StarPowerAward,
				"starpower_gain"      => SfxSample.StarPowerGain,
				"starpower_deploy"    => SfxSample.StarPowerDeploy,
				"starpower_release"   => SfxSample.StarPowerRelease,
				"clap"                => SfxSample.Clap,
				"star"                => SfxSample.StarGain,
				"star_gold"           => SfxSample.StarGold,
				"menu_navigation"     => SfxSample.MenuNavigation,
				"menu_select_gtr_1"   => SfxSample.SelectGtr1,
				"menu_select_gtr_2"   => SfxSample.SelectGtr2,
				"menu_select_gtr_3"   => SfxSample.SelectGtr3,
				"menu_select_gtr_4"   => SfxSample.SelectGtr4,
				"menu_back_gtr_1"     => SfxSample.BackGtr1,
				"menu_back_gtr_2"     => SfxSample.BackGtr2,
				"menu_back_gtr_3"     => SfxSample.BackGtr3,
				"menu_back_gtr_4"     => SfxSample.BackGtr4,
				"menu_select_bass_1"  => SfxSample.SelectBass1,
				"menu_select_bass_2"  => SfxSample.SelectBass2,
				"menu_select_bass_3"  => SfxSample.SelectBass3,
				"menu_select_bass_4"  => SfxSample.SelectBass4,
				"menu_back_bass_1"    => SfxSample.BackBass1,
				"menu_back_bass_2"    => SfxSample.BackBass2,
				"menu_back_bass_3"    => SfxSample.BackBass3,
				"menu_back_bass_4"    => SfxSample.BackBass4,
				"menu_select_keys_1"  => SfxSample.SelectKeys1,
				"menu_select_keys_2"  => SfxSample.SelectKeys2,
				"menu_select_keys_3"  => SfxSample.SelectKeys3,
				"menu_select_keys_4"  => SfxSample.SelectKeys4,
				"menu_back_keys_1"    => SfxSample.BackKeys1,
				"menu_back_keys_2"    => SfxSample.BackKeys2,
				"menu_back_keys_3"    => SfxSample.BackKeys3,
				"menu_back_keys_4"    => SfxSample.BackKeys4,
				"menu_select_drums_1" => SfxSample.SelectDrums1,
				"menu_select_drums_2" => SfxSample.SelectDrums2,
				"menu_select_drums_3" => SfxSample.SelectDrums3,
				"menu_select_drums_4" => SfxSample.SelectDrums4,
				"menu_back_drums_1"   => SfxSample.BackDrums1,
				"menu_back_drums_2"   => SfxSample.BackDrums2,
				"menu_back_drums_3"   => SfxSample.BackDrums3,
				"menu_back_drums_4"   => SfxSample.BackDrums4,
				_                     => SfxSample.NoteMiss,
			};
		}

		public static SfxSample GetSelectSfxFromInstrument(Instrument instrument) {
			var instrumentSfx = instrument.ToSfxName();
			var selectIndex = Random.Range(1, 5);
			var sfx = $"menu_select_{instrumentSfx}_{selectIndex}";
			return GetSfxFromName(sfx);
		}
		
		public static SfxSample GetBackSfxFromInstrument(Instrument instrument) {
			var instrumentSfx = instrument.ToSfxName();
			var selectIndex = Random.Range(1, 5);
			var sfx = $"menu_back_{instrumentSfx}_{selectIndex}";
			return GetSfxFromName(sfx);
		}
		
#pragma warning restore format
	}
}
