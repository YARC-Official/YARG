using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Song;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Customization;
using YARG.Song;
using YARG.Scores;

namespace YARG.Menu.ProfileInfo.Overview
{
    public class OverviewTab : MonoBehaviour
    {
        [SerializeField]
        private ProfileInfoMenu _profileInfoMenu;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private Image _profilePicture;
        [SerializeField]
        private RawImage _avatar;
        [SerializeField]
        private TextMeshProUGUI _profileExtras;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _totalScore;
        [SerializeField]
        private TextMeshProUGUI _totalStars;
        [SerializeField]
        private TextMeshProUGUI _totalFcs;

        [Space]
        [SerializeField]
        private RectTransform _statsViewport;
        [SerializeField]
        private GameObject _scrollViewPrefab;
        [SerializeField]
        private GameObject _settingsHeaderPrefab;
        [SerializeField]
        private GameObject _labeledRowPrefab;

        private RectTransform _statsContent;
        private NavigationGroup _statsNavigationGroup;
        private ScrollRect _statsScrollRect;
        private RectTransform _profileSummaryContent;
        private NavigationGroup _profileSummaryNavigationGroup;
        private ScrollRect _profileSummaryScrollRect;
        private TextMeshProUGUI _profileSummaryHeader;
        private int? _profileSummarySelectedIndex;
        private int? _statsSelectedIndex;
        private readonly TextMeshProUGUI[] _profileSummaryValues = new TextMeshProUGUI[14];
        private static readonly Dictionary<string, Sprite> InstrumentIconCache = new();

        private static readonly Instrument[] Instruments = Enum.GetValues(typeof(Instrument))
            .Cast<Instrument>()
            .Where(instrument => instrument != Instrument.Band)
            .ToArray();

        private static readonly Instrument[] VocalInstruments =
        {
            Instrument.Vocals,
            Instrument.PartyVocals,
            Instrument.Harmony
        };

        private static readonly string[] StatLabelKeys =
        {
            "Menu.ProfileInfo.Stats.AllSongsScore",
            "Menu.ProfileInfo.Stats.StarsEarned",
            "Menu.ProfileInfo.Stats.ExpertGoldStarSongs",
            "Menu.ProfileInfo.Stats.ExpertFullCombos",
            "Menu.ProfileInfo.Stats.OfficialSetlistScore"
        };

        private static readonly string[] ProfileSummaryLabelKeys =
        {
            "Menu.ProfileInfo.Stats.SongsOwned",
            "Menu.ProfileInfo.Stats.TotalSongPlays",
            "Menu.ProfileInfo.Stats.LifetimeScore",
            "Menu.ProfileInfo.Stats.LifetimeStars",
            "Menu.ProfileInfo.Stats.LifetimeFullCombos",
            "Menu.ProfileInfo.Stats.TotalTimePlayed",
            "Menu.ProfileInfo.Stats.FavoriteEngine",
            "Menu.ProfileInfo.Stats.FavoriteSong",
            "Menu.ProfileInfo.Stats.FavoriteArtist",
            "Menu.ProfileInfo.Stats.FavoriteGenre",
            "Menu.ProfileInfo.Stats.FavoriteSource",
            "Menu.ProfileInfo.Stats.AverageStars",
            "Menu.ProfileInfo.Stats.AveragePercentage",
            "Menu.ProfileInfo.Stats.LongestFullComboStreak"
        };

        private void Awake()
        {
            CreateTwoPanelLayout();
        }

        private void OnEnable()
        {
            var profile = _profileInfoMenu.CurrentProfile;
            var scores = ScoreContainer.GetAllScoresWithChecksumsByPlayerId(profile.Id)
                .Where(score => SongContainer.SongsByHash.TryGetValue(HashWrapper.Create(score.SongChecksum),
                    out var songs) && songs.Count > 0)
                .ToList();

            _profilePicture.enabled = true;
            _avatar.gameObject.SetActive(false);
            _profileName.text = profile.Name;
            _profileExtras.text = Localize.KeyFormat("Menu.ProfileInfo.Stats.SongPlays", scores.Count);

            // Make sure to cast to double to prevent overflows
            _totalScore.text = scores
                .Select(i => (double) i.Score)
                .Sum()
                .ToString("N0");
            _totalStars.text = scores
                .Select(i => (double) GetProfileStarPoints(i.Stars))
                .Sum()
                .ToString("N0");
            _totalFcs.text = scores
                .LongCount(i => i.IsFc)
                .ToString("N0");

            PopulateProfileSummary(profile.Name, scores);
            PopulateInstrumentStats(ScoreContainer.GetScoresInCurrentLibraryByPlayerId(profile.Id));
            FocusProfileSummary(true);

            if (profile.Avatar == null)
            {
                return;
            }

            _avatar.gameObject.SetActive(true);
            _avatar.texture = profile.Avatar.LoadTexture(false);
            _profilePicture.enabled = false;
        }

