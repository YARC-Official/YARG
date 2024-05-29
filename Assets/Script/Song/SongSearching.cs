using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
                new(sort, Instrument.FiveFretGuitar, SearchMode.Contains, string.Empty)
            };
            currentFilters.AddRange(GetFilters(value.Split(';')));

            int currFilterIndex = 1;
            int prevFilterIndex = 1;

            var regexMatch = Regex.Match(value, @"\W\S\S\S\S+\W$");

            if (searches.Count > 0 && searches[0].Filter.Attribute == sort &&
                // Invalidate cache if filter argument has a word of at least 4 characters at the end but is not the first word
                !(value.Length > searches.Last().Filter.Argument.Length && regexMatch.Success
                    && !searches.Last().Filter.Argument.EndsWith(regexMatch.Value)))
            {
                while (currFilterIndex < currentFilters.Count)
                {
                    while (prevFilterIndex < searches.Count && currentFilters[currFilterIndex].StartsWith(searches[prevFilterIndex].Filter, prevFilterIndex))
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

        private enum SearchMode
        {
            Contains,
            Fuzzy,
            Exact
        }

        private class FilterNode : IEquatable<FilterNode>
        {
            public readonly SortAttribute Attribute;
            public readonly Instrument Instrument;
            public readonly SearchMode Mode;
            public readonly string Argument;

            public FilterNode(SortAttribute attribute, Instrument instrument, SearchMode mode, string argument)
            {
                Attribute = attribute;
                Instrument = instrument;
                Mode = mode;
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
                return Mode == other.Mode && Argument == other.Argument;
            }

            public override int GetHashCode()
            {
                return Attribute.GetHashCode() ^ Argument.GetHashCode();
            }

            public bool StartsWith(FilterNode other) => StartsWith(other, other.Argument.Length);

            public bool StartsWith(FilterNode other, int maxMatchLength)
            {
                var matchLength = Math.Min(other.Argument.Length, maxMatchLength);
                if (Attribute != other.Attribute || Mode != other.Mode || !Argument.StartsWith(other.Argument.Substring(0, matchLength)))
                {
                    return false;
                }
                return Argument.Length == other.Argument.Length
                    || Mode == SearchMode.Contains
                    || (Mode == SearchMode.Fuzzy && (Attribute is SortAttribute.Instrument or SortAttribute.Year));
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
                var instrument = Instrument.FiveFretGuitar;
                var (argument, mode) = ParseArgument(arg);
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
                argument = argument.Trim();

                nodes.Add(new FilterNode(attribute, instrument, mode, argument));
                if (attribute == SortAttribute.Unspecified)
                {
                    break;
                }
            }
            return nodes;

            static unsafe (string Argument, SearchMode Mode) ParseArgument(string arg)
            {
                var buffer = stackalloc char[arg.Length];
                int length = 0;
                int index = 0;
                while (index < arg.Length)
                {
                    char c = arg[index++];
                    if (c <= 32)
                    {
                        buffer[length++] = ' ';
                        while (index < arg.Length && arg[index] <= 32)
                        {
                            ++index;
                        }
                    }
                    else
                    {
                        buffer[length++] = char.ToLowerInvariant(c);
                    }
                }

                index = 0;
                // Trim beginning
                while (index < length && buffer[index] <= 32)
                {
                    ++index;
                }

                // Time ending
                while (index < length && buffer[length - 1] <= 32)
                {
                    --length;
                }

                var mode = SearchMode.Contains;
                if (length >= index + 2 && buffer[index] == '\"' && buffer[length - 1] == '\"')
                {
                    ++index;
                    --length;
                    if (index < length && buffer[index] <= 32)
                    {
                        ++index;
                    }

                    if (index < length - 1 && buffer[length - 1] <= 32)
                    {
                        --length;
                    }

                    mode = SearchMode.Exact;
                }
                else if (length >= index + 1 && buffer[length - 1] == '~')
                {
                    --length;
                    if (index < length - 1 && buffer[length - 1] <= 32)
                    {
                        --length;
                    }
                    mode = SearchMode.Fuzzy;
                }

                var argument = new string(buffer, index, length - index);
                return (argument, mode);
            }
        }

        private static IReadOnlyList<SongCategory> SearchSongs(FilterNode filter, IReadOnlyList<SongCategory> searchList)
        {
            if (filter.Attribute == SortAttribute.Unspecified)
            {
                List<SongEntry> entriesToSearch = new();
                foreach (var entry in searchList)
                {
                    entriesToSearch.AddRange(entry.Songs);
                }
                return new List<SongCategory> { new("Search Results", UnspecifiedSearch(filter, entriesToSearch)) };
            }

            if (filter.Attribute == SortAttribute.Instrument)
            {
                return SearchInstrument(filter, searchList);
            }

            var result = new List<SongCategory>();
            var match = GetPredicate(filter);
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

        private static Predicate<SongEntry> GetPredicate(FilterNode filter)
        {
            return filter.Mode switch
            {
                SearchMode.Contains => filter.Attribute switch
                {
                    SortAttribute.Name => entry => entry.Name.SortStr.Contains(filter.Argument),
                    SortAttribute.Artist => entry => RemoveArticle(entry.Artist.SortStr).Contains(filter.Argument),
                    SortAttribute.Album => entry => entry.Album.SortStr.Contains(filter.Argument),
                    SortAttribute.Genre => entry => entry.Genre.SortStr.Contains(filter.Argument),
                    SortAttribute.Year => entry => entry.Year.Contains(filter.Argument) || entry.UnmodifiedYear.Contains(filter.Argument),
                    SortAttribute.Charter => entry => entry.Charter.SortStr.Contains(filter.Argument),
                    SortAttribute.Playlist => entry => entry.Playlist.SortStr.Contains(filter.Argument),
                    SortAttribute.Source => entry => entry.Source.SortStr.Contains(filter.Argument),
                    _ => throw new Exception("Unhandled seacrh filter")
                },
                SearchMode.Fuzzy => filter.Attribute switch
                {
                    SortAttribute.Name => entry => IsAboveFuzzyThreshold(entry.Name.SortStr, filter.Argument),
                    SortAttribute.Artist => entry => IsAboveFuzzyThreshold(RemoveArticle(entry.Artist.SortStr), filter.Argument),
                    SortAttribute.Album => entry => IsAboveFuzzyThreshold(entry.Album.SortStr, filter.Argument),
                    SortAttribute.Genre => entry => IsAboveFuzzyThreshold(entry.Genre.SortStr, filter.Argument),
                    SortAttribute.Year => entry => entry.Year.Contains(filter.Argument) || entry.UnmodifiedYear.Contains(filter.Argument),
                    SortAttribute.Charter => entry => IsAboveFuzzyThreshold(entry.Charter.SortStr, filter.Argument),
                    SortAttribute.Playlist => entry => IsAboveFuzzyThreshold(entry.Playlist.SortStr, filter.Argument),
                    SortAttribute.Source => entry => IsAboveFuzzyThreshold(entry.Source.SortStr, filter.Argument),
                    _ => throw new Exception("Unhandled seacrh filter")
                },
                SearchMode.Exact => filter.Attribute switch
                {
                    SortAttribute.Name => entry => entry.Name.SortStr == filter.Argument,
                    SortAttribute.Artist => entry => RemoveArticle(entry.Artist.SortStr) == filter.Argument,
                    SortAttribute.Album => entry => entry.Album.SortStr == filter.Argument,
                    SortAttribute.Genre => entry => entry.Genre.SortStr == filter.Argument,
                    SortAttribute.Year => entry => entry.Year == filter.Argument || entry.UnmodifiedYear == filter.Argument,
                    SortAttribute.Charter => entry => entry.Charter.SortStr == filter.Argument,
                    SortAttribute.Playlist => entry => entry.Playlist.SortStr == filter.Argument,
                    SortAttribute.Source => entry => entry.Source.SortStr == filter.Argument,
                    _ => throw new Exception("Unhandled seacrh filter")
                },
                _ => throw new Exception("Unexpected Mode type"),
            };
        }

        private class UnspecifiedSortNode : IComparable<UnspecifiedSortNode>
        {
            public readonly SongEntry Song;
            public readonly int Rank;

            private readonly int _nameIndex;
            private readonly int _artistIndex;
            private readonly SearchMode _mode;

#nullable enable
            public static UnspecifiedSortNode? TryCreate(SongEntry song, FilterNode filter)
#nullable disable
            {
                int nameIndex = -1;
                int artistIndex = -1;
                SearchMode mode;
                if (filter.Mode == SearchMode.Exact)
                {
                    mode = SearchMode.Exact;
                    nameIndex = song.Name.SortStr == filter.Argument ? 0 : -1;
                    artistIndex = song.Artist.SortStr == filter.Argument ? 0 : -1;
                }
                else
                {
                    mode = SearchMode.Fuzzy;
                    bool nameFuzzy = IsAboveFuzzyThreshold(song.Name.SortStr, filter.Argument);
                    bool artistFuzzy = IsAboveFuzzyThreshold(song.Artist.SortStr, filter.Argument);

                    if (nameFuzzy || artistFuzzy)
                    {
                        nameIndex = song.Name.SortStr.IndexOf(filter.Argument, StringComparison.Ordinal);
                        artistIndex = song.Artist.SortStr.IndexOf(filter.Argument, StringComparison.Ordinal);

                        if (nameIndex >= 0 || artistIndex >= 0)
                        {
                            mode = SearchMode.Contains;
                        }
                        else
                        {
                            nameIndex = nameFuzzy ? GetIndex(song.Name.SortStr, filter.Argument) : -1;
                            artistIndex = artistFuzzy ? GetIndex(song.Artist.SortStr, filter.Argument) : -1;
                        }
                    }
                }
                int rank = nameIndex >= 0 && (artistIndex < 0 || nameIndex <= artistIndex)
                     ? nameIndex
                     : artistIndex;
                return rank >= 0 ? new UnspecifiedSortNode(song, rank, nameIndex, artistIndex, mode) : null;
            }

            private UnspecifiedSortNode(SongEntry song, int rank, int nameIndex, int artistIndex, SearchMode mode)
            {
                Song = song;
                Rank = rank;
                _nameIndex = nameIndex;
                _artistIndex = artistIndex;
                _mode = mode;
            }

            public int CompareTo(UnspecifiedSortNode other)
            {
                if (Rank != other.Rank)
                {
                    return Rank - other.Rank;
                }

                if (_mode != other._mode)
                {
                    return _mode == SearchMode.Contains ? -1 : 1;
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
                foreach (char c in argument)
                {
                    int index = songStr.IndexOf(c);
                    if (index >= 0)
                    {
                        return index;
                    }
                }
                return -1;
            }
        }

        private static List<SongEntry> UnspecifiedSearch(FilterNode filter, IReadOnlyList<SongEntry> songs)
        {
            var nodes = new UnspecifiedSortNode[songs.Count];
            Parallel.For(0, Environment.ProcessorCount, i =>
            {
                while (i < songs.Count)
                {
                    nodes[i] = UnspecifiedSortNode.TryCreate(songs[i], filter);
                    i += Environment.ProcessorCount;
                }
            });

            var order = new List<UnspecifiedSortNode>(nodes.Length);
            foreach (var node in nodes)
            {
                if (node == null)
                {
                    continue;
                }
                order.Insert(~order.BinarySearch(node), node);
            }

            var result = new List<SongEntry>(order.Count);
            foreach (var node in order)
            {
                result.Add(node.Song);
            }
            return result;
        }

        private static List<SongCategory> SearchInstrument(FilterNode filter, IReadOnlyList<SongCategory> searchList)
        {
            var songsToMatch = SongContainer.Instruments[filter.Instrument]
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

        private static bool IsAboveFuzzyThreshold(string songStr, string argument)
        {
            var songInfoLengthDiff = songStr.Length - argument.Length;
            var songInfoMult = argument.Length > songStr.Length ? 1.0 - Math.Abs(songInfoLengthDiff) / 100.0 : 1.0;
            var rank = OptimizedFuzzySharp.PartialRatio(argument.AsSpan(), songStr.AsSpan()) * songInfoMult;
            return rank >= RANK_THRESHOLD;
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
