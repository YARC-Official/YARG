using System.Collections.Generic;
using YARG.Core.Song.Cache;
using YARG.Core.Song;
using System;
using YARG.Helpers.Extensions;
using YARG.Settings;
using YARG.Helpers;
using Cysharp.Threading.Tasks;
using YARG.Menu.MusicLibrary;
using YARG.Core.Logging;
using YARG.Core;
using YARG.Player;
using System.Linq;

namespace YARG.Song
{
    public enum SortAttribute
    {
        Unspecified,
        Name,
        Artist,
        Album,
        Artist_Album,
        Genre,
        Year,
        Charter,
        Playlist,
        Source,
        SongLength,
        DateAdded,
        Playable,

        Instrument,
        FiveFretGuitar,
        FiveFretBass,
        FiveFretRhythm,
        FiveFretCoop,
        Keys,
        SixFretGuitar,
        SixFretBass,
        SixFretRhythm,
        SixFretCoop,
        FourLaneDrums,
        ProDrums,
        FiveLaneDrums,
        ProGuitar_17,
        ProGuitar_22,
        ProBass_17,
        ProBass_22,
        ProKeys,
        Vocals,
        Harmony,
        Band
    }

    public readonly struct SongCategory
    {
        public readonly string Category;
        public readonly List<SongEntry> Songs;

        public SongCategory(string category, List<SongEntry> songs)
        {
            Category = category;
            Songs = songs;
        }

        public void Deconstruct(out string category, out List<SongEntry> songs)
        {
            category = Category;
            songs = Songs;
        }
    }

    public static class SongContainer
    {
        private static SongCache _songCache = new();
        private static List<SongEntry> _songs = new();

        private static List<SongCategory> _sortTitles = new();
        private static List<SongCategory> _sortArtists = new();
        private static List<SongCategory> _sortAlbums = new();
        private static List<SongCategory> _sortGenres = new();
        private static List<SongCategory> _sortYears = new();
        private static List<SongCategory> _sortCharters = new();
        private static List<SongCategory> _sortPlaylists = new();
        private static List<SongCategory> _sortSources = new();
        private static List<SongCategory> _sortArtistAlbums = new();
        private static List<SongCategory> _sortSongLengths = new();
        private static List<SongCategory> _sortDatesAdded = new();
        private static Dictionary<Instrument, List<SongCategory>> _sortInstruments = new();
        
        private static List<SongCategory> _playables = null;

        public static IReadOnlyDictionary<string, List<SongEntry>> Titles => _songCache.Titles;
        public static IReadOnlyDictionary<string, List<SongEntry>> Years => _songCache.Years;
        public static IReadOnlyDictionary<string, List<SongEntry>> ArtistAlbums => _songCache.ArtistAlbums;
        public static IReadOnlyDictionary<string, List<SongEntry>> SongLengths => _songCache.SongLengths;
        public static IReadOnlyDictionary<DateTime, List<SongEntry>> AddedDates => _songCache.DatesAdded;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Artists => _songCache.Artists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Albums => _songCache.Albums;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Genres => _songCache.Genres;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Charters => _songCache.Charters;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Playlists => _songCache.Playlists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Sources => _songCache.Sources;
        public static IReadOnlyDictionary<Instrument, SortedDictionary<int, List<SongEntry>>> Instruments => _songCache.Instruments;