        private void OnDisable()
        {
            if (_profileInfoMenu.CurrentProfile.Avatar != null)
            {
                Destroy(_avatar.texture);
                _avatar.texture = null;
            }
        }

        public void NavigateUp(bool isRepeat)
        {
            GetFocusedNavigationGroup()?.SelectPrevious(isRepeat);
        }

        public void NavigateDown(bool isRepeat)
        {
            GetFocusedNavigationGroup()?.SelectNext(isRepeat);
        }

        public void SwapSide()
        {
            if (NavigationGroup.CurrentNavigationGroup == _statsNavigationGroup)
            {
                FocusProfileSummary(false);
            }
            else
            {
                FocusInstrumentStats();
            }
        }

        private void CreateTwoPanelLayout()
        {
            if (_statsViewport == null || _scrollViewPrefab == null)
            {
                Debug.LogError($"{nameof(OverviewTab)} is missing its stats viewport or scroll view prefab.", this);
                return;
            }

            // The original summary occupied a full-width strip above the stats. It is replaced
            // by independently scrollable profile and instrument panes that use the full height.
            _profileName.transform.parent.parent.gameObject.SetActive(false);
            var placeholderText = _statsViewport.GetComponent<TextMeshProUGUI>();
            placeholderText.enabled = false;
            _statsViewport.anchorMin = new Vector2(0.5f, 0);
            _statsViewport.anchorMax = Vector2.one;
            _statsViewport.anchoredPosition = Vector2.zero;
            _statsViewport.sizeDelta = Vector2.zero;

            // Reuse the same ScrollView prefab as Settings and Filters. This supplies the
            // standard vertical scrollbar, viewport mask, drag behavior, and visuals.
            var scrollView = Instantiate(_scrollViewPrefab, _statsViewport, false);
            scrollView.name = "Instrument Stats Scroll View";
            var scrollViewRect = (RectTransform) scrollView.transform;
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.anchoredPosition = Vector2.zero;
            scrollViewRect.sizeDelta = Vector2.zero;

            _statsScrollRect = scrollView.GetComponent<ScrollRect>();
            _statsScrollRect.scrollSensitivity = 50;
            _statsContent = _statsScrollRect.content;
            _statsContent.name = "Instrument Stats";
            _statsNavigationGroup = scrollView.AddComponent<NavigationGroup>();
            _statsNavigationGroup.SelectionChanged += OnInstrumentStatsSelectionChanged;
            scrollView.AddComponent<ScrollViewNavigationUpdater>();

            var layout = _statsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var sizeFitter = _statsContent.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateProfileSummaryPanel();
        }

