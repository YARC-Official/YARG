using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Settings;

namespace YARG.Menu.Navigation
{
    /// <summary>
    /// A <see cref="NavigatableBehaviour"/> that can be added to a GameObject at
    /// runtime. The base class requires a serialized "selected visual" GameObject
    /// that only prefab-authored navigatables can provide (its Awake throws on
    /// null); this variant instead drives a translucent overlay created by
    /// <see cref="Attach"/>, and invokes a callback on confirm.
    /// </summary>
    public class RuntimeNavigatable : NavigatableBehaviour
    {
        /// <summary>
        /// The selected-text yellow used by the main menu's
        /// NavigationTextColorizer (serialized in MainMenu.prefab).
        /// </summary>
        public static readonly Color SelectedTextColor = new(1f, 0.83137256f, 0.22745098f, 1f);

        /// <summary>
        /// Desaturated selection color for selected but inert controls.
        /// </summary>
        public static readonly Color DimmedSelectionGrey = Desaturated(SelectedTextColor * 0.7f);

        private static Color Desaturated(Color color)
        {
            Color.RGBToHSV(color, out _, out _, out float v);
            return Color.HSVToRGB(0f, 0f, v);
        }

        private static Sprite _selectionGradient;
        private static Sprite _buttonOutline;

        private static Sprite GetSelectionGradient()
        {
            if (_selectionGradient == null)
            {
                _selectionGradient = UnityEngine.AddressableAssets.Addressables
                    .LoadAssetAsync<Sprite>("SelectionGradient")
                    .WaitForCompletion();
            }
            return _selectionGradient;
        }

        private static Sprite GetButtonOutline()
        {
            if (_buttonOutline == null)
            {
                _buttonOutline = UnityEngine.AddressableAssets.Addressables
                    .LoadAssetAsync<Sprite>("ButtonOutline")
                    .WaitForCompletion();
            }
            return _buttonOutline;
        }

        /// <summary>
        /// Creates an inactive selection outline around <paramref name="parent"/>
        /// in the given color, using RoundButton's authored "Selection Outline"
        /// sprite and oversize.
        /// </summary>
        public static GameObject CreateSelectionOutline(Transform parent, Color color)
        {
            var outline = new GameObject("SelectedOutline", typeof(RectTransform));
            var outlineRect = outline.GetComponent<RectTransform>();
            outlineRect.SetParent(parent, false);
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.anchoredPosition = Vector2.zero;
            outlineRect.sizeDelta = new Vector2(12f, 12f);

            var outlineImage = outline.AddComponent<Image>();
            outlineImage.sprite = GetButtonOutline();
            outlineImage.type = Image.Type.Sliced;
            outlineImage.color = color;
            outlineImage.raycastTarget = false;

            outline.SetActive(false);
            return outline;
        }

        /// <summary>
        /// The rect a focus outline should hug for a button root: SmallRoundButton-
        /// based prefabs have an oversized root around a 32-tall visible "Button"
        /// pill, so the outline hugs the visible <see cref="Button"/> child when
        /// one exists, otherwise the passed-in component's own transform.
        /// </summary>
        public static Transform GetButtonOutlineTarget(Component button)
        {
            var visible = button.GetComponentInChildren<Button>(true);
            return visible != null ? visible.transform : button.transform;
        }

        // The authored outline's green, for the default button/dropdown visuals
        private static GameObject CreateOutline(Transform parent)
        {
            return CreateSelectionOutline(parent, new Color(0.03529412f, 0.87058824f, 0.48235294f));
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                var found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public Action ConfirmCallback;
        public Action<bool> SelectionVisual;

        // Don't call base.Awake — it accesses the null _selectedVisual field.
        protected override void Awake()
        {
            // Bridge child-button clicks to keyboard-nav selection. Unity's
            // pointer-down event stops at the first IPointerDownHandler (the
            // child Button via Selectable), so the parent row's navigatable
            // never receives it. This listener fires on click (pointer-up),
            // after the button's own onClick action (e.g. opening a dialog),
            // so the row is selected in the same frame the dialog opens.
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                button.onClick.AddListener(() => SetSelected(true, SelectionOrigin.Mouse));
            }

            // TMP_Dropdown is a Selectable, not a Button — there's no onClick
            // to hook. Add a pointer-down forwarder on the dropdown's own
            // GameObject. Execute calls every IPointerDownHandler on that
            // GameObject, so both the Selectable and our forwarder fire.
            foreach (var dropdown in GetComponentsInChildren<TMP_Dropdown>(true))
            {
                var forwarder = dropdown.gameObject.AddComponent<DropdownClickForwarder>();
                forwarder.Target = this;
            }
        }

        protected override void OnSelectionChanged(bool selected)
        {
            SelectionVisual?.Invoke(selected);
        }

        public override void Confirm()
        {
            ConfirmCallback?.Invoke();
        }

