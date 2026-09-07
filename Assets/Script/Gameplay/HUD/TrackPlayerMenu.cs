using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public readonly struct PlayerMenuItem
    {
        public string Label { get; }
        public Action OnConfirm { get; }
        public Func<bool> IsEnabled { get; }

        public PlayerMenuItem(string label, Action onConfirm, Func<bool> isEnabled = null)
        {
            Label = label;
            OnConfirm = onConfirm;
            IsEnabled = isEnabled;
        }
    }

    public class TrackPlayerMenu : MonoBehaviour
    {
        private const float OPEN_SECONDS = 0.22f;
        private const float CLOSE_SECONDS = 0.22f;

        private const int MAX_VISIBLE_OPTIONS = 5;

        private const float OPTION_HEIGHT = 54f;
        private const float OPTION_SPACING = 0f;
        private const float TOP_PAD = 13f;
        private const float HEADING_HEIGHT = 60f;
        private const float BOTTOM_PAD = 44f;
        private const float BOTTOM_OFFSCREEN = 36f;
        private const float HIDDEN_OFFSET = 8f;
        private const float DISABLED_ALPHA = 0.35f;

        private const float HEADER_ICON_SPACING = 10f;

        public bool IsOpen { get; private set; }

        private RectTransform _panel;
        private RectTransform _parent;
        private Vector2 _screenBottom;
        private Canvas _canvas;
        private Vector2 _size;
        private float _trackScale;
        private float _progress;

        private RectTransform _optionTemplate;
        private RectTransform _heading;
        private TextMeshProUGUI _headingText;
        private RectTransform _icon;
        private AsyncOperationHandle<Sprite> _iconHandle;

        private IReadOnlyList<PlayerMenuItem> _items = Array.Empty<PlayerMenuItem>();
        private readonly List<GameObject> _rows = new();
        private readonly List<Image> _rowBackgrounds = new();
        private readonly List<TextMeshProUGUI> _rowLabels = new();
        private int _selectedIndex;
        private int _scrollOffset;
        private Color _normalColor;
        private Color _selectedColor;

        public void Initialize(YargPlayer player)
        {
            _panel = (RectTransform) transform;
            _parent = (RectTransform) transform.parent;
            _canvas = GetComponentInParent<Canvas>();
            _size = _panel.sizeDelta;

            _optionTemplate = (RectTransform) transform.Find("Option Template");
            _optionTemplate.anchorMin = new Vector2(0f, 0.5f);
            _optionTemplate.anchorMax = new Vector2(1f, 0.5f);
            _optionTemplate.sizeDelta = new Vector2(_optionTemplate.sizeDelta.x - _size.x,
                _optionTemplate.sizeDelta.y);

            _heading = (RectTransform) transform.Find("Heading");
            _headingText = _heading.GetComponent<TextMeshProUGUI>();
            _headingText.text = player.Profile.Name;

            _icon = (RectTransform) transform.Find("Instrument Icon");
            _iconHandle = Addressables.LoadAssetAsync<Sprite>(player.GetInstrumentSprite());
            _icon.GetComponent<Image>().sprite = _iconHandle.WaitForCompletion();
            _normalColor = _optionTemplate.GetComponent<Image>().color;
            _selectedColor = Color.white;

            StretchToPanel("Background");
            StretchToPanel("Border");
            StretchToPanel("Shadow");

            _optionTemplate.gameObject.SetActive(false);
        }

        private void StretchToPanel(string childName)
        {
            var child = (RectTransform) transform.Find(childName);
            if (child == null)
            {
                return;
            }

            var sizeOffset = child.sizeDelta - _size;
            var position = child.anchoredPosition;
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.sizeDelta = sizeOffset;
            child.anchoredPosition = position;
        }

        public void SetItems(IReadOnlyList<PlayerMenuItem> items)
        {
            _items = items;

            while (_rows.Count < _items.Count)
            {
                var row = Instantiate(_optionTemplate, transform);
                _rows.Add(row.gameObject);
                _rowBackgrounds.Add(row.GetComponent<Image>());
                _rowLabels.Add(row.GetComponentInChildren<TextMeshProUGUI>(true));
            }

            for (int i = 0; i < _items.Count; i++)
            {
                _rowLabels[i].text = _items[i].Label;
            }

            for (int i = _items.Count; i < _rows.Count; i++)
            {
                _rows[i].SetActive(false);
            }

            _selectedIndex = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (IsEnabled(_items[i]))
                {
                    _selectedIndex = i;
                    break;
                }
            }

            _scrollOffset = 0;
            EnsureSelectionVisible();

            LayoutMenu();
            RefreshSelection();
        }

        public void SelectNext()
        {
            MoveSelection(1);
        }

        public void SelectPrevious()
        {
            MoveSelection(-1);
        }

        public void ConfirmSelection()
        {
            if (!IsOpen || _items.Count == 0)
            {
                return;
            }

            var item = _items[_selectedIndex];
            if (!IsEnabled(item))
            {
                return;
            }

            item.OnConfirm?.Invoke();
        }

        private void MoveSelection(int direction)
        {
            if (!IsOpen || _items.Count == 0)
            {
                return;
            }

            int index = _selectedIndex;
            for (int i = 0; i < _items.Count; i++)
            {
                index = (index + direction + _items.Count) % _items.Count;
                if (IsEnabled(_items[index]))
                {
                    _selectedIndex = index;
                    EnsureSelectionVisible();
                    RefreshSelection();
                    return;
                }
            }
        }

        private static bool IsEnabled(PlayerMenuItem item) => item.IsEnabled?.Invoke() ?? true;

        private int VisibleOptionCount() => Math.Min(_items.Count, MAX_VISIBLE_OPTIONS);

        private void EnsureSelectionVisible()
        {
            int visibleCount = VisibleOptionCount();
            if (visibleCount == 0)
            {
                _scrollOffset = 0;
                return;
            }

            if (_selectedIndex < _scrollOffset)
            {
                _scrollOffset = _selectedIndex;
            }
            else if (_selectedIndex >= _scrollOffset + visibleCount)
            {
                _scrollOffset = _selectedIndex - visibleCount + 1;
            }
            _scrollOffset = Math.Max(_scrollOffset, 0);
        }

        private void RefreshSelection()
        {
            float half = _panel.sizeDelta.y / 2f;
            float optionsTop = half - TOP_PAD - HEADING_HEIGHT - OPTION_SPACING;
            int visibleCount = VisibleOptionCount();
            for (int i = 0; i < _items.Count; i++)
            {
                bool visible = i >= _scrollOffset && i < _scrollOffset + visibleCount;
                _rows[i].SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                int slot = i - _scrollOffset;
                var row = (RectTransform) _rows[i].transform;
                row.anchoredPosition = new Vector2(row.anchoredPosition.x,
                    optionsTop - OPTION_HEIGHT / 2f - slot * (OPTION_HEIGHT + OPTION_SPACING));
                _rowBackgrounds[i].color = i == _selectedIndex ? _selectedColor : _normalColor;
                _rowLabels[i].alpha = IsEnabled(_items[i]) ? 1f : DISABLED_ALPHA;
            }
        }

        private void LayoutMenu()
        {
            int count = Math.Max(VisibleOptionCount(), 1);
            float optionsHeight = count * OPTION_HEIGHT + (count - 1) * OPTION_SPACING;
            float panelHeight = TOP_PAD + HEADING_HEIGHT + OPTION_SPACING
                + optionsHeight + BOTTOM_PAD;
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, panelHeight);

            float half = panelHeight / 2f;
            float headingY = half - (TOP_PAD + HEADING_HEIGHT) / 2f;

            float iconWidth = _icon.sizeDelta.x;
            float maxTextWidth = _panel.sizeDelta.x - 40f - iconWidth - HEADER_ICON_SPACING;
            float textWidth = Mathf.Min(_headingText.GetPreferredValues().x, maxTextWidth);
            float totalHeaderWidth = iconWidth + HEADER_ICON_SPACING + textWidth;
            float headerStartX = -totalHeaderWidth / 2f;

            _icon.anchoredPosition = new Vector2(headerStartX + iconWidth / 2f, headingY);
            _heading.sizeDelta = new Vector2(textWidth, HEADING_HEIGHT);
            _heading.anchoredPosition = new Vector2(headerStartX + iconWidth + HEADER_ICON_SPACING + textWidth / 2f, headingY);
        }

        public void Show()
        {
            IsOpen = true;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void SetTrackBounds(Rect? bounds, Vector2? bottom, Vector2? top)
        {
            if (!bounds.HasValue || !bottom.HasValue || !top.HasValue || bounds.Value.width <= 0f)
            {
                _panel.gameObject.SetActive(false);
                return;
            }

            var camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var direction = top.Value - bottom.Value;
            var baseX = Mathf.Approximately(direction.y, 0f)
                ? bottom.Value.x
                : bottom.Value.x - bottom.Value.y * direction.x / direction.y;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent,
                new Vector2(baseX, 0f), camera, out _screenBottom);
            var width = bounds.Value.width / _canvas.scaleFactor;
            _trackScale = Mathf.Min(1f, width / _size.x);
            _panel.sizeDelta = new Vector2(Mathf.Max(_size.x, width), _panel.sizeDelta.y);
            _panel.gameObject.SetActive(IsOpen || _progress > 0f);
            UpdateAppearance();
        }

        private void Update()
        {
            var duration = IsOpen ? OPEN_SECONDS : CLOSE_SECONDS;
            _progress = Mathf.MoveTowards(_progress, IsOpen ? 1f : 0f, Time.unscaledDeltaTime / duration);
            UpdateAppearance();
            if (!IsOpen && _progress == 0f)
            {
                gameObject.SetActive(false);
            }
        }

        private void UpdateAppearance()
        {
            var height = _panel.rect.height * _trackScale;
            var slide = Mathf.SmoothStep(0f, 1f, _progress);
            var closedY = -height / 2f - HIDDEN_OFFSET * _trackScale;
            var openY = height / 2f - BOTTOM_OFFSCREEN * _trackScale;
            var position = _screenBottom + Vector2.up * Mathf.Lerp(closedY, openY, slide);
            _panel.localPosition = new Vector3(position.x, position.y, 0f);
            _panel.localScale = Vector3.one * _trackScale;
        }

        private void OnDestroy()
        {
            if (_iconHandle.IsValid())
            {
                Addressables.Release(_iconHandle);
            }
        }
    }
}
