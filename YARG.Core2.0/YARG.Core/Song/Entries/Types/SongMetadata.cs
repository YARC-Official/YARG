using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;

namespace YARG.Core.Song
{
    public struct SongMetadata
    {
        public const double MILLISECOND_FACTOR = 1000.0;
        public static readonly SortString DEFAULT_NAME = "Unknown Name";
        public static readonly SortString DEFAULT_ARTIST = "Unknown Artist";
        public static readonly SortString DEFAULT_ALBUM = "Unknown Album";
        public static readonly SortString DEFAULT_GENRE = "Unknown Genre";
        public static readonly SortString DEFAULT_CHARTER = "Unknown Charter";
        public static readonly SortString DEFAULT_SOURCE = "Unknown Source";
        public const string DEFAULT_YEAR = "####";

        public static readonly SongMetadata Default = new()
        {
            Name = SortString.Empty,
            Artist = DEFAULT_ARTIST,
            Album = DEFAULT_ALBUM,
            Genre = DEFAULT_GENRE,
            Charter = DEFAULT_CHARTER,
            Source = DEFAULT_SOURCE,
            Playlist = SortString.Empty,
            IsMaster = true,
            AlbumTrack = 0,
            PlaylistTrack = 0,
            LoadingPhrase = string.Empty,
            CreditWrittenBy = string.Empty,
            CreditPerformedBy = string.Empty,
            CreditCourtesyOf = string.Empty,
            CreditAlbumCover = string.Empty,
            CreditLicense = string.Empty,
            Year = DEFAULT_YEAR,
            SongOffset = 0,
            PreviewStart = -1,
            PreviewEnd = -1,
            VideoStartTime = 0,
            VideoEndTime = -1,
        };

        public SortString Name;
        public SortString Artist;
        public SortString Album;
        public SortString Genre;
        public SortString Charter;
        public SortString Source;
        public SortString Playlist;

        public string Year;

        public long SongOffset;
        public uint SongRating;  // 1 = FF; 2 = SR; 3 = M; 4 = NR

        public long PreviewStart;
        public long PreviewEnd;

        public long VideoStartTime;
        public long VideoEndTime;

        public bool IsMaster;

        public int AlbumTrack;
        public int PlaylistTrack;

        public string LoadingPhrase;

        public string CreditWrittenBy;
        public string CreditPerformedBy;
        public string CreditCourtesyOf;
        public string CreditAlbumCover;
        public string CreditLicense;

        public SongMetadata(IniSection modifiers, string defaultPlaylist)
        {
            modifiers.TryGet("name", out Name, DEFAULT_NAME);
            modifiers.TryGet("artist", out Artist, DEFAULT_ARTIST);
            modifiers.TryGet("album", out Album, DEFAULT_ALBUM);
            modifiers.TryGet("genre", out Genre, DEFAULT_GENRE);

            if (!modifiers.TryGet("year", out Year))
            {
                if (modifiers.TryGet("year_chart", out Year))
                {
                    if (Year.StartsWith(", "))
                    {
                        Year = Year[2..];
                    }
                    else if (Year.StartsWith(','))
                    {
                        Year = Year[1..];
                    }
                }
                else
                {
                    Year = DEFAULT_YEAR;
                }
            }

            if (!modifiers.TryGet("charter", out Charter, DEFAULT_CHARTER))
            {
                modifiers.TryGet("frets", out Charter, DEFAULT_CHARTER);
            }

            modifiers.TryGet("icon", out Source, DEFAULT_SOURCE);
            modifiers.TryGet("playlist", out Playlist, defaultPlaylist);

            modifiers.TryGet("loading_phrase", out LoadingPhrase);
            
            modifiers.TryGet("credit_written_by", out CreditWrittenBy);
            modifiers.TryGet("credit_performed_by", out CreditPerformedBy);
            modifiers.TryGet("credit_courtesy_of", out CreditCourtesyOf);
            modifiers.TryGet("credit_album_cover", out CreditAlbumCover);
            modifiers.TryGet("credit_license", out CreditLicense);

            if (!modifiers.TryGet("playlist_track", out PlaylistTrack))
            {
                PlaylistTrack = -1;
            }

            if (!modifiers.TryGet("album_track", out AlbumTrack))
            {
                AlbumTrack = -1;
            }

            modifiers.TryGet("rating", out SongRating);

            modifiers.TryGet("video_start_time", out VideoStartTime);
            if (!modifiers.TryGet("video_end_time", out VideoEndTime))
            {
                VideoEndTime = -1;
            }

            if (!modifiers.TryGet("preview", out PreviewStart, out PreviewEnd))
            {
                if (!modifiers.TryGet("preview_start_time", out PreviewStart))
                {
                    // Capitlization = from .chart
                    if (modifiers.TryGet("previewStart_chart", out double previewStartSeconds))
                    {
                        PreviewStart = (long) (previewStartSeconds * MILLISECOND_FACTOR);
                    }
                    else
                    {
                        PreviewStart = -1;
                    }
                }

                if (!modifiers.TryGet("preview_end_time", out PreviewEnd))
                {
                    // Capitlization = from .chart
                    if (modifiers.TryGet("previewEnd_chart", out double previewEndSeconds))
                    {
                        PreviewEnd = (long) (previewEndSeconds * MILLISECOND_FACTOR);
                    }
                    else
                    {
                        PreviewEnd = -1;
                    }
                }
            }

            if (!modifiers.TryGet("delay", out SongOffset) || SongOffset == 0)
            {
                if (modifiers.TryGet("delay_chart", out double songOffsetSeconds))
                {
                    SongOffset = (long) (songOffsetSeconds * MILLISECOND_FACTOR);
                }
            }

            IsMaster = !modifiers.TryGet("tags", out string tag) || tag.ToLower() != "cover";
        }

