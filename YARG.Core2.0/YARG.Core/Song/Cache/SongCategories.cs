using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    public interface CategoryConfig<TKey>
    {
        public EntryComparer Comparer { get; }
        public TKey GetKey(SongEntry entry);
    }

    public readonly struct TitleConfig : CategoryConfig<string>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Name);
        public EntryComparer Comparer => _COMPARER;

        public string GetKey(SongEntry entry)
        {
            var name = entry.Name;
            return name.Group switch
            {
                CharacterGroup.Empty or
                CharacterGroup.AsciiSymbol => "*",
                CharacterGroup.AsciiNumber => "0-9",
                _ => char.ToUpperInvariant(name.SortStr[0]).ToString(),
            };
        }
    }

    public readonly struct YearConfig : CategoryConfig<string>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Year);
        public EntryComparer Comparer => _COMPARER;
        public string GetKey(SongEntry entry)
        {
            return entry.YearAsNumber != int.MaxValue ? entry.Year.Remove(entry.Year.Length - 1) + "0s" : entry.Year;
        }
    }

    public readonly struct ArtistAlbumConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Album);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongEntry entry)
        {
            return SortString.Combine(entry.Artist, entry.Album);
        }
    }

    public readonly struct SongLengthConfig : CategoryConfig<string>
    {
        private const int MILLISECONDS_PER_MINUTE = 60 * 1000;
        private static readonly EntryComparer _COMPARER = new(SongAttribute.SongLength);
        public EntryComparer Comparer => _COMPARER;
        public string GetKey(SongEntry entry)
        {
            return (entry.SongLengthMilliseconds / MILLISECONDS_PER_MINUTE) switch
            {
                < 2 => "00:00 - 02:00",
                < 5 => "02:00 - 05:00",
                < 10 => "05:00 - 10:00",
                < 15 => "10:00 - 15:00",
                < 20 => "15:00 - 20:00",
                _ => "20:00+",
            };
        }
    }

    public readonly struct DateAddedConfig : CategoryConfig<DateTime>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Name);
        public EntryComparer Comparer => _COMPARER;
        public DateTime GetKey(SongEntry entry)
        {
            return entry.GetAddDate();
        }
    }

    public readonly struct ArtistConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Artist);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongEntry entry) => entry.Artist;
    }

    public readonly struct AlbumConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Album);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongEntry entry) => entry.Album;
    }

    public readonly struct GenreConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Genre);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongEntry entry) => entry.Genre;
    }

    public readonly struct CharterConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Charter);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongEntry entry) => entry.Charter;
    }

    public readonly struct PlaylistConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Playlist);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongEntry entry) => entry.Playlist;
    }

    public readonly struct SourceConfig : CategoryConfig<SortString>
    {
        private static readonly EntryComparer _COMPARER = new(SongAttribute.Source);
        public EntryComparer Comparer => _COMPARER;
        public SortString GetKey(SongEntry entry) => entry.Source;
    }

    public static class CategorySorter<TKey, TConfig>
        where TConfig : struct, CategoryConfig<TKey>
    {
        private static readonly TConfig CONFIG = default;

        public static void Add(SongEntry entry, SortedDictionary<TKey, List<SongEntry>> sections)
        {
            var key = CONFIG.GetKey(entry);

            List<SongEntry> entries;
            lock (sections)
            {
                if (!sections.TryGetValue(key, out entries))
                {
                    sections.Add(key, entries = new List<SongEntry>());
                }
            }

            lock (entries)
            {
                int index = entries.BinarySearch(entry, CONFIG.Comparer);
                entries.Insert(~index, entry);
            }
        }
    }

    public static class InstrumentSorter
    {
        private static readonly InstrumentComparer[] COMPARERS;
        static InstrumentSorter()
        {
            var instruments = (Instrument[]) Enum.GetValues(typeof(Instrument));
            COMPARERS = new InstrumentComparer[instruments.Length];
            for (int i = 0; i < instruments.Length; i++)
            {
                COMPARERS[i] = new InstrumentComparer(instruments[i]);
            }
        }

        public static void Add(SongEntry entry, SortedDictionary<Instrument, SortedDictionary<int, List<SongEntry>>> instruments)
        {
            foreach (var comparer in COMPARERS)
            {
                var part = entry[comparer.instrument];
                if (part.SubTracks <= 0)
                {
                    continue;
                }

                var entries = instruments[comparer.instrument];
                List<SongEntry> intensity;
                lock (entries)
                {
                    if (!entries.TryGetValue(part.Intensity, out intensity))
                    {
                        entries.Add(part.Intensity, intensity = new List<SongEntry>());
                    }
                }

                lock (intensity)
                {
                    int index = intensity.BinarySearch(entry, comparer);
                    intensity.Insert(~index, entry);
                }
            }
        }
    }

    public static class CategoryWriter
    {
        public static void WriteToCache<TKey>(FileStream filestream, SortedDictionary<TKey, List<SongEntry>> sections, SongAttribute attribute, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> strings = new();
            foreach (var element in sections)
            {
                foreach (var entry in element.Value)
                {
                    string str = attribute switch
                    {
                        SongAttribute.Name => entry.Name.Str,
                        SongAttribute.Artist => entry.Artist.Str,
                        SongAttribute.Album => entry.Album.Str,
                        SongAttribute.Genre => entry.Genre.Str,
                        SongAttribute.Year => entry.UnmodifiedYear,
                        SongAttribute.Charter => entry.Charter.Str,
                        SongAttribute.Playlist => entry.Playlist.Str,
                        SongAttribute.Source => entry.Source.Str,
                        _ => throw new Exception("stoopid - only string attributes can be used here"),
                    };

                    int index = strings.BinarySearch(str);
                    if (index < 0)
                    {
                        index = strings.Count;
                        strings.Add(str);
                    }

                    CategoryCacheWriteNode node;
                    if (attribute == SongAttribute.Name)
                        nodes[entry] = node = new();
                    else
                        node = nodes[entry];

                    switch (attribute)
                    {
                        case SongAttribute.Name: node.title = index; break;
                        case SongAttribute.Artist: node.artist = index; break;
                        case SongAttribute.Album: node.album = index; break;
                        case SongAttribute.Genre: node.genre = index; break;
                        case SongAttribute.Year: node.year = index; break;
                        case SongAttribute.Charter: node.charter = index; break;
                        case SongAttribute.Playlist: node.playlist = index; break;
                        case SongAttribute.Source: node.source = index; break;
                    }
                }
            }

            using MemoryStream ms = new();
            ms.Write(strings.Count, Endianness.Little);
            foreach (string str in strings)
            {
                ms.Write(str);
            }

            filestream.Write((int) ms.Length, Endianness.Little);
            filestream.Write(ms.GetBuffer(), 0, (int)ms.Length);
        }
    }
}
