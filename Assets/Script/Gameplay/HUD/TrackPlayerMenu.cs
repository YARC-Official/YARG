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

        public PlayerMenuItem(string label, Action onConfirm, Func<bool> isEnabled = null) =>
            (Label, OnConfirm, IsEnabled) = (label, onConfirm, isEnabled);
    }

    public class TrackPlayerMenu : MonoBehaviour
    {
        private const float OPEN_SECONDS         = 0.22f;
        private const float CLOSE_SECONDS        = 0.22f;
        private const float BOTTOM_OFFSCREEN     = 36f;
        private const float HIDDEN_OFFSET        = 8f;
        private const float DISABLED_ALPHA       = 0.35f;
        private const float HEADER_MARGIN          = 40f;

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
        private LayoutElement                 _headingLayout;
        private HorizontalLayoutGroup         _headingLayoutGroup;
        private AsyncOperationHandle<Sprite>  _iconHandle;
        private IReadOnlyList<PlayerMenuItem> _items = Array.Empty<PlayerMenuItem>();
        private float                         _lastBaseX = -1f;
        private float                         _lastTrackWidth = -1f;
        private RectTransform                 _panel;
        private RectTransform                 _parent;
        private YargPlayer                    _player;
        private Vector2                       _screenBottom;
        private int                           _scrollOffset;
        private int                           _selectedIndex;

        public bool IsOpen { get; private set; }

        private bool   HasItems        => _items.Count > 0;
        private bool   CanInteract     => IsOpen && HasItems;
        private Camera CanvasCamera    => _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        private int    SlotCount       => _slots.Length;
        private float  TrackScale      => _lastTrackWidth > 0f ? Mathf.Min(1f, (_lastTrackWidth / _canvas.scaleFactor) / _defaultWidth) : 1f;
        private float  PanelHalfHeight => (_panel.rect.height * TrackScale) * 0.5f;
        private float  ClosedY         => _screenBottom.y - PanelHalfHeight - (HIDDEN_OFFSET * TrackScale);
        private float  OpenY           => _screenBottom.y + PanelHalfHeight - (BOTTOM_OFFSCREEN * TrackScale);
        private float  TargetY         => IsOpen ? OpenY : ClosedY;

        private void Awake()
        {
            _panel = (RectTransform) transform;
            _parent = (RectTransform) transform.parent;
            _canvas = GetComponentInParent<Canvas>();
            _defaultWidth = _panel.sizeDelta.x;
            _headingLayout = _headingText.GetComponent<LayoutElement>();
            _headingLayoutGroup = _headingText.GetComponentInParent<HorizontalLayoutGroup>();
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
            IsOpen = true;
            gameObject.SetActive(true);
            SlideTo(targetY: OpenY, duration: OPEN_SECONDS, ease: Ease.OutCubic);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            SlideTo(targetY: ClosedY, duration: CLOSE_SECONDS, ease: Ease.InCubic)
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

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect: _parent,
                screenPoint: new Vector2(baseScreenX, 0f),
                cam: CanvasCamera,
                localPoint: out _screenBottom);

            var width = trackWidth / _canvas.scaleFactor;
            _panel.sizeDelta = new Vector2(x: Mathf.Max(_defaultWidth, width), y: _panel.sizeDelta.y);
            _panel.localScale = Vector3.one * TrackScale;

            var currentY = DOTween.IsTweening(_panel) ? _panel.localPosition.y : TargetY;
            _panel.localPosition = new Vector3(x: _screenBottom.x, y: currentY, z: 0f);
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
            if (!CanInteract)
            {
                return;
            }

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
            if (!CanInteract)
            {
                return;
            }

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
            if (!HasItems)
            {
                _scrollOffset = 0;
                return;
            }

            if (_selectedIndex < _scrollOffset)
            {
                _scrollOffset = _selectedIndex;
            }
            else if (_selectedIndex >= _scrollOffset + SlotCount)
            {
                _scrollOffset = _selectedIndex - SlotCount + 1;
            }
        }

        private void RefreshSelection()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                var itemIndex = _scrollOffset + i;
                if (itemIndex < _items.Count)
                {
                    var item = _items[itemIndex];
                    var isEnabled = item.IsUsable();
                    _slots[i].Bind(
                        item: item,
                        isSelected: itemIndex == _selectedIndex,
                        isEnabled: isEnabled,
                        selectedColor: _selectedColor,
                        normalColor: _normalColor,
                        disabledAlpha: DISABLED_ALPHA);
                }
                else
                {
                    _slots[i].Hide();
                }
            }
        }

        private void LayoutHeading()
        {
            var iconWidth = _icon.rectTransform.sizeDelta.x;
            var maxTextWidth = _panel.sizeDelta.x - HEADER_MARGIN - iconWidth - _headingLayoutGroup.spacing;

            _headingText.enableAutoSizing = false;
            _headingText.fontSize = _headingText.fontSizeMax;

            var preferredWidth = _headingText.preferredWidth;
            _headingLayout.preferredWidth = Mathf.Min(preferredWidth, maxTextWidth);
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

            public void Bind(
                PlayerMenuItem item,
                bool isSelected,
                bool isEnabled,
                Color selectedColor,
                Color normalColor,
                float disabledAlpha)
            {
                _label.text = item.Label;
                _label.alpha = isEnabled ? 1f : disabledAlpha;
                _background.color = isSelected ? selectedColor : normalColor;
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
                MenuAction.Up   => MenuAction.Down,
                MenuAction.Down => MenuAction.Up,
                _               => action
            };
        }

        public static bool IsUsable(this PlayerMenuItem item) => item.IsEnabled?.Invoke() ?? true;

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
