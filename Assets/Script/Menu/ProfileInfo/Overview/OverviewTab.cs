using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Song;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings;
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
        private RectTransform _profileSummaryContent;
        private TextMeshProUGUI _profileSummaryHeader;
        private readonly TextMeshProUGUI[] _profileSummaryValues = new TextMeshProUGUI[5];

        private static readonly Instrument[] Instruments = Enum.GetValues(typeof(Instrument))
            .Cast<Instrument>()
            .Where(instrument => instrument != Instrument.Band)
            .ToArray();

        private static readonly string[] StatLabels =
        {
            "All Songs Score",
            "Expert Gold Star Songs",
            "Expert Full Combos",
            "Total Stars Earned"
        };

        private static readonly string[] ProfileSummaryLabels =
        {
            "Songs Owned",
            "Total Song Plays",
            "Total Score",
            "Total Stars Earned",
            "Full Combos"
        };

        private void Awake()
        {
            CreateTwoPanelLayout();
        }

        private void OnEnable()
        {
            var profile = _profileInfoMenu.CurrentProfile;
            var scores = ScoreContainer.GetAllScoresByPlayerId(profile.Id);

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
            _statsNavigationGroup?.SelectPrevious(isRepeat);
        }

        public void NavigateDown(bool isRepeat)
        {
            _statsNavigationGroup?.SelectNext(isRepeat);
        }

        private void CreateTwoPanelLayout()
        {
            if (_statsViewport == null || _scrollViewPrefab == null)
            {
                Debug.LogError($"{nameof(OverviewTab)} is missing its stats viewport or scroll view prefab.", this);
                return;
            }

            // The original summary occupied a full-width strip above the stats. It is replaced
            // by the profile summary in the right pane, so let both panes use the full height.
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

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.scrollSensitivity = 50;
            _statsContent = scrollRect.content;
            _statsContent.name = "Instrument Stats";
            _statsNavigationGroup = scrollView.AddComponent<NavigationGroup>();
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
            _profileSummaryContent = panel.GetComponent<RectTransform>();
            _profileSummaryContent.SetParent(_statsViewport.parent, false);
            _profileSummaryContent.anchorMin = Vector2.zero;
            _profileSummaryContent.anchorMax = new Vector2(0.5f, 1);
            _profileSummaryContent.anchoredPosition = Vector2.zero;
            _profileSummaryContent.sizeDelta = Vector2.zero;
            _profileSummaryContent.pivot = new Vector2(0.5f, 1);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = CreateHeader(_profileSummaryContent, "Profile");
            _profileSummaryHeader = header.GetComponentInChildren<TextMeshProUGUI>();

            for (int i = 0; i < ProfileSummaryLabels.Length; i++)
            {
                _profileSummaryValues[i] = CreateValueRow(_profileSummaryContent,
                    ProfileSummaryLabels[i], string.Empty, i, false, i > 0);
            }
        }

        private void PopulateProfileSummary(string profileName, List<PlayerScoreRecord> scores)
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
            var summaryValues = new[]
            {
                songsOwned.ToString("N0"),
                scores.Count.ToString("N0"),
                scores.Sum(score => (double) score.Score).ToString("N0"),
                scores.Sum(score => (long) GetProfileStarPoints(score.Stars)).ToString("N0"),
                scores.LongCount(score => score.IsFc).ToString("N0")
            };
            for (int i = 0; i < _profileSummaryValues.Length; i++)
            {
                _profileSummaryValues[i].text = summaryValues[i];
            }
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

            foreach (var instrument in Instruments)
            {
                bool isDrums = MidiDrumkitHelper.Instruments.Contains(instrument);
                if (isDrums && instrument != MidiDrumkitHelper.Instruments[0])
                {
                    continue;
                }

                var groupedInstruments = isDrums
                    ? MidiDrumkitHelper.Instruments
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
                var expertChartCount = instrument is Instrument.Vocals or Instrument.Harmony
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
                var maximumStars = chartedSongs.Count * 6L;
                var statValues = new[]
                {
                    cumulativeHighScore.ToString("N0"),
                    FormatProgress(goldStarSongCount, expertChartCount),
                    FormatProgress(expertFullComboCount, expertChartCount),
                    FormatProgress(totalStars, maximumStars)
                };

                CreateInstrumentSection(isDrums ? "Aggregate Drums" : GetInstrumentName(instrument), statValues);
            }

            _statsNavigationGroup.SelectFirst(SelectionOrigin.Navigation);
        }

        private void CreateInstrumentSection(string instrumentName, string[] statValues)
        {
            CreateHeader(_statsContent, instrumentName);

            for (int statIndex = 0; statIndex < StatLabels.Length; statIndex++)
            {
                CreateValueRow(_statsContent, StatLabels[statIndex], statValues[statIndex], statIndex, true, false);
            }
        }

        private GameObject CreateHeader(Transform parent, string label)
        {
            var header = Instantiate(_settingsHeaderPrefab, parent, false);
            header.name = $"{label} Header";
            var headerRect = (RectTransform) header.transform;
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 70);
            headerRect.anchoredPosition = Vector2.zero;
            header.GetComponentInChildren<TextMeshProUGUI>().text = label;
            header.AddComponent<LayoutElement>().preferredHeight = 70;
            return header;
        }

        private TextMeshProUGUI CreateValueRow(Transform parent, string label, string value, int rowIndex,
            bool navigatable, bool bold)
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
            if (bold)
            {
                labelText.fontStyle |= FontStyles.Bold;
            }

            var evenBackground = row.transform.Find("Even Background");
            evenBackground.gameObject.SetActive(rowIndex % 2 == 0);

            var container = (RectTransform) row.transform.Find("Container");
            var valueText = Instantiate(labelText, container, false);
            valueText.name = "Value";
            valueText.text = value;
            valueText.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            if (bold)
            {
                // Match the large values from the original profile summary header.
                valueText.fontSize = _totalScore.fontSize;
            }
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            var valueTextRect = (RectTransform) valueText.transform;
            valueTextRect.anchorMin = Vector2.zero;
            valueTextRect.anchorMax = Vector2.one;
            valueTextRect.offsetMin = Vector2.zero;
            valueTextRect.offsetMax = Vector2.zero;

            if (navigatable)
            {
                _statsNavigationGroup.AddNavigatable(row);
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

            return $"{value:N0} / {maximum:N0} ({percentage})";
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
            return instrument switch
            {
                Instrument.FiveFretGuitar => "5-Fret Guitar",
                Instrument.FiveFretBass => "5-Fret Bass",
                Instrument.FiveFretRhythm => "5-Fret Rhythm",
                Instrument.FiveFretCoopGuitar => "5-Fret Co-op Guitar",
                Instrument.Keys => "Keys",
                Instrument.SixFretGuitar => "6-Fret Guitar",
                Instrument.SixFretBass => "6-Fret Bass",
                Instrument.SixFretRhythm => "6-Fret Rhythm",
                Instrument.SixFretCoopGuitar => "6-Fret Co-op Guitar",
                Instrument.FourLaneDrums => "4-Lane Drums",
                Instrument.ProDrums => "Pro Drums",
                Instrument.FiveLaneDrums => "5-Lane Drums",
                Instrument.EliteDrums => "Elite Drums",
                Instrument.ProGuitar_17Fret => "Pro Guitar (17 Fret)",
                Instrument.ProGuitar_22Fret => "Pro Guitar (22 Fret)",
                Instrument.ProBass_17Fret => "Pro Bass (17 Fret)",
                Instrument.ProBass_22Fret => "Pro Bass (22 Fret)",
                Instrument.ProKeys => "Pro Keys",
                Instrument.Vocals => "Vocals",
                Instrument.Harmony => "Harmonies",
                _ => instrument.ToString()
            };
        }
    }
}
