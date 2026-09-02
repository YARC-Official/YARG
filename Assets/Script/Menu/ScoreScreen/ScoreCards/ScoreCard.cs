using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Gameplay.HUD;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Player;
using YARG.Settings;

namespace YARG.Menu.ScoreScreen
{
    public abstract class ScoreCard<T> : MonoBehaviour, IScoreCard<T> where T : BaseStats
    {
        private const int OFFSET_HISTOGRAM_BIN_COUNT = 35;
        private const float OFFSET_HISTOGRAM_ABS_BOUND_MS = 70f;
        private const float OFFSET_HISTOGRAM_TOTAL_HEIGHT = 154f;
        private const float OFFSET_HISTOGRAM_GRAPH_HEIGHT = 132f;
        private const float OFFSET_HISTOGRAM_AXIS_LABEL_HEIGHT = 22f;
        private const float OFFSET_HISTOGRAM_AXIS_FONT_SIZE = 20f;
        private const float OFFSET_HISTOGRAM_HORIZONTAL_MARGIN = 54f;

        // The filter-category notes (strums for guitar, kicks for drums) render below in white,
        // the rest stack above in gray, so a skewed histogram can be attributed to real filter-
        // category timing vs. the other category's infinite front end (HOPO/tap for guitar).
        private static readonly Color OFFSET_HISTOGRAM_PRIMARY_COLOR   = new(1f, 1f, 1f, 0.85f);
        private static readonly Color OFFSET_HISTOGRAM_SECONDARY_COLOR = new(1f, 1f, 1f, 0.30f);

        /// <summary>
        /// Label prefix used for the filter-category offset summary rows ("STRUM AVERAGE"/"STRUM
        /// MEDIAN" by default). Overridden per instrument -- e.g. "KICK" for drums.
        /// </summary>
        protected virtual string CategoryLabel => "STRUM";

        /// <summary>
        /// Label for the *other* side of the filter category, used in place of
        /// <see cref="CategoryLabel"/> when <see cref="FilterMode"/> is ExcludeSelected (e.g. "No
        /// Strums") -- that's the side actually driving calibration in that mode.
        /// </summary>
        protected virtual string OppositeCategoryLabel => "HOPO/TAP";

        /// <summary>
        /// Which per-instrument calibration filter dropdown decides whether the filter-category
        /// notes or the other side render as "primary" (white, and shown in the summary rows) --
        /// e.g. UseStrumOnlyOffsetForCalibration for guitar, UseKickOnlyOffsetForCalibration for
        /// drums. Read directly from settings rather than passed in through Initialize, since it
        /// can be changed from the main Settings menu between score screens.
        /// </summary>
        protected virtual OffsetCalibrationFilter FilterMode =>
            SettingsManager.Settings.UseStrumOnlyOffsetForCalibration.Value;

        [SerializeField]
        private ModifierIcon _modifierIconPrefab;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _accuracyPercent;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _playerName;
        [SerializeField]
        private TextMeshProUGUI _instrument;
        [SerializeField]
        private TextMeshProUGUI _difficulty;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _score;
        [SerializeField]
        private StarView _starView;
        [SerializeField]
        private DifficultyRing _difficultyRing;
        [SerializeField]
        private Transform _modifierIconContainer;

        [Space]
        [SerializeField]
        protected Image _instrumentIcon;

        [Space]
        [SerializeField]
        private GameObject _tagGameObject;
        [SerializeField]
        private TextMeshProUGUI _tagText;

        [Space]
        [SerializeField]
        private ScrollRect _statsRect;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _notesHit;
        [SerializeField]
        private TextMeshProUGUI _maxStreak;
        [SerializeField]
        private GameObject _notesMissedContainer;
        [SerializeField]
        private TextMeshProUGUI _notesMissed;
        [SerializeField]
        private TextMeshProUGUI _starpowerPhrases;
        [SerializeField]
        private TextMeshProUGUI _averageMultiplier;
        [SerializeField]
        private TextMeshProUGUI _bandBonusScore;
        [SerializeField]
        protected TextMeshProUGUI _averageOffset;
        /// <summary>
        /// The static label to the left of <see cref="_averageOffset"/>. Optional -- if it is not
        /// wired in the Inspector, the score card finds the other text element in the row.
        /// </summary>
        [SerializeField]
        private TextMeshProUGUI _averageOffsetLabel;
        [SerializeField]
        private TextMeshProUGUI _starPowerActivations;
        [SerializeField]
        private TextMeshProUGUI _timeInStarPower;

        [SerializeField]
        private RectTransform _advancedStatsRect;
        [SerializeField]
        private RectTransform _basicStatsRect;

