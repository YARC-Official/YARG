namespace YARG.Data
{
    /*
    
    TODO: Also switch this out for players. Will wait until merge.

    */

    public enum Instrument
    {
        INVALID = -1,

        GUITAR,
        BASS,
        DRUMS,
        KEYS,
        VOCALS,

        REAL_GUITAR,
        REAL_BASS,
        REAL_DRUMS,
        REAL_KEYS,
        HARMONY,

        GH_DRUMS,
        RHYTHM,
        GUITAR_COOP,
    }

    public static class InstrumentHelper
    {
#pragma warning disable format

        public static string ToLocalizedName(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.GUITAR      => "Guitar",
                Instrument.BASS        => "Bass",
                Instrument.DRUMS       => "Drums",
                Instrument.KEYS        => "Keys",
                Instrument.VOCALS      => "Vocals",
                Instrument.REAL_GUITAR => "Pro Guitar",
                Instrument.REAL_BASS   => "Pro Bass",
                Instrument.REAL_DRUMS  => "Pro Drums",
                Instrument.REAL_KEYS   => "Pro Keys",
                Instrument.HARMONY     => "Harmony",
                Instrument.GH_DRUMS    => "5-lane Drums",
                Instrument.RHYTHM      => "Rhythm Guitar",
                Instrument.GUITAR_COOP => "Co-op Guitar",
                _                      => null,
            };
        }

        public static string ToStringName(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.GUITAR      => "guitar",
                Instrument.BASS        => "bass",
                Instrument.DRUMS       => "drums",
                Instrument.KEYS        => "keys",
                Instrument.VOCALS      => "vocals",
                Instrument.REAL_GUITAR => "realGuitar",
                Instrument.REAL_BASS   => "realBass",
                Instrument.REAL_DRUMS  => "realDrums",
                Instrument.REAL_KEYS   => "realKeys",
                Instrument.HARMONY     => "harmVocals",
                Instrument.GH_DRUMS    => "ghDrums",
                Instrument.RHYTHM      => "rhythm",
                Instrument.GUITAR_COOP => "guitarCoop",
                _                      => null,
            };
        }

        public static string ToSongIniName(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.GUITAR      => "diff_guitar",
                Instrument.BASS        => "diff_bass",
                Instrument.DRUMS       => "diff_drums",
                Instrument.KEYS        => "diff_keys",
                Instrument.VOCALS      => "diff_vocals",
                Instrument.REAL_GUITAR => "diff_guitar_real",
                Instrument.REAL_BASS   => "diff_bass_real",
                Instrument.REAL_DRUMS  => "diff_drums_real",
                Instrument.REAL_KEYS   => "diff_keys_real",
                Instrument.HARMONY     => "diff_vocals_harm",
                Instrument.RHYTHM      => "diff_rhythm",
                Instrument.GUITAR_COOP => "diff_guitarCoop",
                _                      => null,
            };
        }

        public static Instrument FromStringName(string name)
        {
            return name switch
            {
                // Recommended names
                "guitar"     => Instrument.GUITAR,
                "bass"       => Instrument.BASS,
                "drums"      => Instrument.DRUMS,
                "keys"       => Instrument.KEYS,
                "vocals"     => Instrument.VOCALS,
                "realGuitar" => Instrument.REAL_GUITAR,
                "realBass"   => Instrument.REAL_BASS,
                "realDrums"  => Instrument.REAL_DRUMS,
                "realKeys"   => Instrument.REAL_KEYS,
                "harmVocals" => Instrument.HARMONY,
                "ghDrums"    => Instrument.GH_DRUMS,
                "rhythm"     => Instrument.RHYTHM,
                "guitarCoop" => Instrument.GUITAR_COOP,

                // Alternate names
                "drum"        => Instrument.DRUMS,
                "real_guitar" => Instrument.REAL_GUITAR,
                "real_bass"   => Instrument.REAL_BASS,
                "real_drums"  => Instrument.REAL_DRUMS,
                "real_keys"   => Instrument.REAL_KEYS,
                "vocal_harm"  => Instrument.HARMONY,

                _ => Instrument.INVALID,
            };
        }

#pragma warning restore format
    }
}