        private void CreateProfileSummaryPanel()
        {
            var panel = new GameObject("Profile Summary", typeof(RectTransform));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(_statsViewport.parent, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = new Vector2(0.5f, 1);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;
            panelRect.pivot = new Vector2(0.5f, 1);

            var scrollView = Instantiate(_scrollViewPrefab, panelRect, false);
            scrollView.name = "Profile Summary Scroll View";
            var scrollViewRect = (RectTransform) scrollView.transform;
            scrollViewRect.anchorMin = Vector2.zero;
            scrollViewRect.anchorMax = Vector2.one;
            scrollViewRect.anchoredPosition = Vector2.zero;
            scrollViewRect.sizeDelta = Vector2.zero;

            _profileSummaryScrollRect = scrollView.GetComponent<ScrollRect>();
            _profileSummaryScrollRect.scrollSensitivity = 50;
            _profileSummaryContent = _profileSummaryScrollRect.content;
            _profileSummaryContent.name = "Profile Summary";
            _profileSummaryNavigationGroup = scrollView.AddComponent<NavigationGroup>();
            _profileSummaryNavigationGroup.SelectionChanged += OnProfileSummarySelectionChanged;
            scrollView.AddComponent<ScrollViewNavigationUpdater>();

            var layout = _profileSummaryContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var sizeFitter = _profileSummaryContent.gameObject.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = CreateHeader(_profileSummaryContent, Localize.Key("Menu.ProfileInfo.ProfileHeader"),
                headerHeight: 105,
                navigationGroup: _profileSummaryNavigationGroup);
            _profileSummaryHeader = header.GetComponentInChildren<TextMeshProUGUI>();
            _profileSummaryHeader.fontSize = 40;
            _profileSummaryHeader.fontStyle = FontStyles.Bold;
            AttachProfilePicture(header);

            for (int i = 0; i < ProfileSummaryLabelKeys.Length; i++)
            {
                _profileSummaryValues[i] = CreateValueRow(_profileSummaryContent,
                    Localize.Key(ProfileSummaryLabelKeys[i]), string.Empty, i, true, i >= 2 && i <= 4,
                    _profileSummaryNavigationGroup);
            }
        }

        private NavigationGroup GetFocusedNavigationGroup()
        {
            return NavigationGroup.CurrentNavigationGroup == _statsNavigationGroup
                ? _statsNavigationGroup
                : _profileSummaryNavigationGroup;
        }

        private void FocusProfileSummary(bool selectFirst)
        {
            if (_profileSummaryNavigationGroup == null || _profileSummaryNavigationGroup.Count == 0)
            {
                return;
            }

            _statsSelectedIndex = _statsNavigationGroup?.SelectedIndex ?? _statsSelectedIndex;
            DeactivateNavigationGroup(_statsNavigationGroup);

            if (selectFirst)
            {
                _profileSummaryNavigationGroup.SelectFirst(SelectionOrigin.Navigation);
                _profileSummaryScrollRect.verticalNormalizedPosition = 1;
            }
            else if (_profileSummarySelectedIndex is { } index && index < _profileSummaryNavigationGroup.Count)
            {
                _profileSummaryNavigationGroup.SelectAt(index, SelectionOrigin.Navigation);
            }
            else
            {
                _profileSummaryNavigationGroup.SelectFirst(SelectionOrigin.Navigation);
            }

            _profileSummaryNavigationGroup.PushNavGroupToStack();
        }

        private void FocusInstrumentStats()
        {
            if (_statsNavigationGroup == null || _statsNavigationGroup.Count == 0)
            {
                return;
            }

            _profileSummarySelectedIndex = _profileSummaryNavigationGroup.SelectedIndex
                ?? _profileSummarySelectedIndex;
            DeactivateNavigationGroup(_profileSummaryNavigationGroup);

            if (_statsSelectedIndex is { } index && index < _statsNavigationGroup.Count)
            {
                _statsNavigationGroup.SelectAt(index, SelectionOrigin.Navigation);
            }
            else
            {
                _statsNavigationGroup.SelectFirst(SelectionOrigin.Navigation);
                _statsScrollRect.verticalNormalizedPosition = 1;
            }

            _statsNavigationGroup.PushNavGroupToStack();
        }

        private void OnProfileSummarySelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null)
            {
                return;
            }

            _profileSummarySelectedIndex = _profileSummaryNavigationGroup.SelectedIndex;
            DeactivateNavigationGroup(_statsNavigationGroup);
        }

