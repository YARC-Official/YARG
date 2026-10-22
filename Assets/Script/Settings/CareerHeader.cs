using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Menu;

namespace YARG.Settings
{
    public class CareerHeader : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private static readonly Color ROW_DARK = new(0f, 0f, 13f / 255f, 1f);
        private static readonly Color ROW_LIGHT = new(1f / 255f, 8f / 255f, 22f / 255f, 1f);
        private static readonly Color HOVER_COLOR = new(0.28f, 0.55f, 0.65f, 0.15f);

        [SerializeField]
        private TextMeshProUGUI _text;
        [Space]
        [SerializeField]
        private Button _addButton;
        [SerializeField]
        private Button _editButton;
        [Space]
        [SerializeField]
        private Button _upButton;
        [SerializeField]
        private Button _downButton;
        [SerializeField]
        private Button _removeButton;

        private Action _onAdd;
        private Action _onEdit;
        private Action _onUp;
        private Action _onDown;
        private Action _onRemove;
        private TextMarquee _marquee;
        private GameObject _hoverBackground;
        private Action _showDetails;
        public bool IsSongRow { get; private set; }
        public Action MoveUp { get; private set; }
        public Action MoveDown { get; private set; }
        public Action Delete { get; private set; }
        public string DetailsTitle { get; private set; }
        public object RowIdentity { get; private set; }
        public bool IsEditorActionRow { get; private set; }

        public void SetDetailsTitle(string title) => DetailsTitle = title;
        public void SetText(string text)
        {
            if (_text != null)
            {
                _text.text = text;
            }
        }
        public void SetRowIdentity(object identity) => RowIdentity = identity;
        public void SetShowDetails(Action showDetails) => _showDetails = showDetails;
        public void ShowDetails() => _showDetails?.Invoke();

        public void SetRowActions(bool isSong, Action moveUp, Action moveDown, Action delete)
        {
            IsSongRow = isSong;
            MoveUp = moveUp;
            MoveDown = moveDown;
            Delete = delete;
        }

        public void SetButtonsVisible(bool visible)
        {
            SetButtonVisible(_addButton, visible);
            SetButtonVisible(_editButton, visible);
            SetButtonVisible(_upButton, visible);
            SetButtonVisible(_downButton, visible);
            SetButtonVisible(_removeButton, visible);
        }

        public void ConfigureAsStandaloneAddButton(string label, int leftPadding, bool alternate)
        {
            var buttonRoot = _addButton.transform.parent != null
                ? _addButton.transform.parent.gameObject
                : _addButton.gameObject;

            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(child.gameObject == buttonRoot);
            }

            if (TryGetComponent<Image>(out var background))
            {
                background.enabled = true;
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = alternate ? ROW_LIGHT : ROW_DARK;
            }

            if (TryGetComponent<RectTransform>(out var rect))
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 48f);
            }

            if (TryGetComponent<HorizontalLayoutGroup>(out var layout))
            {
                layout.padding.left = leftPadding;
                layout.childAlignment = TextAnchor.MiddleLeft;
            }

            var buttonLabel = buttonRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            if (buttonLabel != null)
            {
                buttonLabel.text = label;
            }

            IsEditorActionRow = true;
        }

        public void TightenStandaloneButtonOutline()
        {
            if (_addButton == null)
            {
                return;
            }

            var outline = _addButton.transform.Find("SelectedOutline") as RectTransform;
            if (outline != null)
            {
                // Keep the standard horizontal expansion, but tighten the
                // vertical expansion around this compact 32px-tall pill for
                // spacing that optically matches the standard buttons.
                outline.sizeDelta = new Vector2(12f, 8f);
            }
        }

        public void UseSettingRowStyle(bool alternate)
        {
            SetButtonsVisible(false);
            if (TryGetComponent<Image>(out var background))
            {
                background.enabled = true;
                // Career headers normally use a dark gradient sprite. Clear it
                // so these summary rows render the requested solid alternating
                // setting-row colors instead of multiplying them by the sprite.
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = alternate ? ROW_LIGHT : ROW_DARK;
            }

            _hoverBackground = CreateRowOverlay("Hover Background", HOVER_COLOR);

            // RuntimeNavigatable recognizes this name and toggles it as the
            // row's selection visual. Match the normal settings-row selection
            // color instead of using the very faint generic fallback overlay.
            var selectedBackground = CreateRowOverlay("Selected Background",
                new Color(0.035f, 0.3f, 0.42f, 0.9f));
            selectedBackground.transform.SetSiblingIndex(1);

            if (_text != null)
            {
                _text.fontStyle = FontStyles.Normal;
            }
        }

        private GameObject CreateRowOverlay(string objectName, Color color)
        {
            var overlay = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(LayoutElement));
            var rect = overlay.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetSiblingIndex(0);

            var image = overlay.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            overlay.GetComponent<LayoutElement>().ignoreLayout = true;
            overlay.SetActive(false);
            return overlay;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hoverBackground?.SetActive(true);

        public void OnPointerExit(PointerEventData eventData) => _hoverBackground?.SetActive(false);

        public void OnPointerDown(PointerEventData eventData) => ShowDetails();

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                // The visible label/icon is a sibling of the actual Button on
                // the shared round-button prefabs, so hide the whole control.
                var controlRoot = button.transform.parent != null
                    ? button.transform.parent.gameObject
                    : button.gameObject;
                controlRoot.SetActive(visible);
            }
        }

        public void Initialize(string text, Action onAdd, Action onEdit, Action onUp, Action onDown, Action onRemove,
            bool marquee = false)
        {
            if (_text != null)
            {
                _text.text = text;
                if (marquee)
                {
                    _marquee ??= new TextMarquee(_text);
                    _marquee.SetActive(true);
                }
                else
                {
                    _marquee?.SetActive(false);
                }
            }

            _onAdd = onAdd;
            _onEdit = onEdit;
            _onUp = onUp;
            _onDown = onDown;
            _onRemove = onRemove;

            SetupButton(_addButton, _onAdd);
            SetupButton(_editButton, _onEdit);
            SetupButton(_upButton, _onUp);
            SetupButton(_downButton, _onDown);
            SetupButton(_removeButton, _onRemove);
        }

        private void LateUpdate()
        {
            _marquee?.Update();
        }

        private static void SetupButton(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();

            if (action != null)
            {
                button.interactable = true;
                button.onClick.AddListener(() => action());
            }
            else
            {
                button.interactable = false;
                if (button.TryGetComponent<Image>(out var buttonImage))
                {
                    buttonImage.color = button.colors.disabledColor;
                }
            }
        }
    }
}
