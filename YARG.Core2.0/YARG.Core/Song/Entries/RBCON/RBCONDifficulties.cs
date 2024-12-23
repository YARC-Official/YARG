using System;

namespace YARG.Core.Song
{
    [Serializable]
    public struct RBCONDifficulties
    {
        public static readonly RBCONDifficulties Default = new()
        {
            Band = -1,
            FiveFretGuitar = -1,
            FiveFretBass = -1,
            FiveFretRhythm = -1,
            FiveFretCoop = -1,
            Keys = -1,
            FourLaneDrums = -1,
            ProDrums = -1,
            ProGuitar = -1,
            ProBass = -1,
            ProKeys = -1,
            LeadVocals = -1,
            HarmonyVocals = -1,
        };

        public short Band;
        public short FiveFretGuitar;
        public short FiveFretBass;
        public short FiveFretRhythm;
        public short FiveFretCoop;
        public short Keys;
        public short FourLaneDrums;
        public short ProDrums;
        public short ProGuitar;
        public short ProBass;
        public short ProKeys;
        public short LeadVocals;
        public short HarmonyVocals;
    }
}
