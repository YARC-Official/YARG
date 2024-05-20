﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YARG.Core;
using YARG.Core.Song;
using YARG.Player;

namespace YARG.Song
{
    public class SongSearching
    {
        private List<SearchNode> searches = new();

        public IReadOnlyList<SongCategory> Search(string value, SortAttribute sort)
        {
            var currentFilters = new List<FilterNode>()
            {
                // Instrument of the first node doesn't matter
                new(sort, Instrument.FiveFretGuitar, string.Empty)
            };
            currentFilters.AddRange(GetFilters(value.Split(';')));

            int currFilterIndex = 1;
            int prevFilterIndex = 1;
            if (searches.Count > 0 && searches[0].Filter.Attribute == sort)
            {
                while (currFilterIndex < currentFilters.Count)
                {
                    while (prevFilterIndex < searches.Count && currentFilters[currFilterIndex].StartsWith(searches[prevFilterIndex].Filter))
                    {
                        ++prevFilterIndex;
                    }

                    if (!currentFilters[currFilterIndex].Equals(searches[prevFilterIndex - 1].Filter))
                    {
                        break;
                    }
                    ++currFilterIndex;
                }
            }
            else
            {
                var songs = sort != SortAttribute.Playable
                    ? SongContainer.GetSortedCategory(sort)
                    : SongContainer.GetPlayableSongs(PlayerContainer.Players);
                searches.Clear();
                searches.Add(new SearchNode(currentFilters[0], songs));
            }

            while (currFilterIndex < currentFilters.Count)
            {
                var filter = currentFilters[currFilterIndex];
                var searchList = SearchSongs(filter, searches[prevFilterIndex - 1].Songs);

                if (prevFilterIndex < searches.Count)
                {
                    searches[prevFilterIndex] = new(filter, searchList);
                }
                else
                {
                    searches.Add(new(filter, searchList));
                }

                ++currFilterIndex;
                ++prevFilterIndex;
            }

            if (prevFilterIndex < searches.Count)
            {
                searches.RemoveRange(prevFilterIndex, searches.Count - prevFilterIndex);
            }
            return searches[prevFilterIndex - 1].Songs;
        }

        public bool IsUnspecified()
        {
            if (searches.Count <= 0)
            {
                return true;
            }

            return searches[^1].Filter.Attribute == SortAttribute.Unspecified;
        }

        private class FilterNode : IEquatable<FilterNode>
        {
            public readonly SortAttribute Attribute;
            public readonly Instrument Instrument;
            public readonly string Argument;

            public FilterNode(SortAttribute attribute, Instrument instrument, string argument)
            {
                Attribute = attribute;
                Instrument = instrument;
                Argument = argument;
            }

            public override bool Equals(object o)
            {
                return o is FilterNode node && Equals(node);
            }

            public bool Equals(FilterNode other)
            {
                if (Attribute != other.Attribute)
                {
                    return false;
                }

                if (Attribute == SortAttribute.Instrument)
                {
                    if (Instrument != other.Instrument)
                    {
                        return false;
                    }
                }
                return Argument == other.Argument;
            }

            public override int GetHashCode()
            {
                return Attribute.GetHashCode() ^ Argument.GetHashCode();
            }

            public bool StartsWith(FilterNode other)
            {
                return Attribute == other.Attribute && Argument.StartsWith(other.Argument);
            }
        }

        private class SearchNode
        {
            public readonly FilterNode Filter;
            public IReadOnlyList<SongCategory> Songs;

            public SearchNode(FilterNode filter, IReadOnlyList<SongCategory> songs)
            {
                Filter = filter;
                Songs = songs;
            }
        }

        private static readonly List<string> ALL_INSTRUMENTNAMES = new(Enum.GetNames(typeof(Instrument)).Select(ins => ins + ':'));
        private static readonly Instrument[] ALL_INSTRUMENTS = (Instrument[])Enum.GetValues(typeof(Instrument));

