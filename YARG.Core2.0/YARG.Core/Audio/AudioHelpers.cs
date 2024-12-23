using System;
using System.Collections.Generic;

namespace YARG.Core.Audio
{
    public static class AudioHelpers
    {
        public static readonly Dictionary<string, SongStem> SupportedStems = new()
        {
            { "song",     SongStem.Song    },
            { "guitar",   SongStem.Guitar  },
            { "bass",     SongStem.Bass    },
            { "rhythm",   SongStem.Rhythm  },
            { "keys",     SongStem.Keys    },
            { "vocals",   SongStem.Vocals  },
            { "vocals_1", SongStem.Vocals1 },
            { "vocals_2", SongStem.Vocals2 },
            { "drums",    SongStem.Drums   },
            { "drums_1",  SongStem.Drums1  },
            { "drums_2",  SongStem.Drums2  },
            { "drums_3",  SongStem.Drums3  },
            { "drums_4",  SongStem.Drums4  },
            { "crowd",    SongStem.Crowd   },
            // "preview"
        };

        public static readonly IList<string> SfxPaths = new[]
        {
            "note_miss",
            "starpower_award",
            "starpower_gain",
            "starpower_deploy",
            "starpower_release",
            "clap",
            "star",
            "star_gold",
            "overstrum_1",
            "overstrum_2",
            "overstrum_3",
            "overstrum_4",
        };

        public static readonly IList<double> SfxVolume = new[]
        {
            0.55,
            0.5,
            0.5,
            0.4,
            0.5,
            0.16,
            1.0,
            1.0,
            0.4,
            0.4,
            0.4,
            0.4,
        };

        public static readonly List<SongStem> PitchBendAllowedStems = new()
        {
            SongStem.Guitar,
            SongStem.Bass,
            SongStem.Rhythm,
        };

        public static SongStem GetStemFromName(string stem)
        {
            return stem.ToLowerInvariant() switch
            {
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
                _ => SongStem.Song,
            };
        }

        public static SongStem ToSongStem(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or
                Instrument.SixFretGuitar or
                Instrument.ProGuitar_17Fret or
                Instrument.ProGuitar_22Fret => SongStem.Guitar,

                Instrument.FiveFretBass or
                Instrument.SixFretBass or
                Instrument.ProBass_17Fret or
                Instrument.ProBass_22Fret => SongStem.Bass,

                Instrument.FiveFretRhythm or
                Instrument.SixFretRhythm or
                Instrument.FiveFretCoopGuitar or
                Instrument.SixFretCoopGuitar => SongStem.Rhythm,

                Instrument.Keys or
                Instrument.ProKeys => SongStem.Keys,

                Instrument.ProDrums or
                Instrument.FourLaneDrums or
                Instrument.FiveLaneDrums => SongStem.Drums,

                Instrument.Vocals or
                Instrument.Harmony => SongStem.Vocals,

                _ => throw new Exception("Unreachable.")
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
                "overstrum_1"       => SfxSample.Overstrum1,
                "overstrum_2"       => SfxSample.Overstrum2,
                "overstrum_3"       => SfxSample.Overstrum3,
                "overstrum_4"       => SfxSample.Overstrum4,
                _                   => SfxSample.NoteMiss,
            };
        }

