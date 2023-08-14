using System;
using System.Collections.Generic;
using YARG.Core.Song;

namespace YARG.Song
{
    public class SongSorting
    {
        private SortedDictionary<string, List<SongMetadata>> Titles;
        private SortedDictionary<string, List<SongMetadata>> Years;
        private SortedDictionary<string, List<SongMetadata>> ArtistAlbums;
        private SortedDictionary<string, List<SongMetadata>> SongLengths;
        private SortedDictionary<string, List<SongMetadata>> Instruments;
        private SortedDictionary<string, List<SongMetadata>> Artists;
        private SortedDictionary<string, List<SongMetadata>> Albums;
        private SortedDictionary<string, List<SongMetadata>> Genres;
        private SortedDictionary<string, List<SongMetadata>> Charters;
        private SortedDictionary<string, List<SongMetadata>> Playlists;
        private SortedDictionary<string, List<SongMetadata>> Sources;

        public SongSorting()
        {
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

        public SongSorting(SongContainer container)
        {
            Titles = container.Titles;
            Years = container.Years;
            ArtistAlbums = container.ArtistAlbums;
            SongLengths = container.SongLengths;
            
            Artists = Convert(container.Artists);
            Albums = Convert(container.Albums);
            Genres = Convert(container.Genres);
            Charters = Convert(container.Charters);
            Playlists = Convert(container.Playlists);
            Sources = Convert(container.Sources);
            Instruments = Convert(container.Instruments);

            static SortedDictionary<string, List<SongMetadata>> Convert(SortedDictionary<SortString, List<SongMetadata>> list)
            {
                SortedDictionary<string, List<SongMetadata>> map = new();
                foreach (var node in list)
                {
                    string key = node.Key;
                    if (key.Length > 0 && char.IsLower(key[0]))
                    {
                        key = new(char.ToUpperInvariant(key[0]), 1);
                        if (node.Key.Length > 1)
                            key += node.Key.Str[1..];
                    }
                    map.Add(key, node.Value);
                }
                return map;
            }
        }

        public SortedDictionary<string, List<SongMetadata>> GetSongList(SongAttribute sort)
        {
            return sort switch
            {
                SongAttribute.Name => Titles,
                SongAttribute.Artist => Artists,
                SongAttribute.Album => Albums,
                SongAttribute.Genre => Genres,
                SongAttribute.Year => Years,
                SongAttribute.Charter => Charters,
                SongAttribute.Playlist => Playlists,
                SongAttribute.Source => Sources,
                SongAttribute.Artist_Album => ArtistAlbums,
                SongAttribute.SongLength => SongLengths,
                SongAttribute.Instrument => Instruments,
                _ => throw new Exception("stoopid"),
            };
        }
    }
}