        private static List<FilterNode> GetFilters(string[] split)
        {
            var nodes = new List<FilterNode>();
            foreach (string arg in split)
            {
                SortAttribute attribute;
                Instrument instrument = Instrument.FiveFretGuitar;
                string argument = arg.Trim().ToLowerInvariant();
                if (argument == string.Empty)
                {
                    continue;
                }

                if (argument.StartsWith("artist:"))
                {
                    attribute = SortAttribute.Artist;
                    argument = RemoveDiacriticsAndArticle(argument[7..]);
                }
                else if (argument.StartsWith("source:"))
                {
                    attribute = SortAttribute.Source;
                    argument = argument[7..];
                }
                else if (argument.StartsWith("album:"))
                {
                    attribute = SortAttribute.Album;
                    argument = SortString.RemoveDiacritics(argument[6..]);
                }
                else if (argument.StartsWith("charter:"))
                {
                    attribute = SortAttribute.Charter;
                    argument = argument[8..];
                }
                else if (argument.StartsWith("year:"))
                {
                    attribute = SortAttribute.Year;
                    argument = argument[5..];
                }
                else if (argument.StartsWith("genre:"))
                {
                    attribute = SortAttribute.Genre;
                    argument = argument[6..];
                }
                else if (argument.StartsWith("playlist:"))
                {
                    attribute = SortAttribute.Playlist;
                    argument = argument[9..];
                }
                else if (argument.StartsWith("name:"))
                {
                    attribute = SortAttribute.Name;
                    argument = RemoveDiacriticsAndArticle(argument[5..]);
                }
                else if (argument.StartsWith("title:"))
                {
                    attribute = SortAttribute.Name;
                    argument = RemoveDiacriticsAndArticle(argument[6..]);
                }
                else
                {
                    var result = ALL_INSTRUMENTNAMES.FindIndex(ins => argument.StartsWith(ins.ToLower()));
                    if (result >= 0)
                    {
                        attribute = SortAttribute.Instrument;
                        instrument = ALL_INSTRUMENTS[result];
                        argument = argument[ALL_INSTRUMENTNAMES[result].Length..];
                    }
                    else
                    {
                        attribute = SortAttribute.Unspecified;
                        argument = SortString.RemoveDiacritics(argument);
                    }
                }

                nodes.Add(new FilterNode(attribute, instrument, argument.Trim()));
                if (attribute == SortAttribute.Unspecified)
                {
                    break;
                }
            }
            return nodes;
        }

        private static List<SongCategory> SearchSongs(FilterNode arg, IReadOnlyList<SongCategory> searchList)
        {
            if (arg.Attribute == SortAttribute.Unspecified)
            {
                List<SongEntry> entriesToSearch = new();
                foreach (var entry in searchList)
                {
                    entriesToSearch.AddRange(entry.Songs);
                }
                return UnspecifiedSearch(entriesToSearch, arg.Argument);
            }

            if (arg.Attribute == SortAttribute.Instrument)
            {
                return SearchInstrument(searchList, arg.Instrument, arg.Argument);
            }

            Predicate<SongEntry> match = arg.Attribute switch
            {
                SortAttribute.Name => entry => RemoveArticle(entry.Name.SortStr).Contains(arg.Argument),
                SortAttribute.Artist => entry => RemoveArticle(entry.Artist.SortStr).Contains(arg.Argument),
                SortAttribute.Album => entry => entry.Album.SortStr.Contains(arg.Argument),
                SortAttribute.Genre => entry => entry.Genre.SortStr.Contains(arg.Argument),
                SortAttribute.Year => entry => entry.Year.Contains(arg.Argument) || entry.UnmodifiedYear.Contains(arg.Argument),
                SortAttribute.Charter => entry => entry.Charter.SortStr.Contains(arg.Argument),
                SortAttribute.Playlist => entry => entry.Playlist.SortStr.Contains(arg.Argument),
                SortAttribute.Source => entry => entry.Source.SortStr.Contains(arg.Argument),
                _ => throw new Exception("Unhandled seacrh filter")
            };

            List<SongCategory> result = new();
            foreach (var node in searchList)
            {
                var entries = node.Songs.FindAll(match);
                if (entries.Count > 0)
                {
                    result.Add(new SongCategory(node.Category, entries));
                }
            }
            return result;
        }

