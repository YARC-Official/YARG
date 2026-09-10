using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public readonly struct PlayerMenuItem
    {
        public string     Label     { get; }
        public Action     OnConfirm { get; }
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
        private const float OPEN_SECONDS        = 0.22f;
        private const float CLOSE_SECONDS       = 0.22f;
        private const float OPEN_BOTTOM_OFFSET  = 36f;
        private const float CLOSED_EXTRA_OFFSET = 8f;
        private const float DISABLED_ALPHA      = 0.35f;
        private const float HEADER_MARGIN       = 40f;

        [SerializeField]
        private TextMeshProUGUI _headingText;

        [SerializeField]
        private Image _icon;

        [SerializeField]
        private Color _normalColor = new(0f, 0.23f, 0.36f, 0f);

        [SerializeField]
        private Color _selectedColor = Color.white;

        [SerializeField]
        private MenuSlot[] _slots;

        private Canvas                        _canvas;
        private float                         _defaultWidth;
        private float                         _openY;
        private float                         _closedY;
        private float                         _lastBaseX = -1f;
        private float                         _lastTrackWidth = -1f;
        private RectTransform                 _panel;
        private YargPlayer                    _player;
        private AsyncOperationHandle<Sprite>  _iconHandle;
        private IReadOnlyList<PlayerMenuItem> _items = Array.Empty<PlayerMenuItem>();
        private int                           _selectedIndex;
        private int                           _scrollOffset;

        public bool IsOpen { get; private set; }

        private Camera CanvasCamera    => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        private int    VisibleRowCount => _slots.Length;

        private void Awake()
        {
            _panel = (RectTransform) transform;
            _canvas = GetComponentInParent<Canvas>();
            _defaultWidth = _panel.sizeDelta.x;
        }

        private void OnDisable()
        {
            _panel.DOKill();
            IsOpen = false;
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.MenuInput -= OnMenuInput;
            }

            _panel.DOKill();
            if (_iconHandle.IsValid())
            {
                Addressables.Release(_iconHandle);
            }
        }

        public void Initialize(YargPlayer player)
        {
            _player = player;
            _player.MenuInput += OnMenuInput;
            _headingText.text = player.Profile.Name;

            _iconHandle = Addressables.LoadAssetAsync<Sprite>(player.GetInstrumentSprite());
            _icon.sprite = _iconHandle.WaitForCompletion();
        }

        public void SetItems(IReadOnlyList<PlayerMenuItem> items)
        {
            _items = items;
            _selectedIndex = _items.FirstUsableIndex();
            _scrollOffset = 0;
            EnsureSelectionVisible();
            RefreshSelection();
            LayoutHeading();
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            _panel.localPosition = new Vector3(_panel.localPosition.x, _closedY, 0f);
            gameObject.SetActive(true);
            SlideTo(targetY: _openY, duration: OPEN_SECONDS, ease: Ease.OutCubic);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            SlideTo(targetY: _closedY, duration: CLOSE_SECONDS, ease: Ease.InCubic)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private Tweener SlideTo(float targetY, float duration, Ease ease)
        {
            _panel.DOKill();
            return _panel.DOLocalMoveY(endValue: targetY, duration: duration)
                .SetEase(ease)
                .SetUpdate(isIndependentUpdate: true);
        }

        public void SetLayout(float baseScreenX, float trackWidth)
        {
            if (Mathf.Approximately(baseScreenX, _lastBaseX) && Mathf.Approximately(trackWidth, _lastTrackWidth))
            {
                return;
            }

            _lastBaseX = baseScreenX;
            _lastTrackWidth = trackWidth;

            var parent = (RectTransform) transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect: parent,
                screenPoint: new Vector2(baseScreenX, 0f),
                cam: CanvasCamera,
                localPoint: out var screenBottom);

            var trackScale = Mathf.Min(1f, (trackWidth / _canvas.scaleFactor) / _defaultWidth);
            var panelHalfHeight = (_panel.rect.height * trackScale) * 0.5f;

            _closedY = screenBottom.y - panelHalfHeight - (CLOSED_EXTRA_OFFSET * trackScale);
            _openY = screenBottom.y + panelHalfHeight - (OPEN_BOTTOM_OFFSET * trackScale);

            var width = trackWidth / _canvas.scaleFactor;
            _panel.sizeDelta = new Vector2(x: Mathf.Max(_defaultWidth, width), y: _panel.sizeDelta.y);
            _panel.localScale = Vector3.one * trackScale;

            var currentY = DOTween.IsTweening(_panel) ? _panel.localPosition.y : (IsOpen ? _openY : _closedY);
            _panel.localPosition = new Vector3(x: screenBottom.x, y: currentY, z: 0f);

            LayoutHeading();
        }

        public void HideImmediate()
        {
            IsOpen = false;
            _panel.DOKill();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (IsOpen)
            {
                RefreshSelection();
            }
        }

        private void ConfirmSelection()
        {
            var item = _items[_selectedIndex];
            if (item.IsUsable())
            {
                item.OnConfirm?.Invoke();
            }
            else
            {
                RefreshSelection();
            }
        }

        private void MoveSelection(int direction)
        {
            var nextIndex = _items.NextUsableIndex(_selectedIndex, direction);
            if (nextIndex.HasValue)
            {
                _selectedIndex = nextIndex.Value;
                EnsureSelectionVisible();
                RefreshSelection();
            }
        }

        private void OnMenuInput(YargPlayer _, ref GameInput input)
        {
            if (!IsOpen || !input.Button)
            {
                return;
            }

            var action = ((MenuAction) input.Action).AdjustForLeftyFlip(_player.Profile.LeftyFlip);
            switch (action)
            {
                case MenuAction.Up:
                    MoveSelection(direction: -1);
                    break;
                case MenuAction.Down:
                    MoveSelection(direction: 1);
                    break;
                case MenuAction.Green:
                    ConfirmSelection();
                    break;
                case MenuAction.Red:
                case MenuAction.Start:
                    Close();
                    break;
            }
        }

        private void EnsureSelectionVisible()
        {
            if (_selectedIndex < _scrollOffset)
            {
                _scrollOffset = _selectedIndex;
            }
            else if (_selectedIndex >= _scrollOffset + VisibleRowCount)
            {
                _scrollOffset = _selectedIndex - VisibleRowCount + 1;
            }
        }

        private void RefreshSelection()
        {
            for (var i = 0; i < VisibleRowCount; i++)
            {
                var itemIndex = _scrollOffset + i;
                if (itemIndex < _items.Count)
                {
                    var item = _items[itemIndex];
                    var isSelected = itemIndex == _selectedIndex;
                    var isEnabled = item.IsUsable();
                    var color = isSelected ? _selectedColor : _normalColor;
                    var alpha = isEnabled ? 1f : DISABLED_ALPHA;
                    _slots[i].Bind(item.Label, color, alpha);
                }
                else
                {
                    _slots[i].Hide();
                }
            }
        }

        private void LayoutHeading()
        {
            var iconLayout = _icon.GetComponent<LayoutElement>();
            var iconWidth = iconLayout != null && iconLayout.preferredWidth > 0f
                ? iconLayout.preferredWidth
                : _icon.rectTransform.sizeDelta.x;
            var layoutGroup = _headingText.GetComponentInParent<HorizontalLayoutGroup>(includeInactive: true);
            var spacing = layoutGroup != null ? layoutGroup.spacing : 0f;
            var maxTextWidth = _panel.sizeDelta.x - HEADER_MARGIN - iconWidth - spacing;

            _headingText.enableAutoSizing = false;
            _headingText.fontSize = _headingText.fontSizeMax;

            var preferredWidth = _headingText.preferredWidth;
            if (_headingText.TryGetComponent<LayoutElement>(out var layoutElement))
            {
                layoutElement.preferredWidth = Mathf.Min(preferredWidth, maxTextWidth);
            }

            _headingText.enableAutoSizing = preferredWidth > maxTextWidth;
        }

        [Serializable]
        public struct MenuSlot
        {
            [SerializeField]
            private GameObject _root;

            [SerializeField]
            private Image _background;

            [SerializeField]
            private TextMeshProUGUI _label;

            public void Bind(string label, Color backgroundColor, float textAlpha)
            {
                _label.text = label;
                _label.alpha = textAlpha;
                _background.color = backgroundColor;
                _root.SetActive(true);
            }

            public void Hide() => _root.SetActive(false);
        }
    }

    internal static class TrackPlayerMenuExtensions
    {
        public static MenuAction AdjustForLeftyFlip(this MenuAction action, bool isLeftyFlip)
        {
            if (!isLeftyFlip)
            {
                return action;
            }

            return action switch
            {
                MenuAction.Up    => MenuAction.Down,
                MenuAction.Down  => MenuAction.Up,
                MenuAction.Left  => MenuAction.Right,
                MenuAction.Right => MenuAction.Left,
                _                => action
            };
        }

        public static bool IsUsable(this PlayerMenuItem item) => item.IsEnabled?.Invoke() ?? true;

        public static bool HasUsable(this IReadOnlyList<PlayerMenuItem> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].IsUsable())
                {
                    return true;
                }
            }

            return false;
        }

        public static int FirstUsableIndex(this IReadOnlyList<PlayerMenuItem> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].IsUsable())
                {
                    return i;
                }
            }

            return 0;
        }

        public static int? NextUsableIndex(this IReadOnlyList<PlayerMenuItem> items, int startIndex, int direction)
        {
            var count = items.Count;
            var index = startIndex;
            for (var i = 0; i < count; i++)
            {
                index = (index + direction + count) % count;
                if (items[index].IsUsable())
                {
                    return index;
                }
            }

            return null;
        }
    }
}
