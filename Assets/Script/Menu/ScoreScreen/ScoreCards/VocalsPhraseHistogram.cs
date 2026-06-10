using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Gameplay.Vocals;
using YARG.Localization;

namespace YARG.Menu.ScoreScreen
{
    public static class VocalsPhraseHistogram
    {
        // Matches the offset histogram's footprint exactly (see OFFSET_HISTOGRAM_* in ScoreCard).
        private const float GRAPH_HEIGHT = 132f;
        private const float HORIZONTAL_MARGIN = 54f;
        // Floor so a fully-missed (0%) phrase still shows a clearly visible stub, kept just under the
        // Messy cutoff line so it still reads as "below Messy".
        private const float BAR_MIN_HEIGHT = 9f;
        private const float BAR_ALPHA = 0.875f;
        private const float BAR_DIM_TINT = 0.82f;       // multiplier for even Awesome bars (applied as RawImage color tint)
        private const float BAR_GRADIENT_BOTTOM = 0.55f; // fraction of top color at bar bottom (gray bar vertical gradient)
        // Re-introduce a half-gap between bars when the phrase count is low enough to benefit.
        private const int BAR_GAP_THRESHOLD = 25;
        private const float BAR_HALF_GAP_PX = 1.5f;
        private const float BAR_EDGE_PAD = BAR_HALF_GAP_PX * 2f; // fixed outer inset, always present

        private const float TALLY_ROW_HEIGHT = 22f;
        private const float TALLY_SPACING = 2f;
        private const float TALLY_SIDE_PADDING = HORIZONTAL_MARGIN; // line table edges up with the bars
        private const float TALLY_COUNT_WIDTH = 56f;
        private const float DIVIDER_THICKNESS = 2f;
        private const float SECTION_SPACING = 14f;
        private const float BAR_BASE_Y = 1f;

        // Match the score card's existing section-header and stat-label text styling (see
        // ScoreCard.prefab) so the summary blends in rather than standing out.
        private const float TEXT_SIZE = 20f;
        private static readonly Color HeaderColor = new(0.8509804f, 0.8509804f, 1f);          // section header lavender
        private static readonly Color CoolGrayColor = new(0.48235294f, 0.49803922f, 0.60392157f); // #7b7f9a (tier labels)
        private static readonly Color MutedColor    = new(0.49019608f, 0.49019608f, 0.6392157f);  // #7D7DA3 (zero counts)
        private static readonly Color GoldColor      = new(1f,        0.83921569f, 0.25882353f); // #FFD642 Awesome bar top
        private static readonly Color UtOrangeColor  = new(1f,        0.51764706f, 0.07450980f); // #FF8413 Awesome bar bottom
        private static readonly Color BarDefaultColor = new(0.47843137f, 0.47843137f, 0.47843137f); // #7a7a7a Gray (Light #4) — dim bars
        private static readonly Color BarBrightColor  = new(0.62745098f, 0.62745098f, 0.62745098f); // #a0a0a0 Gray (Light #3.5) — bright bars
        private static readonly Color BarCapColor      = new(0.9607843f,  0.9607843f,  0.9607843f,  1f); // #F5F5F5 White Smoke
        // Tier cutoff line colors (branding palette).
        private static readonly Color LineColorMessy  = new(0.93725490f, 0.20392157f, 0.21960784f); // #ef3438 Imperial Red
        private static readonly Color LineColorOkay   = new(0.72156863f, 0.72156863f, 0.72156863f); // #b8b8b8 Silver (Light #3)
        private static readonly Color LineColorGood   = new(0.27058824f, 0.84705882f, 0.99607843f); // #45d8fe Vivid Sky Blue
        private static readonly Color LineColorStrong = new(0.16862745f, 0.88235294f, 0.55294118f); // #2be18d Emerald

