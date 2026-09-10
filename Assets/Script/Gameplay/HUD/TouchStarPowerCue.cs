using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    /// <summary>
    ///     Marks the star power zone for touch play. Nothing is drawn in that
    ///     stretch of screen above the highway, so without a cue there is no
    ///     way to tell it can be tapped at all. It shows only while there is
    ///     star power to spend, which is the only time tapping there does
    ///     anything.
    /// </summary>
    public class TouchStarPowerCue : MonoBehaviour
    {
        // The gameplay canvases scale to this, so sizes here read the same as
        // the rest of the HUD
        private static readonly Vector2 REFERENCE_RESOLUTION = new(1920f, 1080f);

        private const float READY_ALPHA = 0.55f;
        private const float PULSE_DEPTH = 0.18f;
        private const float PULSE_SPEED = 3f;
        private const float FADE_SPEED = 4f;

        private const float LINE_THICKNESS = 3f;
        private const float LABEL_FONT_SIZE = 22f;
        private const float LABEL_GAP = 8f;
        private const float LABEL_HEIGHT = 34f;

        private static readonly Color CUE_COLOR = new(0.6f, 0.89f, 1f);

        private RectTransform _zone;
        private CanvasGroup   _group;

        private bool _available;

        public static TouchStarPowerCue Create()
        {
            var gameObject = new GameObject("Touch Star Power Cue")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // The highway and the HUD all sort at zero; a hairline and a
            // label over them hide nothing
            canvas.sortingOrder = 1;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;
            scaler.matchWidthOrHeight = 0f;

            return gameObject.AddComponent<TouchStarPowerCue>();
        }

        private void Awake()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;

            // The zone itself has to stay tappable
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _zone = NewRect("Zone", (RectTransform) transform);

            // The lower edge of the zone, which is the part a player needs to
            // see: everything above it counts as a tap
            var edge = NewRect("Edge", _zone);
            edge.anchorMin = new Vector2(0f, 0f);
            edge.anchorMax = new Vector2(1f, 0f);
            edge.pivot = new Vector2(0.5f, 0f);
            edge.sizeDelta = new Vector2(0f, LINE_THICKNESS);
            edge.anchoredPosition = Vector2.zero;
            AddImage(edge);

            var label = NewRect("Label", _zone);
            label.anchorMin = new Vector2(0f, 0f);
            label.anchorMax = new Vector2(1f, 0f);
            label.pivot = new Vector2(0.5f, 0f);
            label.sizeDelta = new Vector2(0f, LABEL_HEIGHT);
            label.anchoredPosition = new Vector2(0f, LINE_THICKNESS + LABEL_GAP);
            AddLabel(label);
        }

        /// <summary>
        ///     Places the cue along the bottom edge of the tappable zone, in
        ///     screen pixels.
        /// </summary>
        public void SetZone(float leftX, float rightX, float bottomY)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            _zone.anchorMin = new Vector2(leftX / Screen.width, bottomY / Screen.height);
            _zone.anchorMax = new Vector2(rightX / Screen.width, 1f);
            _zone.offsetMin = Vector2.zero;
            _zone.offsetMax = Vector2.zero;
        }

        public void SetAvailable(bool available)
        {
            _available = available;
        }

        private void Update()
        {
            float target = _available
                ? READY_ALPHA + Mathf.Sin(Time.unscaledTime * PULSE_SPEED) * PULSE_DEPTH
                : 0f;

            _group.alpha = Mathf.MoveTowards(_group.alpha, target, FADE_SPEED * Time.unscaledDeltaTime);
        }

        private static RectTransform NewRect(string name, RectTransform parent)
        {
            var rect = (RectTransform) new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void AddImage(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = CUE_COLOR;
            image.raycastTarget = false;
        }

        private static void AddLabel(RectTransform rect)
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                // The edge alone still shows where the zone starts
                return;
            }

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = "STAR POWER";
            text.fontSize = LABEL_FONT_SIZE;
            text.fontStyle = FontStyles.UpperCase;
            text.characterSpacing = 8f;
            text.alignment = TextAlignmentOptions.Bottom;
            text.color = CUE_COLOR;
            text.raycastTarget = false;
        }
    }
}
