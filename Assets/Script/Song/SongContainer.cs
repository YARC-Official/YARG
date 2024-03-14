using System.Collections.Generic;
using YARG.Core.Song.Cache;
using YARG.Core.Song;
using System;
using YARG.Helpers.Extensions;

namespace YARG.Song
{
    public readonly struct SongCategory
    {
        public readonly string Category;
        public readonly List<SongEntry> Songs;

        public SongCategory(string category, List<SongEntry> songs)
        {
            Category = category;
            Songs = songs;
        }

        public void Deconstruct(out string category, out List<SongEntry> songs)
        {
            category = Category;
            songs = Songs;
        }
    }

    public static class SongContainer
    {
        private static SongCache _songCache = new();
        private static List<SongEntry> _songs = new();

        private static List<SongCategory> _sortTitles = new();
        private static List<SongCategory> _sortArtists = new();
        private static List<SongCategory> _sortAlbums = new();
        private static List<SongCategory> _sortGenres = new();
        private static List<SongCategory> _sortYears = new();
        private static List<SongCategory> _sortCharters = new();
        private static List<SongCategory> _sortPlaylists = new();
        private static List<SongCategory> _sortSources = new();
        private static List<SongCategory> _sortArtistAlbums = new();
        private static List<SongCategory> _sortSongLengths = new();
        private static List<SongCategory> _sortDatesAdded = new();
        private static List<SongCategory> _sortInstruments = new();

        public static IReadOnlyDictionary<string, List<SongEntry>> Titles => _songCache.Titles;
        public static IReadOnlyDictionary<string, List<SongEntry>> Years => _songCache.Years;
        public static IReadOnlyDictionary<string, List<SongEntry>> ArtistAlbums => _songCache.ArtistAlbums;
        public static IReadOnlyDictionary<string, List<SongEntry>> SongLengths => _songCache.SongLengths;
        public static IReadOnlyDictionary<string, List<SongEntry>> Instruments => _songCache.Instruments;
        public static IReadOnlyDictionary<DateTime, List<SongEntry>> AddedDates => _songCache.DatesAdded;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Artists => _songCache.Artists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Albums => _songCache.Albums;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Genres => _songCache.Genres;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Charters => _songCache.Charters;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Playlists => _songCache.Playlists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Sources => _songCache.Sources;

        public static int Count => _songs.Count;
        public static IReadOnlyDictionary<HashWrapper, List<SongEntry>> SongsByHash => _songCache.Entries;
        public static IReadOnlyList<SongEntry> Songs => _songs;

        public static void Refresh(SongCache cache)
        {
            _songCache = cache;
            _songs.Clear();
            foreach (var node in cache.Entries)
                _songs.AddRange(node.Value);
            _songs.TrimExcess();

            Convert(_sortArtists, cache.Artists, SongAttribute.Artist);
            Convert(_sortAlbums, cache.Albums, SongAttribute.Album);
            Convert(_sortGenres, cache.Genres, SongAttribute.Genre);
            Convert(_sortCharters, cache.Charters, SongAttribute.Charter);
            Convert(_sortPlaylists, cache.Playlists, SongAttribute.Playlist);
            Convert(_sortSources, cache.Sources, SongAttribute.Source);

            Cast(_sortTitles, cache.Titles);
            Cast(_sortYears, cache.Years);
            Cast(_sortArtistAlbums, cache.ArtistAlbums);
            Cast(_sortSongLengths, cache.SongLengths);
            Cast(_sortInstruments, cache.Instruments);

            _sortDatesAdded.Clear();
            foreach (var node in cache.DatesAdded)
            {
                _sortDatesAdded.Add(new(node.Key.ToLongDateString(), node.Value));
            }

            static void Convert(List<SongCategory> sections, SortedDictionary<SortString, List<SongEntry>> list, SongAttribute attribute)
            {
                sections.Clear();
                foreach (var node in list)
                {
                    string key = node.Key;
                    if (attribute == SongAttribute.Genre && key.Length > 0 && char.IsLower(key[0]))
                    {
                        key = char.ToUpperInvariant(key[0]).ToString();
                        if (node.Key.Length > 1)
                            key += node.Key.Str[1..];
                    }
                    sections.Add(new(key, node.Value));
                }
            }

            static void Cast(List<SongCategory> sections, SortedDictionary<string, List<SongEntry>> list)
            {
                sections.Clear();
                foreach (var section in list)
                {
                    sections.Add(new SongCategory(section.Key, section.Value));
                }
            }
        }

        public static IReadOnlyList<SongCategory> GetSortedSongList(SongAttribute sort)
        {
            return sort switch
            {
                SongAttribute.Name => _sortTitles,
                SongAttribute.Artist => _sortArtists,
                SongAttribute.Album => _sortAlbums,
                SongAttribute.Genre => _sortGenres,
                SongAttribute.Year => _sortYears,
                SongAttribute.Charter => _sortCharters,
                SongAttribute.Playlist => _sortPlaylists,
                SongAttribute.Source => _sortSources,
                SongAttribute.Artist_Album => _sortArtistAlbums,
                SongAttribute.SongLength => _sortSongLengths,
                SongAttribute.DateAdded => _sortDatesAdded,
                SongAttribute.Instrument => _sortInstruments,
                _ => throw new Exception("stoopid"),
            };
        }

        public static SongEntry GetRandomSong()
        {
            return _songs.Pick();
        }
    }
}