        // The brand fonts used by the card: Red Hat Display (headers), Barlow (body). Resolved
        // lazily from already-loaded assets so we don't need serialized prefab references.
        private const string HEADER_FONT_NAME = "RedHatDisplay-ExtraBold";
        private const string LABEL_FONT_NAME = "Barlow-Medium";
        private static TMP_FontAsset _headerFont;
        private static TMP_FontAsset _labelFont;
        // One 1×N vertical gradient texture per tier region; created once and reused across score screens.
        private const int GRADIENT_TEX_WIDTH = 256;
        private const float REGION_FILL_ALPHA = 0.125f;
        private const float REGION_FADE_MIN = 0.375f;
        private static readonly Texture2D[] _tierGradientTextures = new Texture2D[5];

        private static Texture2D _awesomeBarGradient;
        private static Texture2D _grayBarGradient;

        public static void Build(RectTransform parent, IReadOnlyList<float> percents,
            Func<Transform, string, TextAlignmentOptions, TextMeshProUGUI> labelFactory, Color accentColor,
            int percussionHits, int percussionTotal)
        {
            if (parent == null || percents == null || percents.Count == 0)
            {
                return;
            }

            EnsureFonts();

            var rootRect = CreateLayoutColumn("Vocals Phrase Summary", parent, SECTION_SPACING);
            // Sit at the top of the advanced container (above the now-empty offset-histogram slot),
            // so the header lines up with "PERFORMANCE" in the basic view.
            rootRect.SetAsFirstSibling();

            // Section header — centered, uppercase, subdued, matching "PERFORMANCE" above it.
            var header = labelFactory(rootRect, "Header", TextAlignmentOptions.Top);
            header.text = Localize.Key("Menu.ScoreScreen.PhraseSummaryHeader");
            StyleText(header, _headerFont, HeaderColor, TextAlignmentOptions.Top);
            AddLayoutElement(header.rectTransform, 50f);

            BuildGraph(rootRect, percents);
            BuildTally(rootRect, percents, labelFactory, accentColor, percussionHits, percussionTotal);
        }