        /// <summary>
        /// Adds a RuntimeNavigatable to <paramref name="go"/> with a selection
        /// indicator matching what the game uses for the widget type: a
        /// prefab-authored visual if one exists ("Selected Background" on
        /// setting rows, "Selection Outline" on buttons), the button outline
        /// drawn around the dropdown box for bare dropdown rows (whose box
        /// would hide a row overlay), or a stretched overlay replicating the
        /// setting row highlight (SelectionGradient at 0.05 alpha) otherwise.
        /// </summary>
        public static RuntimeNavigatable Attach(GameObject go, Action confirm)
        {
            var nav = go.AddComponent<RuntimeNavigatable>();
            nav.ConfirmCallback = confirm;

            if (go.TryGetComponent<SettingsItemVisual>(out var visual))
            {
                visual.BindNavigatable(nav);
                return nav;
            }

            var authored = FindDeepChild(go.transform, "Selected Background")
                ?? FindDeepChild(go.transform, "Selection Outline");
            if (authored != null)
            {
                authored.gameObject.SetActive(false);
                nav.SelectionVisual = authored.gameObject.SetActive;
                return nav;
            }

            var dropdown = go.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown != null)
            {
                var outline = CreateOutline(dropdown.transform);
                nav.SelectionVisual = outline.SetActive;
                return nav;
            }

            // Buttons without an authored outline (e.g. dialog buttons based on
            // SmallRoundButton) get a runtime one — an overlay would be
            // invisible on top of the colored button image. The outline hugs
            // the Button's own rect: SmallRoundButton-style roots are oversized
            // around the visible pill.
            var button = go.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                var outline = CreateOutline(GetButtonOutlineTarget(go.transform));
                nav.SelectionVisual = outline.SetActive;
                return nav;
            }

            var overlay = new GameObject("SelectedOverlay", typeof(RectTransform));
            var rect = overlay.GetComponent<RectTransform>();
            rect.SetParent(go.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            // Stock rows draw the highlight behind the row content
            rect.SetSiblingIndex(0);

            // Keep layout-group parents from repositioning the overlay
            var layoutElement = overlay.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            var image = overlay.AddComponent<Image>();
            image.sprite = GetSelectionGradient();
            image.color = new Color(1f, 1f, 1f, 0.050980393f);
            image.raycastTarget = false;

            overlay.SetActive(false);
            nav.SelectionVisual = overlay.SetActive;

            return nav;
        }

