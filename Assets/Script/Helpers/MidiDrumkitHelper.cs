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

        // Four-lane drum controllers can play both chart types, but should prefer Pro Drums.
        public static readonly Instrument[] FourLaneInstruments =
        {
            Instrument.ProDrums,
            Instrument.FourLaneDrums
        };

        public static Instrument[] GetInstruments(GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.EliteDrums => Instruments,
                GameMode.FourLaneDrums => FourLaneInstruments,
                _ => null
            };
        }

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
            return GetPreferredInstrumentForSong(song, Instruments);
        }

        public static Instrument? GetPreferredInstrumentForSong(SongEntry song, GameMode gameMode)
        {
            var instruments = GetInstruments(gameMode);
            return instruments == null ? null : GetPreferredInstrumentForSong(song, instruments);
        }

        public static Instrument? GetPreferredInstrumentForSong(SongEntry song, Instrument[] instruments)
        {
            foreach (var instrument in instruments)
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
