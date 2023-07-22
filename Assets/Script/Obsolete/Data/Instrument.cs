using System;
using YARG.Core;

namespace YARG.Data
{
    public static class InstrumentHelper
    {
#pragma warning disable format

        public static string ToLocalizedName(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar     => "Guitar",
                Instrument.FiveFretBass       => "Bass",
                Instrument.FiveFretRhythm     => "Rhythm Guitar",
                Instrument.FiveFretCoopGuitar => "Co-op Guitar",
                Instrument.Keys               => "Keys",

                Instrument.FourLaneDrums => "Drums",
                Instrument.ProDrums      => "Pro Drums",
                Instrument.FiveLaneDrums => "5-lane Drums",

                Instrument.ProGuitar_17Fret => "Pro Guitar",
                Instrument.ProBass_17Fret   => "Pro Bass",
                Instrument.ProKeys          => "Pro Keys",

                Instrument.Vocals  => "Vocals",
                Instrument.Harmony => "Harmony",

                _ => null,
            };
        }

        public static string ToResourceName(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar     => "guitar",
                Instrument.FiveFretBass       => "bass",
                Instrument.FiveFretRhythm     => "rhythm",
                Instrument.FiveFretCoopGuitar => "guitarCoop",
                Instrument.Keys               => "keys",

                Instrument.FourLaneDrums => "drums",
                Instrument.ProDrums      => "realDrums",
                Instrument.FiveLaneDrums => "ghDrums",

                Instrument.ProGuitar_17Fret => "realGuitar",
                Instrument.ProBass_17Fret   => "realBass",
                Instrument.ProKeys          => "realKeys",

                Instrument.Vocals  => "vocals",
                Instrument.Harmony => "harmVocals",

                _ => null,
            };
        }

        public static Instrument FromResourceName(string name)
        {
            return name switch
            {
                "guitar"     => Instrument.FiveFretGuitar,
                "bass"       => Instrument.FiveFretBass,
                "rhythm"     => Instrument.FiveFretRhythm,
                "guitarCoop" => Instrument.FiveFretCoopGuitar,
                "keys"       => Instrument.Keys,

                "drums"     => Instrument.FourLaneDrums,
                "realDrums" => Instrument.ProDrums,
                "ghDrums"   => Instrument.FiveLaneDrums,

                "realGuitar" => Instrument.ProGuitar_17Fret,
                "realBass"   => Instrument.ProBass_17Fret,
                "realKeys"   => Instrument.ProKeys,

                "vocals"     => Instrument.Vocals,
                "harmVocals" => Instrument.Harmony,

                _ => throw new NotImplementedException($"Unhandled instrument resource name {name}!"),
            };
        }

#pragma warning restore format
    }
}