        /// <summary>
        /// Adds a RuntimeNavigatable to <paramref name="go"/> that indicates
        /// selection by tinting text yellow, like the main menu entries do
        /// (main-menu style: no highlight rectangle). Tints the given labels,
        /// or every TMP under <paramref name="go"/> when none are passed.
        /// </summary>
        public static RuntimeNavigatable AttachTextHighlight(GameObject go, Action confirm,
            params TMP_Text[] texts)
        {
            var nav = go.AddComponent<RuntimeNavigatable>();
            nav.ConfirmCallback = confirm;

            var targets = texts is { Length: > 0 }
                ? texts
                : go.GetComponentsInChildren<TextMeshProUGUI>(true);

            var defaults = new Color[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                defaults[i] = targets[i].color;
            }

            nav.SelectionVisual = selected =>
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] == null) continue;

                    if (selected)
                    {
                        var color = SelectedTextColor;
                        color.a = defaults[i].a;
                        targets[i].color = color;
                    }
                    else
                    {
                        targets[i].color = defaults[i];
                    }
                }
            };

            return nav;
        }

        /// <summary>
        /// Opens a TMP dropdown's list and navigates it with controller input:
        /// Up/Down move the highlighted item (via EventSystem selection, which
        /// drives the item's normal selected tint), Confirm picks it (firing the
        /// dropdown's regular change handling), Back/Escape cancels. The pushed
        /// scheme pops itself when the list closes by any path — pick, cancel, or
        /// clicking the blocker — via a watcher on the list object.
        /// </summary>
        public static void OpenDropdownList(TMP_Dropdown dropdown)
        {
            if (dropdown == null || !dropdown.interactable || dropdown.options.Count == 0)
            {
                return;
            }

            // Open the list unless TMP already opened it (e.g. via its native
            // OnPointerClick from a mouse click). Calling Show() twice creates
            // duplicate lists, so check for the existing child first.
            if (dropdown.transform.Find("Dropdown List") == null)
            {
                dropdown.Show();
            }

            // TMP instantiates its template as a child named "Dropdown List"
            var list = dropdown.transform.Find("Dropdown List");
            if (list == null)
            {
                return;
            }

            // A native mouse click and a row-level click forwarder can both arrive
            // on the frame after TMP opens the list. Only attach one search/nav
            // handler to a given list instance.
            if (list.TryGetComponent<DropdownTypeaheadSearch>(out _))
            {
                return;
            }

            var toggles = list.GetComponentsInChildren<Toggle>();
            if (toggles.Length == 0)
            {
                return;
            }

            var scrollRect = list.GetComponent<ScrollRect>();
            int index = Mathf.Clamp(dropdown.value, 0, toggles.Length - 1);

            DropdownTypeaheadSearch search = null;
            SearchableDropdownMarker searchableMarker = null;
            if (dropdown.TryGetComponent(out searchableMarker) && searchableMarker.IsConfigured)
            {
                search = list.gameObject.AddComponent<DropdownTypeaheadSearch>();
                search.Initialize(dropdown, toggles, searchableMarker);
            }

            int FindVisible(int start, int direction)
            {
                for (int offset = 0; offset < toggles.Length; offset++)
                {
                    int rawCandidate = start + direction * offset;
                    int candidate = ((rawCandidate % toggles.Length) + toggles.Length) % toggles.Length;
                    if (toggles[candidate].gameObject.activeSelf && toggles[candidate].interactable)
                    {
                        return candidate;
                    }
                }

                return -1;
            }

            void Highlight()
            {
                if (index < 0 || index >= toggles.Length ||
                    !toggles[index].gameObject.activeSelf || !toggles[index].interactable)
                {
                    index = FindVisible(0, 1);
                }

                if (index < 0)
                {
                    EventSystem.current?.SetSelectedGameObject(null);
                    return;
                }

                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(toggles[index].gameObject);
                }

                // Keep the highlighted item in view (items are uniform height)
                if (scrollRect != null && toggles.Length > 1)
                {
                    scrollRect.verticalNormalizedPosition =
                        1f - (float) index / (toggles.Length - 1);
                }
            }

            if (search != null)
            {
                search.FilterChanged = Highlight;
            }

            Highlight();

            bool popped = false;
            void PopOnce()
            {
                // Navigator can already be gone if the list dies during teardown
                if (!popped && Navigator.Instance != null)
                {
                    popped = true;
                    Navigator.Instance.PopScheme();
                }
            }

            var watcher = list.gameObject.AddComponent<DropdownListCloseWatcher>();
            watcher.Closed = PopOnce;

            if (Navigator.Instance == null) return;

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Previous", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    index = FindVisible(index - 1, -1);
                    Highlight();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Next", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    index = FindVisible(index + 1, 1);
                    Highlight();
                }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    if (index < 0 || !toggles[index].gameObject.activeSelf || !toggles[index].interactable) return;
                    if (searchableMarker != null)
                    {
                        searchableMarker.SelectVisibleIndex(index);
                        if (dropdown != null) dropdown.Hide();
                        return;
                    }
                    if (index == dropdown.value)
                    {
                        // TMP suppresses the change event for the same index, so
                        // the list wouldn't close; treat re-picking as a cancel.
                        dropdown.Hide();
                    }
                    else
                    {
                        toggles[index].isOn = true;
                    }
                }),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Cancel", () =>
                {
                    if (dropdown == null) { PopOnce(); return; }
                    dropdown.Hide();
                }),
            }, null));
        }
    }

    /// <summary>
    /// Opt-in marker placed on dropdowns whose open list supports keyboard text filtering.
    /// The current query is shown in the closed dropdown's caption while its list is open.
    /// </summary>
    public sealed class SearchableDropdownMarker : MonoBehaviour, IPointerClickHandler
    {
        private const int MAX_VISIBLE_OPTIONS = 20;
        private const int REFINE_SEARCH_INDEX = -1;
        private const string SELECT_LABEL = "(Select)";
        private const string REFINE_SEARCH_LABEL = "(Refine Search for More)";
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
            if (sourceIndex == REFINE_SEARCH_INDEX) return REFINE_SEARCH_LABEL;
            string label = GetSourceLabel(sourceIndex);
            return string.IsNullOrEmpty(label) ? SELECT_LABEL : label;
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
            _dropdown.captionText.text = isBlank ? SELECT_LABEL : CurrentLabel;
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
            _dropdown.captionText.text = $"Search: {query}";
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
            if (Keyboard.current?.backspaceKey.wasPressedThisFrame == true && _query.Length > 0)
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
    public class DropdownListCloseWatcher : MonoBehaviour
    {
        public Action Closed;

        private void OnDestroy()
        {
            Closed?.Invoke();
        }
    }

    /// <summary>
    /// Forwards pointer events from a child TMP_Dropdown (a Selectable
    /// with no onClick) to the parent RuntimeNavigatable. On pointer-down,
    /// selects the parent row. On pointer-click (after TMP's own
    /// OnPointerClick has opened the list natively), defers one frame then
    /// calls <see cref="RuntimeNavigatable.OpenDropdownList"/> to attach
    /// keyboard/controller navigation to the already-open list.
    /// </summary>
    public class DropdownClickForwarder : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        public NavigatableBehaviour Target;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Target != null)
            {
                Target.SetSelected(true, SelectionOrigin.Mouse);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // TMP's OnPointerClick (also on this GameObject) already called
            // Show() and opened the native list. Defer to next frame so we
            // can detect the open list and attach keyboard navigation to it
            // without re-calling Show().
            var dropdown = GetComponent<TMP_Dropdown>();
            if (dropdown == null) return;

            UniTask.NextFrame().ContinueWith(() =>
            {
                if (dropdown != null && dropdown.transform.Find("Dropdown List") != null)
                {
                    RuntimeNavigatable.OpenDropdownList(dropdown);
                }
            }).Forget();
        }
    }
}