        // Drum SFX related
        public static readonly IList<string> DrumSfxPaths = new[]
        {
            "vel0pad0smp0",
            "vel0pad0smp1",
            "vel0pad0smp2",
            "vel0pad1smp0",
            "vel0pad1smp1",
            "vel0pad1smp2",
            "vel0pad2smp0",
            "vel0pad2smp1",
            "vel0pad2smp2",
            "vel0pad3smp0",
            "vel0pad3smp1",
            "vel0pad3smp2",
            "vel0pad4smp0",
            "vel0pad4smp1",
            "vel0pad4smp2",
            "vel0pad5smp0",
            "vel0pad5smp1",
            "vel0pad5smp2",
            "vel0pad6smp0",
            "vel0pad6smp1",
            "vel0pad6smp2",
            "vel0pad7smp0",
            "vel0pad7smp1",
            "vel0pad7smp2",
            "vel1pad0smp0",
            "vel1pad0smp1",
            "vel1pad0smp2",
            "vel1pad1smp0",
            "vel1pad1smp1",
            "vel1pad1smp2",
            "vel1pad2smp0",
            "vel1pad2smp1",
            "vel1pad2smp2",
            "vel1pad3smp0",
            "vel1pad3smp1",
            "vel1pad3smp2",
            "vel1pad4smp0",
            "vel1pad4smp1",
            "vel1pad4smp2",
            "vel1pad5smp0",
            "vel1pad5smp1",
            "vel1pad5smp2",
            "vel1pad6smp0",
            "vel1pad6smp1",
            "vel1pad6smp2",
            "vel1pad7smp0",
            "vel1pad7smp1",
            "vel1pad7smp2",
            "vel2pad0smp0",
            "vel2pad0smp1",
            "vel2pad0smp2",
            "vel2pad1smp0",
            "vel2pad1smp1",
            "vel2pad1smp2",
            "vel2pad2smp0",
            "vel2pad2smp1",
            "vel2pad2smp2",
            "vel2pad3smp0",
            "vel2pad3smp1",
            "vel2pad3smp2",
            "vel2pad4smp0",
            "vel2pad4smp1",
            "vel2pad4smp2",
            "vel2pad5smp0",
            "vel2pad5smp1",
            "vel2pad5smp2",
            "vel2pad6smp0",
            "vel2pad6smp1",
            "vel2pad6smp2",
            "vel2pad7smp0",
            "vel2pad7smp1",
            "vel2pad7smp2",
        };

