using YARG.Core;
using YARG.Core.Song;

namespace YARG.Helpers
{
    public static class MidiDrumkitHelper
    {
        // Preferred order for display and fallbacks when multiple drum charts exist.
        public static readonly Instrument[] Instruments =
        {
            Instrument.EliteDrums,
            Instrument.ProDrums,
            Instrument.FiveLaneDrums,
            Instrument.FourLaneDrums
        };

        public static bool HasAnyDrumPart(SongEntry song)
        {
            foreach (var instrument in Instruments)
            {
                if (song.HasInstrument(instrument))
                {
                    return true;
                }
            }

            return false;
        }

        public static Instrument? GetPreferredInstrumentForSong(SongEntry song)
        {
            foreach (var instrument in Instruments)
            {
                if (song.HasInstrument(instrument))
                {
                    return instrument;
                }
            }

            return null;
        }
    }
}