        private static void BuildGraph(RectTransform parent, IReadOnlyList<float> percents)
        {
            var graphObject = new GameObject("Graph", typeof(RectTransform));
            var graphRect = (RectTransform) graphObject.transform;
            graphRect.SetParent(parent, false);
            AddLayoutElement(graphRect, GRAPH_HEIGHT);

            // Inset the bars to match the offset histogram's horizontal margins.
            var barsObject = new GameObject("Bars", typeof(RectTransform));
            var barsRect = (RectTransform) barsObject.transform;
            barsRect.SetParent(graphRect, false);
            barsRect.anchorMin = Vector2.zero;
            barsRect.anchorMax = Vector2.one;
            barsRect.offsetMin = new Vector2(HORIZONTAL_MARGIN, 0f);
            barsRect.offsetMax = new Vector2(-HORIZONTAL_MARGIN, 0f);

            // Inner rect inset by BAR_EDGE_PAD on each side — bars live here so the fixed outer
            // margin is independent of the inter-bar half-gap. Regions/axis stay on barsRect.
            var innerBarsObject = new GameObject("BarsInner", typeof(RectTransform));
            var innerBarsRect = (RectTransform) innerBarsObject.transform;
            innerBarsRect.SetParent(barsRect, false);
            innerBarsRect.anchorMin = Vector2.zero;
            innerBarsRect.anchorMax = Vector2.one;
            innerBarsRect.offsetMin = new Vector2(BAR_EDGE_PAD, 0f);
            innerBarsRect.offsetMax = new Vector2(-BAR_EDGE_PAD, 0f);

            float lineThickness = Mathf.Ceil(PixelUnit(barsRect));

            // Baseline axis line.
            var axis = new GameObject("XAxis", typeof(RectTransform), typeof(Image));
            var axisRect = (RectTransform) axis.transform;
            axisRect.SetParent(barsRect, false);
            axisRect.anchorMin = new Vector2(0f, 0f);
            axisRect.anchorMax = new Vector2(1f, 0f);
            // Axis top sits at BAR_BASE_Y so it doesn't overlap with the bar bottoms.
            axisRect.offsetMin = new Vector2(0f, BAR_BASE_Y - 3f);
            axisRect.offsetMax = new Vector2(0f, BAR_BASE_Y);
            var axisImage = axis.GetComponent<Image>();
            axisImage.color = new Color(1f, 1f, 1f, 0.25f);
            axisImage.raycastTarget = false;

            // Tier background regions at uniform 0.125 alpha, drawn BEHIND the bars.
            DrawTierRegions(barsRect);

            // Subtle tier boundary lines on top of the regions, still behind bars.
            for (int tier = 1; tier <= 4; tier++)
            {
                var grade = (VocalPhraseGrade) tier;
                float threshold = (float) grade.LowerBound();
                var lineColor = grade switch
                {
                    VocalPhraseGrade.Messy => LineColorMessy,
                    VocalPhraseGrade.Okay  => LineColorOkay,
                    VocalPhraseGrade.Good  => LineColorGood,
                    _                      => LineColorStrong
                };
                lineColor.a = 0.03125f;

                var cutoffObj = new GameObject($"Cutoff {grade}", typeof(RectTransform), typeof(Image));
                var cutoffRect = (RectTransform) cutoffObj.transform;
                cutoffRect.SetParent(barsRect, false);
                cutoffRect.anchorMin = new Vector2(0f, 0f);
                cutoffRect.anchorMax = new Vector2(1f, 0f);
                cutoffRect.pivot = new Vector2(0.5f, 0.5f);
                cutoffRect.sizeDelta = new Vector2(0f, lineThickness);
                cutoffRect.anchoredPosition = new Vector2(0f, BAR_BASE_Y + threshold * GRAPH_HEIGHT);
                var cutoffImg = cutoffObj.GetComponent<Image>();
                cutoffImg.color = lineColor;
                cutoffImg.raycastTarget = false;
            }


            // Push bars to the top of the render order so they sit in front of regions and lines.
            innerBarsRect.SetAsLastSibling();

            int count = percents.Count;
            float halfGap = count < BAR_GAP_THRESHOLD ? BAR_HALF_GAP_PX : 0f;

            for (int i = 0; i < count; i++)
            {
                var grade = VocalPhraseGradeExtensions.Classify(percents[i]);
                float fraction = Mathf.Clamp01(percents[i]);
                float height = Mathf.Max(BAR_MIN_HEIGHT, fraction * GRAPH_HEIGHT);

                bool isBright = (i % 2) == 1; // odd bars brighter

                var barObject = new GameObject($"Bar {i}", typeof(RectTransform));
                var barRect = (RectTransform) barObject.transform;
                barRect.SetParent(innerBarsRect, false);
                barRect.anchorMin = new Vector2(i / (float) count, 0f);
                barRect.anchorMax = new Vector2((i + 1f) / count, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.offsetMin = new Vector2(halfGap, BAR_BASE_Y);
                barRect.offsetMax = new Vector2(-halfGap, BAR_BASE_Y + height);

                if (grade == VocalPhraseGrade.Awesome)
                {
                    // Vertical gradient: gold (#FFD642) at top, UT Orange (#FF8413) at bottom.
                    // Brightness alternates via RawImage tint (same texture, no second allocation).
                    var rawImage = barObject.AddComponent<RawImage>();
                    rawImage.texture = GetOrCreateAwesomeBarGradient();
                    float tint = isBright ? 1f : BAR_DIM_TINT;
                    rawImage.color = new Color(tint, tint, tint, BAR_ALPHA);
                    rawImage.raycastTarget = false;
                }
                else
                {
                    // Vertical gradient: top = bar color, bottom = BAR_GRADIENT_BOTTOM fraction of it.
                    // One normalized texture is shared; the tint color shifts between dim and bright.
                    var rawImage = barObject.AddComponent<RawImage>();
                    rawImage.texture = GetOrCreateGrayBarGradient();
                    var baseColor = isBright ? BarBrightColor : BarDefaultColor;
                    rawImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, BAR_ALPHA);
                    rawImage.raycastTarget = false;

                    // Tier-colored cap line across the top of non-gold bars.
                    var capObj = new GameObject("Cap", typeof(RectTransform), typeof(Image));
                    var capRect = (RectTransform) capObj.transform;
                    capRect.SetParent(barObject.transform, false);
                    capRect.anchorMin = new Vector2(0f, 1f);
                    capRect.anchorMax = new Vector2(1f, 1f);
                    capRect.pivot = new Vector2(0.5f, 1f);
                    capRect.sizeDelta = new Vector2(0f, lineThickness);
                    capRect.anchoredPosition = new Vector2(0f, lineThickness * 0.5f);
                    var capImage = capObj.GetComponent<Image>();
                    var capColor = BarCapColor;
                    capColor.a = 0.5f;
                    capImage.color = capColor;
                    capImage.raycastTarget = false;
                }
            }
        }

