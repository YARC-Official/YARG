using System;
using System.Collections.Generic;
using System.IO;
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

        private static readonly Instrument[] Instruments = CreateInstrumentList();

        private sealed class BestDifficultyScore
        {
            public HashWrapper Song;
            public Instrument Instrument;
            public Difficulty Difficulty;
            public long HighScore;
            public StarAmount BestStars;
            public long BestStarsScore;
            public bool IsFc;
        }

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
            YargProfile profile = _profileInfoMenu.CurrentProfile;
            List<PlayerScoreWithChecksum> allScores =
                ScoreContainer.GetAllScoresWithChecksumsByPlayerId(profile.Id);
            List<PlayerScoreWithChecksum> scores = new(allScores.Count);
            double totalScore = 0;
            double totalStars = 0;
            long totalFcs = 0;
            foreach (PlayerScoreWithChecksum score in allScores)
            {
                HashWrapper songHash = HashWrapper.Create(score.SongChecksum);
                if (!SongContainer.SongsByHash.TryGetValue(songHash, out List<SongEntry> songs) ||
                    songs.Count == 0)
                    continue;

                scores.Add(score);
                totalScore += score.Score;
                totalStars += GetProfileStarPoints(score.Stars);
                if (score.IsFc)
                    totalFcs++;
            }

            _profilePicture.enabled = true;
            _avatar.gameObject.SetActive(false);
            _profileName.text = profile.Name;
            _profileExtras.text = Localize.KeyFormat("Menu.ProfileInfo.Stats.SongPlays", scores.Count);

            _totalScore.text = totalScore.ToString("N0");
            _totalStars.text = totalStars.ToString("N0");
            _totalFcs.text = totalFcs.ToString("N0");

            PopulateProfileSummary(profile.Name, scores);
            PopulateInstrumentStats(ScoreContainer.GetScoresInCurrentLibraryByPlayerId(profile.Id));
            FocusProfileSummary(true);

            if (profile.Avatar == null) return;

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
                FocusProfileSummary(false);
            else
                FocusInstrumentStats();
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
                headerHeight: 105);
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
            if (_profileSummaryNavigationGroup == null || _profileSummaryNavigationGroup.Count == 0) return;

            _statsSelectedIndex = _statsNavigationGroup?.SelectedIndex ?? _statsSelectedIndex;
            DeactivateNavigationGroup(_statsNavigationGroup);

            if (selectFirst)
            {
                _profileSummaryNavigationGroup.SelectFirst(SelectionOrigin.Navigation);
                _profileSummaryScrollRect.verticalNormalizedPosition = 1;
            }
            else if (_profileSummarySelectedIndex is { } index && index < _profileSummaryNavigationGroup.Count)
                _profileSummaryNavigationGroup.SelectAt(index, SelectionOrigin.Navigation);
            else
            {
                _profileSummaryNavigationGroup.SelectFirst(SelectionOrigin.Navigation);
            }

            _profileSummaryNavigationGroup.PushNavGroupToStack();
        }

        private void FocusInstrumentStats()
        {
            if (_statsNavigationGroup == null || _statsNavigationGroup.Count == 0) return;

            _profileSummarySelectedIndex = _profileSummaryNavigationGroup.SelectedIndex
                ?? _profileSummarySelectedIndex;
            DeactivateNavigationGroup(_profileSummaryNavigationGroup);

            if (_statsSelectedIndex is { } index && index < _statsNavigationGroup.Count)
                _statsNavigationGroup.SelectAt(index, SelectionOrigin.Navigation);
            else
            {
                _statsNavigationGroup.SelectFirst(SelectionOrigin.Navigation);
                _statsScrollRect.verticalNormalizedPosition = 1;
            }

            _statsNavigationGroup.PushNavGroupToStack();
        }

        private void OnProfileSummarySelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null) return;

            _profileSummarySelectedIndex = _profileSummaryNavigationGroup.SelectedIndex;
            DeactivateNavigationGroup(_statsNavigationGroup);
        }

        private void OnInstrumentStatsSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null) return;

            _statsSelectedIndex = _statsNavigationGroup.SelectedIndex;
            DeactivateNavigationGroup(_profileSummaryNavigationGroup);
        }

        private static void DeactivateNavigationGroup(NavigationGroup navigationGroup)
        {
            if (navigationGroup == null) return;

            if (NavigationGroup.CurrentNavigationGroup == navigationGroup)
                navigationGroup.SelectLastNavGroup();
            else
                navigationGroup.ClearSelection();
        }

        private void PopulateProfileSummary(string profileName, List<PlayerScoreWithChecksum> scores)
        {
            if (_profileSummaryContent == null) return;

            _profileSummaryHeader.text = profileName;

            long songsOwned = 0;
            foreach (List<SongEntry> songs in SongContainer.SongsByHash.Values)
            {
                if (songs.Count > 0)
                    songsOwned++;
            }

            double lifetimeScore = 0;
            long lifetimeStars = 0;
            long lifetimeFullCombos = 0;
            foreach (PlayerScoreWithChecksum score in scores)
            {
                lifetimeScore += score.Score;
                lifetimeStars += GetProfileStarPoints(score.Stars);
                if (score.IsFc)
                    lifetimeFullCombos++;
            }

            var playHistory = GetPlayHistorySummary(scores);
            string[] summaryValues =
            {
                songsOwned.ToString("N0"),
                scores.Count.ToString("N0"),
                lifetimeScore.ToString("N0"),
                lifetimeStars.ToString("N0"),
                lifetimeFullCombos.ToString("N0"),
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
            GetPlayHistorySummary(IReadOnlyList<PlayerScoreWithChecksum> scores)
        {
            long totalMilliseconds = 0;
            long currentFullComboStreak = 0;
            long longestFullComboStreak = 0;
            double totalPercentage = 0;
            long percentageCount = 0;
            Dictionary<Guid, int> enginePlayCounts = new();
            Dictionary<HashWrapper, (string Name, int Count)> songPlayCounts = new();
            Dictionary<string, int> artistPlayCounts = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> genrePlayCounts = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> sourcePlayCounts = new(StringComparer.OrdinalIgnoreCase);

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

                HashWrapper songHash = HashWrapper.Create(score.SongChecksum);
                if (SongContainer.SongsByHash.TryGetValue(songHash, out List<SongEntry> songs) && songs.Count > 0)
                {
                    SongEntry song = songs[0];
                    totalMilliseconds += song.SongLengthMilliseconds;

                    if (songPlayCounts.TryGetValue(songHash, out var songPlayCount))
                        songPlayCounts[songHash] = (songPlayCount.Name, songPlayCount.Count + 1);
                    else
                        songPlayCounts[songHash] = (Localize.KeyFormat(
                            "Menu.ProfileInfo.Stats.FavoriteSongValue", song.Name, song.Artist), 1);

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

            TimeSpan totalTime = TimeSpan.FromMilliseconds(totalMilliseconds);
            Guid? favoriteEngineId = FindFavoriteEngine(enginePlayCounts);
            string mostPlayedSong = FindMostPlayedSong(songPlayCounts);
            string mostPlayedGenre = FindMostPlayedValue(genrePlayCounts);
            string mostPlayedArtist = FindMostPlayedValue(artistPlayCounts);
            string mostPlayedSource = FindMostPlayedValue(sourcePlayCounts);
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

        private static Instrument[] CreateInstrumentList()
        {
            Array values = Enum.GetValues(typeof(Instrument));
            List<Instrument> instruments = new(values.Length);
            foreach (Instrument instrument in values)
            {
                if (instrument != Instrument.Band)
                    instruments.Add(instrument);
            }
            return instruments.ToArray();
        }

        private static Guid? FindFavoriteEngine(Dictionary<Guid, int> playCounts)
        {
            Guid favorite = Guid.Empty;
            int highestCount = 0;
            foreach (KeyValuePair<Guid, int> pair in playCounts)
            {
                if (pair.Value > highestCount ||
                    (pair.Value == highestCount && pair.Key.CompareTo(favorite) < 0))
                {
                    favorite = pair.Key;
                    highestCount = pair.Value;
                }
            }
            return highestCount > 0 ? favorite : null;
        }

        private static string FindMostPlayedSong(
            Dictionary<HashWrapper, (string Name, int Count)> playCounts)
        {
            string favorite = null;
            int highestCount = 0;
            foreach ((string name, int count) in playCounts.Values)
            {
                if (count > highestCount ||
                    (count == highestCount && StringComparer.OrdinalIgnoreCase.Compare(name, favorite) < 0))
                {
                    favorite = name;
                    highestCount = count;
                }
            }
            return favorite ?? "-";
        }

        private static string FindMostPlayedValue(Dictionary<string, int> playCounts)
        {
            string favorite = null;
            int highestCount = 0;
            foreach (KeyValuePair<string, int> pair in playCounts)
            {
                if (pair.Value > highestCount ||
                    (pair.Value == highestCount &&
                        StringComparer.OrdinalIgnoreCase.Compare(pair.Key, favorite) < 0))
                {
                    favorite = pair.Key;
                    highestCount = pair.Value;
                }
            }
            return favorite ?? "-";
        }

        private static bool ContainsInstrument(IReadOnlyList<Instrument> instruments, Instrument target)
        {
            for (int i = 0; i < instruments.Count; i++)
            {
                if (instruments[i] == target) return true;
            }
            return false;
        }

        private static bool HasAnyInstrument(SongEntry song, IReadOnlyList<Instrument> instruments)
        {
            for (int i = 0; i < instruments.Count; i++)
            {
                if (song.HasInstrument(instruments[i])) return true;
            }
            return false;
        }

        private static bool HasAnyExpertChart(SongEntry song, IReadOnlyList<Instrument> instruments)
        {
            for (int i = 0; i < instruments.Count; i++)
            {
                if (song.HasDifficultyForInstrument(instruments[i], Difficulty.Expert)) return true;
            }
            return false;
        }

        private static void SetMaximum(Dictionary<HashWrapper, long> values, HashWrapper key, long value)
        {
            if (!values.TryGetValue(key, out long current) || value > current)
                values[key] = value;
        }

        private static void SetMaximum(Dictionary<HashWrapper, int> values, HashWrapper key, int value)
        {
            if (!values.TryGetValue(key, out int current) || value > current)
                values[key] = value;
        }

        private static long SumValues(Dictionary<HashWrapper, long> values)
        {
            long total = 0;
            foreach (long value in values.Values)
            {
                total += value;
            }
            return total;
        }

        private static long SumValues(Dictionary<HashWrapper, int> values)
        {
            long total = 0;
            foreach (int value in values.Values)
            {
                total += value;
            }
            return total;
        }

        private static string FormatAverageStars(IReadOnlyCollection<PlayerScoreRecord> scores)
        {
            if (scores.Count == 0) return "-";

            long totalStars = 0;
            foreach (PlayerScoreRecord score in scores)
            {
                totalStars += GetProfileStarPoints(score.Stars);
            }
            double averageStars = (double) totalStars / scores.Count;
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

            if (scores == null) return;

            List<SongEntry> librarySongs = new(SongContainer.SongsByHash.Count);
            foreach (List<SongEntry> songs in SongContainer.SongsByHash.Values)
            {
                if (songs.Count > 0)
                    librarySongs.Add(songs[0]);
            }

            List<string> setlistRoots = new();
            HashSet<string> uniqueSetlistRoots = new(StringComparer.OrdinalIgnoreCase);
            foreach (string path in SettingsManager.Settings.SongFolders)
            {
                if (!string.IsNullOrWhiteSpace(path) && uniqueSetlistRoots.Add(path))
                    setlistRoots.Add(path);
            }
            if (!string.IsNullOrWhiteSpace(PathHelper.SetlistPath) &&
                uniqueSetlistRoots.Add(PathHelper.SetlistPath))
                setlistRoots.Add(PathHelper.SetlistPath);

            Dictionary<string, bool> officialPackCache = new(StringComparer.OrdinalIgnoreCase);
            HashSet<HashWrapper> officialSetlistSongHashes = new();
            foreach (SongEntry song in librarySongs)
            {
                if (IsOfficialSetlistSong(song, setlistRoots, officialPackCache))
                    officialSetlistSongHashes.Add(song.Hash);
            }

            Dictionary<(HashWrapper Song, Instrument Instrument, Difficulty Difficulty), BestDifficultyScore>
                bestByDifficulty = new();
            foreach (PlayerScoreWithChecksum score in scores)
            {
                HashWrapper songHash = HashWrapper.Create(score.SongChecksum);
                var key = (songHash, score.Instrument, score.Difficulty);
                int starPoints = GetStarPoints(score.Stars, score.Difficulty);
                if (!bestByDifficulty.TryGetValue(key, out BestDifficultyScore bestScore))
                {
                    bestScore = new BestDifficultyScore
                    {
                        Song = songHash,
                        Instrument = score.Instrument,
                        Difficulty = score.Difficulty,
                        HighScore = score.Score,
                        BestStars = score.Stars,
                        BestStarsScore = score.Score,
                        IsFc = score.IsFc
                    };
                    bestByDifficulty.Add(key, bestScore);
                    continue;
                }

                if (score.Score > bestScore.HighScore)
                    bestScore.HighScore = score.Score;
                int bestStarPoints = GetStarPoints(bestScore.BestStars, bestScore.Difficulty);
                if (starPoints > bestStarPoints ||
                    (starPoints == bestStarPoints && score.Score > bestScore.BestStarsScore))
                {
                    bestScore.BestStars = score.Stars;
                    bestScore.BestStarsScore = score.Score;
                }
                bestScore.IsFc |= score.IsFc;
            }

            List<(string Name, string[] Values, long Score,
                Instrument IconInstrument, bool HasOfficialSetlistScore)> instrumentSections = new();
            foreach (Instrument instrument in Instruments)
            {
                bool isDrums = ContainsInstrument(MidiDrumkitHelper.Instruments, instrument);
                if (isDrums && instrument != MidiDrumkitHelper.Instruments[0]) continue;

                bool isVocals = ContainsInstrument(VocalInstruments, instrument);
                if (isVocals && instrument != VocalInstruments[0]) continue;

                IReadOnlyList<Instrument> groupedInstruments = isDrums
                    ? MidiDrumkitHelper.Instruments
                    : isVocals
                        ? VocalInstruments
                    : new[] { instrument };

                Dictionary<HashWrapper, long> highScores = new();
                Dictionary<HashWrapper, int> starPoints = new();
                HashSet<HashWrapper> goldStarSongs = new();
                HashSet<HashWrapper> fullComboSongs = new();
                Dictionary<HashWrapper, long> officialScores = new();
                foreach (BestDifficultyScore score in bestByDifficulty.Values)
                {
                    if (!ContainsInstrument(groupedInstruments, score.Instrument)) continue;

                    SetMaximum(highScores, score.Song, score.HighScore);
                    SetMaximum(starPoints, score.Song, GetStarPoints(score.BestStars, score.Difficulty));
                    if (score.Difficulty == Difficulty.Expert)
                    {
                        if (score.BestStars == StarAmount.StarGold)
                            goldStarSongs.Add(score.Song);
                        if (score.IsFc)
                            fullComboSongs.Add(score.Song);
                    }
                    if (officialSetlistSongHashes.Contains(score.Song))
                        SetMaximum(officialScores, score.Song, score.HighScore);
                }

                long cumulativeHighScore = SumValues(highScores);
                if (cumulativeHighScore == 0) continue;

                long chartedSongCount = 0;
                long expertChartCount = 0;
                bool vocals = instrument is Instrument.Vocals or Instrument.Harmony or Instrument.PartyVocals;
                foreach (SongEntry song in librarySongs)
                {
                    if (!HasAnyInstrument(song, groupedInstruments)) continue;
                    chartedSongCount++;
                    if (vocals || HasAnyExpertChart(song, groupedInstruments))
                        expertChartCount++;
                }

                long totalStars = SumValues(starPoints);
                long officialSetlistScore = SumValues(officialScores);
                long maximumStars = chartedSongCount * 6L;
                string[] statValues =
                {
                    cumulativeHighScore.ToString("N0"),
                    FormatProgress(totalStars, maximumStars),
                    FormatProgress(goldStarSongs.Count, expertChartCount),
                    FormatProgress(fullComboSongs.Count, expertChartCount),
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

            instrumentSections.Sort((left, right) => right.Score.CompareTo(left.Score));
            foreach (var section in instrumentSections)
            {
                CreateInstrumentSection(section.Name, section.Values, section.IconInstrument,
                    section.HasOfficialSetlistScore);
            }

        }

        private static bool IsOfficialSetlistSong(SongEntry song, IEnumerable<string> setlistRoots,
            IDictionary<string, bool> officialPackCache)
        {
            if (string.IsNullOrWhiteSpace(song.ActualLocation)) return false;

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
                        continue;

                    int separatorIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
                    if (separatorIndex <= 0) continue;

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
            CreateHeader(_statsContent, instrumentName, GetInstrumentIcon(instrument));

            for (int statIndex = 0; statIndex < StatLabelKeys.Length; statIndex++)
            {
                if (statIndex == StatLabelKeys.Length - 1 && !hasOfficialSetlistScore) continue;

                CreateValueRow(_statsContent, Localize.Key(StatLabelKeys[statIndex]), statValues[statIndex],
                    statIndex, true, false, _statsNavigationGroup);
            }
        }

        private GameObject CreateHeader(Transform parent, string label, Sprite icon = null, float headerHeight = 70)
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
                AddHeaderIcon(header, headerText, icon, 40, 20);
            header.AddComponent<LayoutElement>().preferredHeight = headerHeight;
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
            if (string.IsNullOrEmpty(resourceName)) return null;

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
            // This prefab normally auto-sizes values to 24 points. Keep the three lifetime
            // totals at the same size as the profile name instead.
            if (largeValue)
            {
                valueText.enableAutoSizing = false;
                valueText.fontSize = _profileSummaryHeader.fontSize;
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
                navigationGroup.AddNavigatable(row);
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
                percentage = $"{Math.Floor(progress * 1000) / 10:0.0}%";
            else
                percentage = $"{Math.Floor(progress * 100):0}%";

            return Localize.KeyFormat("Menu.ProfileInfo.Stats.ProgressValue", value, maximum, percentage);
        }

        private static int GetStarPoints(StarAmount stars, Difficulty difficulty)
        {
            if (stars != StarAmount.StarGold) return GetNonNegativeStarCount(stars);

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