        [SerializeField]
        private ColoredPillElement _enginePresetTag;
        [SerializeField]
        private ColoredPillElement _modifiersUsedTag;
        [SerializeField]
        private GameObject _modifiersUsedContainer;
        [SerializeField]
        private GameObject _modifiersUsedSeparator;


        private ScoreCardColorizer _colorizer;
        private GameObject _offsetHistogramObject;
        private RectTransform _offsetHistogramRootRect;
        private RectTransform _offsetHistogramContentRect;
        private RectTransform _offsetHistogramGraphRect;
        private RectTransform _offsetHistogramBarsRect;
        private RectTransform _offsetHistogramZeroLineRect;
        private TextMeshProUGUI _offsetHistogramLeftAxisLabel;
        private TextMeshProUGUI _offsetHistogramCenterAxisLabel;
        private TextMeshProUGUI _offsetHistogramRightAxisLabel;
        private readonly List<RectTransform> _offsetHistogramBarPool = new();

        protected bool  IsHighScore;
        protected T     Stats;
        protected bool  IsReplay;

        /// <summary>
        /// Aligned 1:1 with <see cref="Stats"/>'s offset samples: true for the filter-category note
        /// (a strum for guitar, a kick for drums), false for the other side. Null if the instrument
        /// has no such distinction.
        /// </summary>
        protected IReadOnlyList<bool> OffsetSampleFilterCategory;

        public YargPlayer Player { get; private set; }

        private void Awake()
        {
            _colorizer = GetComponent<ScoreCardColorizer>();
        }

        public void Initialize(bool isHighScore, YargPlayer player, T stats, bool isReplay,
            IReadOnlyList<bool> offsetSampleFilterCategory = null)
        {
            IsHighScore = isHighScore;
            Player = player;
            Stats = stats;
            IsReplay  = isReplay;
            OffsetSampleFilterCategory = offsetSampleFilterCategory;
        }

        public virtual void SetCardContents()
        {
            _playerName.text = Player.Profile.Name;

            _instrument.text = Player.Profile.CurrentInstrument.ToLocalizedName();
            _difficulty.text = Player.Profile.CurrentDifficulty.ToDisplayName();

            if (_difficultyRing != null)
            {
                _difficultyRing.SetInfo(Player.Profile.CurrentInstrument.ToResourceName(),
                    Player.Profile.CurrentInstrument,
                    GlobalVariables.State.CurrentSong[Player.Profile.CurrentInstrument]);
            }

            // Set percent
            if (SettingsManager.Settings.ShowPercentDecimals.Value)
            {
                var percent = Mathf.Floor(Stats.Percent * 1000f) / 10f;
                _accuracyPercent.text = $"{percent:0.0}%";
            }
            else
            {
                _accuracyPercent.text = $"{Mathf.FloorToInt(Stats.Percent * 100f)}%";
            }

            // Set background and foreground colors
            if (Player.Profile.IsBot)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gray);
                ShowTag("Bot");
            }
            else if (IsReplay)
            {
                if (Stats.IsFullCombo)
                {
                    _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gold);
                }
                else
                {
                    _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
                }

