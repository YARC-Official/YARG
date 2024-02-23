using System;
using YARG.Core.Song;

namespace YARG.Song
{

    public enum SortOption
    {
        Name,
        Artist,
        Album,
        Artist_Album,
        Genre,
        Year,
        Charter,
        Playlist,
        Source,
        SongLength,
        DateAdded,
        Instrument,
        PlayCount,
    }

    public static class SortOptionExtensions
    {
        public static SongAttribute ToSongAttribute(this SortOption sortOption)
        {
            switch (sortOption)
            {
                case SortOption.Name:         return SongAttribute.Name;
                case SortOption.Artist:       return SongAttribute.Artist;
                case SortOption.Album:        return SongAttribute.Album;
                case SortOption.Artist_Album: return SongAttribute.Artist_Album;
                case SortOption.Genre:        return SongAttribute.Genre;
                case SortOption.Year:         return SongAttribute.Year;
                case SortOption.Charter:      return SongAttribute.Charter;
                case SortOption.Playlist:     return SongAttribute.Playlist;
                case SortOption.Source:       return SongAttribute.Source;
                case SortOption.SongLength:   return SongAttribute.SongLength;
                case SortOption.DateAdded:    return SongAttribute.DateAdded;
                case SortOption.Instrument:   return SongAttribute.Instrument;
                default:                      return SongAttribute.Unspecified;
            }
        }
    }
}