        public static int Count => _songs.Count;
        public static IReadOnlyDictionary<HashWrapper, List<SongEntry>> SongsByHash => _songCache.Entries;
        public static IReadOnlyList<SongEntry> Songs => _songs;

#nullable enable
        public static async UniTask RunRefresh(bool quick, LoadingContext? context = null)
#nullable disable
        {
            var directories = new List<string>(SettingsManager.Settings.SongFolders);
            string setlistPath = PathHelper.SetlistPath;
            if (!string.IsNullOrEmpty(setlistPath) && !directories.Contains(setlistPath))
            {
                directories.Add(setlistPath);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var task = UniTask.RunOnThreadPool(() =>
            {
                const bool MULTITHREADING = true;
                _songCache = CacheHandler.RunScan(quick,
                    PathHelper.SongCachePath,
                    PathHelper.BadSongsPath,
                    MULTITHREADING,
                    SettingsManager.Settings.AllowDuplicateSongs.Value,
                    SettingsManager.Settings.UseFullDirectoryForPlaylists.Value,
                    directories);
            });

            while (task.Status == UniTaskStatus.Pending)
            {
                if (context != null)
                {
                    UpdateSongUi(context);
                }
                await UniTask.NextFrame();
            }
            FillContainers();
            stopwatch.Stop();

            YargLogger.LogFormatInfo("Scan time: {0}s", stopwatch.Elapsed.TotalSeconds);
            MusicLibraryMenu.SetReload(MusicLibraryReloadState.Full);
        }
        
        public static IReadOnlyList<SongCategory> GetSortedCategory(SortAttribute sort)
        {
            return sort switch
            {
                SortAttribute.Name => _sortTitles,
                SortAttribute.Artist => _sortArtists,
                SortAttribute.Album => _sortAlbums,
                SortAttribute.Genre => _sortGenres,
                SortAttribute.Year => _sortYears,
                SortAttribute.Charter => _sortCharters,
                SortAttribute.Playlist => _sortPlaylists,
                SortAttribute.Source => _sortSources,
                SortAttribute.Artist_Album => _sortArtistAlbums,
                SortAttribute.SongLength => _sortSongLengths,
                SortAttribute.DateAdded => _sortDatesAdded,
                SortAttribute.Playable => _playables,

                SortAttribute.FiveFretGuitar => _sortInstruments[Instrument.FiveFretGuitar],
                SortAttribute.FiveFretBass   => _sortInstruments[Instrument.FiveFretBass],
                SortAttribute.FiveFretRhythm => _sortInstruments[Instrument.FiveFretRhythm],
                SortAttribute.FiveFretCoop   => _sortInstruments[Instrument.FiveFretCoopGuitar   ],
                SortAttribute.Keys           => _sortInstruments[Instrument.Keys],
                SortAttribute.SixFretGuitar  => _sortInstruments[Instrument.SixFretGuitar],
                SortAttribute.SixFretBass    => _sortInstruments[Instrument.SixFretBass],
                SortAttribute.SixFretRhythm  => _sortInstruments[Instrument.SixFretRhythm],
                SortAttribute.SixFretCoop    => _sortInstruments[Instrument.SixFretCoopGuitar    ],
                SortAttribute.FourLaneDrums  => _sortInstruments[Instrument.FourLaneDrums],
                SortAttribute.ProDrums       => _sortInstruments[Instrument.ProDrums],
                SortAttribute.FiveLaneDrums  => _sortInstruments[Instrument.FiveLaneDrums],
                SortAttribute.ProGuitar_17   => _sortInstruments[Instrument.ProGuitar_17Fret],
                SortAttribute.ProGuitar_22   => _sortInstruments[Instrument.ProGuitar_22Fret],
                SortAttribute.ProBass_17     => _sortInstruments[Instrument.ProBass_17Fret],
                SortAttribute.ProBass_22     => _sortInstruments[Instrument.ProBass_22Fret],
                SortAttribute.ProKeys        => _sortInstruments[Instrument.ProKeys],
                SortAttribute.Vocals         => _sortInstruments[Instrument.Vocals],
                SortAttribute.Harmony        => _sortInstruments[Instrument.Harmony],
                SortAttribute.Band           => _sortInstruments[Instrument.Band],
                _ => throw new Exception("stoopid"),
            };
        }

        public static bool HasInstrument(Instrument instrument)
        {
            return _sortInstruments[instrument].Count > 0;
        }

        public static void ResetPlayableSongs()
        {
            _playables = null;
        }

        public static IReadOnlyList<SongCategory> GetPlayableSongs(IReadOnlyList<YargPlayer> players)
        {
            if (_playables == null)
            {
                if (players.Count == 0)
                {
                    _playables = new List<SongCategory>();
                }
                else
                {
                    var gamemodes = players.Select(player => player.Profile.GameMode).Distinct();

                    IEnumerable<SongEntry> queries = null;
                    foreach (var gamemode in gamemodes)
                    {
                        var list = gamemode.PossibleInstruments()
                            .SelectMany(instrument => _songCache.Instruments[instrument])
                            .SelectMany(group => group.Value)
                            .Distinct();

                        queries = queries == null ? list : queries.Intersect(list);
                    }

                    _playables = _sortTitles
                        .Select(node => new SongCategory($"Playable [{node.Category}]", node.Songs.Intersect(queries).ToList()))
                        .Where(node => node.Songs.Count > 0)
                        .ToList();
                }
            }

            return _playables;
        }

        public static SongEntry GetRandomSong()
        {
            return _songs.Pick();
        }

        private static void UpdateSongUi(LoadingContext context)
        {
            var tracker = CacheHandler.Progress;

            string phrase = string.Empty;
            string subText = null;
            switch (tracker.Stage)
            {
                case ScanStage.LoadingCache:
                    phrase = "Loading song cache...";
                    break;
                case ScanStage.LoadingSongs:
                    phrase = "Loading songs...";
                    break;
                case ScanStage.Sorting:
                    phrase = "Sorting songs...";
                    break;
                case ScanStage.WritingCache:
                    phrase = "Writing song cache...";
                    break;
                case ScanStage.WritingBadSongs:
                    phrase = "Writing bad songs...";
                    break;
            }

            switch (tracker.Stage)
            {
                case ScanStage.LoadingCache:
                case ScanStage.LoadingSongs:
                    subText = $"Folders Scanned: {tracker.NumScannedDirectories}\n" +
                              $"Songs Scanned: {tracker.Count}\n" +
                              $"Errors: {tracker.BadSongCount}"; break;
            }
            context.SetLoadingText(phrase, subText);
        }

        private static void FillContainers()
        {
            _songs.Clear();
            foreach (var node in _songCache.Entries)
            {
                _songs.AddRange(node.Value);
            }
            _songs.TrimExcess();
            
            _playables = null;
            Convert(_sortArtists, _songCache.Artists, SongAttribute.Artist);
            Convert(_sortAlbums, _songCache.Albums, SongAttribute.Album);
            Convert(_sortGenres, _songCache.Genres, SongAttribute.Genre);
            Convert(_sortCharters, _songCache.Charters, SongAttribute.Charter);
            Convert(_sortPlaylists, _songCache.Playlists, SongAttribute.Playlist);
            Convert(_sortSources, _songCache.Sources, SongAttribute.Source);

            Cast(_sortTitles, _songCache.Titles);
            Cast(_sortYears, _songCache.Years);
            Cast(_sortArtistAlbums, _songCache.ArtistAlbums);
            Cast(_sortSongLengths, _songCache.SongLengths);

            _sortDatesAdded.Clear();
            foreach (var node in _songCache.DatesAdded)
            {
                _sortDatesAdded.Add(new(node.Key.ToLongDateString(), node.Value));
            }

            _sortInstruments.Clear();
            foreach (var instrument in _songCache.Instruments)
            {
                var list = new List<SongCategory>();
                try
                {
                    foreach (var difficulty in instrument.Value)
                    {
                        list.Add(new SongCategory($"{instrument.Key.ToSortAttribute().ToLocalizedName()} [{difficulty.Key}]", difficulty.Value));
                    }
                    _sortInstruments.Add(instrument.Key, list);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex);
                }
            }

            static void Convert(List<SongCategory> sections, SortedDictionary<SortString, List<SongEntry>> list, SongAttribute attribute)
            {
                sections.Clear();
                foreach (var node in list)
                {
                    string key = node.Key;
                    if (attribute == SongAttribute.Genre && key.Length > 0 && char.IsLower(key[0]))
                    {
                        key = char.ToUpperInvariant(key[0]).ToString();
                        if (node.Key.Length > 1)
                            key += node.Key.Str[1..];
                    }
                    sections.Add(new(key, node.Value));
                }
            }

            static void Cast(List<SongCategory> sections, SortedDictionary<string, List<SongEntry>> list)
            {
                sections.Clear();
                foreach (var section in list)
                {
                    sections.Add(new SongCategory(section.Key, section.Value));
                }
            }
        }
    }
}