        private class UnspecifiedSortNode : IComparable<UnspecifiedSortNode>
        {
            public readonly SongEntry Song;
            public readonly int Rank;

            private readonly int NameIndex;
            private readonly int ArtistIndex;

            public UnspecifiedSortNode(SongEntry song, string argument)
            {
                Song = song;
                NameIndex = song.Name.SortStr.IndexOf(argument, StringComparison.Ordinal);
                ArtistIndex = song.Artist.SortStr.IndexOf(argument, StringComparison.Ordinal);

                Rank = NameIndex;
                if (Rank < 0 || (ArtistIndex >= 0 && ArtistIndex < Rank))
                {
                    Rank = ArtistIndex;
                }
            }

            public int CompareTo(UnspecifiedSortNode other)
            {
                if (Rank != other.Rank)
                {
                    return Rank - other.Rank;
                }

                if (NameIndex >= 0)
                {
                    if (other.NameIndex < 0)
                    {
                        // Prefer Name to Artist for equality
                        // other.ArtistIndex guaranteed valid
                        return NameIndex <= other.ArtistIndex ? -1 : 1;
                    }

                    if (NameIndex != other.NameIndex)
                    {
                        return NameIndex - other.NameIndex;
                    }
                    return Song.Name.CompareTo(other.Song.Name);
                }

                // this.ArtistIndex guaranteed valid from this point

                if (other.NameIndex >= 0)
                {
                    return ArtistIndex < other.NameIndex ? -1 : 1;
                }

                // other.ArtistIndex guaranteed valid from this point

                if (ArtistIndex != other.ArtistIndex)
                {
                    return ArtistIndex - other.ArtistIndex;
                }
                return Song.Artist.CompareTo(other.Song.Artist);
            }
        }

        private static List<SongCategory> UnspecifiedSearch(IReadOnlyList<SongEntry> songs, string argument)
        {
            var nodes = new UnspecifiedSortNode[songs.Count];
            Parallel.For(0, songs.Count, i => nodes[i] = new UnspecifiedSortNode(songs[i], argument));
            var results = nodes
                .Where(node => node.Rank >= 0)
                .OrderBy(i => i)
                .Select(i => i.Song).ToList();
            return new() { new SongCategory("Search Results", results) };
        }

        private static List<SongCategory> SearchInstrument(IReadOnlyList<SongCategory> searchList, Instrument instrument, string argument)
        {
            var songsToMatch = SongContainer.Instruments[instrument]
                .Where(node => node.Key.ToString().StartsWith(argument))
                .SelectMany(node => node.Value);

            List<SongCategory> result = new();
            foreach (var node in searchList)
            {
                var songs = node.Songs.Intersect(songsToMatch).ToList();
                if (songs.Count > 0)
                {
                    result.Add(new SongCategory(node.Category, songs));
                }
            }
            return result;
        }

        private static readonly string[] Articles =
        {
            "The ", // The beatles, The day that never comes
            "El ",  // El final, El sol no regresa
            "La ",  // La quinta estacion, La bamba, La muralla verde
            "Le ",  // Le temps de la rentrée
            "Les ", // Les Rita Mitsouko, Les Wampas
            "Los ", // Los fabulosos cadillacs, Los enanitos verdes,
        };

        public static string RemoveArticle(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            foreach (var article in Articles)
            {
                if (name.StartsWith(article, StringComparison.InvariantCultureIgnoreCase))
                {
                    return name[article.Length..];
                }
            }

            return name;
        }

        public static string RemoveDiacriticsAndArticle(string text)
        {
            var textWithoutDiacritics = SortString.RemoveDiacritics(text);
            return RemoveArticle(textWithoutDiacritics);
        }
    }
}