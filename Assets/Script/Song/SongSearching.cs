﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YARG.Core.Song;

namespace YARG.Song
{
    public class SongSearching
    {
        private class FilterNode : IEquatable<FilterNode>
        {
            public readonly SongAttribute attribute;
            public readonly string argument;

            public FilterNode(SongAttribute attribute, string argument)
            {
                this.attribute = attribute;
                this.argument = argument;
            }

            public override bool Equals(object o)
            {
                return o is FilterNode node && Equals(node);
            }

            public bool Equals(FilterNode other)
            {
                return attribute == other.attribute && argument == other.argument;
            }

            public override int GetHashCode()
            {
                return attribute.GetHashCode() ^ argument.GetHashCode();
            }

            public static bool operator ==(FilterNode left, FilterNode right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(FilterNode left, FilterNode right)
            {
                return !left.Equals(right);
            }

            public bool StartsWith(FilterNode other)
            {
                return attribute == other.attribute && argument.StartsWith(other.argument);
            }
        }

        private List<(FilterNode, SortedDictionary<string, List<SongMetadata>>)> filters = new();

        public IReadOnlyDictionary<string, List<SongMetadata>> Search(string value, SongAttribute sort)
        {
            var currentFilters = GetFilters(value.Split(';'));
            if (currentFilters.Count == 0)
            {
                filters.Clear();
                return GlobalVariables.Instance.SongContainer.GetSortedSongList(sort);
            }

            int currFilterIndex = 0;
            int prevFilterIndex = 0;
            while (currFilterIndex < currentFilters.Count && prevFilterIndex < filters.Count && currentFilters[currFilterIndex].StartsWith(filters[prevFilterIndex].Item1))
            {
                do
                {
                    ++prevFilterIndex;
                } while (prevFilterIndex < filters.Count && currentFilters[currFilterIndex].StartsWith(filters[prevFilterIndex].Item1));

                if (currentFilters[currFilterIndex] != filters[prevFilterIndex - 1].Item1)
                {
                    break;
                }
                ++currFilterIndex;
            }

            if (currFilterIndex == 0 && (prevFilterIndex == 0 || currentFilters[0].attribute != SongAttribute.Unspecified))
            {
                (FilterNode, SortedDictionary<string, List<SongMetadata>>) newNode = new(currentFilters[0], SearchSongs(currentFilters[0]));
                if (prevFilterIndex < filters.Count)
                {
                    filters[prevFilterIndex] = newNode;
                }
                else
                {
                    filters.Add(newNode);
                }

                ++prevFilterIndex;
                ++currFilterIndex;
            }

            while (currFilterIndex < currentFilters.Count)
            {
                var filter = currentFilters[currFilterIndex];
                var searchList = SearchSongs(filter, filters[prevFilterIndex - 1].Item2);

                if (prevFilterIndex < filters.Count)
                {
                    filters[prevFilterIndex] = new(filter, searchList);
                }
                else
                {
                    filters.Add(new(filter, searchList));
                }

                ++currFilterIndex;
                ++prevFilterIndex;
            }

            if (prevFilterIndex < filters.Count)
            {
                filters.RemoveRange(prevFilterIndex, filters.Count - prevFilterIndex);
            }
            return filters[prevFilterIndex - 1].Item2;
        }

