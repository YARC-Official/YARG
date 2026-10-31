using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Localization;

namespace YARG.Menu.Navigation
{
    public sealed class SearchableDropdownMarker : MonoBehaviour, IPointerClickHandler
    {
        private const int MAX_VISIBLE_OPTIONS = 20;
        private const int REFINE_SEARCH_INDEX = -1;
        private static string SelectLabel => Localize.Key("Settings.Dropdown.Select");
        private static string RefineSearchLabel => Localize.Key("Settings.Dropdown.RefineSearch");
        private static readonly Color PLACEHOLDER_COLOR = new Color32(148, 158, 174, 255);

        private TMP_Dropdown _dropdown;
        private IReadOnlyList<string> _allLabels;
        private readonly List<int> _visibleIndices = new(MAX_VISIBLE_OPTIONS);
        private Action<int> _selectIndex;
        private int _currentIndex;
        private int _lastSelectionFrame = -1;
        private int _lastSelectedIndex = -1;
        private Color _captionColor;
        private FontStyles _captionStyle;
        private bool _captionStyleCaptured;
        private bool _searchOpen;

        public bool IsConfigured => _dropdown != null && _allLabels != null;

        public void Initialize(TMP_Dropdown dropdown, IReadOnlyList<string> labels,
            int currentIndex, Action<int> selectIndex)
        {
            _dropdown = dropdown;
            _allLabels = labels;
            _currentIndex = currentIndex;
            _selectIndex = selectIndex;
            if (!_captionStyleCaptured && dropdown.captionText != null)
            {
                _captionColor = dropdown.captionText.color;
                _captionStyle = dropdown.captionText.fontStyle;
                _captionStyleCaptured = true;
            }
            ApplyFilter(string.Empty, null);
        }

        public void ApplyFilter(string query, Toggle[] toggles)
        {
            if (!IsConfigured) return;

            _visibleIndices.Clear();
            bool hasQuery = !string.IsNullOrEmpty(query);
            if (!hasQuery && _currentIndex >= 0 && _currentIndex < _allLabels.Count &&
                (!_searchOpen || !string.IsNullOrEmpty(GetSourceLabel(_currentIndex))))
            {
                _visibleIndices.Add(_currentIndex);
            }

            bool hasMoreMatches = false;
            for (int i = 0; i < _allLabels.Count; i++)
            {
                if (i == _currentIndex && !hasQuery) continue;
                if (_searchOpen && string.IsNullOrEmpty(GetSourceLabel(i))) continue;
                if (!hasQuery || _allLabels[i].IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (_visibleIndices.Count < MAX_VISIBLE_OPTIONS)
                    {
                        _visibleIndices.Add(i);
                    }
                    else
                    {
                        hasMoreMatches = true;
                        break;
                    }
                }
            }

            // Keep the list at the established 20-row maximum. When additional
            // matches exist, reserve the final row for a non-selectable hint.
            if (hasMoreMatches)
            {
                if (_visibleIndices.Count == MAX_VISIBLE_OPTIONS)
                {
                    _visibleIndices.RemoveAt(MAX_VISIBLE_OPTIONS - 1);
                }
                _visibleIndices.Add(REFINE_SEARCH_INDEX);
            }

            // TMP fixes the number of list toggles when it opens. Keep no more than
            // 20 reusable slots and hide unused ones as the query changes.
            int slotCount = Mathf.Min(MAX_VISIBLE_OPTIONS, Mathf.Max(_allLabels.Count, 1));
            _dropdown.options.Clear();
            for (int slot = 0; slot < slotCount; slot++)
            {
                string label = slot < _visibleIndices.Count
                    ? GetDisplayLabel(_visibleIndices[slot])
                    : string.Empty;
                _dropdown.options.Add(new TMP_Dropdown.OptionData(label));
            }

            _dropdown.SetValueWithoutNotify(0);
            // A freshly-instantiated TMP dropdown already defaults to index 0,
            // so SetValueWithoutNotify(0) exits without rebuilding its caption.
            // The option data was just replaced; refresh the displayed value explicitly.
            _dropdown.RefreshShownValue();
            ShowCurrentCaption();
            if (toggles == null) return;

            for (int slot = 0; slot < toggles.Length; slot++)
            {
                bool visible = slot < _visibleIndices.Count;
                toggles[slot].gameObject.SetActive(visible);
                if (visible)
                {
                    int sourceIndex = _visibleIndices[slot];
                    bool isHelper = sourceIndex == REFINE_SEARCH_INDEX;
                    var label = toggles[slot].GetComponentInChildren<TMP_Text>(true);

                    if (!toggles[slot].TryGetComponent(out SearchableDropdownOptionClick click))
                    {
                        click = toggles[slot].gameObject.AddComponent<SearchableDropdownOptionClick>();
                    }
                    click.Configure(this, sourceIndex, label, GetDisplayLabel(sourceIndex),
                        isHelper || string.IsNullOrEmpty(GetSourceLabel(sourceIndex)), PLACEHOLDER_COLOR);
                    toggles[slot].interactable = !isHelper;
                }
            }
        }