        public static DrumSfxSample GetDrumSfxFromName(string sfx)
        {
            return sfx.ToLowerInvariant() switch
            {
                "vel0pad0smp0" => DrumSfxSample.Vel0Pad0Smp0,
                "vel0pad0smp1" => DrumSfxSample.Vel0Pad0Smp1,
                "vel0pad0smp2" => DrumSfxSample.Vel0Pad0Smp2,
                "vel0pad1smp0" => DrumSfxSample.Vel0Pad1Smp0,
                "vel0pad1smp1" => DrumSfxSample.Vel0Pad1Smp1,
                "vel0pad1smp2" => DrumSfxSample.Vel0Pad1Smp2,
                "vel0pad2smp0" => DrumSfxSample.Vel0Pad2Smp0,
                "vel0pad2smp1" => DrumSfxSample.Vel0Pad2Smp1,
                "vel0pad2smp2" => DrumSfxSample.Vel0Pad2Smp2,
                "vel0pad3smp0" => DrumSfxSample.Vel0Pad3Smp0,
                "vel0pad3smp1" => DrumSfxSample.Vel0Pad3Smp1,
                "vel0pad3smp2" => DrumSfxSample.Vel0Pad3Smp2,
                "vel0pad4smp0" => DrumSfxSample.Vel0Pad4Smp0,
                "vel0pad4smp1" => DrumSfxSample.Vel0Pad4Smp1,
                "vel0pad4smp2" => DrumSfxSample.Vel0Pad4Smp2,
                "vel0pad5smp0" => DrumSfxSample.Vel0Pad5Smp0,
                "vel0pad5smp1" => DrumSfxSample.Vel0Pad5Smp1,
                "vel0pad5smp2" => DrumSfxSample.Vel0Pad5Smp2,
                "vel0pad6smp0" => DrumSfxSample.Vel0Pad6Smp0,
                "vel0pad6smp1" => DrumSfxSample.Vel0Pad6Smp1,
                "vel0pad6smp2" => DrumSfxSample.Vel0Pad6Smp2,
                "vel0pad7smp0" => DrumSfxSample.Vel0Pad7Smp0,
                "vel0pad7smp1" => DrumSfxSample.Vel0Pad7Smp1,
                "vel0pad7smp2" => DrumSfxSample.Vel0Pad7Smp2,
                "vel1pad0smp0" => DrumSfxSample.Vel1Pad0Smp0,
                "vel1pad0smp1" => DrumSfxSample.Vel1Pad0Smp1,
                "vel1pad0smp2" => DrumSfxSample.Vel1Pad0Smp2,
                "vel1pad1smp0" => DrumSfxSample.Vel1Pad1Smp0,
                "vel1pad1smp1" => DrumSfxSample.Vel1Pad1Smp1,
                "vel1pad1smp2" => DrumSfxSample.Vel1Pad1Smp2,
                "vel1pad2smp0" => DrumSfxSample.Vel1Pad2Smp0,
                "vel1pad2smp1" => DrumSfxSample.Vel1Pad2Smp1,
                "vel1pad2smp2" => DrumSfxSample.Vel1Pad2Smp2,
                "vel1pad3smp0" => DrumSfxSample.Vel1Pad3Smp0,
                "vel1pad3smp1" => DrumSfxSample.Vel1Pad3Smp1,
                "vel1pad3smp2" => DrumSfxSample.Vel1Pad3Smp2,
                "vel1pad4smp0" => DrumSfxSample.Vel1Pad4Smp0,
                "vel1pad4smp1" => DrumSfxSample.Vel1Pad4Smp1,
                "vel1pad4smp2" => DrumSfxSample.Vel1Pad4Smp2,
                "vel1pad5smp0" => DrumSfxSample.Vel1Pad5Smp0,
                "vel1pad5smp1" => DrumSfxSample.Vel1Pad5Smp1,
                "vel1pad5smp2" => DrumSfxSample.Vel1Pad5Smp2,
                "vel1pad6smp0" => DrumSfxSample.Vel1Pad6Smp0,
                "vel1pad6smp1" => DrumSfxSample.Vel1Pad6Smp1,
                "vel1pad6smp2" => DrumSfxSample.Vel1Pad6Smp2,
                "vel1pad7smp0" => DrumSfxSample.Vel1Pad7Smp0,
                "vel1pad7smp1" => DrumSfxSample.Vel1Pad7Smp1,
                "vel1pad7smp2" => DrumSfxSample.Vel1Pad7Smp2,
                "vel2pad0smp0" => DrumSfxSample.Vel2Pad0Smp0,
                "vel2pad0smp1" => DrumSfxSample.Vel2Pad0Smp1,
                "vel2pad0smp2" => DrumSfxSample.Vel2Pad0Smp2,
                "vel2pad1smp0" => DrumSfxSample.Vel2Pad1Smp0,
                "vel2pad1smp1" => DrumSfxSample.Vel2Pad1Smp1,
                "vel2pad1smp2" => DrumSfxSample.Vel2Pad1Smp2,
                "vel2pad2smp0" => DrumSfxSample.Vel2Pad2Smp0,
                "vel2pad2smp1" => DrumSfxSample.Vel2Pad2Smp1,
                "vel2pad2smp2" => DrumSfxSample.Vel2Pad2Smp2,
                "vel2pad3smp0" => DrumSfxSample.Vel2Pad3Smp0,
                "vel2pad3smp1" => DrumSfxSample.Vel2Pad3Smp1,
                "vel2pad3smp2" => DrumSfxSample.Vel2Pad3Smp2,
                "vel2pad4smp0" => DrumSfxSample.Vel2Pad4Smp0,
                "vel2pad4smp1" => DrumSfxSample.Vel2Pad4Smp1,
                "vel2pad4smp2" => DrumSfxSample.Vel2Pad4Smp2,
                "vel2pad5smp0" => DrumSfxSample.Vel2Pad5Smp0,
                "vel2pad5smp1" => DrumSfxSample.Vel2Pad5Smp1,
                "vel2pad5smp2" => DrumSfxSample.Vel2Pad5Smp2,
                "vel2pad6smp0" => DrumSfxSample.Vel2Pad6Smp0,
                "vel2pad6smp1" => DrumSfxSample.Vel2Pad6Smp1,
                "vel2pad6smp2" => DrumSfxSample.Vel2Pad6Smp2,
                "vel2pad7smp0" => DrumSfxSample.Vel2Pad7Smp0,
                "vel2pad7smp1" => DrumSfxSample.Vel2Pad7Smp1,
                "vel2pad7smp2" => DrumSfxSample.Vel2Pad7Smp2,
                _              => DrumSfxSample.Vel0Pad0Smp0,
            };
        }
    }
}