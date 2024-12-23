using System;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public enum ScanResult
    {
        Success,
        DirectoryError,
        DuplicateFilesFound,
        IniEntryCorruption,
        IniNotDownloaded,
        ChartNotDownloaded,
        NoName,
        NoNotes,
        DTAError,
        MoggError,
        UnsupportedEncryption,
        MissingCONMidi,
        PossibleCorruption,
        FailedSngLoad,

        InvalidResolution,
        InvalidResolution_Update,
        InvalidResolution_Upgrade,

        NoAudio,
        PathTooLong,
        MultipleMidiTrackNames,
        MultipleMidiTrackNames_Update,
        MultipleMidiTrackNames_Upgrade,

        LooseChart_Warning,
    }

    /// <summary>
    /// The type of chart file to read.
    /// </summary>
    public enum ChartFormat
    {
        Mid,
        Midi,
        Chart,
    };

    public enum EntryType
    {
        Ini,
        Sng,
        ExCON,
        CON,
    }

    public struct LoaderSettings
    {
        public static readonly LoaderSettings Default = new()
        {
            HopoThreshold = -1,
            SustainCutoffThreshold = -1,
            OverdiveMidiNote = 116
        };

        public long HopoThreshold;
        public long SustainCutoffThreshold;
        public int OverdiveMidiNote;
    }

    /// <summary>
    /// The metadata for a song.
    /// </summary>
    /// <remarks>
    /// This class is intended to hold all metadata for all songs, whether it be displayed in the song list or used for
    /// parsing/loading of the song.
    /// <br/>
    /// Display/common metadata should be added directly to this class. Metadata only used in a specific file type
    /// should not be handled through inheritance, make a separate class for that data instead and add it as a field to
    /// this one.
    /// <br/>
    /// Instances of this class should not be created directly (except for things like a chart editor), instead they
    /// should be created through static methods which parse in a metadata file of a specific type and return an
    /// instance.
    /// </remarks>
    [Serializable]
    public abstract partial class SongEntry
    {
        protected static readonly string[] BACKGROUND_FILENAMES =
        {
            "bg", "background", "video"
        };

        protected static readonly string[] VIDEO_EXTENSIONS =
        {
            ".mp4", ".mov", ".webm",
        };

        protected static readonly string[] IMAGE_EXTENSIONS =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".gif", ".pic"
        };

        protected static readonly string YARGROUND_EXTENSION = ".yarground";
        protected static readonly string YARGROUND_FULLNAME = "bg.yarground";
        protected static readonly Random BACKROUND_RNG = new();

        public const int MINIMUM_YEAR_DIGITS = 4;

        public readonly SongMetadata Metadata;
        public readonly AvailableParts Parts;
        public readonly HashWrapper Hash;
        public readonly LoaderSettings Settings;

        public abstract string Location { get; }
        public abstract string DirectoryActual { get; }
        public abstract EntryType SubType { get; }
        public abstract string Year { get; }
        public abstract int YearAsNumber { get; }
        public abstract ulong SongLengthMilliseconds { get; }
        public abstract bool LoopVideo { get; }

        public SortString Name => Metadata.Name;
        public SortString Artist => Metadata.Artist;
        public SortString Album => Metadata.Album;
        public SortString Genre => Metadata.Genre;
        public SortString Charter => Metadata.Charter;
        public SortString Source => Metadata.Source;
        public SortString Playlist => Metadata.Playlist;

        public string UnmodifiedYear => Metadata.Year;

        public bool IsMaster => Metadata.IsMaster;

        public int AlbumTrack => Metadata.AlbumTrack;

        public int PlaylistTrack => Metadata.PlaylistTrack;

        public string LoadingPhrase => Metadata.LoadingPhrase;
        
        public string CreditWrittenBy => Metadata.CreditWrittenBy;
        
        public string CreditPerformedBy => Metadata.CreditPerformedBy;
        
        public string CreditCourtesyOf => Metadata.CreditCourtesyOf;
        
        public string CreditAlbumCover => Metadata.CreditAlbumCover;
        
        public string CreditLicense => Metadata.CreditLicense;

        public long SongOffsetMilliseconds => Metadata.SongOffset;

        public long PreviewStartMilliseconds => Metadata.PreviewStart;

        public long PreviewEndMilliseconds => Metadata.PreviewEnd;

        public long VideoStartTimeMilliseconds => Metadata.VideoStartTime;

        public long VideoEndTimeMilliseconds => Metadata.VideoEndTime;

        public double SongLengthSeconds => SongLengthMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double SongOffsetSeconds => SongOffsetMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double PreviewStartSeconds => PreviewStartMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double PreviewEndSeconds => PreviewEndMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double VideoStartTimeSeconds => VideoStartTimeMilliseconds / SongMetadata.MILLISECOND_FACTOR;

        public double VideoEndTimeSeconds => VideoEndTimeMilliseconds >= 0 ? VideoEndTimeMilliseconds / SongMetadata.MILLISECOND_FACTOR : -1;

        public int VocalsCount
        {
            get
            {
                if (Parts.HarmonyVocals[2])
                {
                    return 3;
                }

                if (Parts.HarmonyVocals[1])
                {
                    return 2;
                }
                return Parts.HarmonyVocals[0] || Parts.LeadVocals[0] ? 1 : 0;
            }
        }

        public sbyte BandDifficulty => Parts.BandDifficulty.Intensity;

        public override string ToString() { return Artist + " | " + Name; }

        public PartValues this[Instrument instrument]
        {
            get
            {
                return instrument switch
                {
                    Instrument.FiveFretGuitar => Parts.FiveFretGuitar,
                    Instrument.FiveFretBass => Parts.FiveFretBass,
                    Instrument.FiveFretRhythm => Parts.FiveFretRhythm,
                    Instrument.FiveFretCoopGuitar => Parts.FiveFretCoopGuitar,
                    Instrument.Keys => Parts.Keys,

                    Instrument.SixFretGuitar => Parts.SixFretGuitar,
                    Instrument.SixFretBass => Parts.SixFretBass,
                    Instrument.SixFretRhythm => Parts.SixFretRhythm,
                    Instrument.SixFretCoopGuitar => Parts.SixFretCoopGuitar,

                    Instrument.FourLaneDrums => Parts.FourLaneDrums,
                    Instrument.FiveLaneDrums => Parts.FiveLaneDrums,
                    Instrument.ProDrums => Parts.ProDrums,

                    Instrument.EliteDrums => Parts.EliteDrums,

                    Instrument.ProGuitar_17Fret => Parts.ProGuitar_17Fret,
                    Instrument.ProGuitar_22Fret => Parts.ProGuitar_22Fret,
                    Instrument.ProBass_17Fret => Parts.ProBass_17Fret,
                    Instrument.ProBass_22Fret => Parts.ProBass_22Fret,

                    Instrument.ProKeys => Parts.ProKeys,

                    // Instrument.Dj => DJ,

                    Instrument.Vocals => Parts.LeadVocals,
                    Instrument.Harmony => Parts.HarmonyVocals,
                    Instrument.Band => Parts.BandDifficulty,

                    _ => throw new NotImplementedException($"Unhandled instrument {instrument}!")
                };
            }
        }

        public bool HasInstrument(Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar => Parts.FiveFretGuitar.SubTracks > 0,
                Instrument.FiveFretBass => Parts.FiveFretBass.SubTracks > 0,
                Instrument.FiveFretRhythm => Parts.FiveFretRhythm.SubTracks > 0,
                Instrument.FiveFretCoopGuitar => Parts.FiveFretCoopGuitar.SubTracks > 0,
                Instrument.Keys => Parts.Keys.SubTracks > 0,

                Instrument.SixFretGuitar => Parts.SixFretGuitar.SubTracks > 0,
                Instrument.SixFretBass => Parts.SixFretBass.SubTracks > 0,
                Instrument.SixFretRhythm => Parts.SixFretRhythm.SubTracks > 0,
                Instrument.SixFretCoopGuitar => Parts.SixFretCoopGuitar.SubTracks > 0,

                Instrument.FourLaneDrums => Parts.FourLaneDrums.SubTracks > 0,
                Instrument.FiveLaneDrums => Parts.FiveLaneDrums.SubTracks > 0,
                Instrument.ProDrums => Parts.ProDrums.SubTracks > 0,

                Instrument.EliteDrums => Parts.EliteDrums.SubTracks > 0,

                Instrument.ProGuitar_17Fret => Parts.ProGuitar_17Fret.SubTracks > 0,
                Instrument.ProGuitar_22Fret => Parts.ProGuitar_22Fret.SubTracks > 0,
                Instrument.ProBass_17Fret => Parts.ProBass_17Fret.SubTracks > 0,
                Instrument.ProBass_22Fret => Parts.ProBass_22Fret.SubTracks > 0,

                Instrument.ProKeys => Parts.ProKeys.SubTracks > 0,

                // Instrument.Dj => Parts.DJ.SubTracks > 0,

                Instrument.Vocals => Parts.LeadVocals.SubTracks > 0,
                Instrument.Harmony => Parts.HarmonyVocals.SubTracks > 0,
                Instrument.Band => Parts.BandDifficulty.SubTracks > 0,

                _ => false
            };
        }

        protected SongEntry(in SongMetadata metadata, in AvailableParts parts, in HashWrapper hash, in LoaderSettings settings)
        {
            Metadata = metadata;
            Parts = parts;
            Hash = hash;
            Settings = settings;
        }

        protected SongEntry(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            Hash = HashWrapper.Deserialize(stream);
            unsafe
            {
                Parts = *(AvailableParts*) stream.PositionPointer;
                stream.Position += sizeof(AvailableParts);
            }
            Metadata = new SongMetadata(stream, strings);
            Settings.HopoThreshold = stream.Read<long>(Endianness.Little);
            Settings.SustainCutoffThreshold = stream.Read<long>(Endianness.Little);
            Settings.OverdiveMidiNote = stream.Read<int>(Endianness.Little);
        }

        public virtual void Serialize(MemoryStream stream, CategoryCacheWriteNode node)
        {
            Hash.Serialize(stream);
            unsafe
            {
                fixed (AvailableParts* ptr = &Parts)
                {
                    stream.Write(new Span<byte>(ptr, sizeof(AvailableParts)));
                }
            }
            Metadata.Serialize(stream, node);
            stream.Write(Settings.HopoThreshold, Endianness.Little);
            stream.Write(Settings.SustainCutoffThreshold, Endianness.Little);
            stream.Write(Settings.OverdiveMidiNote, Endianness.Little);
        }
    }
}