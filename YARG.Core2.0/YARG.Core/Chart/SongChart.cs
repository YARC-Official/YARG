using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Logging;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The chart data for a song.
    /// </summary>
    public partial class SongChart
    {
        public uint Resolution => SyncTrack.Resolution;

        public List<TextEvent> GlobalEvents { get; set; } = new();
        public List<Section> Sections { get; set; } = new();

        public SyncTrack SyncTrack { get; set; }
        public VenueTrack VenueTrack { get; set; } = new();
        public LyricsTrack Lyrics { get; set; } = new();

        public InstrumentTrack<GuitarNote> FiveFretGuitar { get; set; } = new(Instrument.FiveFretGuitar);
        public InstrumentTrack<GuitarNote> FiveFretCoop { get; set; } = new(Instrument.FiveFretCoopGuitar);
        public InstrumentTrack<GuitarNote> FiveFretRhythm { get; set; } = new(Instrument.FiveFretRhythm);
        public InstrumentTrack<GuitarNote> FiveFretBass { get; set; } = new(Instrument.FiveFretBass);
        public InstrumentTrack<GuitarNote> Keys { get; set; } = new(Instrument.Keys);

        public IEnumerable<InstrumentTrack<GuitarNote>> FiveFretTracks
        {
            get
            {
                yield return FiveFretGuitar;
                yield return FiveFretCoop;
                yield return FiveFretRhythm;
                yield return FiveFretBass;
                yield return Keys;
            }
        }

        // Not supported yet
        public InstrumentTrack<GuitarNote> SixFretGuitar { get; set; } = new(Instrument.SixFretGuitar);
        public InstrumentTrack<GuitarNote> SixFretCoop { get; set; } = new(Instrument.SixFretCoopGuitar);
        public InstrumentTrack<GuitarNote> SixFretRhythm { get; set; } = new(Instrument.SixFretRhythm);
        public InstrumentTrack<GuitarNote> SixFretBass { get; set; } = new(Instrument.SixFretBass);

        public IEnumerable<InstrumentTrack<GuitarNote>> SixFretTracks
        {
            get
            {
                yield return SixFretGuitar;
                yield return SixFretCoop;
                yield return SixFretRhythm;
                yield return SixFretBass;
            }
        }

        public InstrumentTrack<DrumNote> FourLaneDrums { get; set; } = new(Instrument.FourLaneDrums);
        public InstrumentTrack<DrumNote> ProDrums { get; set; } = new(Instrument.ProDrums);
        public InstrumentTrack<DrumNote> FiveLaneDrums { get; set; } = new(Instrument.FiveLaneDrums);

        // public InstrumentTrack<DrumNote> EliteDrums { get; set; } = new(Instrument.EliteDrums);

        public IEnumerable<InstrumentTrack<DrumNote>> DrumsTracks
        {
            get
            {
                yield return FourLaneDrums;
                yield return ProDrums;
                yield return FiveLaneDrums;
            }
        }

        public InstrumentTrack<ProGuitarNote> ProGuitar_17Fret { get; set; } = new(Instrument.ProGuitar_17Fret);
        public InstrumentTrack<ProGuitarNote> ProGuitar_22Fret { get; set; } = new(Instrument.ProGuitar_22Fret);
        public InstrumentTrack<ProGuitarNote> ProBass_17Fret { get; set; } = new(Instrument.ProBass_17Fret);
        public InstrumentTrack<ProGuitarNote> ProBass_22Fret { get; set; } = new(Instrument.ProBass_22Fret);

        public IEnumerable<InstrumentTrack<ProGuitarNote>> ProGuitarTracks
        {
            get
            {
                yield return ProGuitar_17Fret;
                yield return ProGuitar_22Fret;
                yield return ProBass_17Fret;
                yield return ProBass_22Fret;
            }
        }

        public InstrumentTrack<ProKeysNote> ProKeys { get; set; } = new(Instrument.ProKeys);

        public VocalsTrack Vocals { get; set; } = new(Instrument.Vocals);
        public VocalsTrack Harmony { get; set; } = new(Instrument.Harmony);

        public IEnumerable<VocalsTrack> VocalsTracks
        {
            get
            {
                yield return Vocals;
                yield return Harmony;
            }
        }

        // public InstrumentTrack<DjNote> Dj { get; set; } = new(Instrument.Dj);

        // To explicitly allow creation without going through a file
        public SongChart(uint resolution)
        {
            SyncTrack = new(resolution);
        }

        internal SongChart(ISongLoader loader)
        {
            GlobalEvents = loader.LoadGlobalEvents();
            SyncTrack = loader.LoadSyncTrack();
            VenueTrack = loader.LoadVenueTrack();
            Sections = loader.LoadSections();
            Lyrics = loader.LoadLyrics();

            FiveFretGuitar = loader.LoadGuitarTrack(Instrument.FiveFretGuitar);
            FiveFretCoop = loader.LoadGuitarTrack(Instrument.FiveFretCoopGuitar);
            FiveFretRhythm = loader.LoadGuitarTrack(Instrument.FiveFretRhythm);
            FiveFretBass = loader.LoadGuitarTrack(Instrument.FiveFretBass);
            Keys = loader.LoadGuitarTrack(Instrument.Keys);

            SixFretGuitar = loader.LoadGuitarTrack(Instrument.SixFretGuitar);
            SixFretCoop = loader.LoadGuitarTrack(Instrument.SixFretCoopGuitar);
            SixFretRhythm = loader.LoadGuitarTrack(Instrument.SixFretRhythm);
            SixFretBass = loader.LoadGuitarTrack(Instrument.SixFretBass);

            FourLaneDrums = loader.LoadDrumsTrack(Instrument.FourLaneDrums);
            ProDrums = loader.LoadDrumsTrack(Instrument.ProDrums);
            FiveLaneDrums = loader.LoadDrumsTrack(Instrument.FiveLaneDrums);

            // EliteDrums = loader.LoadDrumsTrack(Instrument.EliteDrums);

            ProGuitar_17Fret = loader.LoadProGuitarTrack(Instrument.ProGuitar_17Fret);
            ProGuitar_22Fret = loader.LoadProGuitarTrack(Instrument.ProGuitar_22Fret);
            ProBass_17Fret = loader.LoadProGuitarTrack(Instrument.ProBass_17Fret);
            ProBass_22Fret = loader.LoadProGuitarTrack(Instrument.ProBass_22Fret);

            ProKeys = loader.LoadProKeysTrack(Instrument.ProKeys);

            Vocals = loader.LoadVocalsTrack(Instrument.Vocals);
            Harmony = loader.LoadVocalsTrack(Instrument.Harmony);

            // Dj = loader.LoadDjTrack(Instrument.Dj);

            // Ensure beatlines are present
            if (SyncTrack.Beatlines is null or { Count: < 1 })
            {
                SyncTrack.GenerateBeatlines(GetLastTick());
            }

            // Use beatlines to place auto-generated drum activation phrases for charts without manually authored phrases
            CreateDrumActivationPhrases();

            PostProcessSections();
            FixDrumPhraseEnds();
        }

        public void Append(SongChart song)
        {
            if (!song.FiveFretGuitar.IsEmpty)
                FiveFretGuitar = song.FiveFretGuitar;

            if (!song.FiveFretCoop.IsEmpty)
                FiveFretCoop = song.FiveFretCoop;

            if (!song.FiveFretRhythm.IsEmpty)
                FiveFretRhythm = song.FiveFretRhythm;

            if (!song.FiveFretBass.IsEmpty)
                FiveFretBass = song.FiveFretBass;

            if (!song.Keys.IsEmpty)
                Keys = song.Keys;

            if (!song.SixFretGuitar.IsEmpty)
                SixFretGuitar = song.SixFretGuitar;

            if (!song.SixFretCoop.IsEmpty)
                SixFretCoop = song.SixFretCoop;

            if (!song.SixFretRhythm.IsEmpty)
                SixFretRhythm = song.SixFretRhythm;

            if (!song.SixFretBass.IsEmpty)
                SixFretBass = song.SixFretBass;

            if (!song.FourLaneDrums.IsEmpty)
                FourLaneDrums = song.FourLaneDrums;

            if (!song.ProDrums.IsEmpty)
                ProDrums = song.ProDrums;

            if (!song.FiveLaneDrums.IsEmpty)
                FiveLaneDrums = song.FiveLaneDrums;

            if (!song.ProGuitar_17Fret.IsEmpty)
                ProGuitar_17Fret = song.ProGuitar_17Fret;

            if (!song.ProGuitar_22Fret.IsEmpty)
                ProGuitar_22Fret = song.ProGuitar_22Fret;

            if (!song.ProBass_17Fret.IsEmpty)
                ProBass_17Fret = song.ProBass_17Fret;

            if (!song.ProBass_22Fret.IsEmpty)
                ProBass_22Fret = song.ProBass_22Fret;

            if (!song.ProKeys.IsEmpty)
                ProKeys = song.ProKeys;

            if (!song.Vocals.IsEmpty)
                Vocals = song.Vocals;

            if (!song.Harmony.IsEmpty)
                Harmony = song.Harmony;
        }

        public static SongChart FromFile(in ParseSettings settings, string filePath)
        {
            var loader = MoonSongLoader.LoadSong(settings, filePath);
            return new(loader);
        }

        public static SongChart FromMidi(in ParseSettings settings, MidiFile midi)
        {
            var loader = MoonSongLoader.LoadMidi(settings, midi);
            return new(loader);
        }

        public static SongChart FromDotChart(in ParseSettings settings, string chartText)
        {
            var loader = MoonSongLoader.LoadDotChart(settings, chartText);
            return new(loader);
        }

        public InstrumentTrack<GuitarNote> GetFiveFretTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => FiveFretGuitar,
                Instrument.FiveFretCoopGuitar => FiveFretCoop,
                Instrument.FiveFretRhythm => FiveFretRhythm,
                Instrument.FiveFretBass => FiveFretBass,
                Instrument.Keys => Keys,
                _ => throw new ArgumentException($"Instrument {instrument} is not a 5-fret guitar instrument!")
            };
        }

        public InstrumentTrack<GuitarNote> GetSixFretTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.SixFretGuitar => SixFretGuitar,
                Instrument.SixFretCoopGuitar => SixFretCoop,
                Instrument.SixFretRhythm => SixFretRhythm,
                Instrument.SixFretBass => SixFretBass,
                _ => throw new ArgumentException($"Instrument {instrument} is not a 6-fret guitar instrument!")
            };
        }

        public InstrumentTrack<DrumNote> GetDrumsTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FourLaneDrums => FourLaneDrums,
                Instrument.ProDrums => ProDrums,
                Instrument.FiveLaneDrums => FiveLaneDrums,
                _ => throw new ArgumentException($"Instrument {instrument} is not a drums instrument!")
            };
        }

        public InstrumentTrack<ProGuitarNote> GetProGuitarTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.ProGuitar_17Fret => ProGuitar_17Fret,
                Instrument.ProGuitar_22Fret => ProGuitar_22Fret,
                Instrument.ProBass_17Fret => ProBass_17Fret,
                Instrument.ProBass_22Fret => ProBass_22Fret,
                _ => throw new ArgumentException($"Instrument {instrument} is not a Pro Guitar instrument!")
            };
        }

        public VocalsTrack GetVocalsTrack(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.Vocals => Vocals,
                Instrument.Harmony => Harmony,
                _ => throw new ArgumentException($"Instrument {instrument} is not a vocals instrument!")
            };
        }

        public double GetStartTime()
        {
            static double TrackMin<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Min((track) => track.GetStartTime());
            static double VoxMin(IEnumerable<VocalsTrack> tracks)
                => tracks.Min((track) => track.GetStartTime());

            double totalStartTime = 0;

            // Tracks

            totalStartTime = Math.Min(TrackMin(FiveFretTracks), totalStartTime);
            totalStartTime = Math.Min(TrackMin(SixFretTracks), totalStartTime);
            totalStartTime = Math.Min(TrackMin(DrumsTracks), totalStartTime);
            totalStartTime = Math.Min(TrackMin(ProGuitarTracks), totalStartTime);

            totalStartTime = Math.Min(ProKeys.GetStartTime(), totalStartTime);

            totalStartTime = Math.Min(VoxMin(VocalsTracks), totalStartTime);

            // Global

            totalStartTime = Math.Min(Lyrics.GetStartTime(), totalStartTime);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalStartTime = Math.Min(GlobalEvents.GetStartTime(), totalStartTime);
            // totalStartTime = Math.Min(Sections.GetStartTime(), totalStartTime);
            // totalStartTime = Math.Min(SyncTrack.GetStartTime(), totalStartTime);
            // totalStartTime = Math.Min(VenueTrack.GetStartTime(), totalStartTime);

            return totalStartTime;
        }

        public double GetEndTime()
        {
            static double TrackMax<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Max((track) => track.GetEndTime());
            static double VoxMax(IEnumerable<VocalsTrack> tracks)
                => tracks.Max((track) => track.GetEndTime());

            double totalEndTime = 0;

            // Tracks

            totalEndTime = Math.Max(TrackMax(FiveFretTracks), totalEndTime);
            totalEndTime = Math.Max(TrackMax(SixFretTracks), totalEndTime);
            totalEndTime = Math.Max(TrackMax(DrumsTracks), totalEndTime);
            totalEndTime = Math.Max(TrackMax(ProGuitarTracks), totalEndTime);

            totalEndTime = Math.Max(ProKeys.GetEndTime(), totalEndTime);

            totalEndTime = Math.Max(VoxMax(VocalsTracks), totalEndTime);

            // Global

            totalEndTime = Math.Max(Lyrics.GetEndTime(), totalEndTime);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalEndTime = Math.Max(GlobalEvents.GetEndTime(), totalEndTime);
            // totalEndTime = Math.Max(Sections.GetEndTime(), totalEndTime);
            // totalEndTime = Math.Max(SyncTrack.GetEndTime(), totalEndTime);
            // totalEndTime = Math.Max(VenueTrack.GetEndTime(), totalEndTime);

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            static uint TrackMin<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Min((track) => track.GetFirstTick());
            static uint VoxMin(IEnumerable<VocalsTrack> tracks)
                => tracks.Min((track) => track.GetFirstTick());

            uint totalFirstTick = 0;

            // Tracks

            totalFirstTick = Math.Min(TrackMin(FiveFretTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(SixFretTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(DrumsTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(ProGuitarTracks), totalFirstTick);

            totalFirstTick = Math.Min(ProKeys.GetFirstTick(), totalFirstTick);

            totalFirstTick = Math.Min(VoxMin(VocalsTracks), totalFirstTick);

            // Global

            totalFirstTick = Math.Min(Lyrics.GetFirstTick(), totalFirstTick);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalFirstTick = Math.Min(GlobalEvents.GetFirstTick(), totalFirstTick);
            // totalFirstTick = Math.Min(Sections.GetFirstTick(), totalFirstTick);
            // totalFirstTick = Math.Min(SyncTrack.GetFirstTick(), totalFirstTick);
            // totalFirstTick = Math.Min(VenueTrack.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            static uint TrackMax<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Max((track) => track.GetLastTick());
            static uint VoxMax(IEnumerable<VocalsTrack> tracks)
                => tracks.Max((track) => track.GetLastTick());

            uint totalLastTick = 0;

            // Tracks

            totalLastTick = Math.Max(TrackMax(FiveFretTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(SixFretTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(DrumsTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(ProGuitarTracks), totalLastTick);

            totalLastTick = Math.Max(ProKeys.GetLastTick(), totalLastTick);

            totalLastTick = Math.Max(VoxMax(VocalsTracks), totalLastTick);

            // Global

            totalLastTick = Math.Max(Lyrics.GetLastTick(), totalLastTick);

            // Deliberately excluded, as they're not major contributors to the chart bounds
            // totalLastTick = Math.Max(GlobalEvents.GetLastTick(), totalLastTick);
            // totalLastTick = Math.Max(Sections.GetLastTick(), totalLastTick);
            // totalLastTick = Math.Max(SyncTrack.GetLastTick(), totalLastTick);
            // totalLastTick = Math.Max(VenueTrack.GetLastTick(), totalLastTick);

            return totalLastTick;
        }

        public TextEvent? GetEndEvent()
        {
            // Reverse-search through a limited amount of events
            for (int i = 1; i <= 10; i++)
            {
                int index = GlobalEvents.Count - i;
                if (index < 0)
                    break;

                var text = GlobalEvents[index];
                if (text.Text == TextEvents.END_MARKER)
                    return text;
            }

            return null;
        }
    }
}