        private string GetSourceLabel(int sourceIndex) =>
            sourceIndex >= 0 && sourceIndex < _allLabels.Count
                ? _allLabels[sourceIndex]
                : string.Empty;

        private string GetDisplayLabel(int sourceIndex)
        {
            if (sourceIndex == REFINE_SEARCH_INDEX) return RefineSearchLabel;
            string label = GetSourceLabel(sourceIndex);
            return string.IsNullOrEmpty(label) ? SelectLabel : label;
        }

        public void SelectVisibleIndex(int visibleIndex)
        {
            if (visibleIndex < 0 || visibleIndex >= _visibleIndices.Count) return;
            SelectSourceIndex(_visibleIndices[visibleIndex]);
        }

        public void SelectSourceIndex(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= _allLabels.Count) return;
            if (_lastSelectionFrame == Time.frameCount && _lastSelectedIndex == sourceIndex) return;

            _lastSelectionFrame = Time.frameCount;
            _lastSelectedIndex = sourceIndex;
            _currentIndex = sourceIndex;
            _selectIndex?.Invoke(sourceIndex);
        }

        public string CurrentLabel => _currentIndex >= 0 && _currentIndex < _allLabels.Count
            ? _allLabels[_currentIndex]
            : string.Empty;

        public int VisibleCount => _visibleIndices.Count;

        public void BeginSearch()
        {
            _searchOpen = true;
        }

        public void EndSearch()
        {
            _searchOpen = false;
            ShowCurrentCaption();
        }

        public void ShowCurrentCaption()
        {
            if (_dropdown?.captionText == null) return;

            bool isBlank = string.IsNullOrEmpty(CurrentLabel);
            if (_searchOpen && isBlank)
            {
                ShowEmptySearchCaption(true);
                return;
            }
            _dropdown.captionText.text = isBlank ? SelectLabel : CurrentLabel;
            SetCaptionHelperStyle(isBlank);
        }

        public void ShowEmptySearchCaption(bool caretVisible)
        {
            if (_dropdown?.captionText == null) return;
            _dropdown.captionText.text = caretVisible ? "|" : string.Empty;
            SetCaptionHelperStyle(false);
        }

        public void ShowSearchCaption(string query)
        {
            if (_dropdown?.captionText == null) return;
            _dropdown.captionText.text = Localize.KeyFormat("Settings.Dropdown.Search", query);
            SetCaptionHelperStyle(false);
        }

