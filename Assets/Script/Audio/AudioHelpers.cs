using System.Collections.Generic;
using System.IO;

namespace YARG.Audio
{
    public static class AudioHelpers
    {
        public static readonly Dictionary<string, SongStem> SupportedStems = new()
        {
            { "song", SongStem.Song },
            { "guitar", SongStem.Guitar },
            { "bass", SongStem.Bass },
            { "rhythm", SongStem.Rhythm },
            { "keys", SongStem.Keys },
            { "vocals", SongStem.Vocals },
            { "vocals_1", SongStem.Vocals1 },
            { "vocals_2", SongStem.Vocals2 },
            { "drums", SongStem.Drums },
            { "drums_1", SongStem.Drums1 },
            { "drums_2", SongStem.Drums2 },
            { "drums_3", SongStem.Drums3 },
            { "drums_4", SongStem.Drums4 },
            { "crowd", SongStem.Crowd },
            // "preview"
        };

        public static readonly IList<string> SfxPaths = new[]
        {
            "note_miss", "starpower_award", "starpower_gain", "starpower_deploy", "starpower_release", "clap", "star",
            "star_gold",
        };

        public static readonly IList<double> SfxVolume = new[]
        {
            0.5, 0.45, 0.5, 0.45, 0.5, 0.15, 1.0, 1.0,
        };

        public static readonly List<SongStem> PitchBendAllowedStems = new()
        {
            SongStem.Guitar,
            SongStem.Bass,
            SongStem.Rhythm,
        };

        public static IDictionary<SongStem, string> GetSupportedStems(string folder)
        {
            var stems = new Dictionary<SongStem, string>();
            foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
            {
                if (!GameManager.AudioManager.SupportedFormats.Contains(file.Extension.ToLowerInvariant()))
                {
                    continue;
                }

                if (SupportedStems.TryGetValue(Path.GetFileNameWithoutExtension(file.Name).ToLowerInvariant(), out var stem))
                    stems.TryAdd(stem, file.FullName);
            }

            return stems;
        }

        public static SongStem GetStemFromName(string stem)
        {
            return stem.ToLowerInvariant() switch
            {
                "song"     => SongStem.Song,
                "guitar"   => SongStem.Guitar,
                "bass"     => SongStem.Bass,
                "rhythm"   => SongStem.Rhythm,
                "keys"     => SongStem.Keys,
                "vocals"   => SongStem.Vocals,
                "vocals_1" => SongStem.Vocals1,
                "vocals_2" => SongStem.Vocals2,
                "drums"    => SongStem.Drums,
                "drums_1"  => SongStem.Drums1,
                "drums_2"  => SongStem.Drums2,
                "drums_3"  => SongStem.Drums3,
                "drums_4"  => SongStem.Drums4,
                "crowd"    => SongStem.Crowd,
                // "preview" => SongStem.Preview,
                _ => SongStem.Song,
            };
        }

        public static SfxSample GetSfxFromName(string sfx)
        {
            return sfx.ToLowerInvariant() switch
            {
                "note_miss"         => SfxSample.NoteMiss,
                "starpower_award"   => SfxSample.StarPowerAward,
                "starpower_gain"    => SfxSample.StarPowerGain,
                "starpower_deploy"  => SfxSample.StarPowerDeploy,
                "starpower_release" => SfxSample.StarPowerRelease,
                "clap"              => SfxSample.Clap,
                "star"              => SfxSample.StarGain,
                "star_gold"         => SfxSample.StarGold,
                _                   => SfxSample.NoteMiss,
            };
        }
    }
}