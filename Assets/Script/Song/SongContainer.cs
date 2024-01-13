using System.Collections.Generic;
using YARG.Core.Song.Cache;
using YARG.Core.Song;
using System;

namespace YARG.Song
{
    public class SongContainer
    {
        private readonly SongCache _songCache = new();
        private readonly List<SongMetadata> _songs = new();

        private readonly SortedDictionary<string, List<SongMetadata>> _sortArtists = new();
        private readonly SortedDictionary<string, List<SongMetadata>> _sortAlbums = new();
        private readonly SortedDictionary<string, List<SongMetadata>> _sortGenres = new();
        private readonly SortedDictionary<string, List<SongMetadata>> _sortCharters = new();
        private readonly SortedDictionary<string, List<SongMetadata>> _sortPlaylists = new();
        private readonly SortedDictionary<string, List<SongMetadata>> _sortSources = new();

        public IReadOnlyDictionary<string, List<SongMetadata>> Titles => _songCache.Titles;
        public IReadOnlyDictionary<string, List<SongMetadata>> Years => _songCache.Years;
        public IReadOnlyDictionary<string, List<SongMetadata>> ArtistAlbums => _songCache.ArtistAlbums;
        public IReadOnlyDictionary<string, List<SongMetadata>> SongLengths => _songCache.SongLengths;
        public IReadOnlyDictionary<string, List<SongMetadata>> Instruments => _songCache.Instruments;
        public IReadOnlyDictionary<SortString, List<SongMetadata>> Artists => _songCache.Artists;
        public IReadOnlyDictionary<SortString, List<SongMetadata>> Albums => _songCache.Albums;
        public IReadOnlyDictionary<SortString, List<SongMetadata>> Genres => _songCache.Genres;
        public IReadOnlyDictionary<SortString, List<SongMetadata>> Charters => _songCache.Charters;
        public IReadOnlyDictionary<SortString, List<SongMetadata>> Playlists => _songCache.Playlists;
        public IReadOnlyDictionary<SortString, List<SongMetadata>> Sources => _songCache.Sources;

        public int Count => _songs.Count;
        public IReadOnlyDictionary<HashWrapper, List<SongMetadata>> SongsByHash => _songCache.Entries;
        public IReadOnlyList<SongMetadata> Songs => _songs;

        public SongContainer() { }

        public SongContainer(SongCache cache)
        {
            _songCache = cache;
            foreach (var node in cache.Entries)
                _songs.AddRange(node.Value);
            _songs.TrimExcess();

            _sortArtists = Convert(cache.Artists, SongAttribute.Artist);
            _sortAlbums = Convert(cache.Albums, SongAttribute.Album);
            _sortGenres = Convert(cache.Genres, SongAttribute.Genre);
            _sortCharters = Convert(cache.Charters, SongAttribute.Charter);
            _sortPlaylists = Convert(cache.Playlists, SongAttribute.Playlist);
            _sortSources = Convert(cache.Sources, SongAttribute.Source);

            static SortedDictionary<string, List<SongMetadata>> Convert(SortedDictionary<SortString, List<SongMetadata>> list, SongAttribute attribute)
            {
                SortedDictionary<string, List<SongMetadata>> map = new();
                foreach (var node in list)
                {
                    string key = node.Key;
                    if (attribute == SongAttribute.Genre && key.Length > 0 && char.IsLower(key[0]))
                    {
                        key = char.ToUpperInvariant(key[0]).ToString();
                        if (node.Key.Length > 1)
                            key += node.Key.Str[1..];
                    }
                    map.Add(key, node.Value);
                }
                return map;
            }
        }

        public IReadOnlyDictionary<string, List<SongMetadata>> GetSortedSongList(SongAttribute sort)
        {
            return sort switch
            {
                SongAttribute.Name => Titles,
                SongAttribute.Artist => _sortArtists,
                SongAttribute.Album => _sortAlbums,
                SongAttribute.Genre => _sortGenres,
                SongAttribute.Year => Years,
                SongAttribute.Charter => _sortCharters,
                SongAttribute.Playlist => _sortPlaylists,
                SongAttribute.Source => _sortSources,
                SongAttribute.Artist_Album => ArtistAlbums,
                SongAttribute.SongLength => SongLengths,
                SongAttribute.Instrument => Instruments,
                _ => throw new Exception("stoopid"),
            };
        }
    }
}