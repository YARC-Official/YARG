using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using FuzzySharp;
using YARG.Core;
using YARG.Core.Song;
using YARG.Player;

namespace YARG.Song
{
    public class SongSearching
    {
        private const double RANK_THRESHOLD = 70;
        private List<SearchNode> searches = new();

        public void ClearList()
        {
            searches.Clear();
        }

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

        private static readonly Dictionary<FilterNode, IReadOnlyList<SongCategory>> SearchCache = new();

        private static List<SongCategory> SearchSongs(FilterNode arg, IReadOnlyList<SongCategory> searchList)
        {
            if (arg.argument.Length == 0)
            {
                return searchList as List<SongCategory>;
            }

            var filterNode = SearchCache.Keys.LastOrDefault(filter =>
                arg.Argument.StartsWith(filter.Argument) && arg.Attribute == filter.Attribute);

            if (filterNode is not null && filterNode == arg)
            {
                return SearchCache[arg!] as List<SongCategory>;
            }
            var cachedSearchList = filterNode is not null ? SearchCache[filterNode] : searchList;
            if (SearchCache.Count > 3)
            {
                SearchCache.Remove(SearchCache.Keys.First());
            }

            // Cached search list modifiers
            if (arg.Attribute == SortAttribute.Unspecified)
            {
                cachedSearchList = new List<SongCategory> { new("Search Results",
                    cachedSearchList.SelectMany(category => category.Songs).ToList()) };
            }
            if (arg.attribute == SongAttribute.Instrument &&
                !cachedSearchList.Any(category => Enum.GetNames(typeof(Instrument)).Contains(category.Category)))
            {
                List<SongCategory> instSongList = new();
                var instruments = Enum.GetValues(typeof(Instrument)) as Instrument[];
                foreach (var instrument in instruments!)
                {
                    var songs = cachedSearchList.SelectMany(category => category.Songs).Where(song => song.HasInstrument(instrument)).ToList();
                    if (songs.Count > 0)
                    {
                        instSongList.Add(new SongCategory(instrument.ToString(), songs));
                    }
                }
                cachedSearchList = instSongList;
            }

            List<SongCategory> result = new();
            foreach (var node in cachedSearchList)
            {
                var entries = arg.attribute == SongAttribute.Unspecified ?
                    SearchSongList(node.Songs, arg.argument) : SearchSongList(node.Songs, arg.argument, arg.attribute);
                if (entries.Count > 0)
                {
                    result.Add(new SongCategory(node.Category, entries));
                }
            }
            SearchCache.Add(arg, result);
            return result;
        }

        private class SortNode : IComparable<SortNode>
        {
            public readonly SongEntry Song;
            public readonly int Rank;

            private static IEnumerable<SongAttribute> UnspecifiedAttributes { get; } = new[]
                { SongAttribute.Name, SongAttribute.Artist, SongAttribute.Charter };

            private readonly Dictionary<SongAttribute, double> _ranks = new();
            private readonly int _matchIndex;

            public SortNode(SongEntry song, string argument, [CanBeNull] SongAttribute[] attributes)
            {
                Song = song;

                Dictionary<double, int> matchIndices = new();

                foreach (var attribute in attributes != null && attributes.Any() ? attributes : UnspecifiedAttributes)
                {
                    string songInfo = attribute switch
                    {
                        SongAttribute.Name         => song.Name.SortStr,
                        SongAttribute.Artist       => song.Artist.SortStr,
                        SongAttribute.Album        => song.Album.SortStr,
                        SongAttribute.Genre        => song.Genre.SortStr,
                        SongAttribute.Year         => string.Empty,
                        SongAttribute.Charter      => song.Charter.SortStr,
                        SongAttribute.Playlist     => song.Playlist.SortStr,
                        SongAttribute.Source       => song.Source.SortStr,
                        _                          => throw new Exception("Unhandled attribute")
                    };

                    double rank = 0.0;
                    int index = -1;
                    if (attribute == SongAttribute.Year)
                    {
                        if (song.Year.Contains(argument))
                        {
                            rank = 100.0;
                            index = song.Year.IndexOf(argument, StringComparison.OrdinalIgnoreCase);
                        }
                        else if (song.UnmodifiedYear.Contains(argument))
                        {
                            rank = 100.0;
                            index = song.UnmodifiedYear.IndexOf(argument, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    else
                    {
                        (rank, index) = GetRankAndMatchIndex(songInfo, argument);
                    }

                    _ranks.Add(attribute, rank);
                    matchIndices.TryAdd(rank, index);
                }

                var max = _ranks.Values.Max();

                Rank = (int)Math.Floor(max);
                _matchIndex = matchIndices[max];
            }

            private static (double, int) GetRankAndMatchIndex(string songStr, string argument)
            {
                var songInfoLength = songStr.Length - argument.Length;
                var songInfoMult = argument.Length > songStr.Length ? 1.0 - Math.Abs(songInfoLength) / 100.0 : 1.0;

                rank = Fuzz.PartialRatio(argument, songStr) * songInfoMult;

                var commonChars = string.Join(string.Empty, argument.Intersect(songStr));
                var commonString = !string.IsNullOrEmpty(commonChars)
                    ? Regex.Match(argument, $"[{commonChars}]+").Value : null;

                var index = !string.IsNullOrEmpty(commonString)
                    ? songStr.IndexOf(commonString, StringComparison.OrdinalIgnoreCase) : -1;

                return (rank, index);
            }

            public int CompareTo(SortNode other)
            {
                if (Rank != other.Rank)
                {
                    return Rank > other.Rank ? 1 : -1;
                }

                if (_matchIndex != other._matchIndex)
                {
                    return _matchIndex >= 0 && _matchIndex < other._matchIndex ? 1 : -1;
                }

                return -string.Compare(Song.Name.Str, other.Song.Name.Str, StringComparison.Ordinal);
            }
        }

        private static List<SongEntry> SearchSongList(IReadOnlyList<SongEntry> songs, string argument, [CanBeNull] params SongAttribute[] attributes)
        {
            var nodes = new SortNode[songs.Count];

            Parallel.For(0, songs.Count, i => nodes[i] = new SortNode(songs[i], argument, attributes));

            var results = nodes
                .Where(node => node.Rank >= RANK_THRESHOLD)
                .OrderByDescending(i => i)
                .Select(i => i.Song).ToList();

            return results;
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