        private void SetCaptionHelperStyle(bool enabled)
        {
            if (!_captionStyleCaptured || _dropdown?.captionText == null) return;
            _dropdown.captionText.color = enabled ? PLACEHOLDER_COLOR : _captionColor;
            _dropdown.captionText.fontStyle = enabled
                ? _captionStyle | FontStyles.Italic
                : _captionStyle;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // TMP opens its native list during this same pointer event. Attach
            // typeahead on the next frame, after "Dropdown List" exists.
            UniTask.NextFrame().ContinueWith(() =>
            {
                if (_dropdown != null && _dropdown.transform.Find("Dropdown List") != null)
                {
                    RuntimeNavigatable.OpenDropdownList(_dropdown);
                }
            }).Forget();
        }
    }

    public sealed class DropdownTypeaheadSearch : MonoBehaviour
    {
        public Action FilterChanged;
        public Action ConfirmPressed;
        public Action CancelPressed;

        private TMP_Dropdown _dropdown;
        private Toggle[] _toggles;
        private SearchableDropdownMarker _marker;
        private string _query = string.Empty;
        private RectTransform _listRect;
        private ScrollRect _scrollRect;
        private float _maximumHeight;
        private float _itemHeight;
        private float _verticalPadding;
        private Vector2 _contentTopPosition;
        private bool _lockContentToTop;
        private float _fullContentHeight;
        private Vector2[] _originalRowPositions;
        private bool _caretVisible = true;
        private float _nextCaretBlink;

        public void Initialize(TMP_Dropdown dropdown, Toggle[] toggles,
            SearchableDropdownMarker marker)
        {
            _dropdown = dropdown;
            _toggles = toggles;
            _marker = marker;
            _marker.BeginSearch();
            _listRect = transform as RectTransform;
            _scrollRect = GetComponent<ScrollRect>();

            if (_listRect != null)
            {
                _maximumHeight = _listRect.rect.height;
                _itemHeight = toggles.Length > 0 && toggles[0].transform is RectTransform itemRect
                    ? itemRect.rect.height
                    : 0f;

                if (_scrollRect?.content != null && _itemHeight > 0f)
                {
                    _contentTopPosition = _scrollRect.content.anchoredPosition;
                    _fullContentHeight = _scrollRect.content.rect.height;
                    _originalRowPositions = new Vector2[toggles.Length];
                    for (int i = 0; i < toggles.Length; i++)
                    {
                        _originalRowPositions[i] =
                            ((RectTransform) toggles[i].transform).anchoredPosition;
                    }
                    _verticalPadding = Mathf.Max(0f,
                        _scrollRect.content.rect.height - _itemHeight * toggles.Length);
                }
            }

            _marker.ApplyFilter(string.Empty, _toggles);
            if (string.IsNullOrEmpty(_marker.CurrentLabel))
                _marker.ShowEmptySearchCaption(true);
            else
                _marker.ShowCurrentCaption();
            _nextCaretBlink = Time.unscaledTime + 0.5f;
            ResizeToResults();

            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput += OnTextInput;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true)
            {
                CancelPressed?.Invoke();
                return;
            }

            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                ConfirmPressed?.Invoke();
                return;
            }

            if (keyboard?.backspaceKey.wasPressedThisFrame == true && _query.Length > 0)
            {
                _query = _query[..^1];
                ApplyFilter();
            }

            if (_query.Length == 0 && string.IsNullOrEmpty(_marker?.CurrentLabel) &&
                Time.unscaledTime >= _nextCaretBlink)
            {
                _caretVisible = !_caretVisible;
                _nextCaretBlink = Time.unscaledTime + 0.5f;
                _marker?.ShowEmptySearchCaption(_caretVisible);
            }
        }

        private void LateUpdate()
        {
            if (_lockContentToTop && _scrollRect?.content != null)
            {
                _scrollRect.content.anchoredPosition = _contentTopPosition;
            }
        }

        private void OnTextInput(char character)
        {
            if (!char.IsControl(character))
            {
                _query += character;
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            if (_dropdown == null || _toggles == null)
            {
                return;
            }

            _marker?.ApplyFilter(_query, _toggles);

            if (string.IsNullOrEmpty(_query))
            {
                if (string.IsNullOrEmpty(_marker?.CurrentLabel))
                {
                    _caretVisible = true;
                    _nextCaretBlink = Time.unscaledTime + 0.5f;
                    _marker?.ShowEmptySearchCaption(true);
                }
                else
                {
                    _marker?.ShowCurrentCaption();
                }
            }
            else
                _marker?.ShowSearchCaption(_query);

            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            ResizeToResults();
            FilterChanged?.Invoke();
        }

        private void ResizeToResults()
        {
            if (_listRect == null || _itemHeight <= 0f || _maximumHeight <= 0f)
            {
                return;
            }

            int resultCount = Mathf.Max(1, _marker?.VisibleCount ?? 0);
            float contentHeight = _verticalPadding + _itemHeight * resultCount;
            float targetHeight = Mathf.Min(_maximumHeight, contentHeight);

            // TMP sizes its Content rect for every option when the list opens.
            // Deactivating filtered rows does not reduce that explicit height,
            // so update it alongside the outer list to remove blank scroll space.
            if (_scrollRect?.content != null)
            {
                var content = _scrollRect.content;
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
                content.anchoredPosition = _contentTopPosition;

                // TMP positions its generated rows relative to the bottom of the
                // original full-height content. Move those rows by the same delta
                // as the content height so they remain pinned to its top edge.
                if (_originalRowPositions != null)
                {
                    float heightDelta = contentHeight - _fullContentHeight;
                    for (int i = 0; i < _toggles.Length && i < _originalRowPositions.Length; i++)
                    {
                        var row = (RectTransform) _toggles[i].transform;
                        var position = _originalRowPositions[i];
                        position.y += heightDelta;
                        row.anchoredPosition = position;
                    }
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                _scrollRect.StopMovement();

                _lockContentToTop = contentHeight <= _maximumHeight + 0.5f;
                _scrollRect.vertical = !_lockContentToTop;
                if (!_lockContentToTop)
                {
                    _scrollRect.verticalNormalizedPosition = 1f;
                }
            }

            _listRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

            if (_scrollRect?.verticalScrollbar != null)
            {
                _scrollRect.verticalScrollbar.gameObject.SetActive(contentHeight > _maximumHeight + 0.5f);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_listRect);
        }

        private void OnDestroy()
        {
            if (Keyboard.current != null)
            {
                Keyboard.current.onTextInput -= OnTextInput;
            }

            _marker?.EndSearch();
        }
    }

    /// <summary>
    /// Preserves the result represented by a recycled option row so clicking the
    /// first result still selects it even when TMP considers its slot unchanged.
    /// </summary>
    public sealed class SearchableDropdownOptionClick : MonoBehaviour, IPointerClickHandler
    {
        private SearchableDropdownMarker _marker;
        private int _sourceIndex;
        private TMP_Text _label;
        private Color _defaultColor;
        private FontStyles _defaultStyle;
        private bool _styleCaptured;

        public void Configure(SearchableDropdownMarker marker, int sourceIndex, TMP_Text label,
            string displayLabel, bool helperStyle, Color helperColor)
        {
            _marker = marker;
            _sourceIndex = sourceIndex;
            if (!_styleCaptured && label != null)
            {
                _label = label;
                _defaultColor = label.color;
                _defaultStyle = label.fontStyle;
                _styleCaptured = true;
            }

            if (_label != null)
            {
                _label.text = displayLabel;
                _label.color = helperStyle ? helperColor : _defaultColor;
                _label.fontStyle = helperStyle
                    ? _defaultStyle | FontStyles.Italic
                    : _defaultStyle;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _marker?.SelectSourceIndex(_sourceIndex);
        }
    }

    /// <summary>
    /// Pops the dropdown-list navigation scheme when the TMP list object is
    /// destroyed (TMP destroys it on every close path). Added at runtime by
    /// <see cref="RuntimeNavigatable.OpenDropdownList"/>.
    /// </summary>
}