        private static List<FilterNode> GetFilters(string[] split)
        {
            List<FilterNode> nodes = new();
            foreach (string arg in split)
            {
                SongAttribute attribute;
                string argument = arg.Trim();
                if (argument == string.Empty)
                {
                    continue;
                }

                if (argument.StartsWith("artist:"))
                {
                    attribute = SongAttribute.Artist;
                    argument = RemoveDiacriticsAndArticle(argument[7..]);
                }
                else if (argument.StartsWith("source:"))
                {
                    attribute = SongAttribute.Source;
                    argument = argument[7..].ToLower();
                }
                else if (argument.StartsWith("album:"))
                {
                    attribute = SongAttribute.Album;
                    argument = SortString.RemoveDiacritics(argument[6..]);
                }
                else if (argument.StartsWith("charter:"))
                {
                    attribute = SongAttribute.Charter;
                    argument = argument[8..].ToLower();
                }
                else if (argument.StartsWith("year:"))
                {
                    attribute = SongAttribute.Year;
                    argument = argument[5..].ToLower();
                }
                else if (argument.StartsWith("genre:"))
                {
                    attribute = SongAttribute.Genre;
                    argument = argument[6..].ToLower();
                }
                else if (argument.StartsWith("playlist:"))
                {
                    attribute = SongAttribute.Playlist;
                    argument = argument[9..].ToLower();
                }
                else if (argument.StartsWith("name:"))
                {
                    attribute = SongAttribute.Name;
                    argument = RemoveDiacriticsAndArticle(argument[5..]);
                }
                else if (argument.StartsWith("title:"))
                {
                    attribute = SongAttribute.Name;
                    argument = RemoveDiacriticsAndArticle(argument[6..]);
                }
                else if (argument.StartsWith("instrument:"))
                {
                    attribute = SongAttribute.Instrument;
                    argument = RemoveDiacriticsAndArticle(argument[11..]);
                }
                else
                {
                    attribute = SongAttribute.Unspecified;
                    argument = SortString.RemoveDiacritics(argument);
                }

                argument = argument!.Trim();
                nodes.Add(new(attribute, argument));
                if (attribute == SongAttribute.Unspecified)
                {
                    break;
                }
            }
            return nodes;
        }

        private class SearchNode : IComparable<SearchNode>
        {
            public readonly SongMetadata Song;
            public readonly int Rank;

            private readonly int NameIndex;
            private readonly int ArtistIndex;

            public SearchNode(SongMetadata song, string argument)
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

            public int CompareTo(SearchNode other)
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

        private static SortedDictionary<string, List<SongMetadata>> SearchSongs(FilterNode arg)
        {
            if (arg.attribute == SongAttribute.Unspecified)
            {
                return UnspecifiedSearch(GlobalVariables.Instance.SongContainer.Songs, arg.argument);
            }
            return SearchByFilter(arg.attribute, arg.argument);
        }

        private static SortedDictionary<string, List<SongMetadata>> SearchSongs(FilterNode arg, SortedDictionary<string, List<SongMetadata>> searchList)
        {
            if (arg.attribute == SongAttribute.Unspecified)
            {
                List<SongMetadata> entriesToSearch = new();
                foreach (var entry in searchList)
                {
                    entriesToSearch.AddRange(entry.Value);
                }
                return UnspecifiedSearch(entriesToSearch, arg.argument);
            }

            Predicate<SongMetadata> match = arg.attribute switch
            {
                SongAttribute.Name => entry => RemoveArticle(entry.Name.SortStr).Contains(arg.argument),
                SongAttribute.Artist => entry => RemoveArticle(entry.Artist.SortStr).Contains(arg.argument),
                SongAttribute.Album => entry => entry.Album.SortStr.Contains(arg.argument),
                SongAttribute.Genre => entry => entry.Genre.SortStr.Contains(arg.argument),
                SongAttribute.Year => entry => entry.Year.Contains(arg.argument) || entry.UnmodifiedYear.Contains(arg.argument),
                SongAttribute.Charter => entry => entry.Charter.SortStr.Contains(arg.argument),
                SongAttribute.Playlist => entry => entry.Playlist.SortStr.Contains(arg.argument),
                SongAttribute.Source => entry => entry.Source.SortStr.Contains(arg.argument),
                _ => throw new Exception("Unhandled seacrh filter")
            };

            SortedDictionary<string, List<SongMetadata>> result = new();
            foreach (var node in searchList)
            {
                var entries = node.Value.FindAll(match);
                if (entries.Count > 0)
                {
                    result.Add(node.Key, entries);
                }
            }
            return result;
        }

