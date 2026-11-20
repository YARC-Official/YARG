using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YARG.Input.Touch
{
    /// <summary>
    /// One on-screen touch button (a fret or the strum bar).
    ///
    /// Play mode: pointer-down injects <c>true</c> for its
    /// <see cref="YARG.Core.Input.GuitarAction"/>, pointer-up / pointer-exit /
    /// disable injects <c>false</c> (fret = hold; strum only reacts to the
    /// <c>true</c> edge, see YargFiveFretGuitarEngine). Multi-touch works
    /// because every button handles its own pointer events — no touch index
    /// is ever used, so chords with 2-3 fingers cannot swap frets.
    ///
    /// Edit mode: the button no longer injects input. Instead it can be
    /// dragged to a new position; the manager handles resize of the
    /// currently selected button.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class TouchFretButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Stable id from <see cref="TouchButtonIds"/>.</summary>
        public string ButtonId { get; private set; }

        /// <summary>True while this button is in layout-edit mode.</summary>
        public bool EditMode { get; set; }

        /// <summary>True while this button is the selected one in edit mode.</summary>
        public bool Selected { get; set; }

        public RectTransform RectTransform { get; private set; }

        private Image _image;
        private Color _baseColor;
        private Color _pressedColor;
        private Color _editColor = new Color(1f, 0.85f, 0.2f, 0.85f);

        private bool _pressed;
        private bool _dragging;

        /// <summary>Raised when the button starts/stops being held (play mode only).</summary>
        public event System.Action<TouchFretButton, bool> PressedChanged;

        /// <summary>Raised when the button is dragged in edit mode.</summary>
        public event System.Action<TouchFretButton, Vector2> Dragged;

        /// <summary>Raised when the button is tapped in edit mode (select it).</summary>
        public event System.Action<TouchFretButton> TappedInEditMode;

        public void Setup(string buttonId, Color color)
        {
            ButtonId = buttonId;
            _baseColor = color;
            _pressedColor = new Color(
                Mathf.Min(color.r * 1.4f, 1f),
                Mathf.Min(color.g * 1.4f, 1f),
                Mathf.Min(color.b * 1.4f, 1f),
                0.95f);
        }

        private void Awake()
        {
            RectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
        }

        private void OnDisable()
        {
            // Never leave a fret stuck held if the button is disabled mid-press.
            if (_pressed)
            {
                _pressed = false;
                PressedChanged?.Invoke(this, false);
            }

            _dragging = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (EditMode)
            {
                TappedInEditMode?.Invoke(this);
                return;
            }

            SetPressed(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (EditMode)
                return;

            SetPressed(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (EditMode)
                return;

            // A finger sliding off the button must release the fret,
            // otherwise it stays held in the engine's fret mask.
            SetPressed(false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!EditMode)
                return;

            _dragging = true;
            TappedInEditMode?.Invoke(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!EditMode || !_dragging)
                return;

            var canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                // Keep the button center inside the screen.
                var half = RectTransform.rect.size * 0.5f;
                localPoint.x = Mathf.Clamp(localPoint.x, -canvasRect.rect.width * 0.5f + half.x,
                    canvasRect.rect.width * 0.5f - half.x);
                localPoint.y = Mathf.Clamp(localPoint.y, -canvasRect.rect.height * 0.5f + half.y,
                    canvasRect.rect.height * 0.5f - half.y);
                Dragged?.Invoke(this, localPoint);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        private void SetPressed(bool pressed)
        {
            if (_pressed == pressed)
                return;

            _pressed = pressed;
            RefreshVisual();
            PressedChanged?.Invoke(this, pressed);
        }

        public void RefreshVisual()
        {
            if (_image == null)
                return;

            if (EditMode)
            {
                // Selected button: bright yellow; others: base color, more transparent.
                _image.color = Selected
                    ? _editColor
                    : new Color(_baseColor.r, _baseColor.g, _baseColor.b, 0.45f);
                return;
            }

            _image.color = _pressed ? _pressedColor : _baseColor;
        }
    }
}
