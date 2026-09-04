using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Player;
using YARG.Playlists;
using YARG.Scores;
using YARG.Settings;

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
        Subgenre,
        Year,
        Charter,
        Folder,
        Source,
        SongLength,
        DateAdded,
        Playcount,
        Stars,
        Percentage,
        Score,
        Playable,
        Random,

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
        AggregateDrums,
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
        public string      Category      { get; }
        public string      CategoryGroup { get; }
        public SongEntry[] Songs         { get; }
        public bool Collapsed { get; }

        public SongCategory(string category, SongEntry[] songs, string categoryGroupName, bool collapsed = false)
        {
            Category = category;
            Songs = songs;
            CategoryGroup = categoryGroupName;
            Collapsed = collapsed;
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
        private static SortedSongs _sortedSongs = new();
        private static SongEntry[] _songs = Array.Empty<SongEntry>();
        private static Dictionary<HashWrapper, List<SongEntry>> _songsByHash = new();

        private static SongCategory[] _sortTitles = Array.Empty<SongCategory>();
        private static SongCategory[] _sortArtists = Array.Empty<SongCategory>();
        private static SongCategory[] _sortAlbums = Array.Empty<SongCategory>();
        private static SongCategory[] _sortGenres = Array.Empty<SongCategory>();
        private static SongCategory[] _sortSubgenres = Array.Empty<SongCategory>();
        private static SongCategory[] _sortYears = Array.Empty<SongCategory>();
        private static SongCategory[] _sortCharters = Array.Empty<SongCategory>();
        private static SongCategory[] _sortPlaylists = Array.Empty<SongCategory>();
        private static SongCategory[] _sortSources = Array.Empty<SongCategory>();
        private static SongCategory[] _sortArtistAlbums = Array.Empty<SongCategory>();
        private static SongCategory[] _sortSongLengths = Array.Empty<SongCategory>();
        private static SongCategory[] _sortDatesAdded = Array.Empty<SongCategory>();
        private static Dictionary<Instrument, SongCategory[]> _sortInstruments = new();
        private static SongCategory[] _sortAggregateDrums = Array.Empty<SongCategory>();

        private static SongCategory[] _playables = null;
        private static SongCategory[] _sortStars = Array.Empty<SongCategory>();
        private static readonly Dictionary<SongEntry, StarAmount> _runtimeStars = new();
        private static Guid _starsCacheProfileId = Guid.Empty;
        private static Instrument _starsCacheInstrument = Instrument.Band;
        private static Difficulty _starsCacheDifficulty = Difficulty.Easy;
        private static HighScoreHistoryMode _starsCacheHighScoreHistoryMode;
        private static bool _starsCacheUsesBandScores;
        private static bool _starsCacheValid;

        public static IReadOnlyDictionary<string, List<SongEntry>> Titles => _sortedSongs.Titles;
        public static IReadOnlyDictionary<string, List<SongEntry>> Years => _sortedSongs.Years;
        public static IReadOnlyDictionary<string, List<SongEntry>> SongLengths => _sortedSongs.SongLengths;
        public static IReadOnlyDictionary<DateTime, List<SongEntry>> AddedDates => _sortedSongs.DatesAdded;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Artists => _sortedSongs.Artists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Albums => _sortedSongs.Albums;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Genres => _sortedSongs.Genres;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Subgenres => _sortedSongs.Subgenres;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Charters => _sortedSongs.Charters;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Playlists => _sortedSongs.Playlists;
        public static IReadOnlyDictionary<SortString, List<SongEntry>> Sources => _sortedSongs.Sources;
        public static IReadOnlyDictionary<SortString, SortedDictionary<SortString, List<SongEntry>>> ArtistAlbums => _sortedSongs.ArtistAlbums;
        public static IReadOnlyDictionary<Instrument, SortedDictionary<int, List<SongEntry>>> Instruments => _sortedSongs.Instruments;
        public static IReadOnlyDictionary<int, List<SongEntry>> AggregateDrums => _sortedSongs.AggregateDrums;

        public static int Count => _songs.Length;
        public static int LibraryRevision { get; private set; }
        // public static IReadOnlyDictionary<HashWrapper, List<SongEntry>> SongsByHash => _songCache.Entries;
        public static IReadOnlyDictionary<HashWrapper, List<SongEntry>> SongsByHash => _songsByHash;
        public static SongEntry[]                                       Songs       => _songs;

        public static SongEntry[] UnfilteredSongs => _songCache.Entries.Values.SelectMany(e => e).ToArray();

        private static bool AllowedByRating(SongRating rating) => rating <= SettingsManager.Settings.MaxSongRating.Value;

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
            var previousSongCache = _songCache;
            SongCache refreshedSongCache = null;
            var task = UniTask.RunOnThreadPool(() =>
            {
                refreshedSongCache = CacheHandler.RunScan(quick,
                    PathHelper.SongCachePath,
                    PathHelper.BadSongsPath,
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

            PlaylistContainer.ReplaceUpdatedSongHashes(
                FindUpdatedSongHashes(previousSongCache, refreshedSongCache));
            _songCache = refreshedSongCache;

            if (SettingsManager.Settings.Genrelizer.Value is GenrelizerMode.Genrelize && !GlobalVariables.OfflineMode)
            {
                Genrelizer.GenrelizeAll(_songCache, false);
            }
            else if (SettingsManager.Settings.Genrelizer.Value is GenrelizerMode.Overgenrelize && !GlobalVariables.OfflineMode)
            {
                Genrelizer.GenrelizeAll(_songCache, true);
            }
            else
            {
                Genrelizer.DegenrelizeAll(_songCache);
            }
            SongSorting.SortEntries(_songCache, _sortedSongs);
            FillContainers();
            stopwatch.Stop();

            YargLogger.LogFormatInfo("Scan time: {0}s", stopwatch.Elapsed.TotalSeconds);
            MusicLibraryMenu.SetReload(MusicLibraryReloadState.Full);
            SongSources.LoadSprites(context);
        }

        private static Dictionary<HashWrapper, HashWrapper> FindUpdatedSongHashes(
            SongCache previousCache, SongCache refreshedCache)
        {
            var previousByLocation = GetSongsByUniqueLocation(previousCache);
            var refreshedByLocation = GetSongsByUniqueLocation(refreshedCache);
            var replacements = new Dictionary<HashWrapper, HashWrapper>();
            var ambiguousHashes = new HashSet<HashWrapper>();

            foreach (var (location, previousSong) in previousByLocation)
            {
                if (!refreshedByLocation.TryGetValue(location, out var refreshedSong) ||
                    previousSong.Hash.Equals(refreshedSong.Hash) ||
                    refreshedCache.Entries.ContainsKey(previousSong.Hash) ||
                    ambiguousHashes.Contains(previousSong.Hash))
                {
                    continue;
                }

                // A hash can represent duplicate copies in different directories. If those
                // copies changed to different hashes, there is no unambiguous replacement.
                if (replacements.TryGetValue(previousSong.Hash, out var replacement))
                {
                    if (!replacement.Equals(refreshedSong.Hash))
                    {
                        replacements.Remove(previousSong.Hash);
                        ambiguousHashes.Add(previousSong.Hash);
                    }
                }
                else
                {
                    replacements.Add(previousSong.Hash, refreshedSong.Hash);
                }
            }

            return replacements;
        }

        private static Dictionary<string, SongEntry> GetSongsByUniqueLocation(SongCache songCache)
        {
            var songsByLocation = new Dictionary<string, SongEntry>(StringComparer.OrdinalIgnoreCase);
            var duplicateLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var song in songCache.Entries.Values.SelectMany(entries => entries))
            {
                string location = song.ActualLocation;
                if (duplicateLocations.Contains(location))
                {
                    continue;
                }

                if (!songsByLocation.TryAdd(location, song))
                {
                    songsByLocation.Remove(location);
                    duplicateLocations.Add(location);
                }
            }

            return songsByLocation;
        }

        public static SongCategory[] GetSortedCategory(SortAttribute sort)
        {
            var proposedSort = Array.Empty<SongCategory>();

            try
            {
                proposedSort = sort switch
                {
                    SortAttribute.Name         => _sortTitles,
                    SortAttribute.Artist       => _sortArtists,
                    SortAttribute.Album        => _sortAlbums,
                    SortAttribute.Genre        => _sortGenres,
                    SortAttribute.Subgenre     => _sortSubgenres,
                    SortAttribute.Year         => _sortYears,
                    SortAttribute.Charter      => _sortCharters,
                    SortAttribute.Folder       => _sortPlaylists,
                    SortAttribute.Source       => _sortSources,
                    SortAttribute.Artist_Album => _sortArtistAlbums,
                    SortAttribute.SongLength   => _sortSongLengths,
                    SortAttribute.DateAdded    => _sortDatesAdded,
                    SortAttribute.Playcount    => GetPlaycounts(),
                    SortAttribute.Playable     => GetPlayableSongs(),
                    SortAttribute.Stars        => GetStars(),
                    SortAttribute.Percentage   => GetPercentage(),
                    SortAttribute.Score        => GetScore(),
                    SortAttribute.Random       => GetRandomSort(),

                    SortAttribute.FiveFretGuitar => _sortInstruments[Instrument.FiveFretGuitar],
                    SortAttribute.FiveFretBass   => _sortInstruments[Instrument.FiveFretBass],
                    SortAttribute.FiveFretRhythm => _sortInstruments[Instrument.FiveFretRhythm],
                    SortAttribute.FiveFretCoop   => _sortInstruments[Instrument.FiveFretCoopGuitar],
                    SortAttribute.Keys           => _sortInstruments[Instrument.Keys],
                    SortAttribute.SixFretGuitar  => _sortInstruments[Instrument.SixFretGuitar],
                    SortAttribute.SixFretBass    => _sortInstruments[Instrument.SixFretBass],
                    SortAttribute.SixFretRhythm  => _sortInstruments[Instrument.SixFretRhythm],
                    SortAttribute.SixFretCoop    => _sortInstruments[Instrument.SixFretCoopGuitar],
                    SortAttribute.FourLaneDrums  => _sortInstruments[Instrument.FourLaneDrums],
                    SortAttribute.ProDrums       => _sortInstruments[Instrument.ProDrums],
                    SortAttribute.FiveLaneDrums  => _sortInstruments[Instrument.FiveLaneDrums],
                    SortAttribute.EliteDrums     => _sortInstruments[Instrument.EliteDrums],
                	SortAttribute.AggregateDrums => _sortAggregateDrums,
                    SortAttribute.ProGuitar_17   => _sortInstruments[Instrument.ProGuitar_17Fret],
                    SortAttribute.ProGuitar_22   => _sortInstruments[Instrument.ProGuitar_22Fret],
                    SortAttribute.ProBass_17     => _sortInstruments[Instrument.ProBass_17Fret],
                    SortAttribute.ProBass_22     => _sortInstruments[Instrument.ProBass_22Fret],
                    SortAttribute.ProKeys        => _sortInstruments[Instrument.ProKeys],
                    SortAttribute.Vocals         => _sortInstruments[Instrument.Vocals],
                    SortAttribute.Harmony        => _sortInstruments[Instrument.Harmony],
                    SortAttribute.Band           => _sortInstruments[Instrument.Band],
                    _                            => null
                };
            }
            catch (KeyNotFoundException)
            {
                YargLogger.LogFormatDebug("Invalid Sort Attribute: {0}", sort);
            }

            // Make life better when people go back a version and we
            // encounter sorts we don't understand by providing a
            // default rather than a blank song library
            if (proposedSort != null)
            {
                return proposedSort;
            }

            YargLogger.LogInfo("Invalid Sort Attribute. Defaulting to Name sort.");
            return _sortTitles;
        }

        public static bool HasInstrument(Instrument instrument)
        {
            return _sortInstruments.ContainsKey(instrument);
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
                            if (HasInstrument(ins))
                            {
                                foreach (var list in _sortedSongs.Instruments[ins].Values)
                                {
                                    foreach (var entry in list)
                                    {
                                        set.Add(entry);
                                    }
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

        public static void InvalidateStarsCache()
        {
            _starsCacheValid = false;
            _sortStars = Array.Empty<SongCategory>();
            _runtimeStars.Clear();
        }

        // Play count sorting is intentionally not cached, as it must be regenerated after
        // every play, when profiles change, and probably a bunch of other stuff
        private static SongCategory[] GetPlaycounts()
        {
            int[] countThresholds = { 100, 50, 40, 30, 20, 10, 5, 1 };
            // This should never happen since play count shouldn't be selectable without
            // a non-bot profile and MusicLibraryMenu already checks for this, but let's double check
            if (PlayerContainer.OnlyHasBotsActive())
            {
                // Titles seems like a reasonable fallback
                return _sortTitles;
            }

            var player = PlayerContainer.Players.FirstOrDefault(e => !e.Profile.IsBot);

            if (player == null)
            {
                // This case should have been caught above, but just in case
                return _sortTitles;
            }

            // Set up an array of lists of songentries, one for each play count threshold (plus one for the unplayed header)
            var categorySongs = new List<SongEntry>[countThresholds.Length];
            for (int i = 0; i < countThresholds.Length; i++)
            {
                categorySongs[i] = new List<SongEntry>();
            }

            var counts = ScoreContainer.GetPlayedSongsForUserByPlaycount(player.Profile, SortOrdering.Descending);

            // Counts will be in descending order, so we can iterate through the list until we drop below the threshold
            // and then move to the next threshold
            int thresholdIndex = 0;
            foreach ((SongEntry song, int count) in counts)
            {
                if (count < countThresholds[thresholdIndex])
                {
                    // Increase thresholdIndex until threshold is less than or equal to count and add the song to that category
                    while (count < countThresholds[thresholdIndex] && thresholdIndex < countThresholds.Length - 1)
                    {
                        thresholdIndex++;
                    }
                }

                // Double check that we haven't run out of thresholds
                if (thresholdIndex >= countThresholds.Length)
                {
                    break;
                }

                categorySongs[thresholdIndex].Add(song);
            }

            // Get all the unplayed songs and stuff them on the end of the list
            var zeroPlaySongs = new List<SongEntry>();
            var zeroPlayCategories = new List<SongCategory>();
            var previousSort = SettingsManager.Settings.PreviousLibrarySort;

            if (previousSort == SortAttribute.Unspecified)
            {
                // I don't think this should ever happen, but I'm not certain,
                // so belt and suspenders wins.
                previousSort = SortAttribute.Name;
            }

            foreach (var category in GetSortedCategory(previousSort))
            {
                foreach (var song in category.Songs)
                {
                    if (!counts.ContainsKey(song))
                    {
                        zeroPlaySongs.Add(song);
                    }
                }

                zeroPlayCategories.Add(new SongCategory(category.Category, zeroPlaySongs.ToArray(), category.CategoryGroup));
                zeroPlaySongs.Clear();
            }

            int filledCategories = 0;
            for (int i = 0; i < countThresholds.Length; i++)
            {
                if (categorySongs[i].Count > 0)
                {
                    filledCategories++;
                }
            }

            var categories = new SongCategory[filledCategories + zeroPlayCategories.Count];

            // Build the played categories, skipping any unfilled categories

            int categoryIndex = 0;
            for (int i = 0; i < countThresholds.Length; i++)
            {
                if (categorySongs[i].Count > 0)
                {
                    categories[categoryIndex] = new SongCategory($"Played {countThresholds[i]}+ times", categorySongs[i].ToArray(), $"Played {countThresholds[i]}+ times");
                    categoryIndex++;
                }
            }

            // Now add the unplayed categories
            for (int i = 0; i < zeroPlayCategories.Count; i++)
            {
                categories[categoryIndex + i] = zeroPlayCategories[i];
            }

            return categories;
        }

        private static SongCategory[] GetStars()
        {

            if (PlayerContainer.OnlyHasBotsActive())
            {
                // If the previous sort exists and is reasonable, use that, otherwise use titles
                var previousSort = SettingsManager.Settings.PreviousLibrarySort;
                if (previousSort != SortAttribute.Stars && previousSort != SortAttribute.Playcount &&
                    previousSort != SortAttribute.Playable)
                {
                    return GetSortedCategory(previousSort);
                }

                return _sortTitles;
            }

            YargPlayer player = PlayerContainer.Players.FirstOrDefault(e => !e.Profile.IsBot);
            if (player == null)
            {
                // This case should have been caught above, but just in case
                return _sortTitles;
            }

            var profile = player.Profile;
            bool useBandScores = ScoreContainer.UseBandHighScoresForCurrentPlayers;
            var cacheInstrument = profile.GameMode == GameMode.EliteDrums
                ? Instrument.EliteDrums
                : profile.CurrentInstrument;
            if (_starsCacheValid &&
                _starsCacheProfileId == profile.Id &&
                _starsCacheInstrument == cacheInstrument &&
                _starsCacheDifficulty == profile.CurrentDifficulty &&
                _starsCacheHighScoreHistoryMode == SettingsManager.Settings.HighScoreHistory.Value &&
                _starsCacheUsesBandScores == useBandScores)
            {
                return _sortStars;
            }

            _runtimeStars.Clear();
            Dictionary<HashWrapper, StarAmount> bestStars =
                ScoreContainer.GetBestStarsForCurrentPlayers(profile);
            foreach (var song in _songs)
            {
                if (!bestStars.TryGetValue(song.Hash, out StarAmount stars))
                {
                    stars = StarAmount.None;
                }
                _runtimeStars[song] = stars;
            }

            Instrument instrument = player.Profile.CurrentInstrument;
            bool useAggregateDrums = profile.GameMode == GameMode.EliteDrums;
            IComparer<SongEntry> comparer = useAggregateDrums
                ? new AggregateDrumsIntensityComparer()
                : new IntensityComparer(instrument);

            // Use Dictionary instead of array due to complex enum values
            Dictionary<StarAmount, List<SongEntry>> grouped = new Dictionary<StarAmount, List<SongEntry>>();

            foreach (var song in _songs)
            {
                StarAmount key = StarAmount.NoPart;
                if (useAggregateDrums)
                {
                    var preferredInstrument = MidiDrumkitHelper.GetPreferredInstrumentForSong(song);
                    if (preferredInstrument.HasValue && song[preferredInstrument.Value].IsActive())
                    {
                        if (!_runtimeStars.TryGetValue(song, out key))
                        {
                            key = StarAmount.None;
                        }
                    }
                }
                else if (song[instrument].IsActive() && !_runtimeStars.TryGetValue(song, out key))
                {
                    key = StarAmount.None;
                }

                if (!grouped.TryGetValue(key, out List<SongEntry> list))
                {
                    list = new List<SongEntry>();
                    grouped[key] = list;
                }

                int index = list.BinarySearch(song, comparer);
                list.Insert(~index, song);
            }

            List<StarAmount> sortedKeys = grouped.Keys.ToList();
            sortedKeys.Sort((a, b) => b.GetSortWeight().CompareTo(a.GetSortWeight()));

            SongCategory[] starCategories = new SongCategory[sortedKeys.Count];
            int i = 0;
            foreach (var key in sortedKeys)
            {
                List<SongEntry> list = grouped[key];
                string label = key.GetDisplayName();
                starCategories[i++] = new SongCategory(label, list.ToArray(), label);
            }

            _sortStars = starCategories;
            _starsCacheProfileId = profile.Id;
            _starsCacheInstrument = cacheInstrument;
            _starsCacheDifficulty = profile.CurrentDifficulty;
            _starsCacheHighScoreHistoryMode = SettingsManager.Settings.HighScoreHistory.Value;
            _starsCacheUsesBandScores = useBandScores;
            _starsCacheValid = true;
            return _sortStars;
        }

        private static SongCategory[] GetRandomSort()
        {
            var shuffled = new List<SongEntry>(_songs);
            shuffled.Shuffle();
            return new[] { new SongCategory(string.Empty, shuffled.ToArray(), null) };
        }

        private static SongCategory[] GetPercentage()
        {
            if (PlayerContainer.OnlyHasBotsActive())
            {
                return GetFallbackScoreSort();
            }

            YargPlayer player = PlayerContainer.Players.FirstOrDefault(e => !e.Profile.IsBot);
            if (player == null)
            {
                return _sortTitles;
            }

            YargProfile profile = player.Profile;
            Instrument instrument = profile.CurrentInstrument;
            IntensityComparer comparer = new(instrument);
            string[] bucketKeys =
            {
                Localize.Key("Menu.MusicLibrary.Sort.Percentage.100"),
                Localize.Key("Menu.MusicLibrary.Sort.Percentage.90"),
                Localize.Key("Menu.MusicLibrary.Sort.Percentage.80"),
                Localize.Key("Menu.MusicLibrary.Sort.Percentage.70"),
                Localize.Key("Menu.MusicLibrary.Sort.Percentage.60"),
                Localize.Key("Menu.MusicLibrary.Sort.Percentage.50"),
                Localize.Key("Menu.MusicLibrary.Sort.Percentage.Below50"),
                Localize.Key("Menu.MusicLibrary.Sort.Unplayed"),
                Localize.Key("Menu.MusicLibrary.Sort.NoPart"),
            };
            var buckets = new List<SongEntry>[bucketKeys.Length];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new List<SongEntry>();
            }
            var percentageRecords = new Dictionary<SongEntry, PlayerScoreRecord>();

            // Prime the cache once. Its validity criteria include the profile,
            // instrument, difficulty, and High Score History mode.
            if (_songs.Length > 0)
            {
                ScoreContainer.GetBestPercentageScore(
                    _songs[0].Hash, profile.Id, instrument, allowCacheUpdate: true);
            }

            foreach (SongEntry song in _songs)
            {
                if (!song[instrument].IsActive())
                {
                    InsertSorted(buckets[^1], song, comparer);
                    continue;
                }

                PlayerScoreRecord record = ScoreContainer.GetBestPercentageScore(
                    song.Hash, profile.Id, instrument, allowCacheUpdate: false);
                if (record == null || record.GetPercent() <= 0f)
                {
                    InsertSorted(buckets[^2], song, comparer);
                    continue;
                }

                float percent = record.GetPercent() * 100f;
                int bucketIndex = percent switch
                {
                    >= 100f => 0,
                    >= 90f  => 1,
                    >= 80f  => 2,
                    >= 70f  => 3,
                    >= 60f  => 4,
                    >= 50f  => 5,
                    _       => 6,
                };
                percentageRecords[song] = record;
                buckets[bucketIndex].Add(song);
            }

            for (int i = 0; i < buckets.Length - 2; i++)
            {
                buckets[i].Sort((x, y) =>
                {
                    PlayerScoreRecord xRecord = percentageRecords[x];
                    PlayerScoreRecord yRecord = percentageRecords[y];
                    int comparison = yRecord.GetPercent().CompareTo(xRecord.GetPercent());

                    // For equal percentages, show full combos first. Then break
                    // remaining ties by instrument intensity and title.
                    if (comparison == 0)
                    {
                        comparison = yRecord.IsFc.CompareTo(xRecord.IsFc);
                    }

                    return comparison != 0 ? comparison : comparer.Compare(x, y);
                });
            }

            return CreateScoreCategories(bucketKeys, buckets);
        }

        private static SongCategory[] GetScore()
        {
            if (PlayerContainer.OnlyHasBotsActive())
            {
                return GetFallbackScoreSort();
            }

            YargPlayer player = PlayerContainer.Players.FirstOrDefault(e => !e.Profile.IsBot);
            if (player == null)
            {
                return _sortTitles;
            }

            YargProfile profile = player.Profile;
            Instrument instrument = profile.CurrentInstrument;
            IntensityComparer comparer = new(instrument);
            int[] thresholds = { 500000, 400000, 300000, 200000, 150000, 100000, 75000, 50000, 30000, 10000, 1 };
            var categorySongs = new List<SongEntry>[thresholds.Length];
            for (int i = 0; i < categorySongs.Length; i++)
            {
                categorySongs[i] = new List<SongEntry>();
            }

            var unplayed = new List<SongEntry>();
            var noPart = new List<SongEntry>();
            var scoreRecords = new Dictionary<SongEntry, PlayerScoreRecord>();

            if (_songs.Length > 0)
            {
                ScoreContainer.GetHighScore(
                    _songs[0].Hash, profile.Id, instrument, allowCacheUpdate: true);
            }

            foreach (SongEntry song in _songs)
            {
                if (!song[instrument].IsActive())
                {
                    InsertSorted(noPart, song, comparer);
                    continue;
                }

                PlayerScoreRecord record = ScoreContainer.GetHighScore(
                    song.Hash, profile.Id, instrument, allowCacheUpdate: false);
                if (record == null || record.Score <= 0)
                {
                    InsertSorted(unplayed, song, comparer);
                    continue;
                }

                int bucketIndex = thresholds.Length - 1;
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (record.Score >= thresholds[i])
                    {
                        bucketIndex = i;
                        break;
                    }
                }

                scoreRecords[song] = record;
                categorySongs[bucketIndex].Add(song);
            }

            var result = new List<SongCategory>();
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (categorySongs[i].Count > 0)
                {
                    categorySongs[i].Sort((x, y) =>
                    {
                        int comparison = scoreRecords[y].Score.CompareTo(scoreRecords[x].Score);

                        // Break equal scores by instrument intensity, then title.
                        return comparison != 0 ? comparison : comparer.Compare(x, y);
                    });
                    string label = Localize.Key($"Menu.MusicLibrary.Sort.Score.{thresholds[i]}");
                    result.Add(new SongCategory(label, categorySongs[i].ToArray(), label));
                }
            }

            AddScoreCategory(result, unplayed, Localize.Key("Menu.MusicLibrary.Sort.Unplayed"));
            AddScoreCategory(result, noPart, Localize.Key("Menu.MusicLibrary.Sort.NoPart"));
            return result.ToArray();
        }

        private static SongCategory[] GetFallbackScoreSort()
        {
            SortAttribute previousSort = SettingsManager.Settings.PreviousLibrarySort;
            if (previousSort != SortAttribute.Stars && previousSort != SortAttribute.Playcount &&
                previousSort != SortAttribute.Playable && previousSort != SortAttribute.Percentage &&
                previousSort != SortAttribute.Score)
            {
                return GetSortedCategory(previousSort);
            }

            return _sortTitles;
        }

        private static SongCategory[] CreateScoreCategories(string[] labels, List<SongEntry>[] buckets)
        {
            var result = new List<SongCategory>();
            for (int i = 0; i < labels.Length; i++)
            {
                AddScoreCategory(result, buckets[i], labels[i]);
            }
            return result.ToArray();
        }

        private static void AddScoreCategory(List<SongCategory> result, List<SongEntry> songs, string label)
        {
            if (songs.Count > 0)
            {
                result.Add(new SongCategory(label, songs.ToArray(), label));
            }
        }

        private static void InsertSorted(List<SongEntry> songs, SongEntry song, IntensityComparer comparer)
        {
            int index = songs.BinarySearch(song, comparer);
            songs.Insert(~index, song);
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
            InvalidateStarsCache();
            _songs = SetAllSongs(_songCache.Entries);

            _sortArtists      = Convert(_sortedSongs.Artists, SongAttribute.Artist);
            _sortAlbums       = Convert(_sortedSongs.Albums, SongAttribute.Album);
            _sortGenres       = Convert(_sortedSongs.Genres, SongAttribute.Genre);
            _sortSubgenres    = Convert(_sortedSongs.Subgenres, SongAttribute.Subgenre);
            _sortCharters     = Convert(_sortedSongs.Charters, SongAttribute.Charter);
            _sortPlaylists    = Convert(_sortedSongs.Playlists, SongAttribute.Playlist);
            _sortSources      = Convert(_sortedSongs.Sources, SongAttribute.Source);
            _sortArtistAlbums = Combine(_sortedSongs.ArtistAlbums);

            _sortTitles       = Cast(_sortedSongs.Titles);
            _sortYears        = Cast(_sortedSongs.Years);
            _sortSongLengths  = GetSongLengthSort();
            _playables = null;

            _sortDatesAdded = new SongCategory[_sortedSongs.DatesAdded.Count];
            {
                int index = 0;
                foreach (var node in _sortedSongs.DatesAdded)
                {
                    _sortDatesAdded[index++] = new(node.Key.ToLongDateString(), node.Value.ToArray(), node.Key.ToString("y"));
                }
            }

            _sortInstruments.Clear();
            foreach (var instrument in _sortedSongs.Instruments)
            {
                try
                {
                    var noPart = _songs.Where(song => !song[instrument.Key].IsActive()).ToList();
                    noPart.Sort(new IntensityComparer(instrument.Key));

                    var arr = new SongCategory[instrument.Value.Count + (noPart.Count > 0 ? 1 : 0)];
                    int index = 0;
                    AddIntensityCategories(arr, ref index, instrument.Value);
                    AddNoPartCategory(arr, ref index, noPart);
                    _sortInstruments.Add(instrument.Key, arr);
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex);
                }
            }

            var noAggregateDrumsPart = _songs.Where(song => !MidiDrumkitHelper.HasAnyDrumPart(song)).ToList();
            noAggregateDrumsPart.Sort(new AggregateDrumsIntensityComparer());
            _sortAggregateDrums = new SongCategory[
                _sortedSongs.AggregateDrums.Count + (noAggregateDrumsPart.Count > 0 ? 1 : 0)];
            {
                int index = 0;
                AddIntensityCategories(_sortAggregateDrums, ref index, _sortedSongs.AggregateDrums);
                AddNoPartCategory(_sortAggregateDrums, ref index, noAggregateDrumsPart);
            }

            static void AddIntensityCategories(SongCategory[] categories, ref int index,
                SortedDictionary<int, List<SongEntry>> intensities)
            {
                for (int intensity = 0; intensity <= 6; intensity++)
                {
                    if (!intensities.TryGetValue(intensity, out var songs))
                        continue;

                    string label = YARG.Menu.Filters.FiltersMenu.GetIntensityLabel(intensity);
                    categories[index++] = new SongCategory(label, songs.ToArray(), label);
                }

                foreach (var intensity in intensities.Where(pair => pair.Key < 0 || pair.Key > 6))
                {
                    string label = YARG.Menu.Filters.FiltersMenu.GetIntensityLabel(intensity.Key);
                    categories[index++] = new SongCategory(label, intensity.Value.ToArray(), label);
                }
            }

            static void AddNoPartCategory(SongCategory[] categories, ref int index, List<SongEntry> noPart)
            {
                if (noPart.Count == 0)
                    return;

                string label = Localize.Key("Menu.MusicLibrary.Sort.NoPart");
                categories[index++] = new SongCategory(label, noPart.ToArray(), label);
            }

            LibraryRevision++;

            static SongEntry[] SetAllSongs(Dictionary<HashWrapper, List<SongEntry>> entries)
            {
                _songsByHash.Clear();

                int songCount = 0;
                foreach (var node in entries)
                {
                    var count = node.Value.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (AllowedByRating(node.Value[i].GetSongRating(SettingsManager.Settings.CensorMatureContent.Value)))
                        {
                            songCount++;
                        }
                    }
                }

                SongEntry[] songs = new SongEntry[songCount];
                int index = 0;

                foreach (var node in entries)
                {
                    for (int i = 0; i < node.Value.Count; i++)
                    {
                        if (AllowedByRating(node.Value[i].GetSongRating(SettingsManager.Settings.CensorMatureContent.Value)))
                        {
                            if (_songsByHash.ContainsKey(node.Key))
                            {
                                _songsByHash[node.Key].Add(node.Value[i]);
                            }
                            else
                            {
                                _songsByHash.Add(node.Key, new List<SongEntry> { node.Value[i] });
                            }

                            songs[index++] = node.Value[i];
                        }
                    }
                }
                return songs;
            }

            static SongCategory[] Convert(SortedDictionary<SortString, List<SongEntry>> list, SongAttribute attribute)
            {
                var sections = new SongCategory[list.Count];

                int index = 0;
                foreach (var node in list)
                {
                    string key;
                    switch (attribute)
                    {
                        case SongAttribute.Artist:
                            key = node.Value[0].Artist;
                            break;
                        case SongAttribute.Album:
                            key = node.Value[0].Album;
                            break;
                        case SongAttribute.Charter:
                            key = node.Value[0].Charter;
                            break;
                        case SongAttribute.Genre:
                        {
                            var genre = node.Value[0].Genre;
                            if (genre.Length > 0 && char.IsLower(genre[0]))
                            {
                                key = genre[0].ToString();
                                if (genre.Length > 1)
                                {
                                    key += genre[1..];
                                }
                            }
                            else
                            {
                                key = genre;
                            }
                            break;
                        }
                        case SongAttribute.Subgenre:
                        {
                            var subgenre = string.IsNullOrEmpty(node.Value[0].Subgenre) ? node.Value[0].Genre : node.Value[0].Subgenre;
                            if (subgenre.Length > 0 && char.IsLower(subgenre[0]))
                            {
                                key = subgenre[0].ToString();
                                if (subgenre.Length > 1)
                                {
                                    key += subgenre[1..];
                                }
                            }
                            else
                            {
                                key = subgenre;
                            }
                            break;
                        }
                        case SongAttribute.Playlist:
                            key = node.Value[0].Playlist;
                            break;
                        case SongAttribute.Source:
                            key = node.Value[0].Source;
                            break;
                        default:
                            throw new ArgumentException(nameof(attribute));
                    }

                    string categoryGroupName = attribute switch
                    {
                        SongAttribute.Artist or
                        SongAttribute.Album or
                        SongAttribute.Charter => node.Key.Group switch
                        {
                            CharacterGroup.Empty or
                            CharacterGroup.AsciiSymbol => "*",
                            CharacterGroup.AsciiNumber => "0-9",
                            _ => char.ToUpperInvariant(node.Key.SortStr[0]).ToString(),
                        },
                        _ => key,
                    };
                    sections[index++] = new SongCategory(key, node.Value.ToArray(), categoryGroupName);
                }
                return sections;
            }

            static SongCategory[] GetSongLengthSort()
            {
                if (SettingsManager.Settings.SongLengthLabels.Value == SongLengthLabelMode.RangeLabels)
                {
                    return Cast(_sortedSongs.SongLengths);
                }

                var groups = new SortedDictionary<int, List<SongEntry>>();

                // The range categories and their contents are already ordered by duration.
                foreach (var range in _sortedSongs.SongLengths.Values)
                {
                    foreach (var song in range)
                    {
                        int groupIndex = song.SongLengthMilliseconds switch
                        {
                            < 180000 => 0,
                            < 300000 => 1,
                            < 420000 => 2,
                            _        => 3,
                        };

                        if (!groups.TryGetValue(groupIndex, out var group))
                        {
                            groups.Add(groupIndex, group = new List<SongEntry>());
                        }
                        group.Add(song);
                    }
                }

                string[] labelKeys =
                {
                    "Menu.Filters.Length.Short",
                    "Menu.Filters.Length.Medium",
                    "Menu.Filters.Length.Long",
                    "Menu.Filters.Length.Epic",
                };

                var sections = new SongCategory[groups.Count];
                int index = 0;
                foreach (var (groupIndex, songs) in groups)
                {
                    string label = Localize.Key(labelKeys[groupIndex]);
                    sections[index++] = new SongCategory(label, songs.ToArray(), label);
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

            static SongCategory[] Combine(SortedDictionary<SortString, SortedDictionary<SortString, List<SongEntry>>> artistAlbums)
            {
                int count = 0;
                foreach (var artist in artistAlbums)
                {
                    count += artist.Value.Count;
                }

                var sort = new SongCategory[count];
                int index = 0;
                foreach (var artist in artistAlbums)
                {
                    string categoryGroupName = artist.Key.Group switch
                    {
                        CharacterGroup.Empty or
                        CharacterGroup.AsciiSymbol => "*",
                        CharacterGroup.AsciiNumber => "0-9",
                        _ => char.ToUpperInvariant(artist.Key.SortStr[0]).ToString(),
                    };

                    foreach (var album in artist.Value)
                    {
                        sort[index++] = new SongCategory($"{album.Value[0].Artist} - {album.Value[0].Album}", album.Value.ToArray(), categoryGroupName);
                    }
                }
                return sort;
            }
        }

        public static void RequestContainerRefresh()
        {
            SongSorting.SortEntries(_songCache, _sortedSongs);
            FillContainers();
        }

        readonly struct IntensityComparer : IComparer<SongEntry>
        {
            private readonly Instrument _instrument;

            public IntensityComparer(Instrument instrument)
            {
                _instrument = instrument;
            }

            public int Compare(SongEntry x, SongEntry y)
            {
                int intensityX = x[_instrument].Intensity;
                int intensityY = y[_instrument].Intensity;

                if (intensityX == intensityY)
                {
                    // MetadataComparer sorts by title first, with the remaining
                    // metadata providing a deterministic fallback for duplicate titles.
                    return SongEntrySorting.MetadataComparer.Instance.Compare(x, y);
                }
                else if (intensityX == -1)
                {
                    return 1;
                }
                else if (intensityY == -1)
                {
                    return -1;
                }

                return intensityX.CompareTo(intensityY);
            }
        }

        readonly struct AggregateDrumsIntensityComparer : IComparer<SongEntry>
        {
            public int Compare(SongEntry x, SongEntry y)
            {
                int intensityX = GetPreferredIntensity(x);
                int intensityY = GetPreferredIntensity(y);

                if (intensityX == intensityY)
                {
                    return SongEntrySorting.MetadataComparer.Instance.Compare(x, y);
                }
                else if (intensityX == -1)
                {
                    return 1;
                }
                else if (intensityY == -1)
                {
                    return -1;
                }

                return intensityX.CompareTo(intensityY);
            }

            private static int GetPreferredIntensity(SongEntry entry)
            {
                var instrument = MidiDrumkitHelper.GetPreferredInstrumentForSong(entry);
                return instrument.HasValue ? entry[instrument.Value].Intensity : -1;
            }
        }
    }
}
