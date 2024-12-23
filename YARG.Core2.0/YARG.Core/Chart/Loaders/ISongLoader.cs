using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Interface used for loading chart files.
    /// </summary>
    internal interface ISongLoader
    {
        List<TextEvent> LoadGlobalEvents();
        List<Section> LoadSections();
        SyncTrack LoadSyncTrack();
        VenueTrack LoadVenueTrack();
        LyricsTrack LoadLyrics();

        InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument instrument);
        InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument instrument);
        InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument instrument);
        InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument);
        VocalsTrack LoadVocalsTrack(Instrument instrument);
    }
}