                ShowTag("Replay");
            }
            else if (Stats.IsFullCombo)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gold);
                ShowTag("Full Combo");
            }
            else if (IsHighScore)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
                ShowTag("High Score");
            }
            else if (!GlobalVariables.State.IsReplay)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
                ShowTag(SettingsManager.Settings.NoFail.Value != NoFailMode.Off ? "Completed" : "Cleared");
            }
            else
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
                _tagGameObject.SetActive(false);
            }

            _score.text = Stats.TotalScore.ToString("N0");
            _starView.SetStars((int) Stats.Stars);

            _notesHit.text = $"{ColorizePrimary(Stats.NotesHit)} / {ColorizeSecondary(Stats.TotalNotes)}";
            _maxStreak.text = ColorizePrimary(Stats.MaxCombo);
            _notesMissed.text = ColorizePrimary("-" + Stats.NotesMissed);
            _notesMissedContainer.gameObject.SetActive(Stats.NotesMissed != 0);
            _starpowerPhrases.text = $"{ColorizePrimary(Stats.StarPowerPhrasesHit)} / " +
                $"{ColorizeSecondary(Stats.TotalStarPowerPhrases)}";
            _averageMultiplier.text = ColorizePrimary(Stats.AverageMultiplier.ToString("0.00"));
            _bandBonusScore.text = ColorizePrimary(Stats.BandBonusScore.ToString("N0"));
            BuildOffsetSummaryRows();
            _starPowerActivations.text = ColorizePrimary(Stats.StarPowerActivationCount);
            string timeInStarPower = TimeSpan.FromSeconds(Stats.TimeInStarPower).ToString(@"m\:ss");
            _timeInStarPower.text = ColorizePrimary(timeInStarPower);
            BuildOffsetHistogram();

            // Set engine preset tag
            var enginePresetId = Player.EnginePreset.Id;
            if (enginePresetId == EnginePreset.Default.Id)
            {
                _enginePresetTag.SetValues(Localize.Key("Settings.PresetSetting.EnginePreset.DefaultEngines.DefaultEngine"),
                    ColoredPillElement.ColoredPillPreset.Default);
            }
            else if (enginePresetId == EnginePreset.Casual.Id)
            {
                _enginePresetTag.SetValues(Localize.Key("Settings.PresetSetting.EnginePreset.DefaultEngines.CasualEngine"),
                    ColoredPillElement.ColoredPillPreset.CasualEngine);
            }
            else if (enginePresetId == EnginePreset.Precision.Id)
            {
                _enginePresetTag.SetValues(Localize.Key("Settings.PresetSetting.EnginePreset.DefaultEngines.PrecisionEngine"),
                    ColoredPillElement.ColoredPillPreset.PrecisionEngine);
            }
            else if (enginePresetId == EnginePreset.SoloTaps.Id)
            {
                _enginePresetTag.SetValues(Localize.Key("Settings.PresetSetting.EnginePreset.DefaultEngines.SoloTapsEngine"),
                    ColoredPillElement.ColoredPillPreset.Default);
            }
            else
            {
                _enginePresetTag.SetValues(Localize.Key("Settings.PresetSetting.EnginePreset.DefaultEngines.CustomEnginePreset"),
                    ColoredPillElement.ColoredPillPreset.CustomEngine);
            }

            // Set engine preset icons
            ModifierIcon.SpawnEnginePresetIcons(_modifierIconPrefab, _modifierIconContainer,
                Player.EnginePreset, Player.Profile.GameMode);

            // Set modifier icons
            bool nonEngineModifiersUsed = false;
            foreach (var modifier in EnumExtensions<Modifier>.Values)
            {
                if (modifier == Modifier.None) continue;

                if (!Player.Profile.IsModifierActive(modifier)) continue;

                var icon = Instantiate(_modifierIconPrefab, _modifierIconContainer);
                icon.InitializeForModifier(modifier);

                nonEngineModifiersUsed = true;
            }

            bool anyModifiersUsed = _modifierIconContainer.childCount > 0;
            _modifiersUsedTag.gameObject.SetActive(nonEngineModifiersUsed);
            _modifiersUsedContainer.gameObject.SetActive(anyModifiersUsed);
            _modifiersUsedSeparator.gameObject.SetActive(anyModifiersUsed);
        }

        private const float OFFSET_STATS_MEDIAN_SPACING = 8f;

        private readonly List<GameObject> _generatedOffsetRows = new();
        private GameObject _offsetMedianSpacer;

        /// <summary>
        /// Displays offset statistics as separate left-label/right-value rows. The filter-category
        /// statistics (<see cref="CategoryLabel"/>) are shown only when the song contains both
        /// category and non-category hits; otherwise they would either be unavailable or identical
        /// to the normal statistics. When <see cref="FilterMode"/> is set to exclude the filter
        /// category (e.g. "No Strums"), the *other* side's statistics are shown instead, under
        /// <see cref="OppositeCategoryLabel"/>, since that's the side actually driving calibration.
        /// </summary>
        private void BuildOffsetSummaryRows()
        {
            var samples = Stats.GetOffsetSamples();
            bool primaryIsCategory = FilterMode != OffsetCalibrationFilter.ExcludeSelected;
            var primarySamples = GetFilterCategorySamples(samples, primaryIsCategory);
            var primaryLabel = primaryIsCategory ? CategoryLabel : OppositeCategoryLabel;

            bool hasPrimary = primarySamples is { Count: > 0 };
            bool hasOther = primarySamples != null && primarySamples.Count < samples.Count;
            bool showCategoryRows = hasPrimary && hasOther;

            var rows = new List<(string Label, double Value)>
            {
                ("AVERAGE OFFSET", Stats.GetAverageOffset()),
                ("MEDIAN OFFSET", GetMedian(samples) ?? Stats.GetAverageOffset())
            };

            if (showCategoryRows)
            {
                rows.Insert(1, ($"{primaryLabel} AVERAGE", primarySamples.Average()));
                rows.Add(($"{primaryLabel} MEDIAN", GetMedian(primarySamples) ?? primarySamples.Average()));
            }

            var templateRow = _averageOffset.transform.parent as RectTransform;
            if (templateRow == null)
            {
                _averageOffset.text =
                    $"{ColorizePrimary(ToMilliseconds(Stats.GetAverageOffset()))} {ColorizeSecondary("ms")}";
                return;
            }

            ClearGeneratedOffsetRows();

            var labelTransform = _averageOffsetLabel != null
                ? _averageOffsetLabel.transform
                : FindOffsetLabel(templateRow)?.transform;
            string labelPath = GetRelativePath(templateRow, labelTransform);
            string valuePath = GetRelativePath(templateRow, _averageOffset.transform);

            SetOffsetRow(templateRow.gameObject, labelPath, valuePath, rows[0].Label, rows[0].Value);

            int templateIndex = templateRow.GetSiblingIndex();
            for (int i = 1; i < rows.Count; i++)
            {
                var clone = Instantiate(templateRow.gameObject, templateRow.parent);
                clone.name = $"AverageOffsetRow_{i}";
                clone.SetActive(true);
                clone.transform.SetSiblingIndex(templateIndex + i);
                SetOffsetRow(clone, labelPath, valuePath, rows[i].Label, rows[i].Value);
                _generatedOffsetRows.Add(clone);
            }

            // Keep the averages together and add a small gap before the median rows.
            int medianStartIndex = showCategoryRows ? 2 : 1;
            var spacer = new GameObject(
                "AverageOffsetMedianSpacer",
                typeof(RectTransform),
                typeof(LayoutElement));
            spacer.transform.SetParent(templateRow.parent, false);
            spacer.transform.SetSiblingIndex(templateIndex + medianStartIndex);
            var spacerLayout = spacer.GetComponent<LayoutElement>();
            spacerLayout.minHeight = OFFSET_STATS_MEDIAN_SPACING;
            spacerLayout.preferredHeight = OFFSET_STATS_MEDIAN_SPACING;
            _offsetMedianSpacer = spacer;
        }

        private static string ToMilliseconds(double seconds)
        {
            return Math.Round(seconds * 1000, MidpointRounding.AwayFromZero).ToString();
        }

        private void SetOffsetRow(GameObject rowObject, string labelPath, string valuePath, string labelText,
            double valueSeconds)
        {
            var row = rowObject.transform as RectTransform;
            var label = labelPath != null
                ? row.Find(labelPath)?.GetComponent<TextMeshProUGUI>()
                : FindOffsetLabel(row, row.Find(valuePath))?.GetComponent<TextMeshProUGUI>();
            var value = row.Find(valuePath)?.GetComponent<TextMeshProUGUI>();

            if (label != null)
            {
                label.text = labelText;
            }

            if (value != null)
            {
                value.alignment = TextAlignmentOptions.MidlineRight;
                value.text =
                    $"{ColorizePrimary(ToMilliseconds(valueSeconds))} {ColorizeSecondary("ms")}";
            }
        }

        private static TextMeshProUGUI FindOffsetLabel(RectTransform row, Transform valueTransform = null)
        {
            foreach (Transform child in row)
            {
                if (child == valueTransform)
                {
                    continue;
                }

                if (child.TryGetComponent(out TextMeshProUGUI text))
                {
                    return text;
                }
            }

            return null;
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (target == null || !target.IsChildOf(root))
            {
                return null;
            }

            var parts = new Stack<string>();
            var current = target;
            while (current != root)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private void ClearGeneratedOffsetRows()
        {
            foreach (var row in _generatedOffsetRows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }

            _generatedOffsetRows.Clear();

            if (_offsetMedianSpacer != null)
            {
                Destroy(_offsetMedianSpacer);
                _offsetMedianSpacer = null;
            }
        }

        private static double? GetMedian(IReadOnlyList<double> samples)
        {
            if (samples.Count == 0)
            {
                return null;
            }

            var sorted = new double[samples.Count];
            for (int i = 0; i < samples.Count; i++)
            {
                sorted[i] = samples[i];
            }

            Array.Sort(sorted);

            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }

        /// <summary>
        /// Returns just the samples on the requested side of <see cref="OffsetSampleFilterCategory"/>
        /// (true for the filter category itself -- a strum for guitar, a kick for drums -- false for
        /// the other side), or null if it isn't available/aligned for this instrument.
        /// </summary>
        private List<double> GetFilterCategorySamples(IReadOnlyList<double> samples, bool wantCategory = true)
        {
            if (OffsetSampleFilterCategory == null || OffsetSampleFilterCategory.Count != samples.Count)
            {
                return null;
            }

            var result = new List<double>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                if (OffsetSampleFilterCategory[i] == wantCategory)
                {
                    result.Add(samples[i]);
                }
            }

            return result;
        }

        private void BuildOffsetHistogram()
        {
            if (!TryGetHistogramSection(out var sectionContainer, out int insertIndex))
            {
                SetOffsetHistogramActive(false);
                return;
            }

            var offsetSamples = Stats.GetOffsetSamples();
            if (offsetSamples.Count == 0)
            {
                SetOffsetHistogramActive(false);
                return;
            }

            float minOffsetMs = -OFFSET_HISTOGRAM_ABS_BOUND_MS;
            float maxOffsetMs = OFFSET_HISTOGRAM_ABS_BOUND_MS;

            // OffsetSampleFilterCategory is read once, post-song, from the live chart's WasHit note
            // flags (see FiveFretGuitarPlayer/DrumsPlayer.GetOffsetSampleFilterCategory) -- it's
            // only meaningful when it lines up 1:1 with offsetSamples. If it's missing or
            // mismatched (an instrument with no distinction, or an older replay/save), everything
            // is treated as one category and the histogram renders as a single white bar per bin,
            // same as before this feature.
            IReadOnlyList<bool> filterCategory = OffsetSampleFilterCategory != null && OffsetSampleFilterCategory.Count == offsetSamples.Count
                ? OffsetSampleFilterCategory
                : null;

            // When calibration is set to exclude the filter category (e.g. "No Strums"), the
            // *other* side is what's actually driving calibration, so it renders as the primary
            // (white, bottom-of-stack) bar instead -- otherwise the graph would spotlight the side
            // being ignored rather than the one actually being calibrated against.
            bool primaryIsCategory = FilterMode != OffsetCalibrationFilter.ExcludeSelected;

            int[] categoryBins = BuildHistogramBins(offsetSamples, filterCategory, wantCategory: primaryIsCategory, minOffsetMs, maxOffsetMs);
            int[] otherBins = filterCategory == null
                ? new int[OFFSET_HISTOGRAM_BIN_COUNT]
                : BuildHistogramBins(offsetSamples, filterCategory, wantCategory: !primaryIsCategory, minOffsetMs, maxOffsetMs);

            int maxCount = 0;
            for (int i = 0; i < OFFSET_HISTOGRAM_BIN_COUNT; i++)
            {
                int stackedCount = categoryBins[i] + otherBins[i];
                if (stackedCount > maxCount)
                {
                    maxCount = stackedCount;
                }
            }

            if (maxCount <= 0)
            {
                SetOffsetHistogramActive(false);
                return;
            }

            EnsureOffsetHistogramLayout(sectionContainer, insertIndex);
            SetOffsetHistogramActive(true);
            var layoutElement = _offsetHistogramObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = OFFSET_HISTOGRAM_TOTAL_HEIGHT;
            layoutElement.minHeight = OFFSET_HISTOGRAM_TOTAL_HEIGHT;

            _offsetHistogramContentRect.offsetMin = new Vector2(OFFSET_HISTOGRAM_HORIZONTAL_MARGIN, 0f);
            _offsetHistogramContentRect.offsetMax = new Vector2(-OFFSET_HISTOGRAM_HORIZONTAL_MARGIN, 0f);
            _offsetHistogramGraphRect.anchoredPosition = new Vector2(0f, OFFSET_HISTOGRAM_AXIS_LABEL_HEIGHT - 2f);
            _offsetHistogramGraphRect.sizeDelta = new Vector2(0f, OFFSET_HISTOGRAM_GRAPH_HEIGHT);

            float zeroAxisPosition = Mathf.InverseLerp(minOffsetMs, maxOffsetMs, 0f);
            SetVerticalAxisLinePosition(_offsetHistogramZeroLineRect, zeroAxisPosition, 3f);
            PopulateHistogramBars(categoryBins, otherBins, maxCount);
            SetHistogramAxisLabels(minOffsetMs, maxOffsetMs);
        }

        private void EnsureOffsetHistogramLayout(Transform sectionContainer, int insertIndex)
        {
            if (_offsetHistogramObject == null)
            {
                CreateOffsetHistogramLayout();
            }

            _offsetHistogramRootRect.SetParent(sectionContainer, false);
            _offsetHistogramRootRect.SetSiblingIndex(insertIndex);
        }

        private bool TryGetHistogramSection(out Transform sectionContainer, out int insertIndex)
        {
            sectionContainer = null;
            insertIndex = 0;

            if (_statsRect == null || _statsRect.content == null || _averageOffset == null)
            {
                return false;
            }

            var averageOffsetRow = _averageOffset.transform.parent;
            if (averageOffsetRow == null)
            {
                return false;
            }

            sectionContainer = averageOffsetRow.parent == null ? _statsRect.content : averageOffsetRow.parent;
            insertIndex = averageOffsetRow.GetSiblingIndex();
            return true;
        }

        private void SetHistogramAxisLabels(float minOffsetMs, float maxOffsetMs)
        {
            _offsetHistogramLeftAxisLabel.fontSize = OFFSET_HISTOGRAM_AXIS_FONT_SIZE;
            _offsetHistogramLeftAxisLabel.text = $"{Mathf.RoundToInt(minOffsetMs)} ms";
            _offsetHistogramCenterAxisLabel.fontSize = OFFSET_HISTOGRAM_AXIS_FONT_SIZE;
            _offsetHistogramCenterAxisLabel.text = "0";
            _offsetHistogramRightAxisLabel.fontSize = OFFSET_HISTOGRAM_AXIS_FONT_SIZE;
            _offsetHistogramRightAxisLabel.text = $"+{Mathf.RoundToInt(maxOffsetMs)} ms";
        }

        private void CreateOffsetHistogramLayout()
        {
            _offsetHistogramObject = new GameObject("Offset Histogram", typeof(RectTransform), typeof(LayoutElement));
            _offsetHistogramRootRect = (RectTransform) _offsetHistogramObject.transform;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            _offsetHistogramContentRect = (RectTransform) contentObject.transform;
            _offsetHistogramContentRect.SetParent(_offsetHistogramRootRect, false);
            _offsetHistogramContentRect.anchorMin = Vector2.zero;
            _offsetHistogramContentRect.anchorMax = Vector2.one;

            var graphObject = new GameObject("Graph", typeof(RectTransform));
            _offsetHistogramGraphRect = (RectTransform) graphObject.transform;
            _offsetHistogramGraphRect.SetParent(_offsetHistogramContentRect, false);
            _offsetHistogramGraphRect.anchorMin = new Vector2(0f, 0f);
            _offsetHistogramGraphRect.anchorMax = new Vector2(1f, 0f);
            _offsetHistogramGraphRect.pivot = new Vector2(0.5f, 0f);

            CreateHorizontalAxisLine(_offsetHistogramGraphRect, "XAxis", new Color(1f, 1f, 1f, 0.25f), 3f);
            _offsetHistogramZeroLineRect = CreateVerticalAxisLine(_offsetHistogramGraphRect, "Zero",
                new Color(1f, 1f, 1f, 0.35f), 3f);

            var barsObject = new GameObject("Bars", typeof(RectTransform));
            _offsetHistogramBarsRect = (RectTransform) barsObject.transform;
            _offsetHistogramBarsRect.SetParent(_offsetHistogramGraphRect, false);
            _offsetHistogramBarsRect.anchorMin = Vector2.zero;
            _offsetHistogramBarsRect.anchorMax = Vector2.one;
            _offsetHistogramBarsRect.offsetMin = Vector2.zero;
            _offsetHistogramBarsRect.offsetMax = Vector2.zero;

            _offsetHistogramLeftAxisLabel = CreateHistogramLabel(_offsetHistogramContentRect, "Axis Left",
                TextAlignmentOptions.Left);
            ConfigureAxisLabel(_offsetHistogramLeftAxisLabel);

            _offsetHistogramCenterAxisLabel = CreateHistogramLabel(_offsetHistogramContentRect, "Axis Center",
                TextAlignmentOptions.Center);
            ConfigureAxisLabel(_offsetHistogramCenterAxisLabel);

            _offsetHistogramRightAxisLabel = CreateHistogramLabel(_offsetHistogramContentRect, "Axis Right",
                TextAlignmentOptions.Right);
            ConfigureAxisLabel(_offsetHistogramRightAxisLabel);
        }

        private static void ConfigureAxisLabel(TextMeshProUGUI label)
        {
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, -3f);
            labelRect.sizeDelta = new Vector2(0f, OFFSET_HISTOGRAM_AXIS_LABEL_HEIGHT);
        }

        private void PopulateHistogramBars(IReadOnlyList<int> categoryBins, IReadOnlyList<int> otherBins, int maxCount)
        {
            float barMaxHeight = OFFSET_HISTOGRAM_GRAPH_HEIGHT - 2f;
            int binCount = categoryBins.Count;
            int barPoolIndex = 0;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_offsetHistogramRootRect);

            float scaleFactor = GetCanvasScaleFactor(_offsetHistogramBarsRect);
            float graphWidthUnits = _offsetHistogramBarsRect.rect.width;
            bool canUsePixelSnapping = graphWidthUnits > 0.01f;
            float graphWidthPixels = canUsePixelSnapping ? Mathf.Max(1f, graphWidthUnits * scaleFactor) : 0f;
            float barBaseYPixels = Mathf.Round(1f * scaleFactor);
            float halfGapUnits = 1f / scaleFactor * 0.5f;

            for (int i = 0; i < binCount; i++)
            {
                int categoryCount = categoryBins[i];
                int otherCount = otherBins[i];
                if (categoryCount <= 0 && otherCount <= 0)
                {
                    continue;
                }

                if (canUsePixelSnapping)
                {
                    float slotLeftPixels = Mathf.Round(i * graphWidthPixels / binCount);
                    float slotRightPixels = Mathf.Round((i + 1f) * graphWidthPixels / binCount);
                    float barLeftPixels = slotLeftPixels;
                    float barRightPixels = slotRightPixels - 1f;

                    if (barRightPixels <= barLeftPixels)
                    {
                        barRightPixels = Mathf.Min(graphWidthPixels, barLeftPixels + 1f);
                    }

                    if (barRightPixels <= barLeftPixels)
                    {
                        continue;
                    }

                    float stackYPixels = barBaseYPixels;

                    if (categoryCount > 0)
                    {
                        float segmentHeightPixels =
                            Mathf.Max(1f, Mathf.Round((float) categoryCount / maxCount * barMaxHeight * scaleFactor));
                        PlaceBarSegmentPixels(GetOrCreateBar(barPoolIndex++), OFFSET_HISTOGRAM_PRIMARY_COLOR,
                            barLeftPixels, barRightPixels, stackYPixels, segmentHeightPixels, scaleFactor);
                        stackYPixels += segmentHeightPixels;
                    }

                    if (otherCount > 0)
                    {
                        float segmentHeightPixels =
                            Mathf.Max(1f, Mathf.Round((float) otherCount / maxCount * barMaxHeight * scaleFactor));
                        PlaceBarSegmentPixels(GetOrCreateBar(barPoolIndex++), OFFSET_HISTOGRAM_SECONDARY_COLOR,
                            barLeftPixels, barRightPixels, stackYPixels, segmentHeightPixels, scaleFactor);
                    }
                }
                else
                {
                    float anchorMinX = i / (float) binCount;
                    float anchorMaxX = (i + 1f) / binCount;
                    float stackHeight = 0f;

                    if (categoryCount > 0)
                    {
                        float segmentHeight = Mathf.Max(1f, (float) categoryCount / maxCount * barMaxHeight);
                        PlaceBarSegmentUnits(GetOrCreateBar(barPoolIndex++), OFFSET_HISTOGRAM_PRIMARY_COLOR,
                            anchorMinX, anchorMaxX, halfGapUnits, stackHeight, segmentHeight);
                        stackHeight += segmentHeight;
                    }

                    if (otherCount > 0)
                    {
                        float segmentHeight = Mathf.Max(1f, (float) otherCount / maxCount * barMaxHeight);
                        PlaceBarSegmentUnits(GetOrCreateBar(barPoolIndex++), OFFSET_HISTOGRAM_SECONDARY_COLOR,
                            anchorMinX, anchorMaxX, halfGapUnits, stackHeight, segmentHeight);
                    }
                }
            }

            for (int i = barPoolIndex; i < _offsetHistogramBarPool.Count; i++)
            {
                _offsetHistogramBarPool[i].gameObject.SetActive(false);
            }
        }

        private static void PlaceBarSegmentPixels(RectTransform barRect, Color color, float leftPixels,
            float rightPixels, float bottomPixels, float heightPixels, float scaleFactor)
        {
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = Vector2.zero;
            barRect.pivot = Vector2.zero;
            barRect.anchoredPosition = new Vector2(leftPixels / scaleFactor, bottomPixels / scaleFactor);
            barRect.sizeDelta = new Vector2((rightPixels - leftPixels) / scaleFactor, heightPixels / scaleFactor);

            var image = barRect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            barRect.gameObject.SetActive(true);
        }

        private static void PlaceBarSegmentUnits(RectTransform barRect, Color color, float anchorMinX,
            float anchorMaxX, float halfGapUnits, float bottomOffset, float height)
        {
            barRect.anchorMin = new Vector2(anchorMinX, 0f);
            barRect.anchorMax = new Vector2(anchorMaxX, 0f);
            barRect.offsetMin = new Vector2(halfGapUnits, 1f + bottomOffset);
            barRect.offsetMax = new Vector2(-halfGapUnits, 1f + bottomOffset + height);

            var image = barRect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            barRect.gameObject.SetActive(true);
        }

        private RectTransform GetOrCreateBar(int index)
        {
            while (_offsetHistogramBarPool.Count <= index)
            {
                var barObject = new GameObject($"Bar {_offsetHistogramBarPool.Count}", typeof(RectTransform),
                    typeof(Image));
                var barRect = (RectTransform) barObject.transform;
                barRect.SetParent(_offsetHistogramBarsRect, false);
                _offsetHistogramBarPool.Add(barRect);
            }

            return _offsetHistogramBarPool[index];
        }

        private static float GetCanvasScaleFactor(Component component)
        {
            var canvas = component.GetComponentInParent<Canvas>();
            return canvas != null ? Mathf.Max(0.0001f, canvas.scaleFactor) : 1f;
        }

        private void SetOffsetHistogramActive(bool active)
        {
            if (_offsetHistogramObject != null && _offsetHistogramObject.activeSelf != active)
            {
                _offsetHistogramObject.SetActive(active);
            }
        }

        /// <summary>
        /// Bins offset samples into <see cref="OFFSET_HISTOGRAM_BIN_COUNT"/> buckets. When
        /// <paramref name="filterCategory"/> is non-null, only samples matching
        /// <paramref name="wantCategory"/> are counted, so callers can build the category and
        /// non-category bins separately for stacking.
        /// </summary>
        private static int[] BuildHistogramBins(IReadOnlyList<double> offsetSamples, IReadOnlyList<bool> filterCategory,
            bool wantCategory, float minOffsetMs, float maxOffsetMs)
        {
            var bins = new int[OFFSET_HISTOGRAM_BIN_COUNT];
            float totalRange = Mathf.Max(1f, maxOffsetMs - minOffsetMs);

            for (int i = 0; i < offsetSamples.Count; i++)
            {
                if (filterCategory != null && filterCategory[i] != wantCategory)
                {
                    continue;
                }

                float offsetMs = Mathf.Clamp((float) (offsetSamples[i] * 1000d), minOffsetMs, maxOffsetMs);
                float normalized = (offsetMs - minOffsetMs) / totalRange;
                int index = Mathf.Clamp(Mathf.FloorToInt(normalized * OFFSET_HISTOGRAM_BIN_COUNT), 0,
                    OFFSET_HISTOGRAM_BIN_COUNT - 1);

                bins[index]++;
            }

            return bins;
        }

        private TextMeshProUGUI CreateHistogramLabel(Transform parent, string name, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = _averageOffset.font;
            label.fontSharedMaterial = _averageOffset.fontSharedMaterial;
            label.color = _averageOffset.color;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = true;
            label.alignment = alignment;
            label.raycastTarget = false;

            return label;
        }

        private static RectTransform CreateVerticalAxisLine(Transform parent, string name, Color color, float thickness)
        {
            var lineObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            var lineRect = (RectTransform) lineObject.transform;
            lineRect.SetParent(parent, false);
            SetVerticalAxisLinePosition(lineRect, 0.5f, thickness);

            var lineImage = lineObject.GetComponent<Image>();
            lineImage.color = color;
            lineImage.raycastTarget = false;

            return lineRect;
        }

        private static void SetVerticalAxisLinePosition(RectTransform lineRect, float normalizedX, float thickness)
        {
            float clampedX = Mathf.Clamp01(normalizedX);
            float halfThickness = thickness * 0.5f;
            lineRect.anchorMin = new Vector2(clampedX, 0f);
            lineRect.anchorMax = new Vector2(clampedX, 1f);
            lineRect.offsetMin = new Vector2(-halfThickness, halfThickness);
            lineRect.offsetMax = new Vector2(halfThickness, halfThickness);
        }

        private static void CreateHorizontalAxisLine(Transform parent, string name, Color color, float thickness)
        {
            var lineObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            var lineRect = (RectTransform) lineObject.transform;
            lineRect.SetParent(parent, false);
            float clampedThickness = Mathf.Max(1f, thickness);
            float halfThickness = clampedThickness * 0.5f;
            float yShiftUnits = 1f / GetCanvasScaleFactor(parent);
            lineRect.anchorMin = new Vector2(0f, 0f);
            lineRect.anchorMax = new Vector2(1f, 0f);
            lineRect.offsetMin = new Vector2(0f, -halfThickness - yShiftUnits);
            lineRect.offsetMax = new Vector2(0f, halfThickness - yShiftUnits);

            var lineImage = lineObject.GetComponent<Image>();
            lineImage.color = color;
            lineImage.raycastTarget = false;
        }

        private void ShowTag(string tagText)
        {
            _tagGameObject.SetActive(true);
            _tagText.text = tagText;
        }

        protected string ColorizePrimary(object s)
        {
            return $"<font-weight=600><color=#FFFFFF>{s}</color></font-weight>";
        }

        private string ColorizeSecondary(object s)
        {
            return $"<font-weight=500><color=#7D7DA3>{s}</color></font-weight>";
        }

        public void ScrollStats(float delta)
        {
            _statsRect.MoveVerticalInUnits(delta);
        }

        protected void ScrollStatsToTop()
        {
            _statsRect.verticalNormalizedPosition = 1f;
        }

        public virtual void SetAdvancedStatsShown(bool showAdvanced)
        {
            _advancedStatsRect.gameObject.SetActive(showAdvanced);
            _basicStatsRect.gameObject.SetActive(!showAdvanced);
            ScrollStatsToTop();
        }
    }

    public interface IScoreCard<out T> where T : BaseStats
    {
        YargPlayer Player { get; }
        void ScrollStats(float delta);
        void SetCardContents();
        void SetAdvancedStatsShown(bool showAdvanced);
    }
}
