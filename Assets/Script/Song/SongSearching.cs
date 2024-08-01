using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Song
{
    public class SongSearching
    {
        private SortAttribute _sort;
        private SongCategory[] _baseList;
        private List<SearchNode> searches = new();

        public void Reset()
        {
            _baseList = null;
        }

        public SongCategory[] Search(string value, SortAttribute sort)
        {
            var filters = GetFilters(value.Split(';'));
            int filterIndex = 0;

            if (_baseList != null && _sort == sort)
            {
                int removeIndex = 0;
                while (filterIndex < filters.Count && filterIndex < searches.Count)
                {
                    var curr = filters[filterIndex];
                    var prev = searches[filterIndex];

                    if (curr.Attribute != prev.Attribute || curr.Mode != prev.Mode)
                    {
                        break;
                    }

                    int subIndex = 0;
                    while (subIndex < prev.Nodes.Count)
                    {
                        var (Argument, _) = prev.Nodes[subIndex];
                        if (!curr.Argument.StartsWith(Argument))
                        {
                            break;
                        }
                        ++subIndex;
                    }

                    ++removeIndex;
                    if (subIndex < prev.Nodes.Count)
                    {
                        prev.Nodes.RemoveRange(subIndex, prev.Nodes.Count - subIndex);
                        // The argument is the same as one that was already searched, so we can skip to the next filter
                        if (subIndex > 0 && prev.Nodes[^1].Argument.Length == curr.Argument.Length)
                        {
                            ++filterIndex;
                        }
                        break;
                    }
                    else if (prev.Nodes[^1].Argument.Length != curr.Argument.Length)
                    {
                        break;
                    }

                    ++filterIndex;
                }

                searches.RemoveRange(removeIndex, searches.Count - removeIndex);
            }
            else
            {
                searches.Clear();
                _sort = sort;
                _baseList = SongContainer.GetSortedCategory(sort);
            }

            while (filterIndex < filters.Count)
            {
                var filter = filters[filterIndex];
                var searchList = filterIndex == 0 ? _baseList : searches[filterIndex - 1].Nodes[^1].Songs;
                var songs = SearchSongs(filter, searchList);
                SearchNode node;
                if (filterIndex < searches.Count)
                {
                    node = searches[filterIndex];
                }
                else
                {
                    searches.Add(node = new(filter.Attribute, filter.Mode));
                }

                node.Nodes.Add((filter.Argument, songs));
                ++filterIndex;
            }

            if (filterIndex < searches.Count)
            {
                searches.RemoveRange(filterIndex, searches.Count - filterIndex);
            }
            return searches.Count > 0 ? searches[^1].Nodes[^1].Songs : _baseList;
        }

        public bool IsUnspecified()
        {
            if (searches.Count <= 0)
            {
                return true;
            }

            return searches[^1].Attribute == SortAttribute.Unspecified;
        }

        private enum SearchMode
        {
            Contains,
            Fuzzy,
            Exact
        }

        private readonly struct FilterNode
        {
            public readonly SortAttribute Attribute;
            public readonly SearchMode Mode;
            public readonly string Argument;

            public FilterNode(SortAttribute attribute, SearchMode mode, string argument)
            {
                Attribute = attribute;
                Mode = mode;
                Argument = argument;
            }
        }

        private readonly struct SearchNode
        {
            public readonly SortAttribute Attribute;
            public readonly SearchMode Mode;
            public readonly List<(string Argument, SongCategory[] Songs)> Nodes;

            public SearchNode(SortAttribute attribute, SearchMode mode)
            {
                Attribute = attribute;
                Mode = mode;
                Nodes = new();
            }
        }

        private static readonly (string Name, SortAttribute Attribute)[] INSTRUMENTS;

        static SongSearching()
        {
            var instruments = (Instrument[]) Enum.GetValues(typeof(Instrument));

            INSTRUMENTS = new (string, SortAttribute)[instruments.Length];
            for (int i = 0; i < instruments.Length; ++i)
            {
                var attribute = instruments[i].ToSortAttribute();
                INSTRUMENTS[i].Name = attribute.ToString().ToLower() + ':';
                INSTRUMENTS[i].Attribute = attribute;
            }
        }

        private static List<FilterNode> GetFilters(string[] split)
        {
            var filters = new List<FilterNode>();
            foreach (string arg in split)
            {
                SortAttribute attribute;
                string argument = RemoveUnwantedWhitespace(arg).ToLowerInvariant();
                if (argument == string.Empty)
                {
                    continue;
                }

                if (argument.StartsWith("artist:"))
                {
                    attribute = SortAttribute.Artist;
                    argument = SortString.RemoveDiacritics(argument[7..]);
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
                    argument = SortString.RemoveDiacritics(argument[5..]);
                }
                else if (argument.StartsWith("title:"))
                {
                    attribute = SortAttribute.Name;
                    argument = SortString.RemoveDiacritics(argument[6..]);
                }
                else
                {
                    var result = Array.FindIndex(INSTRUMENTS, ins => argument.StartsWith(ins.Name));
                    if (result >= 0)
                    {
                        attribute = INSTRUMENTS[result].Attribute;
                        argument = argument[INSTRUMENTS[result].Name.Length..];
                    }
                    else
                    {
                        attribute = SortAttribute.Unspecified;
                        argument = SortString.RemoveDiacritics(argument);
                    }
                }

                var filterIndex = filters.FindIndex(set => set.Attribute == attribute);
                if (filterIndex == -1)
                {
                    SearchMode mode;
                    (argument, mode) = ParseArgument(argument);
                    if (argument.Length > 0 || attribute > SortAttribute.Instrument)
                    {
                        filters.Add(new FilterNode(attribute, mode, argument));
                    }
                }

                if (attribute == SortAttribute.Unspecified)
                {
                    break;
                }
            }
            return filters;

            static unsafe (string Argument, SearchMode Mode) ParseArgument(string arg)
            {
                int beginOffset = arg.Length > 0 && arg[0] == ' ' ? 1 : 0;
                if (arg.Length >= 2 && arg[beginOffset] == '\"' && arg[^1] == '\"')
                {
                    beginOffset += arg[beginOffset + 1] <= 32 ? 2 : 1;
                    int endOffset = beginOffset < arg.Length - 2 && arg[^2] <= 32 ? 2 : 1;
                    return (arg[beginOffset..(arg.Length - endOffset)], SearchMode.Exact);
                }
                return (beginOffset == 0 ? arg : arg[beginOffset..], SearchMode.Fuzzy);
            }
        }

        private static SongCategory[] SearchSongs(in FilterNode filter, SongCategory[] searchList)
        {
            if (filter.Attribute == SortAttribute.Unspecified)
            {
                List<SongEntry> entriesToSearch = new();
                foreach (var entry in searchList)
                {
                    entriesToSearch.AddRange(entry.Songs);
                }
                return new SongCategory[] { new("Search Results", UnspecifiedSearch(filter, entriesToSearch)) };
            }

            if (filter.Attribute >= SortAttribute.FiveFretGuitar)
            {
                return SearchInstrument(filter, searchList);
            }


            var match = GetPredicate(filter);
            var result = new SongCategory[searchList.Length];
            int count = 0;
            foreach (var node in searchList)
            {
                var entries = node.Songs.Where(match).ToArray();
                if (entries.Length > 0)
                {
                    result[count++] = new SongCategory(node.Category, entries);
                }
            }
            return result[..count];
        }

        private static Func<SongEntry, bool> GetPredicate(FilterNode filter)
        {
            return filter.Mode switch
            {
                SearchMode.Fuzzy => filter.Attribute switch
                {
                    SortAttribute.Name => entry => IsAboveFuzzyThreshold(entry.Name.SearchStr, filter.Argument),
                    SortAttribute.Artist => entry => IsAboveFuzzyThreshold(entry.Artist.SearchStr, filter.Argument),
                    SortAttribute.Album => entry => IsAboveFuzzyThreshold(entry.Album.SearchStr, filter.Argument),
                    SortAttribute.Genre => entry => IsAboveFuzzyThreshold(entry.Genre.SearchStr, filter.Argument),
                    SortAttribute.Year => entry => entry.Year.Contains(filter.Argument) || entry.UnmodifiedYear.Contains(filter.Argument),
                    SortAttribute.Charter => entry => IsAboveFuzzyThreshold(entry.Charter.SearchStr, filter.Argument),
                    SortAttribute.Playlist => entry => IsAboveFuzzyThreshold(entry.Playlist.SearchStr, filter.Argument),
                    SortAttribute.Source => entry => IsAboveFuzzyThreshold(entry.Source.SearchStr, filter.Argument),
                    _ => throw new Exception("Unhandled seacrh filter")
                },
                SearchMode.Exact => filter.Attribute switch
                {
                    SortAttribute.Name => entry => entry.Name.SearchStr == filter.Argument,
                    SortAttribute.Artist => entry => entry.Artist.SearchStr == filter.Argument,
                    SortAttribute.Album => entry => entry.Album.SearchStr == filter.Argument,
                    SortAttribute.Genre => entry => entry.Genre.SearchStr == filter.Argument,
                    SortAttribute.Year => entry => entry.Year == filter.Argument || entry.UnmodifiedYear == filter.Argument,
                    SortAttribute.Charter => entry => entry.Charter.SearchStr == filter.Argument,
                    SortAttribute.Playlist => entry => entry.Playlist.SearchStr == filter.Argument,
                    SortAttribute.Source => entry => entry.Source.SearchStr == filter.Argument,
                    _ => throw new Exception("Unhandled seacrh filter")
                },
                _ => throw new Exception("Unused Mode type"),
            };
        }

        private readonly struct UnspecifiedSortNode : IComparable<UnspecifiedSortNode>
        {
            public readonly SongEntry Song;
            public readonly int Rank;

            private readonly int _nameIndex;
            private readonly int _artistIndex;
            private readonly SearchMode _mode;

#nullable enable
            public UnspecifiedSortNode(SongEntry song, in FilterNode filter)
#nullable disable
            {
                Song = song;
                if (filter.Mode == SearchMode.Exact)
                {
                    _mode = SearchMode.Exact;
                    _nameIndex = song.Name.SearchStr == filter.Argument ? 0 : -1;
                    _artistIndex = song.Artist.SearchStr == filter.Argument ? 0 : -1;
                }
                else
                {
                    _mode = SearchMode.Fuzzy;
                    bool nameFuzzy = IsAboveFuzzyThreshold(song.Name.SearchStr, filter.Argument);
                    bool artistFuzzy = IsAboveFuzzyThreshold(song.Artist.SearchStr, filter.Argument);

                    if (nameFuzzy || artistFuzzy)
                    {
                        _nameIndex = song.Name.SearchStr.IndexOf(filter.Argument, StringComparison.Ordinal);
                        _artistIndex = song.Artist.SearchStr.IndexOf(filter.Argument, StringComparison.Ordinal);

                        if (_nameIndex >= 0 || _artistIndex >= 0)
                        {
                            _mode = SearchMode.Contains;
                        }
                        else
                        {
                            _nameIndex = nameFuzzy ? GetIndex(song.Name.SearchStr, filter.Argument) : -1;
                            _artistIndex = artistFuzzy ? GetIndex(song.Artist.SearchStr, filter.Argument) : -1;
                        }
                    }
                    else
                    {
                        _nameIndex = -1;
                        _artistIndex = -1;
                    }
                }

                Rank = _nameIndex >= 0 && (_artistIndex < 0 || _nameIndex <= _artistIndex)
                     ? _nameIndex
                     : _artistIndex;
            }

            public int CompareTo(UnspecifiedSortNode other)
            {
                if (_mode != other._mode)
                {
                    return _mode == SearchMode.Contains ? -1 : 1;
                }

                if (Rank != other.Rank)
                {
                    return Rank - other.Rank;
                }

                if (_nameIndex >= 0)
                {
                    if (other._nameIndex < 0)
                    {
                        // Prefer Name to Artist for equality
                        // other.ArtistIndex guaranteed valid
                        return _nameIndex <= other._artistIndex ? -1 : 1;
                    }

                    if (_nameIndex != other._nameIndex)
                    {
                        return _nameIndex - other._nameIndex;
                    }
                    return Song.CompareTo(other.Song);
                }

                // this.ArtistIndex guaranteed valid from this point
                if (other._nameIndex >= 0)
                {
                    return _artistIndex < other._nameIndex ? -1 : 1;
                }

                // other.ArtistIndex guaranteed valid from this point
                if (_artistIndex != other._artistIndex)
                {
                    return _artistIndex - other._artistIndex;
                }

                int strCmp;
                if ((strCmp = Song.Artist.CompareTo(other.Song.Artist)) == 0 &&
                    (strCmp = Song.Name.CompareTo(other.Song.Name)) == 0 &&
                    (strCmp = Song.Album.CompareTo(other.Song.Album)) == 0 &&
                    (strCmp = Song.Charter.CompareTo(other.Song.Charter)) == 0)
                {
                    strCmp = Song.Directory.CompareTo(other.Song.Directory);
                }
                return strCmp;
            }

            private static int GetIndex(string songStr, string argument)
            {
                for (int i = 0; i < argument.Length; ++i)
                {
                    int index = songStr.IndexOf(argument[i]);
                    if (index >= 0)
                    {
                        return index + i;
                    }
                }
                return -1;
            }
        }

        private static SongEntry[] UnspecifiedSearch(FilterNode filter, List<SongEntry> songs)
        {
            var nodes = new List<UnspecifiedSortNode>(songs.Count);
            for (int i = 0; i < songs.Count; ++i)
            {
                var node = new UnspecifiedSortNode(songs[i], filter);
                if (node.Rank >= 0)
                {
                    nodes.Insert(~nodes.BinarySearch(node), node);
                }
            }

            var arr = new SongEntry[nodes.Count];
            for (int i = 0; i < arr.Length; ++i)
            {
                arr[i] = nodes[i].Song;
            }
            return arr;
        }

        private static SongCategory[] SearchInstrument(FilterNode filter, SongCategory[] searchList)
        {
            var songsToMatch = SongContainer.Instruments[filter.Attribute.ToInstrument()]
                .Where(node =>
                {
                    string key = node.Key.ToString();
                    if (!key.StartsWith(filter.Argument))
                    {
                        return false;
                    }
                    return key.Length == filter.Argument.Length || filter.Mode != SearchMode.Exact;
                })
                .SelectMany(node => node.Value);

            var result = new SongCategory[searchList.Length];
            int count = 0;
            foreach (var node in searchList)
            {
                var songs = node.Songs.Intersect(songsToMatch).ToArray();
                if (songs.Length > 0)
                {
                    result[count++] = new SongCategory(node.Category, songs);
                }
            }
            return result[..count];
        }

        private const double RANK_THRESHOLD = 70;
        private static bool IsAboveFuzzyThreshold(string songStr, string argument)
        {
            double threshold = RANK_THRESHOLD;
            if (argument.Length > songStr.Length)
            {
                if (songStr.Length == 0)
                {
                    return argument.Length == 1;
                }

                threshold *= (double) argument.Length / songStr.Length;
                if (threshold > 1.0)
                {
                    return false;
                }
            }
            return OptimizedFuzzySharp.PartialRatio(argument.AsSpan(), songStr.AsSpan()) >= threshold;
        }

        private static unsafe string RemoveUnwantedWhitespace(string arg)
        {
            var buffer = stackalloc char[arg.Length];
            int length = 0;
            int index = 0;
            while (index < arg.Length)
            {
                char curr = arg[index++];
                if (curr > 32 || (length > 0 && buffer[length - 1] > 32))
                {
                    if (curr > 32)
                    {
                        buffer[length++] = curr;
                    }
                    else
                    {
                        while (index < arg.Length && arg[index] <= 32)
                        {
                            index++;
                        }

                        if (index == arg.Length)
                        {
                            break;
                        }

                        buffer[length++] = ' ';
                    }
                }
            }
            return length == arg.Length ? arg : new string(buffer, 0, length);
        }
    }
}