        private static SortedDictionary<string, List<SongMetadata>> UnspecifiedSearch(IReadOnlyList<SongMetadata> songs, string argument)
        {
            var nodes = new SearchNode[songs.Count];
            Parallel.For(0, songs.Count, i => nodes[i] = new SearchNode(songs[i], argument));
            var results = nodes
                .Where(node => node.Rank >= 0)
                .OrderBy(i => i)
                .Select(i => i.Song).ToList();
            return new() { { "Search Results", results } };
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

        private static SortedDictionary<string, List<SongMetadata>> SearchByFilter(SongAttribute sort, string arg)
        {
            if (sort == SongAttribute.Name)
            {
                return SearchByName(arg);
            }

            if (sort == SongAttribute.Year)
            {
                return SearchByYear(arg);
            }

            if (sort == SongAttribute.Instrument)
            {
                return SearchByInstrument(arg);
            }

            SortedDictionary<string, List<SongMetadata>> map = new();
            var elements = sort switch
            {
                SongAttribute.Artist => GlobalVariables.Instance.SongContainer.Artists,
                SongAttribute.Album => GlobalVariables.Instance.SongContainer.Albums,
                SongAttribute.Source => GlobalVariables.Instance.SongContainer.Sources,
                SongAttribute.Genre => GlobalVariables.Instance.SongContainer.Genres,
                SongAttribute.Charter => GlobalVariables.Instance.SongContainer.Charters,
                SongAttribute.Playlist => GlobalVariables.Instance.SongContainer.Playlists,
                _ => throw new Exception("stoopid"),
            };

            foreach (var element in elements)
            {
                if (arg.Length == 0)
                {
                    map.Add(element.Key, new(element.Value));
                    continue;
                }

                string key = element.Key.SortStr;
                if (sort == SongAttribute.Artist)
                {
                    key = RemoveArticle(key);
                }

                if (key.Contains(arg))
                {
                    map.Add(element.Key, new(element.Value));
                }
            }
            return map;
        }

        private static SortedDictionary<string, List<SongMetadata>> SearchByName(string arg)
        {
            if (arg.Length == 0)
            {
                SortedDictionary<string, List<SongMetadata>> titleMap = new();
                foreach (var element in GlobalVariables.Instance.SongContainer.Titles)
                {
                    titleMap.Add(element.Key, new(element.Value));
                }
                return titleMap;
            }

            int i = 0;
            while (i + 1 < arg.Length && !char.IsLetterOrDigit(arg[i]))
            {
                ++i;
            }

            char character = arg[i];
            string key = char.IsDigit(character) ? "0-9" : char.ToUpper(character).ToString();
            var search = GlobalVariables.Instance.SongContainer.Titles[key];

            List<SongMetadata> result = new(search.Count);
            foreach (var element in search)
            {
                if (element.Name.SortStr.Contains(arg))
                {
                    result.Add(element);
                }
            }
            return new() { { key, result } };
        }

        private static SortedDictionary<string, List<SongMetadata>> SearchByYear(string arg)
        {
            List<SongMetadata> entries = new();
            foreach (var element in GlobalVariables.Instance.SongContainer.Years)
            {
                foreach (var entry in element.Value)
                {
                    if (entry.Year.Contains(arg))
                    {
                        entries.Add(entry);
                    }
                }
            }
            return new() { { arg, entries } };
        }

        private static SortedDictionary<string, List<SongMetadata>> SearchByInstrument(string arg)
        {
            SortedDictionary<string, List<SongMetadata>> map = new();
            foreach (var element in GlobalVariables.Instance.SongContainer.Instruments)
            {
                if (element.Key.Contains(arg, StringComparison.OrdinalIgnoreCase))
                {
                    map.Add(element.Key, new(element.Value));
                }
            }
            return map;
        }
    }
}