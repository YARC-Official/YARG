using System;

namespace YARG.Core.Song
{
    [Serializable]
    public partial struct AvailableParts
    {
        public static readonly AvailableParts Default = new()
        {
            BandDifficulty = PartValues.Default,
            FiveFretGuitar = PartValues.Default,
            FiveFretBass = PartValues.Default,
            FiveFretRhythm = PartValues.Default,
            FiveFretCoopGuitar = PartValues.Default,
            Keys = PartValues.Default,

            SixFretGuitar = PartValues.Default,
            SixFretBass = PartValues.Default,
            SixFretRhythm = PartValues.Default,
            SixFretCoopGuitar = PartValues.Default,

            FourLaneDrums = PartValues.Default,
            ProDrums = PartValues.Default,
            FiveLaneDrums = PartValues.Default,

            EliteDrums = PartValues.Default,

            ProGuitar_17Fret = PartValues.Default,
            ProGuitar_22Fret = PartValues.Default,
            ProBass_17Fret = PartValues.Default,
            ProBass_22Fret = PartValues.Default,

            ProKeys = PartValues.Default,

            // DJ = PartValues.Default,

            LeadVocals = PartValues.Default,
            HarmonyVocals = PartValues.Default,
        };

        public PartValues BandDifficulty;

        public PartValues FiveFretGuitar;
        public PartValues FiveFretBass;
        public PartValues FiveFretRhythm;
        public PartValues FiveFretCoopGuitar;
        public PartValues Keys;

        public PartValues SixFretGuitar;
        public PartValues SixFretBass;
        public PartValues SixFretRhythm;
        public PartValues SixFretCoopGuitar;

        public PartValues FourLaneDrums;
        public PartValues ProDrums;
        public PartValues FiveLaneDrums;

        public PartValues EliteDrums;

        public PartValues ProGuitar_17Fret;
        public PartValues ProGuitar_22Fret;
        public PartValues ProBass_17Fret;
        public PartValues ProBass_22Fret;

        public PartValues ProKeys;

        // private PartValues DJ;

        public PartValues LeadVocals;
        public PartValues HarmonyVocals;
    }
}