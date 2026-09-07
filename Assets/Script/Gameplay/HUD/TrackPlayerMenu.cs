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
        private const float HINT_HEIGHT = 28f;
        private const float SELECTED_BRIGHTNESS = 1.8f;
        private const float DISABLED_ALPHA = 0.35f;

        private static readonly string[] HINT_NAMES =
        {
            "Close Hint", "Accept Hint", "Red Button", "Green Button"
        };

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
        private AsyncOperationHandle<Sprite> _iconHandle;
        private readonly List<RectTransform> _hintObjects = new();
        private float _hintReferenceY;

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
            _heading.GetComponent<TextMeshProUGUI>().text = player.Profile.Name;
            _iconHandle = Addressables.LoadAssetAsync<Sprite>(player.GetInstrumentSprite());
            transform.Find("Instrument Icon").GetComponent<Image>().sprite = _iconHandle.WaitForCompletion();
            foreach (var hintName in HINT_NAMES)
            {
                _hintObjects.Add((RectTransform) transform.Find(hintName));
            }
            _hintReferenceY = ((RectTransform) transform.Find("Close Hint")).anchoredPosition.y;

            _normalColor = _optionTemplate.GetComponent<Image>().color;
            _selectedColor = _normalColor * SELECTED_BRIGHTNESS;
            _selectedColor.a = 1f;

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

            foreach (var row in _rows)
            {
                Destroy(row);
            }
            _rows.Clear();
            _rowBackgrounds.Clear();
            _rowLabels.Clear();

            foreach (var item in _items)
            {
                var row = Instantiate(_optionTemplate, transform);
                row.gameObject.SetActive(true);
                var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
                label.text = item.Label;
                _rows.Add(row.gameObject);
                _rowBackgrounds.Add(row.GetComponent<Image>());
                _rowLabels.Add(label);
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
            for (int i = 0; i < _rows.Count; i++)
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
            int count = MAX_VISIBLE_OPTIONS;
            float optionsHeight = count * OPTION_HEIGHT + (count - 1) * OPTION_SPACING;
            float panelHeight = TOP_PAD + HEADING_HEIGHT + OPTION_SPACING
                + optionsHeight + OPTION_SPACING + HINT_HEIGHT + TOP_PAD;
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, panelHeight);

            float half = panelHeight / 2f;
            _heading.anchoredPosition = new Vector2(_heading.anchoredPosition.x,
                half - TOP_PAD - HEADING_HEIGHT / 2f);
            var icon = (RectTransform) transform.Find("Instrument Icon");
            icon.anchoredPosition = new Vector2(icon.anchoredPosition.x, _heading.anchoredPosition.y);

            float optionsTop = half - TOP_PAD - HEADING_HEIGHT - OPTION_SPACING;
            float hintsY = optionsTop - optionsHeight - OPTION_SPACING - HINT_HEIGHT / 2f;
            float delta = hintsY - _hintReferenceY;
            foreach (var hint in _hintObjects)
            {
                hint.anchoredPosition = new Vector2(hint.anchoredPosition.x, hint.anchoredPosition.y + delta);
            }
            _hintReferenceY = hintsY;
        }

        public void Show()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void SetTrackBounds(Rect? bounds, Vector2? bottom, Vector2? top)
        {
            if (!bounds.HasValue || !bottom.HasValue || !top.HasValue)
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
            var position = _screenBottom + Vector2.up * Mathf.Lerp(-height / 2f - 8f, height / 2f - 8f, slide);
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