        private static void BuildTally(RectTransform parent, IReadOnlyList<float> percents,
            Func<Transform, string, TextAlignmentOptions, TextMeshProUGUI> labelFactory, Color dividerColor,
            int percussionHits, int percussionTotal)
        {
            // Tally phrases per tier.
            int tierCount = VocalPhraseGrade.Awesome - VocalPhraseGrade.Awful + 1;
            var counts = new int[tierCount];
            for (int i = 0; i < percents.Count; i++)
            {
                counts[(int) VocalPhraseGradeExtensions.Classify(percents[i])]++;
            }

            var tallyRect = CreateLayoutColumn("Tally", parent, TALLY_SPACING);
            var tallyLayout = tallyRect.GetComponent<VerticalLayoutGroup>();
            tallyLayout.padding = new RectOffset((int) TALLY_SIDE_PADDING, (int) TALLY_SIDE_PADDING, 0, 0);

            // Always show every tier (best -> worst) so multiple players' tables line up row-for-row,
            // even when a tier has no phrases.
            for (int grade = tierCount - 1; grade >= 0; grade--)
            {
                BuildTallyRow(tallyRect, (VocalPhraseGrade) grade, counts[grade], labelFactory);
            }

            // Vocal percussion (not a graded tier) gets its own row below the tiers, set off by a
            // divider in the card accent color. Omitted entirely when the chart has no percussion.
            if (percussionTotal > 0)
            {
                BuildDivider(tallyRect, dividerColor);
                BuildPercussionRow(tallyRect, percussionHits, percussionTotal, labelFactory);
            }
        }

