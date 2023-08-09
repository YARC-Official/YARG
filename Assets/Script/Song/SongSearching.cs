using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Song;
using YARG.Data;

namespace YARG.Song
{
    public class SongSearching
    {
        private static readonly List<(string, string)> SearchLeniency = new()
        {
            ("Æ", "AE") // Tool - Ænema
        };

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

        public SortedDictionary<string, List<SongMetadata>> Search(string value, SongAttribute sort)
        {
            var currentFilters = GetFilters(value.Split(';'));
            if (currentFilters.Count == 0)
            {
                filters.Clear();
                return GlobalVariables.Instance.SortedSongs.GetSongList(sort);
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
                    break;
                ++currFilterIndex;
            }

            if (currFilterIndex == 0 && (prevFilterIndex == 0 || currentFilters[0].attribute != SongAttribute.Unspecified))
            {
                (FilterNode, SortedDictionary<string, List<SongMetadata>>) newNode = new(currentFilters[0], SearchSongs(currentFilters[0]));
                if (prevFilterIndex < filters.Count)
                    filters[prevFilterIndex] = newNode;
                else
                    filters.Add(newNode);

                ++prevFilterIndex;
                ++currFilterIndex;
            }

            while (currFilterIndex < currentFilters.Count)
            {
                var filter = currentFilters[currFilterIndex];
                var searchList = SearchSongs(filter, Clone(filters[prevFilterIndex - 1].Item2));

                if (prevFilterIndex < filters.Count)
                    filters[prevFilterIndex] = new(filter, searchList);
                else
                    filters.Add(new(filter, searchList));

                ++currFilterIndex;
                ++prevFilterIndex;
            }

            if (prevFilterIndex < filters.Count)
                filters.RemoveRange(prevFilterIndex, filters.Count - prevFilterIndex);
            return filters[prevFilterIndex - 1].Item2;
        }

        private static SortedDictionary<string, List<SongMetadata>> Clone(SortedDictionary<string, List<SongMetadata>> original)
        {
            SortedDictionary<string, List<SongMetadata>> clone = new();
            foreach (var node in original)
                clone.Add(node.Key, new(node.Value));
            return clone;
        }

        private static List<FilterNode> GetFilters(string[] split)
        {
            List<FilterNode> nodes = new();
            foreach (string arg in split)
            {
                SongAttribute attribute;
                string argument = arg.Trim();
                if (argument == string.Empty)
                    continue;

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
                    break;
            }
            return nodes;
        }

        private static SortedDictionary<string, List<SongMetadata>> SearchSongs(FilterNode arg)
        {
            if (arg.attribute == SongAttribute.Unspecified)
            {
                var results = GlobalVariables.Instance.Container.Songs.Select(i => new
                {
                    score = Search(arg.argument, i),
                    songInfo = i
                })
                .Where(i => i.score >= 0)
                .OrderBy(i => i.score)
                .Select(i => i.songInfo).ToList();
                return new() { { "Search Results", results } };
            }

            return SearchByFilter(arg.attribute, arg.argument);
        }