        private void OnInstrumentStatsSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null)
            {
                return;
            }

            _statsSelectedIndex = _statsNavigationGroup.SelectedIndex;
            DeactivateNavigationGroup(_profileSummaryNavigationGroup);
        }

        private static void DeactivateNavigationGroup(NavigationGroup navigationGroup)
        {
            if (navigationGroup == null)
            {
                return;
            }

            if (NavigationGroup.CurrentNavigationGroup == navigationGroup)
            {
                navigationGroup.SelectLastNavGroup();
            }
            else
            {
                navigationGroup.ClearSelection();
            }
        }

        private void PopulateProfileSummary(string profileName, List<PlayerScoreWithChecksum> scores)
        {
            if (_profileSummaryContent == null)
            {
                return;
            }

            _profileSummaryHeader.text = profileName;

            var songsOwned = SongContainer.SongsByHash.Values
                .SelectMany(songs => songs)
                .Select(song => song.Hash)
                .Distinct()
                .LongCount();
            var playHistory = GetPlayHistorySummary(scores);
            var summaryValues = new[]
            {
                songsOwned.ToString("N0"),
                scores.Count.ToString("N0"),
                scores.Sum(score => (double) score.Score).ToString("N0"),
                scores.Sum(score => (long) GetProfileStarPoints(score.Stars)).ToString("N0"),
                scores.LongCount(score => score.IsFc).ToString("N0"),
                playHistory.TotalTime,
                playHistory.FavoriteEngine,
                playHistory.MostPlayedSong,
                playHistory.MostPlayedArtist,
                playHistory.MostPlayedGenre,
                playHistory.MostPlayedSource,
                FormatAverageStars(scores),
                playHistory.AveragePercentage,
                playHistory.LongestFullComboStreak
            };
            for (int i = 0; i < _profileSummaryValues.Length; i++)
            {
                _profileSummaryValues[i].text = summaryValues[i];
            }
        }

        private static (string TotalTime, string FavoriteEngine, string MostPlayedSong,
                        string MostPlayedArtist, string MostPlayedGenre, string MostPlayedSource,
                        string LongestFullComboStreak,
                        string AveragePercentage)
            GetPlayHistorySummary(IEnumerable<PlayerScoreWithChecksum> scores)
        {
            long totalMilliseconds = 0;
            long currentFullComboStreak = 0;
            long longestFullComboStreak = 0;
            double totalPercentage = 0;
            long percentageCount = 0;
            var enginePlayCounts = new Dictionary<Guid, int>();
            var songPlayCounts = new Dictionary<HashWrapper, (string Name, int Count)>();
            var artistPlayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var genrePlayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sourcePlayCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var score in scores)
            {
                if (score.IsFc)
                {
                    currentFullComboStreak += Math.Max(0, score.NotesHit);
                    longestFullComboStreak = Math.Max(longestFullComboStreak, currentFullComboStreak);
                }
                else
                {
                    currentFullComboStreak = 0;
                }

                float percentage = score.GetPercent();
                if (!float.IsNaN(percentage) && !float.IsInfinity(percentage))
                {
                    totalPercentage += percentage;
                    percentageCount++;
                }

                if (score.EnginePresetId != Guid.Empty)
                {
                    enginePlayCounts.TryGetValue(score.EnginePresetId, out int enginePlayCount);
                    enginePlayCounts[score.EnginePresetId] = enginePlayCount + 1;
                }

                var songHash = HashWrapper.Create(score.SongChecksum);
                if (SongContainer.SongsByHash.TryGetValue(songHash, out var songs) && songs.Count > 0)
                {
                    var song = songs[0];
                    totalMilliseconds += song.SongLengthMilliseconds;

                    if (songPlayCounts.TryGetValue(songHash, out var songPlayCount))
                    {
                        songPlayCounts[songHash] = (songPlayCount.Name, songPlayCount.Count + 1);
                    }
                    else
                    {
                        songPlayCounts[songHash] = (Localize.KeyFormat(
                            "Menu.ProfileInfo.Stats.FavoriteSongValue", song.Name, song.Artist), 1);
                    }

                    string artist = song.Artist;
                    if (!string.IsNullOrWhiteSpace(artist))
                    {
                        artistPlayCounts.TryGetValue(artist, out int artistPlayCount);
                        artistPlayCounts[artist] = artistPlayCount + 1;
                    }

                    string genre = song.Genre;
                    if (!string.IsNullOrWhiteSpace(genre))
                    {
                        genrePlayCounts.TryGetValue(genre, out int genrePlayCount);
                        genrePlayCounts[genre] = genrePlayCount + 1;
                    }

                    string source = SongSources.SourceToGameName(song.Source);
                    if (!string.IsNullOrWhiteSpace(source))
                    {
                        sourcePlayCounts.TryGetValue(source, out int sourcePlayCount);
                        sourcePlayCounts[source] = sourcePlayCount + 1;
                    }
                }
            }

            var totalTime = TimeSpan.FromMilliseconds(totalMilliseconds);
            var favoriteEngineId = enginePlayCounts
                .OrderByDescending(engine => engine.Value)
                .ThenBy(engine => engine.Key)
                .Select(engine => (Guid?) engine.Key)
                .FirstOrDefault();
            string mostPlayedSong = songPlayCounts.Values
                .OrderByDescending(song => song.Count)
                .ThenBy(song => song.Name, StringComparer.OrdinalIgnoreCase)
                .Select(song => song.Name)
                .FirstOrDefault() ?? "-";
            string mostPlayedGenre = genrePlayCounts
                .OrderByDescending(genre => genre.Value)
                .ThenBy(genre => genre.Key, StringComparer.OrdinalIgnoreCase)
                .Select(genre => genre.Key)
                .FirstOrDefault() ?? "-";
            string mostPlayedArtist = artistPlayCounts
                .OrderByDescending(artist => artist.Value)
                .ThenBy(artist => artist.Key, StringComparer.OrdinalIgnoreCase)
                .Select(artist => artist.Key)
                .FirstOrDefault() ?? "-";
            string mostPlayedSource = sourcePlayCounts
                .OrderByDescending(source => source.Value)
                .ThenBy(source => source.Key, StringComparer.OrdinalIgnoreCase)
                .Select(source => source.Key)
                .FirstOrDefault() ?? "-";
            string favoriteEngine = favoriteEngineId.HasValue
                ? CustomContentManager.EnginePresets.GetPresetById(favoriteEngineId.Value)?.Name
                    ?? Localize.Key("Menu.ProfileInfo.Stats.UnknownCustomEngine")
                : "-";
            string averagePercentage = percentageCount > 0
                ? FormatPercentage(totalPercentage / percentageCount)
                : "-";

            return (Localize.KeyFormat("Menu.ProfileInfo.Stats.TotalTimePlayedValue",
                    totalTime.Days, totalTime.Hours, totalTime.Minutes),
                    favoriteEngine, mostPlayedSong, mostPlayedArtist, mostPlayedGenre, mostPlayedSource,
                Localize.KeyFormat("Menu.ProfileInfo.Stats.LongestFullComboStreakValue", longestFullComboStreak),
                averagePercentage);
        }

        private static string FormatPercentage(double percentage)
        {
            return SettingsManager.Settings.ShowPercentDecimals.Value
                ? $"{Math.Floor(percentage * 1000) / 10:0.0}%"
                : $"{Math.Floor(percentage * 100):0}%";
        }

        private static string FormatAverageStars(IReadOnlyCollection<PlayerScoreRecord> scores)
        {
            if (scores.Count == 0)
            {
                return "-";
            }

            double averageStars = scores.Average(score => GetProfileStarPoints(score.Stars));
            return averageStars.ToString(SettingsManager.Settings.ShowPercentDecimals.Value ? "0.00" : "0.0");
        }

        private void PopulateInstrumentStats(List<PlayerScoreWithChecksum> scores)
        {
            if (_statsContent == null || _settingsHeaderPrefab == null || _labeledRowPrefab == null)
            {
                Debug.LogError($"{nameof(OverviewTab)} is missing one or more stats UI prefab references.", this);
                return;
            }

            _statsNavigationGroup.ClearNavigatables();
            for (int i = _statsContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_statsContent.GetChild(i).gameObject);
            }

            if (scores == null)
            {
                return;
            }

            var librarySongs = SongContainer.SongsByHash.Values
                .SelectMany(songs => songs)
                .GroupBy(song => song.Hash)
                .Select(group => group.First())
                .ToList();
            var setlistRoots = SettingsManager.Settings.SongFolders
                .Append(PathHelper.SetlistPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var officialPackCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var officialSetlistSongHashes = librarySongs
                .Where(song => IsOfficialSetlistSong(song, setlistRoots, officialPackCache))
                .Select(song => song.Hash)
                .ToHashSet();

            var bestByDifficulty = scores
                .GroupBy(score => new
                {
                    Song = HashWrapper.Create(score.SongChecksum),
                    score.Instrument,
                    score.Difficulty
                })
                .Select(group => new
                {
                    group.Key.Song,
                    group.Key.Instrument,
                    group.Key.Difficulty,
                    HighScore = group.Max(score => score.Score),
                    BestStars = group
                        .OrderByDescending(score => GetStarPoints(score.Stars, score.Difficulty))
                        .ThenByDescending(score => score.Score)
                        .First()
                        .Stars,
                    IsFc = group.Any(score => score.IsFc)
                })
                .ToList();

            var instrumentSections = new List<(string Name, string[] Values, long Score,
                Instrument IconInstrument, bool HasOfficialSetlistScore)>();
            foreach (var instrument in Instruments)
            {
                bool isDrums = MidiDrumkitHelper.Instruments.Contains(instrument);
                if (isDrums && instrument != MidiDrumkitHelper.Instruments[0]) continue;

                bool isVocals = VocalInstruments.Contains(instrument);
                if (isVocals && instrument != VocalInstruments[0]) continue;

                var groupedInstruments = isDrums
                    ? MidiDrumkitHelper.Instruments
                    : isVocals
                        ? VocalInstruments
                    : new[] { instrument };
                var instrumentScores = bestByDifficulty
                    .Where(score => groupedInstruments.Contains(score.Instrument))
                    .ToList();
                var cumulativeHighScore = instrumentScores
                    .GroupBy(score => score.Song)
                    .Sum(song => (long) song.Max(score => score.HighScore));
                if (cumulativeHighScore == 0)
                {
                    continue;
                }

                var chartedSongs = librarySongs
                    .Where(song => groupedInstruments.Any(song.HasInstrument))
                    .ToList();
                var expertChartCount = instrument is Instrument.Vocals or Instrument.Harmony or
                    Instrument.PartyVocals
                    ? chartedSongs.Count
                    : chartedSongs.LongCount(song => groupedInstruments.Any(groupedInstrument =>
                        song.HasDifficultyForInstrument(groupedInstrument, Difficulty.Expert)));
                var expertScores = instrumentScores
                    .Where(score => score.Difficulty == Difficulty.Expert)
                    .ToList();
                var goldStarSongCount = expertScores
                    .GroupBy(score => score.Song)
                    .LongCount(song => song.Any(score => score.BestStars == StarAmount.StarGold));
                var totalStars = instrumentScores
                    .GroupBy(score => score.Song)
                    .Sum(song => (long) song.Max(score => GetStarPoints(score.BestStars, score.Difficulty)));
                var expertFullComboCount = expertScores
                    .GroupBy(score => score.Song)
                    .LongCount(song => song.Any(score => score.IsFc));
                var officialSetlistScore = instrumentScores
                    .Where(score => officialSetlistSongHashes.Contains(score.Song))
                    .GroupBy(score => score.Song)
                    .Sum(song => (long) song.Max(score => score.HighScore));
                var maximumStars = chartedSongs.Count * 6L;
                var statValues = new[]
                {
                    cumulativeHighScore.ToString("N0"),
                    FormatProgress(totalStars, maximumStars),
                    FormatProgress(goldStarSongCount, expertChartCount),
                    FormatProgress(expertFullComboCount, expertChartCount),
                    officialSetlistScore.ToString("N0")
                };

                instrumentSections.Add((
                    isDrums
                        ? Localize.Key("Menu.ProfileInfo.Instruments.AggregateDrums")
                        : isVocals
                            ? Localize.Key("Menu.ProfileInfo.Instruments.AggregateVocals")
                        : GetInstrumentName(instrument),
                    statValues,
                    cumulativeHighScore,
                    isDrums ? Instrument.FourLaneDrums : isVocals ? Instrument.Harmony : instrument,
                    officialSetlistScore > 0));
            }

            foreach (var section in instrumentSections.OrderByDescending(section => section.Score))
            {
                CreateInstrumentSection(section.Name, section.Values, section.IconInstrument,
                    section.HasOfficialSetlistScore);
            }

        }

        private static bool IsOfficialSetlistSong(SongEntry song, IEnumerable<string> setlistRoots,
            IDictionary<string, bool> officialPackCache)
        {
            if (string.IsNullOrWhiteSpace(song.ActualLocation))
            {
                return false;
            }

            try
            {
                string songPath = Path.GetFullPath(song.ActualLocation);
                foreach (string root in setlistRoots)
                {
                    string relativePath = Path.GetRelativePath(Path.GetFullPath(root), songPath);
                    if (relativePath == ".." ||
                        relativePath.StartsWith($"..{Path.DirectorySeparatorChar}",
                            StringComparison.Ordinal) ||
                        Path.IsPathRooted(relativePath))
                    {
                        continue;
                    }

                    int separatorIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string packRoot = Path.Combine(Path.GetFullPath(root),
                        relativePath[..separatorIndex]);
                    if (!officialPackCache.TryGetValue(packRoot, out bool isOfficial))
                    {
                        string tagPath = Path.Combine(packRoot, "tag.txt");
                        isOfficial = File.Exists(tagPath) && File.ReadAllText(tagPath).Trim()
                            .StartsWith("official-", StringComparison.OrdinalIgnoreCase);
                        officialPackCache.Add(packRoot, isOfficial);
                    }

                    return isOfficial;
                }

                return false;
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or
                NotSupportedException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private void CreateInstrumentSection(string instrumentName, string[] statValues, Instrument instrument,
            bool hasOfficialSetlistScore)
        {
            CreateHeader(_statsContent, instrumentName, GetInstrumentIcon(instrument),
                navigationGroup: _statsNavigationGroup);

            for (int statIndex = 0; statIndex < StatLabelKeys.Length; statIndex++)
            {
                if (statIndex == StatLabelKeys.Length - 1 && !hasOfficialSetlistScore)
                {
                    continue;
                }

                CreateValueRow(_statsContent, Localize.Key(StatLabelKeys[statIndex]), statValues[statIndex],
                    statIndex, true, false, _statsNavigationGroup);
            }
        }

        private GameObject CreateHeader(Transform parent, string label, Sprite icon = null, float headerHeight = 70,
            NavigationGroup navigationGroup = null)
        {
            var header = Instantiate(_settingsHeaderPrefab, parent, false);
            header.name = $"{label} Header";
            var headerRect = (RectTransform) header.transform;
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, headerHeight);
            headerRect.anchoredPosition = Vector2.zero;
            var headerText = header.GetComponentInChildren<TextMeshProUGUI>();
            headerText.text = label;
            if (icon != null)
            {
                AddHeaderIcon(header, headerText, icon, 40, 20);
            }
            header.AddComponent<LayoutElement>().preferredHeight = headerHeight;
            if (navigationGroup != null)
            {
                navigationGroup.AddNavigatable(RuntimeNavigatable.Attach(header, null));
            }
            return header;
        }

        private void AttachProfilePicture(GameObject header)
        {
            var pictureContainer = (RectTransform) _profilePicture.transform.parent;
            pictureContainer.SetParent(header.transform, false);
            pictureContainer.anchorMin = new Vector2(0, 0.5f);
            pictureContainer.anchorMax = new Vector2(0, 0.5f);
            pictureContainer.pivot = new Vector2(0, 0.5f);
            pictureContainer.anchoredPosition = new Vector2(15, 0);
            pictureContainer.sizeDelta = new Vector2(75, 75);

            foreach (RectTransform child in pictureContainer)
            {
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
            }

            var headerText = header.GetComponentInChildren<TextMeshProUGUI>();
            var textRect = (RectTransform) headerText.transform;
            textRect.offsetMin = new Vector2(105, textRect.offsetMin.y);
        }

        private static void AddHeaderIcon(GameObject header, TextMeshProUGUI headerText, Sprite sprite,
            float size, float leftOffset)
        {
            var iconObject = new GameObject("Instrument Icon", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image));
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(header.transform, false);
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(leftOffset, 0);
            iconRect.sizeDelta = new Vector2(size, size);

            var image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var textRect = (RectTransform) headerText.transform;
            textRect.offsetMin = new Vector2(leftOffset + size + 15, textRect.offsetMin.y);
        }

        private static Sprite GetInstrumentIcon(Instrument instrument)
        {
            string resourceName = instrument switch
            {
                Instrument.ProGuitar_22Fret => Instrument.ProGuitar_17Fret.ToResourceName(),
                Instrument.ProBass_22Fret => Instrument.ProBass_17Fret.ToResourceName(),
                _ => instrument.ToResourceName()
            };
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            string assetKey = $"InstrumentIcons[{resourceName}]";
            if (!InstrumentIconCache.TryGetValue(assetKey, out var icon))
            {
                icon = Addressables.LoadAssetAsync<Sprite>(assetKey).WaitForCompletion();
                InstrumentIconCache[assetKey] = icon;
            }

            return icon;
        }

        private TextMeshProUGUI CreateValueRow(Transform parent, string label, string value, int rowIndex,
            bool navigatable, bool largeValue, NavigationGroup navigationGroup)
        {
            // This is the same labeled row used by Settings. Keeping the shared prefab here
            // means its spacing, divider, typography, and alternating background follow the
            // rest of the menu automatically when those visuals change.
            var row = Instantiate(_labeledRowPrefab, parent, false);
            row.name = $"{label} Row";
            var rowRect = (RectTransform) row.transform;
            rowRect.anchorMin = new Vector2(0, 1);
            rowRect.anchorMax = new Vector2(1, 1);
            rowRect.pivot = new Vector2(0.5f, 1);
            rowRect.sizeDelta = new Vector2(0, 80);
            rowRect.anchoredPosition = Vector2.zero;
            row.AddComponent<LayoutElement>().preferredHeight = 80;

            var labelTransform = row.transform.Find("Label");
            var labelText = labelTransform.GetComponent<TextMeshProUGUI>();
            labelText.text = label;
            labelText.fontStyle = FontStyles.Normal;
            // Use the font metrics for vertical centering. Midline alignment centers the
            // rendered glyph bounds, which shifts the baseline between bold and regular text
            // (and even between strings with different ascenders or descenders).
            labelText.alignment = TextAlignmentOptions.Left;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            var labelRect = (RectTransform) labelTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0);
            labelRect.anchorMax = Vector2.one;
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            var evenBackground = row.transform.Find("Even Background");
            evenBackground.gameObject.SetActive(rowIndex % 2 == 0);

            var container = (RectTransform) row.transform.Find("Container");
            var valueText = Instantiate(labelText, row.transform, false);
            valueText.name = "Value";
            valueText.text = value;
            valueText.fontStyle = FontStyles.Bold;
            if (largeValue)
            {
                // Match the large values from the original profile summary header.
                valueText.fontSize = _totalScore.fontSize;
            }
            valueText.alignment = TextAlignmentOptions.Right;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.margin = new Vector4(0, 0, labelText.margin.x, 0);
            var valueTextRect = (RectTransform) valueText.transform;
            valueTextRect.anchorMin = Vector2.zero;
            valueTextRect.anchorMax = new Vector2(0.5f, 1);
            valueTextRect.offsetMin = Vector2.zero;
            valueTextRect.offsetMax = Vector2.zero;

            // The Settings row's control container is not used by these read-only rows.
            // Keep it inactive so it cannot intercept input or overlap the centered columns.
            container.gameObject.SetActive(false);

            if (navigatable)
            {
                navigationGroup.AddNavigatable(row);
            }
            else
            {
                var canvasGroup = row.AddComponent<CanvasGroup>();
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            return valueText;
        }

        private static int GetNonNegativeStarCount(StarAmount stars)
        {
            return Math.Max(0, stars.GetStarCount());
        }

        private static int GetProfileStarPoints(StarAmount stars)
        {
            return stars == StarAmount.StarGold ? 6 : GetNonNegativeStarCount(stars);
        }

        private static string FormatProgress(long value, long maximum)
        {
            double progress = maximum > 0 ? (double) value / maximum : 0;
            string percentage;
            if (SettingsManager.Settings.ShowPercentDecimals.Value)
            {
                percentage = $"{Math.Floor(progress * 1000) / 10:0.0}%";
            }
            else
            {
                percentage = $"{Math.Floor(progress * 100):0}%";
            }

            return Localize.KeyFormat("Menu.ProfileInfo.Stats.ProgressValue", value, maximum, percentage);
        }

        private static int GetStarPoints(StarAmount stars, Difficulty difficulty)
        {
            if (stars != StarAmount.StarGold)
            {
                return GetNonNegativeStarCount(stars);
            }

            return difficulty switch
            {
                Difficulty.Beginner => 2,
                Difficulty.Easy => 3,
                Difficulty.Medium => 4,
                Difficulty.Hard => 5,
                _ => 6
            };
        }

        private static string GetInstrumentName(Instrument instrument)
        {
            string key = instrument switch
            {
                Instrument.FiveFretGuitar => "FiveFretGuitar",
                Instrument.FiveFretBass => "FiveFretBass",
                Instrument.FiveFretRhythm => "FiveFretRhythm",
                Instrument.FiveFretCoopGuitar => "FiveFretCoopGuitar",
                Instrument.Keys => "Keys",
                Instrument.SixFretGuitar => "SixFretGuitar",
                Instrument.SixFretBass => "SixFretBass",
                Instrument.SixFretRhythm => "SixFretRhythm",
                Instrument.SixFretCoopGuitar => "SixFretCoopGuitar",
                Instrument.FourLaneDrums => "FourLaneDrums",
                Instrument.ProDrums => "ProDrums",
                Instrument.FiveLaneDrums => "FiveLaneDrums",
                Instrument.EliteDrums => "EliteDrums",
                Instrument.ProGuitar_17Fret => "ProGuitar17Fret",
                Instrument.ProGuitar_22Fret => "ProGuitar22Fret",
                Instrument.ProBass_17Fret => "ProBass17Fret",
                Instrument.ProBass_22Fret => "ProBass22Fret",
                Instrument.ProKeys => "ProKeys",
                Instrument.Vocals => "Vocals",
                Instrument.Harmony => "Harmony",
                _ => null
            };

            return key == null
                ? instrument.ToString()
                : Localize.Key($"Menu.ProfileInfo.Instruments.{key}");
        }
    }
}