        private static void BuildDivider(RectTransform parent, Color color)
        {
            var dividerObject = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            var dividerRect = (RectTransform) dividerObject.transform;
            dividerRect.SetParent(parent, false);
            AddLayoutElement(dividerRect, DIVIDER_THICKNESS);

            var image = dividerObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void BuildPercussionRow(RectTransform parent, int hits, int total,
            Func<Transform, string, TextAlignmentOptions, TextMeshProUGUI> labelFactory)
        {
            var rowObject = new GameObject("Percussion", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rowRect = (RectTransform) rowObject.transform;
            rowRect.SetParent(parent, false);
            AddLayoutElement(rowRect, TALLY_ROW_HEIGHT);

            var rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = 0f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            var label = labelFactory(rowRect, "Label", TextAlignmentOptions.Left);
            label.text = Localize.Key("Menu.ScoreScreen.Percussion");
            StyleText(label, _labelFont, CoolGrayColor, TextAlignmentOptions.Left);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.preferredHeight = TALLY_ROW_HEIGHT;

            // "hits / total" — numerator white, denominator muted, mirroring the regular stat rows.
            // When every percussion note was hit, the whole count goes gold (like a maxed tier).
            var countText = labelFactory(rowRect, "Count", TextAlignmentOptions.Right);
            bool allHit = hits == total;
            countText.text = allHit
                ? $"<color=#FFD642>{hits}</color> <color=#7D7DA3>/ {total}</color>"
                : $"{hits} <color=#7D7DA3>/ {total}</color>";
            StyleText(countText, _labelFont, null, TextAlignmentOptions.Right);
            var countLayout = countText.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = TALLY_COUNT_WIDTH;
            countLayout.preferredWidth = TALLY_COUNT_WIDTH;
            countLayout.flexibleWidth = 0f;
            countLayout.preferredHeight = TALLY_ROW_HEIGHT;
        }

        private static void BuildTallyRow(RectTransform parent, VocalPhraseGrade grade, int count,
            Func<Transform, string, TextAlignmentOptions, TextMeshProUGUI> labelFactory)
        {
            var rowObject = new GameObject($"Tally {grade}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rowRect = (RectTransform) rowObject.transform;
            rowRect.SetParent(parent, false);
            AddLayoutElement(rowRect, TALLY_ROW_HEIGHT);

            var rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = 0f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            // Tier label, left-justified, standard white.
            var label = labelFactory(rowRect, "Label", TextAlignmentOptions.Left);
            label.text = Localize.Key("Gameplay.Vocals.Performance", grade.ToLocalizationKey());
            StyleText(label, _labelFont, CoolGrayColor, TextAlignmentOptions.Left);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f;
            labelLayout.preferredHeight = TALLY_ROW_HEIGHT;

            // Count, right-justified. Same white as the label, dimmed to muted grey when zero.
            var countText = labelFactory(rowRect, "Count", TextAlignmentOptions.Right);
            countText.text = count.ToString();
            StyleText(countText, _labelFont, count > 0 ? (Color?) null : MutedColor,
                TextAlignmentOptions.Right);
            var countLayout = countText.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = TALLY_COUNT_WIDTH;
            countLayout.preferredWidth = TALLY_COUNT_WIDTH;
            countLayout.flexibleWidth = 0f;
            countLayout.preferredHeight = TALLY_ROW_HEIGHT;
        }

        private static void DrawTierRegions(RectTransform barsRect)
        {
            // Five regions covering the full graph height, bottom to top.
            (float bottom, float top, Color color, int idx)[] regions =
            {
                ((float) VocalPhraseGrade.Awful.LowerBound(),  (float) VocalPhraseGrade.Messy.LowerBound(),  LineColorMessy,  0),
                ((float) VocalPhraseGrade.Messy.LowerBound(),  (float) VocalPhraseGrade.Okay.LowerBound(),   BarDefaultColor, 1),
                ((float) VocalPhraseGrade.Okay.LowerBound(),   (float) VocalPhraseGrade.Good.LowerBound(),   LineColorOkay,   2),
                ((float) VocalPhraseGrade.Good.LowerBound(),   (float) VocalPhraseGrade.Strong.LowerBound(), LineColorGood,   3),
                ((float) VocalPhraseGrade.Strong.LowerBound(), (float) VocalPhraseGrade.Awesome.LowerBound(),LineColorStrong, 4),
            };

            foreach (var (bottom, top, color, idx) in regions)
            {
                var obj = new GameObject($"Region {idx}", typeof(RectTransform), typeof(RawImage));
                var rect = (RectTransform) obj.transform;
                rect.SetParent(barsRect, false);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.offsetMin = new Vector2(0f, BAR_BASE_Y + bottom * GRAPH_HEIGHT);
                rect.offsetMax = new Vector2(0f, BAR_BASE_Y + top * GRAPH_HEIGHT);

                var rawImage = obj.GetComponent<RawImage>();
                rawImage.texture = GetOrCreateGradientTexture(idx, color);
                rawImage.color = new Color(1f, 1f, 1f, REGION_FILL_ALPHA);
                rawImage.raycastTarget = false;
            }
        }

        private static Texture2D GetOrCreateAwesomeBarGradient()
        {
            if (_awesomeBarGradient != null)
                return _awesomeBarGradient;

            // 1×N texture: pixel row 0 (UV v=0) = bar bottom = UT Orange; top row = Gold.
            var tex = new Texture2D(1, GRADIENT_TEX_WIDTH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[GRADIENT_TEX_WIDTH];
            for (int i = 0; i < GRADIENT_TEX_WIDTH; i++)
            {
                pixels[i] = Color.Lerp(UtOrangeColor, GoldColor, i / (GRADIENT_TEX_WIDTH - 1f));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _awesomeBarGradient = tex;
            return tex;
        }

        private static Texture2D GetOrCreateGrayBarGradient()
        {
            if (_grayBarGradient != null)
                return _grayBarGradient;

            // 1×N normalized brightness ramp: pixel row 0 (bar bottom) = BAR_GRADIENT_BOTTOM,
            // top row = 1.0. The actual bar color is applied via RawImage.color as a tint.
            var tex = new Texture2D(1, GRADIENT_TEX_WIDTH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[GRADIENT_TEX_WIDTH];
            for (int i = 0; i < GRADIENT_TEX_WIDTH; i++)
            {
                float t = i / (GRADIENT_TEX_WIDTH - 1f);
                float v = Mathf.Lerp(BAR_GRADIENT_BOTTOM, 1f, t);
                pixels[i] = new Color(v, v, v, 1f);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            _grayBarGradient = tex;
            return tex;
        }

        private static Texture2D GetOrCreateGradientTexture(int idx, Color color)
        {
            if (_tierGradientTextures[idx] != null)
            {
                return _tierGradientTextures[idx];
            }

            // 1×N vertical texture: pixel row 0 (UV v=0) = region bottom, top row = region top.
            var tex = new Texture2D(1, GRADIENT_TEX_WIDTH, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[GRADIENT_TEX_WIDTH];

            // All tiers: 1.0 at bottom (lower bound) → REGION_FADE_MIN at top (next cutoff).
            for (int i = 0; i < GRADIENT_TEX_WIDTH; i++)
            {
                float t = i / (GRADIENT_TEX_WIDTH - 1f);
                float alpha = Mathf.Lerp(1f, REGION_FADE_MIN, t);
                var p = color;
                p.a = alpha;
                pixels[i] = p;
            }

            tex.SetPixels(pixels);
            tex.Apply();
            _tierGradientTextures[idx] = tex;
            return tex;
        }

        private static void EnsureFonts()
        {
            // Null-check rather than a resolved flag: Unity's overloaded == null detects
            // destroyed objects (e.g. after domain reload with Enter Play Mode Options),
            // so the scan re-runs automatically when the cached references become stale.
            if (_headerFont != null && _labelFont != null)
            {
                return;
            }

            // The card's brand fonts are already loaded (used across the menu UI); pick them up by
            // name. Falls back to whatever the label factory provided if a font isn't found.
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font.name == HEADER_FONT_NAME)
                {
                    _headerFont = font;
                }
                else if (font.name == LABEL_FONT_NAME)
                {
                    _labelFont = font;
                }
            }
        }

        // Pass null for color to inherit the prefab-derived color from the label factory.
        private static void StyleText(TextMeshProUGUI label, TMP_FontAsset font, Color? color,
            TextAlignmentOptions alignment)
        {
            if (font != null)
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
            }

            label.fontSize = TEXT_SIZE;
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 0f;
            if (color.HasValue)
                label.color = color.Value;
            label.alignment = alignment;
        }

        // Returns the canvas-unit size of one physical pixel, so callers can size thin elements to
        // always render as at least one visible pixel regardless of canvas DPI scaling.
        private static float PixelUnit(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            return canvas != null && canvas.scaleFactor > 0f ? 1f / canvas.scaleFactor : 1f;
        }

        private static RectTransform CreateLayoutColumn(string name, RectTransform parent, float spacing)
        {
            var columnObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            var columnRect = (RectTransform) columnObject.transform;
            columnRect.SetParent(parent, false);

            var layout = columnObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var fitter = columnObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            return columnRect;
        }

        private static void AddLayoutElement(RectTransform rect, float preferredHeight)
        {
            var layout = rect.gameObject.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = rect.gameObject.AddComponent<LayoutElement>();
            }

            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;
        }
    }
}
