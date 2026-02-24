using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Localization;
using YARG.Player;

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
        private Transform _modifierIconContainer;

        [Space]
        [SerializeField]
        private Image _instrumentIcon;

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
        private TextMeshProUGUI _notesMissed;
        [SerializeField]
        private TextMeshProUGUI _starpowerPhrases;
        [SerializeField]
        private TextMeshProUGUI _bandBonusScore;
        [SerializeField]
        private TextMeshProUGUI _averageOffset;

        [SerializeField]
        private RectTransform _advancedStatsRect;
        [SerializeField]
        private RectTransform _basicStatsRect;


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

        protected bool IsHighScore;
        protected T Stats;

        public YargPlayer Player { get; private set; }

        private void Awake()
        {
            _colorizer = GetComponent<ScoreCardColorizer>();
        }

        public void Initialize(bool isHighScore, YargPlayer player, T stats)
        {
            IsHighScore = isHighScore;
            Player = player;
            Stats = stats;
        }

        public virtual void SetCardContents()
        {
            _playerName.text = Player.Profile.Name;

            _instrument.text = Player.Profile.CurrentInstrument.ToLocalizedName();
            _difficulty.text = Player.Profile.CurrentDifficulty.ToDisplayName();

            // Set percent
            _accuracyPercent.text = $"{Mathf.FloorToInt(Stats.Percent * 100f)}%";


            // Set background and foreground colors
            if (Player.Profile.IsBot)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gray);
                ShowTag("Bot");
            }
            else if (Player.IsReplay)
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
            else
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
                HideTag();
            }

            _score.text = Stats.TotalScore.ToString("N0");
            _starView.SetStars((int) Stats.Stars);

            _notesHit.text = $"{WrapWithColor(Stats.NotesHit)} / {Stats.TotalNotes}";
            _maxStreak.text = WrapWithColor(Stats.MaxCombo);
            _notesMissed.text = WrapWithColor(Stats.NotesMissed);
            _starpowerPhrases.text = $"{WrapWithColor(Stats.StarPowerPhrasesHit)} / {Stats.TotalStarPowerPhrases}";
            _bandBonusScore.text = WrapWithColor(Stats.BandBonusScore.ToString("N0"));
            _averageOffset.text = WrapWithColor(
                Mathf.RoundToInt((float) (Stats.GetAverageOffset() * 1000.0)).ToString() + " ms");
            BuildOffsetHistogram();

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{Player.Profile.CurrentInstrument.ToResourceName()}]")
                .WaitForCompletion();

            // Set engine preset icons
            ModifierIcon.SpawnEnginePresetIcons(_modifierIconPrefab, _modifierIconContainer,
                Player.EnginePreset, Player.Profile.GameMode);

            // Set modifier icons
            foreach (var modifier in EnumExtensions<Modifier>.Values)
            {
                if (modifier == Modifier.None) continue;

                if (!Player.Profile.IsModifierActive(modifier)) continue;

                var icon = Instantiate(_modifierIconPrefab, _modifierIconContainer);
                icon.InitializeForModifier(modifier);
            }
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
            int[] bins = BuildHistogramBins(offsetSamples, minOffsetMs, maxOffsetMs, out int maxCount);
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
            PopulateHistogramBars(bins, maxCount);
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

        private void PopulateHistogramBars(IReadOnlyList<int> bins, int maxCount)
        {
            float barMaxHeight = OFFSET_HISTOGRAM_GRAPH_HEIGHT - 2f;
            var barColor = _colorizer.CurrentColor;
            barColor.a = 0.85f;
            int barPoolIndex = 0;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_offsetHistogramRootRect);

            float scaleFactor = GetCanvasScaleFactor(_offsetHistogramBarsRect);
            float graphWidthUnits = _offsetHistogramBarsRect.rect.width;
            bool canUsePixelSnapping = graphWidthUnits > 0.01f;
            float graphWidthPixels = canUsePixelSnapping ? Mathf.Max(1f, graphWidthUnits * scaleFactor) : 0f;
            float barBaseYPixels = Mathf.Round(1f * scaleFactor);
            float halfGapUnits = 1f / scaleFactor * 0.5f;

            for (int i = 0; i < bins.Count; i++)
            {
                int count = bins[i];
                if (count <= 0)
                {
                    continue;
                }

                float normalizedHeight = (float) count / maxCount;
                var barRect = GetOrCreateBar(barPoolIndex++);

                if (canUsePixelSnapping)
                {
                    float barHeightPixels = Mathf.Max(1f, Mathf.Round(normalizedHeight * barMaxHeight * scaleFactor));
                    float slotLeftPixels = Mathf.Round(i * graphWidthPixels / bins.Count);
                    float slotRightPixels = Mathf.Round((i + 1f) * graphWidthPixels / bins.Count);
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

                    barRect.anchorMin = Vector2.zero;
                    barRect.anchorMax = Vector2.zero;
                    barRect.pivot = Vector2.zero;
                    barRect.anchoredPosition = new Vector2(barLeftPixels / scaleFactor, barBaseYPixels / scaleFactor);
                    barRect.sizeDelta = new Vector2((barRightPixels - barLeftPixels) / scaleFactor,
                        barHeightPixels / scaleFactor);
                }
                else
                {
                    float height = Mathf.Max(1f, normalizedHeight * barMaxHeight);
                    barRect.anchorMin = new Vector2(i / (float) bins.Count, 0f);
                    barRect.anchorMax = new Vector2((i + 1f) / bins.Count, 0f);
                    barRect.offsetMin = new Vector2(halfGapUnits, 1f);
                    barRect.offsetMax = new Vector2(-halfGapUnits, 1f + height);
                }

                var image = barRect.GetComponent<Image>();
                image.color = barColor;
                image.raycastTarget = false;
                barRect.gameObject.SetActive(true);
            }

            for (int i = barPoolIndex; i < _offsetHistogramBarPool.Count; i++)
            {
                _offsetHistogramBarPool[i].gameObject.SetActive(false);
            }
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

        private static int[] BuildHistogramBins(IReadOnlyList<double> offsetSamples, float minOffsetMs,
            float maxOffsetMs, out int maxCount)
        {
            var bins = new int[OFFSET_HISTOGRAM_BIN_COUNT];
            float totalRange = Mathf.Max(1f, maxOffsetMs - minOffsetMs);
            maxCount = 0;

            for (int i = 0; i < offsetSamples.Count; i++)
            {
                float offsetMs = Mathf.Clamp((float) (offsetSamples[i] * 1000d), minOffsetMs, maxOffsetMs);
                float normalized = (offsetMs - minOffsetMs) / totalRange;
                int index = Mathf.Clamp(Mathf.FloorToInt(normalized * OFFSET_HISTOGRAM_BIN_COUNT), 0,
                    OFFSET_HISTOGRAM_BIN_COUNT - 1);

                bins[index]++;
                if (bins[index] > maxCount)
                {
                    maxCount = bins[index];
                }
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
            label.enableWordWrapping = false;
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

        private void HideTag()
        {
            _tagGameObject.SetActive(false);
        }

        protected string WrapWithColor(object s)
        {
            return
                $"<font-weight=700><color=#{ColorUtility.ToHtmlStringRGB(_colorizer.CurrentColor)}>" +
                $"{s}</color></font-weight>";
        }

        public void ScrollStats(float delta)
        {
            _statsRect.MoveVerticalInUnits(delta);
        }

        protected void ScrollStatsToTop()
        {
            _statsRect.verticalNormalizedPosition = 1f;
        }

        public void SetAdvancedStatsShown(bool showAdvanced)
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
