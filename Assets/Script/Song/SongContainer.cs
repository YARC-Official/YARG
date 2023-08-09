using System.Collections.Generic;
using YARG.Core.Song.Cache;
using YARG.Core.Song;

namespace YARG.Song
{
    public class SongContainer
    {
        private Dictionary<HashWrapper, List<SongMetadata>> _entries;
        private List<SongMetadata> _songs;

        public SortedDictionary<string, List<SongMetadata>> Titles { get; private set; }
        public SortedDictionary<string, List<SongMetadata>> Years { get; private set; }
        public SortedDictionary<string, List<SongMetadata>> ArtistAlbums { get; private set; }
        public SortedDictionary<string, List<SongMetadata>> SongLengths { get; private set; }
        public SortedDictionary<SortString, List<SongMetadata>> Instruments { get; private set; }
        public SortedDictionary<SortString, List<SongMetadata>> Artists { get; private set; }
        public SortedDictionary<SortString, List<SongMetadata>> Albums { get; private set; }
        public SortedDictionary<SortString, List<SongMetadata>> Genres { get; private set; }
        public SortedDictionary<SortString, List<SongMetadata>> Charters { get; private set; }
        public SortedDictionary<SortString, List<SongMetadata>> Playlists { get; private set; }
        public SortedDictionary<SortString, List<SongMetadata>> Sources { get; private set; }

        public int Count => _songs.Count;
        public IReadOnlyDictionary<HashWrapper, List<SongMetadata>> SongsByHash => _entries;
        public IReadOnlyList<SongMetadata> Songs => _songs;

        public SongContainer()
        {
            _entries = new();
            _songs = new();
            Titles = new();
            Years = new();
            ArtistAlbums = new();
            SongLengths = new();
            Instruments = new();
            Artists = new();
            Albums = new();
            Genres = new();
            Charters = new();
            Playlists = new();
            Sources = new();
        }

        public SongContainer(SongCache cache)
        {
            _entries = cache.entries;
            _songs = new();
            foreach (var node in _entries)
                _songs.AddRange(node.Value);
            _songs.TrimExcess();

            Titles = cache.titles.Elements;
            Years = cache.years.Elements;
            ArtistAlbums = cache.artistAlbums.Elements;
            SongLengths = cache.songLengths.Elements;
            Instruments = cache.instruments.Elements;
            Artists = cache.artists.Elements;
            Albums = cache.albums.Elements;
            Genres = cache.genres.Elements;
            Charters = cache.charters.Elements;
            Playlists = cache.playlists.Elements;
            Sources = cache.sources.Elements;
        }
    }
}