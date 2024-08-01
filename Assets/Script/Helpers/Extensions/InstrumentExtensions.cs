using System;
using YARG.Core;
using YARG.Song;

namespace YARG.Helpers.Extensions
{
    public static class InstrumentExtensions
    {
        public static SortAttribute ToSortAttribute(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar     => SortAttribute.FiveFretGuitar,
                Instrument.FiveFretBass       => SortAttribute.FiveFretBass,
                Instrument.FiveFretRhythm     => SortAttribute.FiveFretRhythm,
                Instrument.FiveFretCoopGuitar => SortAttribute.FiveFretCoop,
                Instrument.Keys               => SortAttribute.Keys,
                Instrument.SixFretGuitar      => SortAttribute.SixFretGuitar,
                Instrument.SixFretBass        => SortAttribute.SixFretBass,
                Instrument.SixFretRhythm      => SortAttribute.SixFretRhythm,
                Instrument.SixFretCoopGuitar  => SortAttribute.SixFretCoop,
                Instrument.FourLaneDrums      => SortAttribute.FourLaneDrums,
                Instrument.ProDrums           => SortAttribute.ProDrums,
                Instrument.FiveLaneDrums      => SortAttribute.FiveLaneDrums,
                Instrument.EliteDrums         => SortAttribute.EliteDrums,
                Instrument.ProGuitar_17Fret   => SortAttribute.ProGuitar_17,
                Instrument.ProGuitar_22Fret   => SortAttribute.ProGuitar_22,
                Instrument.ProBass_17Fret     => SortAttribute.ProBass_17,
                Instrument.ProBass_22Fret     => SortAttribute.ProBass_22,
                Instrument.ProKeys            => SortAttribute.ProKeys,
                Instrument.Vocals             => SortAttribute.Vocals,
                Instrument.Harmony            => SortAttribute.Harmony,
                Instrument.Band               => SortAttribute.Band,
                _ => throw new InvalidOperationException("Invalid instrument value")
            };
        }

         public static Instrument ToInstrument(this SortAttribute attribute)
        {
            return attribute switch
            {
                SortAttribute.FiveFretGuitar => Instrument.FiveFretGuitar,
                SortAttribute.FiveFretBass   => Instrument.FiveFretBass,
                SortAttribute.FiveFretRhythm => Instrument.FiveFretRhythm,
                SortAttribute.FiveFretCoop   => Instrument.FiveFretCoopGuitar,
                SortAttribute.Keys           => Instrument.Keys,
                SortAttribute.SixFretGuitar  => Instrument.SixFretGuitar,
                SortAttribute.SixFretBass    => Instrument.SixFretBass,
                SortAttribute.SixFretRhythm  => Instrument.SixFretRhythm,
                SortAttribute.SixFretCoop    => Instrument.SixFretCoopGuitar,
                SortAttribute.FourLaneDrums  => Instrument.FourLaneDrums,
                SortAttribute.ProDrums       => Instrument.ProDrums,
                SortAttribute.FiveLaneDrums  => Instrument.FiveLaneDrums,
                SortAttribute.EliteDrums     => Instrument.EliteDrums,
                SortAttribute.ProGuitar_17   => Instrument.ProGuitar_17Fret,
                SortAttribute.ProGuitar_22   => Instrument.ProGuitar_22Fret,
                SortAttribute.ProBass_17     => Instrument.ProBass_17Fret,
                SortAttribute.ProBass_22     => Instrument.ProBass_22Fret,
                SortAttribute.ProKeys        => Instrument.ProKeys,
                SortAttribute.Vocals         => Instrument.Vocals,
                SortAttribute.Harmony        => Instrument.Harmony,
                SortAttribute.Band           => Instrument.Band,
                _ => throw new NotImplementedException("SortAttribute was not an instrument type")
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
    }
}