        public SongMetadata(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            Name = strings.titles[stream.Read<int>(Endianness.Little)];
            Artist = strings.artists[stream.Read<int>(Endianness.Little)];
            Album = strings.albums[stream.Read<int>(Endianness.Little)];
            Genre = strings.genres[stream.Read<int>(Endianness.Little)];

            Year = strings.years[stream.Read<int>(Endianness.Little)];
            Charter = strings.charters[stream.Read<int>(Endianness.Little)];
            Playlist = strings.playlists[stream.Read<int>(Endianness.Little)];
            Source = strings.sources[stream.Read<int>(Endianness.Little)];

            IsMaster = stream.ReadBoolean();

            AlbumTrack = stream.Read<int>(Endianness.Little);
            PlaylistTrack = stream.Read<int>(Endianness.Little);

            SongOffset = stream.Read<long>(Endianness.Little);
            SongRating = stream.Read<uint>(Endianness.Little);

            PreviewStart = stream.Read<long>(Endianness.Little);
            PreviewEnd = stream.Read<long>(Endianness.Little);

            VideoStartTime = stream.Read<long>(Endianness.Little);
            VideoEndTime = stream.Read<long>(Endianness.Little);

            LoadingPhrase = stream.ReadString();
            
            CreditWrittenBy = stream.ReadString();
            CreditPerformedBy = stream.ReadString();
            CreditCourtesyOf = stream.ReadString();
            CreditAlbumCover = stream.ReadString();
            CreditLicense = stream.ReadString();
        }

        public readonly void Serialize(MemoryStream stream, CategoryCacheWriteNode node)
        {
            stream.Write(node.title, Endianness.Little);
            stream.Write(node.artist, Endianness.Little);
            stream.Write(node.album, Endianness.Little);
            stream.Write(node.genre, Endianness.Little);
            stream.Write(node.year, Endianness.Little);
            stream.Write(node.charter, Endianness.Little);
            stream.Write(node.playlist, Endianness.Little);
            stream.Write(node.source, Endianness.Little);

            stream.Write(IsMaster);

            stream.Write(AlbumTrack, Endianness.Little);
            stream.Write(PlaylistTrack, Endianness.Little);

            stream.Write(SongOffset, Endianness.Little);
            stream.Write(SongRating, Endianness.Little);

            stream.Write(PreviewStart, Endianness.Little);
            stream.Write(PreviewEnd, Endianness.Little);

            stream.Write(VideoStartTime, Endianness.Little);
            stream.Write(VideoEndTime, Endianness.Little);

            stream.Write(LoadingPhrase);
            
            stream.Write(CreditWrittenBy);
            stream.Write(CreditPerformedBy);
            stream.Write(CreditCourtesyOf);
            stream.Write(CreditAlbumCover);
            stream.Write(CreditLicense);
        }
    }
}
