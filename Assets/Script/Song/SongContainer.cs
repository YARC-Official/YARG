﻿using System.Collections.Generic;
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
using YARG.Localization;
using YARG.Core.Extensions;

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
        EliteDrums,
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
        public readonly string CategoryGroup;
        public readonly SongEntry[] Songs;

        public SongCategory(string category, SongEntry[] songs, string categoryGroupName)
        {
            Category = category;
            Songs = songs;

            CategoryGroup = categoryGroupName;
        }

        public void Deconstruct(out string category, out SongEntry[] songs)
        {
            category = Category;
            songs = Songs;
        }
    }

    public static class SongContainer
    {
        private static SongCache _songCache = new();
        private static SongEntry[] _songs = Array.Empty<SongEntry>();

        private static SongCategory[] _sortTitles = Array.Empty<SongCategory>();
        private static SongCategory[] _sortArtists = Array.Empty<SongCategory>();
        private static SongCategory[] _sortAlbums = Array.Empty<SongCategory>();
        private static SongCategory[] _sortGenres = Array.Empty<SongCategory>();
        private static SongCategory[] _sortYears = Array.Empty<SongCategory>();
        private static SongCategory[] _sortCharters = Array.Empty<SongCategory>();
        private static SongCategory[] _sortPlaylists = Array.Empty<SongCategory>();
        private static SongCategory[] _sortSources = Array.Empty<SongCategory>();
        private static SongCategory[] _sortArtistAlbums = Array.Empty<SongCategory>();
        private static SongCategory[] _sortSongLengths = Array.Empty<SongCategory>();
        private static SongCategory[] _sortDatesAdded = Array.Empty<SongCategory>();
        private static Dictionary<Instrument, SongCategory[]> _sortInstruments = new();

        private static SongCategory[] _playables = null;

        public static IReadOnlyDictionary<string, List<SongEntry>> Titles => _songCache.Titles;
        public static IReadOnlyDictionary<string, List<SongEntry>> Years => _songCache.Years;
        public static IReadOnlyDictionary<string, List<SongEntry>> SongLengths => _songCache.SongLengths;
        public static IReadOnlyDictionary<DateTime, List<SongEntry>> AddedDates => _songCache.DatesAdded;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Artists => _songCache.Artists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Albums => _songCache.Albums;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Genres => _songCache.Genres;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Charters => _songCache.Charters;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Playlists => _songCache.Playlists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Sources => _songCache.Sources;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> ArtistAlbums => _songCache.ArtistAlbums;
        public static IReadOnlyDictionary<Instrument, SortedDictionary<int, List<SongEntry>>> Instruments => _songCache.Instruments;

        public static int Count => _songs.Length;
        public static IReadOnlyDictionary<HashWrapper, List<SongEntry>> SongsByHash => _songCache.Entries;
        public static SongEntry[] Songs => _songs;

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
            SongSources.LoadSprites(context);
        }

        public static SongCategory[] GetSortedCategory(SortAttribute sort)
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
                SortAttribute.Playable => GetPlayableSongs(),

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
                SortAttribute.EliteDrums     => _sortInstruments[Instrument.EliteDrums],
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
            return _sortInstruments[instrument].Length > 0;
        }

        private static HashSet<Instrument> _instruments = null;
        private static SongCategory[] GetPlayableSongs()
        {
            HashSet<Instrument> instruments = new();
            foreach (var player in PlayerContainer.Players)
            {
                instruments.Add(player.Profile.CurrentInstrument);
            }

            if (_playables == null || !_instruments.SetEquals(instruments))
            {
                _instruments = instruments;
                if (instruments.Count == 0)
                {
                    _playables = _sortTitles;
                }
                else
                {
                    var gamemodes = new HashSet<GameMode>();
                    var queries = default(HashSet<SongEntry>);
                    foreach (var player in PlayerContainer.Players)
                    {
                        if (!gamemodes.Add(player.Profile.GameMode))
                        {
                            continue;
                        }

                        var set = new HashSet<SongEntry>();
                        foreach (var ins in player.Profile.GameMode.PossibleInstruments())
                        {
                            foreach (var list in _songCache.Instruments[ins].Values)
                            {
                                foreach (var entry in list)
                                {
                                    set.Add(entry);
                                }
                            }
                        }

                        if (queries != null)
                        {
                            queries.IntersectWith(set);
                        }
                        else
                        {
                            queries = set;
                        }
                    }

                    var arr = new SongCategory[_sortTitles.Length];
                    int categoryCount = 0;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var node = _sortTitles[i];
                        var intersect = new SongEntry[node.Songs.Length];
                        int intersectCount = 0;
                        for (int songIndex = 0; songIndex < node.Songs.Length; ++songIndex)
                        {
                            if (queries.Contains(node.Songs[songIndex]))
                            {
                                intersect[intersectCount++] = node.Songs[songIndex];
                            }
                        }

                        if (intersectCount > 0)
                        {
                            arr[categoryCount++] = new SongCategory($"Playable [{node.Category}]", intersect[..intersectCount], node.Category);
                        }
                    }
                    _playables = arr[..categoryCount];
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
                case ScanStage.CleaningDuplicates:
                    phrase = "Cleaning Duplicates...";
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
            _songs = SetAllSongs(_songCache.Entries);
        
            _sortArtists   = Convert(_songCache.Artists, SongAttribute.Artist, true);
            _sortAlbums    = Convert(_songCache.Albums, SongAttribute.Album, true);
            _sortGenres    = Convert(_songCache.Genres, SongAttribute.Genre, false);
            _sortCharters  = Convert(_songCache.Charters, SongAttribute.Charter, true);
            _sortPlaylists = Convert(_songCache.Playlists, SongAttribute.Playlist, false);
            _sortSources   = Convert(_songCache.Sources, SongAttribute.Source, false);
            _sortArtistAlbums = Convert(_songCache.ArtistAlbums, SongAttribute.Artist_Album, true);

            _sortTitles       = Cast(_songCache.Titles);
            _sortYears        = Cast(_songCache.Years);
            _sortSongLengths  = Cast(_songCache.SongLengths);
            _playables = null;

            _sortDatesAdded = new SongCategory[_songCache.DatesAdded.Count];
            {
                int index = 0;
                foreach (var node in _songCache.DatesAdded)
                {
                    _sortDatesAdded[index++] = new(node.Key.ToLongDateString(), node.Value.ToArray(), node.Key.ToString("y"));
                }
            }

            _sortInstruments.Clear();
            foreach (var instrument in _songCache.Instruments)
            {
                try
                {
                    var arr = new SongCategory[instrument.Value.Count];
                    int index = 0;
                    foreach (var difficulty in instrument.Value)
                    {
                        string categoryName = $"{instrument.Key.ToSortAttribute().ToLocalizedName()} [{difficulty.Key}]";
                        arr[index++] = new SongCategory(categoryName, difficulty.Value.ToArray(), categoryName);
                    }
                    _sortInstruments.Add(instrument.Key, arr);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex);
                }
            }

            static SongEntry[] SetAllSongs(Dictionary<HashWrapper, List<SongEntry>> entries)
            {
                int songCount = 0;
                foreach (var node in entries)
                {
                    songCount += node.Value.Count;
                }

                var songs = new SongEntry[songCount];
                int index = 0;
                foreach (var node in entries)
                {
                    for (int i = 0; i < node.Value.Count; i++)
                    {
                        songs[index++] = node.Value[i];
                    }
                }
                return songs;
            }

            static SongCategory[] Convert(SortedDictionary<SortString, List<SongEntry>> list, SongAttribute attribute, bool createCategoryGroups)
            {
                var sections = new SongCategory[list.Count];
                
                int index = 0;
                foreach (var node in list)
                {
                    string key = node.Key;

                    if (attribute == SongAttribute.Genre && key.Length > 0 && char.IsLower(key[0]))
                    {
                        key = char.ToUpperInvariant(key[0]).ToString();
                        if (node.Key.Length > 1)
                            key += node.Key.Str[1..];
                    }

                    string categoryGroupName;
                    if (createCategoryGroups)
                    {
                        categoryGroupName = node.Key.Group switch
                        {
                            CharacterGroup.Empty or
                            CharacterGroup.AsciiSymbol => "*",
                            CharacterGroup.AsciiNumber => "0-9",
                            _ => char.ToUpperInvariant(node.Key.SortStr[0]).ToString(),
                        };
                    }
                    else
                    {
                        categoryGroupName = key;
                    }
                    sections[index++] = new SongCategory(key, node.Value.ToArray(), categoryGroupName);
                }
                return sections;
            }

            static SongCategory[] Cast(SortedDictionary<string, List<SongEntry>> list)
            {
                var sections = new SongCategory[list.Count];
                int index = 0;
                foreach (var (key, section) in list)
                {
                    sections[index++] = new SongCategory(key, section.ToArray(), key);
                }
                return sections;
            }
        }
    }
}