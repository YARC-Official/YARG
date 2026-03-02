using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Core.Utility;
using static YARG.Core.Song.SongEntrySorting;

namespace YARG.Menu.MusicLibrary
{
    public static class SongSorting
    {
        private readonly struct ArtistComparer : IComparer<SongEntry>
        {
            public static readonly ArtistComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                int strCmp;
                if ((strCmp = lhs.Name.CompareTo(rhs.Name)) == 0 &&
                    (strCmp = lhs.Album.CompareTo(rhs.Album)) == 0 &&
                    (strCmp = lhs.Charter.CompareTo(rhs.Charter)) == 0)
                {
                    strCmp = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
                }
                return strCmp;
            }
        }

        private readonly struct AlbumComparer : IComparer<SongEntry>
        {
            public static readonly AlbumComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                int strCmp;
                if ((strCmp = lhs.AlbumTrack.CompareTo(rhs.AlbumTrack)) == 0 &&
                    (strCmp = lhs.Name.CompareTo(rhs.Name)) == 0 &&
                    (strCmp = lhs.Album.CompareTo(rhs.Album)) == 0 &&
                    (strCmp = lhs.Charter.CompareTo(rhs.Charter)) == 0)
                {
                    strCmp = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
                }
                return strCmp;
            }
        }

        private readonly struct PlaylistComparer : IComparer<SongEntry>
        {
            public static readonly PlaylistComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                if (lhs.PlaylistTrack != rhs.PlaylistTrack)
                {
                    return lhs.PlaylistTrack.CompareTo(rhs.PlaylistTrack);
                }

                if (lhs is RBCONEntry rblhs && rhs is RBCONEntry rbrhs)
                {
                    int lhsBand = rblhs.RBBandDiff;
                    int rhsBand = rbrhs.RBBandDiff;
                    if (lhsBand != rhsBand)
                    {
                        if (lhsBand == -1)
                        {
                            return 1;
                        }
                        if (rhsBand == -1)
                        {
                            return -1;
                        }
                        return lhsBand.CompareTo(rhsBand);
                    }
                }
                return MetadataComparer.Instance.Compare(lhs, rhs);
            }
        }

        private readonly struct YearComparer : IComparer<SongEntry>
        {
            public static readonly YearComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                if (lhs.YearAsNumber != rhs.YearAsNumber)
                {
                    if (lhs.YearAsNumber == int.MaxValue)
                    {
                        return 1;
                    }
                    if (rhs.YearAsNumber == int.MaxValue)
                    {
                        return -1;
                    }
                    return lhs.YearAsNumber.CompareTo(rhs.YearAsNumber);
                }
                return MetadataComparer.Instance.Compare(lhs, rhs);
            }
        }

        private readonly struct CharterComparer : IComparer<SongEntry>
        {
            public static readonly CharterComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                int strCmp;
                if ((strCmp = lhs.AlbumTrack.CompareTo(rhs.AlbumTrack)) == 0 &&
                    (strCmp = lhs.Name.CompareTo(rhs.Name)) == 0 &&
                    (strCmp = lhs.Album.CompareTo(rhs.Album)) == 0)
                {
                    strCmp = lhs.SortBasedLocation.CompareTo(rhs.SortBasedLocation);
                }
                return strCmp;
            }
        }

        private readonly struct LengthComparer : IComparer<SongEntry>
        {
            public static readonly LengthComparer Instance = default;
            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                if (lhs.SongLengthMilliseconds != rhs.SongLengthMilliseconds)
                {
                    return lhs.SongLengthMilliseconds.CompareTo(rhs.SongLengthMilliseconds);
                }
                return MetadataComparer.Instance.Compare(lhs, rhs);
            }
        }

        private readonly struct InstrumentComparer : IComparer<SongEntry>
        {
            private readonly Instrument _instrument;
            private readonly int _intensity;

            public InstrumentComparer(Instrument instrument, int intensity)
            {
                _instrument = instrument;
                _intensity = intensity;
            }

            public readonly int Compare(SongEntry lhs, SongEntry rhs)
            {
                var otherIntensity = rhs[_instrument].Intensity;
                if (_intensity == otherIntensity)
                {
                    return MetadataComparer.Instance.Compare(lhs, rhs);
                }
                return _intensity != -1 && (otherIntensity == -1 || _intensity < otherIntensity)
                    ? -1 : 1;
            }
        }

        private static readonly unsafe delegate*<SongCache, SortedSongs, void>[] SORTERS =
        {
            &SortByTitle,    &SortByArtist,   &SortByAlbum,  &SortByGenre,       &SortBySubgenre,   &SortByYear,
            &SortByCharter,  &SortByPlaylist, &SortBySource, &SortByArtistAlbum, &SortByLength,     &SortByDateAdded,
            &SortByInstruments
        };

        internal static unsafe void SortEntries(SongCache cache, SortedSongs sorted)
        {
            sorted.Clear();
            Parallel.For(0, SORTERS.Length, i => SORTERS[i](cache, sorted));
        }

        private static void SortByTitle(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    string name = entry.Name.Group switch
                    {
                        CharacterGroup.Empty or
                        CharacterGroup.AsciiSymbol => "*",
                        CharacterGroup.AsciiNumber => "0-9",
                        _ => char.ToUpper(entry.Name.SortStr[0]).ToString(),
                    };

                    if (!sorted.Titles.TryGetValue(name, out var category))
                    {
                        sorted.Titles.Add(name, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByArtist(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var artist = entry.Artist;
                    if (!sorted.Artists.TryGetValue(artist, out var category))
                    {
                        sorted.Artists.Add(artist, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByAlbum(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var album = entry.Album;
                    if (!sorted.Albums.TryGetValue(album, out var category))
                    {
                        sorted.Albums.Add(album, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, AlbumComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByGenre(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var genre = entry.Genre;
                    if (!sorted.Genres.TryGetValue(genre, out var category))
                    {
                        sorted.Genres.Add(genre, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortBySubgenre(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var subgenre = string.IsNullOrEmpty(entry.Subgenre) ? entry.Genre : entry.Subgenre;

                    if (!sorted.Subgenres.TryGetValue(subgenre, out var category))
                    {
                        sorted.Subgenres.Add(subgenre, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByYear(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    string year = entry.YearAsNumber != int.MaxValue ? entry.ParsedYear[..^1] + "0s" : entry.ParsedYear;
                    if (!sorted.Years.TryGetValue(year, out var category))
                    {
                        sorted.Years.Add(year, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, YearComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByCharter(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var charter = entry.Charter;
                    if (!sorted.Charters.TryGetValue(charter, out var category))
                    {
                        sorted.Charters.Add(charter, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByPlaylist(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var playlist = entry.Playlist;
                    if (!sorted.Playlists.TryGetValue(playlist, out var category))
                    {
                        sorted.Playlists.Add(playlist, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, PlaylistComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortBySource(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var source = entry.Source;
                    if (!sorted.Sources.TryGetValue(source, out var category))
                    {
                        sorted.Sources.Add(source, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByLength(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    // constants represents upper milliseconds limit of each range
                    string range = entry.SongLengthMilliseconds switch
                    {
                        < 120000 => "00:00 - 02:00",
                        < 300000 => "02:00 - 05:00",
                        < 600000 => "05:00 - 10:00",
                        < 900000 => "10:00 - 15:00",
                        < 1200000 => "15:00 - 20:00",
                        _ => "20:00+",
                    };

                    if (!sorted.SongLengths.TryGetValue(range, out var category))
                    {
                        sorted.SongLengths.Add(range, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, LengthComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByDateAdded(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var dateAdded = entry.GetLastWriteTime().Date;
                    if (!sorted.DatesAdded.TryGetValue(dateAdded, out var category))
                    {
                        sorted.DatesAdded.Add(dateAdded, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, MetadataComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByArtistAlbum(SongCache cache, SortedSongs sorted)
        {
            foreach (var list in cache.Entries)
            {
                foreach (var entry in list.Value)
                {
                    var artist = entry.Artist;
                    if (!sorted.ArtistAlbums.TryGetValue(artist, out var albums))
                    {
                        sorted.ArtistAlbums.Add(artist, albums = new SortedDictionary<SortString, List<SongEntry>>());
                    }

                    var album = entry.Album;
                    if (!albums.TryGetValue(album, out var category))
                    {
                        albums.Add(album, category = new List<SongEntry>());
                    }

                    int index = category.BinarySearch(entry, AlbumComparer.Instance);
                    category.Insert(~index, entry);
                }
            }
        }

        private static void SortByInstruments(SongCache cache, SortedSongs sorted)
        {
            Parallel.ForEach(EnumExtensions<Instrument>.Values, instrument =>
            {
                SortedDictionary<int, List<SongEntry>>? intensities = null;
                foreach (var list in cache.Entries)
                {
                    foreach (var entry in list.Value)
                    {
                        var part = entry[instrument];
                        if (part.IsActive())
                        {
                            if (intensities == null)
                            {
                                lock (sorted.Instruments)
                                {
                                    sorted.Instruments.Add(instrument, intensities = new SortedDictionary<int, List<SongEntry>>());
                                }
                            }

                            if (!intensities.TryGetValue(part.Intensity, out var category))
                            {
                                intensities.Add(part.Intensity, category = new List<SongEntry>());
                            }

                            int index = category.BinarySearch(entry, new InstrumentComparer(instrument, part.Intensity));
                            category.Insert(~index, entry);
                        }
                    }
                }
            });
        }
    }
}