        private static SortedDictionary<string, List<SongMetadata>> SearchSongs(FilterNode arg, SortedDictionary<string, List<SongMetadata>> searchList)
        {
            if (arg.attribute == SongAttribute.Unspecified)
            {
                List<SongMetadata> entriesToSearch = new();
                foreach (var entry in searchList)
                    entriesToSearch.AddRange(entry.Value);

                var results = entriesToSearch.Select(i => new
                {
                    score = Search(arg.argument, i),
                    songInfo = i
                })
                .Where(i => i.score >= 0)
                .OrderBy(i => i.score)
                .Select(i => i.songInfo).ToList();
                searchList = new() { { "Search Results", results } };
            }
            else
            {
                Predicate<SongMetadata> match = arg.attribute switch
                {
                    SongAttribute.Name => entry => !RemoveArticle(entry.Name.SortStr).Contains(arg.argument),
                    SongAttribute.Artist => entry => !RemoveArticle(entry.Artist.SortStr).Contains(arg.argument),
                    SongAttribute.Album => entry => !entry.Album.SortStr.Contains(arg.argument),
                    SongAttribute.Genre => entry => !entry.Genre.SortStr.Contains(arg.argument),
                    SongAttribute.Year => entry => !entry.Year.Contains(arg.argument) && !entry.UnmodifiedYear.Contains(arg.argument),
                    SongAttribute.Charter => entry => !entry.Charter.SortStr.Contains(arg.argument),
                    SongAttribute.Playlist => entry => !entry.Playlist.SortStr.Contains(arg.argument),
                    SongAttribute.Source => entry => !entry.Source.SortStr.Contains(arg.argument),
                    _ => throw new Exception("HOW")
                };

                List<string> removals = new();
                foreach (var entry in searchList)
                {
                    entry.Value.RemoveAll(match);
                    if (entry.Value.Count == 0)
                        removals.Add(entry.Key);
                }

                foreach (var entry in removals)
                    searchList.Remove(entry);
            }
            return searchList;
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

        private static int Search(string input, SongMetadata songInfo)
        {
            // Get name index
            string name = songInfo.Name.SortStr;
            int nameIndex = name.IndexOf(input, StringComparison.Ordinal);

            // Get artist index
            string artist = songInfo.Artist.SortStr;
            int artistIndex = artist.IndexOf(input, StringComparison.Ordinal);

            // Return the best search
            if (nameIndex == -1 && artistIndex == -1)
            {
                return -1;
            }

            if (nameIndex == -1)
            {
                return artistIndex;
            }

            if (artistIndex == -1)
            {
                return nameIndex;
            }

            return Mathf.Min(nameIndex, artistIndex);
        }

        private static SortedDictionary<string, List<SongMetadata>> SearchByFilter(SongAttribute sort, string arg)
        {
            if (sort == SongAttribute.Name)
            {
                if (arg.Length == 0)
                {
                    SortedDictionary<string, List<SongMetadata>> titleMap = new();
                    foreach (var element in GlobalVariables.Instance.Container.Titles)
                        titleMap.Add(element.Key, new(element.Value));
                    return titleMap;
                }

                int i = 0;
                while (i + 1 < arg.Length && !char.IsLetterOrDigit(arg[i]))
                    ++i;

                char character = arg[i];
                string key = char.IsDigit(character) ? "0-9" : char.ToUpper(character).ToString();
                var search = GlobalVariables.Instance.Container.Titles[key];

                List<SongMetadata> result = new(search.Count);
                foreach (var element in search)
                    if (element.Name.SortStr.Contains(arg))
                        result.Add(element);
                return new() { { key, result } };
            }

            SortedDictionary<string, List<SongMetadata>> map = new();
            if (sort == SongAttribute.Year)
            {
                List<SongMetadata> entries = new();
                foreach (var element in GlobalVariables.Instance.Container.Years)
                    foreach (var entry in element.Value)
                        if (entry.Year.Contains(arg))
                            entries.Add(entry);
                return new() { { arg, entries } };
            }

            var elements = sort switch
            {
                SongAttribute.Artist => GlobalVariables.Instance.Container.Artists,
                SongAttribute.Album => GlobalVariables.Instance.Container.Albums,
                SongAttribute.Source => GlobalVariables.Instance.Container.Sources,
                SongAttribute.Genre => GlobalVariables.Instance.Container.Genres,
                SongAttribute.Charter => GlobalVariables.Instance.Container.Charters,
                SongAttribute.Playlist => GlobalVariables.Instance.Container.Playlists,
                SongAttribute.Instrument => GlobalVariables.Instance.Container.Instruments,
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
                    key = RemoveArticle(key);

                if (key.Contains(arg))
                    map.Add(element.Key, new(element.Value));
            }
            return map;
        }
    }
}