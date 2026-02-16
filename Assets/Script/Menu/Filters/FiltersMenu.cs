using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Core;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;
using YARG.Menu.Persistent;
using YARG.Player;
using YARG.Song;
using YARG.Settings;

namespace YARG.Menu.Filters
{
    [DefaultExecutionOrder(-10000)]
    public class FiltersMenu : MonoSingleton<FiltersMenu>
    {
        private const float ResetAllFiltersHoldSeconds = 1f;

        private readonly struct FilterHelpBarState
        {
            public readonly string GreenKey;
            public readonly bool ShowRed;

            public FilterHelpBarState(string greenKey, bool showRed)
            {
                GreenKey = greenKey;
                ShowRed = showRed;
            }
        }

        private readonly struct FilterDef
        {
            public readonly FilterGroup Group;
            public readonly Func<IReadOnlyList<string>> GetValues;
            public readonly Func<Dictionary<string, int>> GetCounts;
            public readonly Dictionary<string, bool> Enabled;
            public readonly Func<string, string> LabelTransform;

            public FilterDef(
                FilterGroup group,
                Func<IReadOnlyList<string>> getValues,
                Func<Dictionary<string, int>> getCounts,
                Dictionary<string, bool> enabled,
                Func<string, string> labelTransform = null)
            {
                Group = group;
                GetValues = getValues;
                GetCounts = getCounts;
                Enabled = enabled;
                LabelTransform = labelTransform;
            }
        }
        [SerializeField]
        private ScrollRect _leftScrollRect;
        [SerializeField]
        private NavigationGroup _leftNavGroup;
        [SerializeField]
        private RectTransform _leftHeaderPrefab;
        [SerializeField]
        private RectTransform _leftContainer;

        [Space]
        [SerializeField]
        private ScrollRect _rightScrollRect;
        [SerializeField]
        private NavigationGroup _rightNavGroup;
        [SerializeField]
        private RectTransform _rightContainer;
        [SerializeField]
        private FilterEntryRow _filterEntryRowPrefab;

        [Space]
        [SerializeField]
        private FilterCategoryRow _filterCategoryRowPrefab;
        [SerializeField]
        private GameObject _sortedByDropdownPrefab;
        [SerializeField]
        private GameObject _showRecommendationsTogglePrefab;

        private readonly Dictionary<FilterGroup, FilterCategoryRow> _leftRows = new();
        private Toggle _showRecommendationsToggle;

        private readonly Dictionary<string, bool> _genreEnabled =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _decadeEnabled =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _vocalPartsEnabled =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _sourceEnabled =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _charterEnabled =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _lengthEnabled =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> _difficultyEnabled =
            new(StringComparer.OrdinalIgnoreCase);

        private static IReadOnlyList<string> _cachedGenres;
        private static IReadOnlyList<string> _cachedDecades;
        private static IReadOnlyList<string> _cachedVocalParts;
        private static IReadOnlyList<string> _cachedSources;
        private static IReadOnlyList<string> _cachedCharters;
        private static IReadOnlyList<string> _cachedLengths;
        private static IReadOnlyList<string> _cachedDifficulties;

        private static int _cachedGenreSongCount = -1;
        private static int _cachedDecadeSongCount = -1;
        private static int _cachedVocalPartsSongCount = -1;
        private static int _cachedSourceSongCount = -1;
        private static int _cachedCharterSongCount = -1;
        private static int _cachedLengthSongCount = -1;
        private static int _cachedDifficultySongCount = -1;

        private static Instrument _cachedDifficultyInstrument = Instrument.FiveFretGuitar;

        private static GameObject _settingsButtonPrefab;

        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;

        private enum FocusSide
        {
            Left,
            Right
        }

        private int? _lastLeftIndex;
        private int? _lastRightIndex;

        private FocusSide _focusSide = FocusSide.Left;

        public static Func<SongEntry, bool> ActiveFilterPredicate { get; private set; }

        private static bool _hasSavedFilters;
        private static readonly Dictionary<FilterGroup, Dictionary<string, bool>> _savedFilters =
            new();

        private FilterSortDropdownSetting _sortDropdownSetting;
        private Button _topBackButton;
        private FilterHelpBarState _lastHelpBarState;
        private bool _pendingHelpBarRefresh;

        protected override void SingletonAwake()
        {
            // Match SettingsMenu behavior: initialized at startup, then hidden.
            gameObject.SetActive(false);
            _ready = true;
        }

        private void OnEnable()
        {
            if (!_ready) return;

            _leftNavGroup.SelectionChanged += OnSelectionChanged;
                _rightNavGroup.SelectionChanged += OnRightSelectionChanged;

            var library = FindFirstObjectByType<MusicLibrary.MusicLibraryMenu>();
            library?.SetSidebarDifficultiesVisible(false);

            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm", HandleConfirm),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", HandleBack, hide: true),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.Filters.ResetAllFilters", _ => { },
                    ResetAllFiltersHoldSeconds, _ => ResetAllFilters()),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown
            }, true));

            WireTopBackButton();

            Refresh();
            RefreshHelpBar();
        }

        private void HandleConfirm()
        {
            if (_focusSide == FocusSide.Left)
            {
                // If we're on an action button, confirm it instead of jumping to the right.
                if (_leftNavGroup?.SelectedBehaviour is SettingsButton buttonRow)
                    buttonRow.Confirm();
                else if (_leftNavGroup?.SelectedBehaviour is BaseSettingNavigatable settingRow)
                    settingRow.Confirm();
                else
                    FocusRight();
            }
            else
            {
                if (_rightNavGroup.SelectedBehaviour is SettingsButton buttonRow)
                    buttonRow.Confirm();
                else
                    ToggleFocusedRight();
            }
        }

        private void HandleBack()
        {
            if (_focusSide == FocusSide.Right)
            {
                FocusLeft();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void WireTopBackButton()
        {
            _topBackButton = FindHeaderBackButton();

            DisablePersistentSetActive(_topBackButton);
            _topBackButton.onClick.RemoveListener(HandleBack);
            _topBackButton.onClick.AddListener(HandleBack);
        }

        private Button FindHeaderBackButton()
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                    continue;

                var image = button.GetComponent<Image>();
                var sprite = image != null ? image.sprite : null;
                if (sprite != null && sprite.name.Contains("RedBackButton", StringComparison.OrdinalIgnoreCase))
                    return button;
            }

            return null;
        }

        private void DisablePersistentSetActive(Button button)
        {
            var onClick = button.onClick;
            int count = onClick.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                if (onClick.GetPersistentTarget(i) == gameObject && onClick.GetPersistentMethodName(i) == "SetActive")
                    onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }
        }

        public void Refresh()
        {
            UpdateUI(resetScroll: true);
        }

        private void UpdateUI(bool resetScroll)
        {
            _leftNavGroup.ClearNavigatables();
            _leftContainer.DestroyChildren();
            BuildLeftPanel(_leftContainer, _leftNavGroup);

            CacheLeftRows();

            RestoreSavedFilters();
            EnsureAllDefaults();
            UpdateAllSummaries();

            if (resetScroll)
            {
                _leftNavGroup.SelectFirst();
                if (_leftScrollRect != null)
                    _leftScrollRect.verticalNormalizedPosition = 1f;
            }
            
            FocusLeft();
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin origin)
        {
            if (_rightContainer == null) return;

            if (selected is not FilterCategoryRow row)
            {
                // Only clear the right panel when the left side is actually in focus.
                // Otherwise, selecting the right panel would clear itself.
                if (_focusSide == FocusSide.Left)
                {
                    _rightNavGroup.ClearNavigatables();
                    _rightContainer.DestroyChildren();
                }
                return;
            }

            _rightNavGroup.ClearNavigatables();
            _rightContainer.DestroyChildren();

            switch (row.Filters)
            {
                case FilterGroup.Genre:
                    BuildOptionsFor(FilterGroup.Genre);
                    break;
                case FilterGroup.Decade:
                    BuildOptionsFor(FilterGroup.Decade);
                    break;
                case FilterGroup.VocalParts:
                    BuildOptionsFor(FilterGroup.VocalParts);
                    break;
                case FilterGroup.Source:
                    BuildOptionsFor(FilterGroup.Source);
                    break;
                case FilterGroup.Charter:
                    BuildOptionsFor(FilterGroup.Charter);
                    break;
                case FilterGroup.Difficulty:
                    BuildOptionsFor(FilterGroup.Difficulty);
                    break;
                case FilterGroup.Length:
                    BuildOptionsFor(FilterGroup.Length);
                    break;
            }
        }

        private void CacheLeftRows()
        {
            _leftRows.Clear();
            foreach (var row in _leftContainer.GetComponentsInChildren<FilterCategoryRow>(true))
                _leftRows[row.Filters] = row;
        }

        private void BuildLeftPanel(Transform container, NavigationGroup navGroup)
        {
            int rowIndex = 0;

            AddHeader(container, Localize.Key("Menu.Filters.OptionsHeader"));
            rowIndex = 0;
            AddDropdown(container, navGroup, Localize.Key("Menu.Filters.SortedBy"))?.AssignIndex(rowIndex++);
            AddToggle(container, navGroup, Localize.Key("Menu.Filters.ShowRecommendations"))?.AssignIndex(rowIndex++);

            AddHeader(container, Localize.Key("Menu.Filters.FiltersHeader"));
            rowIndex = 0;
            AddGroup(container, navGroup, FilterGroup.Genre,  Localize.Key("Menu.Filters.Genres"))?.AssignIndex(rowIndex++);
            AddGroup(container, navGroup, FilterGroup.Decade, Localize.Key("Menu.Filters.Decades"))?.AssignIndex(rowIndex++);
            AddGroup(container, navGroup, FilterGroup.VocalParts, Localize.Key("Menu.Filters.VocalParts.Name"))?.AssignIndex(rowIndex++);
            AddGroup(container, navGroup, FilterGroup.Source, Localize.Key("Menu.Filters.Sources"))?.AssignIndex(rowIndex++);
            AddGroup(container, navGroup, FilterGroup.Charter, Localize.Key("Menu.Filters.Charters"))?.AssignIndex(rowIndex++);
            AddGroup(container, navGroup, FilterGroup.Difficulty, Localize.Key("Menu.Filters.Difficulties.Name"))?.AssignIndex(rowIndex++);
            AddGroup(container, navGroup, FilterGroup.Length, Localize.Key("Menu.Filters.Length.Name"))?.AssignIndex(rowIndex++);
        }

        private void AddHeader(Transform container, string text)
        {
            var prefab = _leftHeaderPrefab;
            if (prefab == null) return;

            var header = Instantiate(prefab, container);
            var tmp = header.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = text;
        }

        private FilterCategoryRow AddGroup(Transform container, NavigationGroup navGroup, FilterGroup group, string label)
        {
            var prefab = _filterCategoryRowPrefab;
            if (prefab == null) return null;

            var row = Instantiate(prefab, container);

            string secondary = group == FilterGroup.Genre ? Localize.Key("Menu.Filters.All") : string.Empty;

            row.Init(group, label, secondary);
            navGroup.AddNavigatable(row);
            return row;
        }

        private BaseSettingVisual AddDropdown(Transform container, NavigationGroup navGroup, string label)
        {
            var prefab = _sortedByDropdownPrefab;
            if (prefab == null) return null;

            var row = Instantiate(prefab, container);
            SetupSortedByDropdown(row, label);

            var navigatable = row.GetComponent<BaseSettingNavigatable>();
            if (navigatable != null)
                navGroup.AddNavigatable(navigatable);

            return row.GetComponent<BaseSettingVisual>();
        }

        private BaseSettingVisual AddToggle(Transform container, NavigationGroup navGroup, string label)
        {
            var prefab = _showRecommendationsTogglePrefab;
            if (prefab == null) return null;

            var row = Instantiate(prefab, container);
            SetupShowRecommendationsToggle(row, label);

            var navigatable = row.GetComponent<BaseSettingNavigatable>();
            if (navigatable != null)
                navGroup.AddNavigatable(navigatable);

            return row.GetComponent<BaseSettingVisual>();
        }

        private void FocusLeft()
        {
            _focusSide = FocusSide.Left;
            _pendingHelpBarRefresh = true;

            // Remember right selection before clearing
            _lastRightIndex = _rightNavGroup.SelectedIndex;

            _rightNavGroup.ClearSelection();

            if (_leftNavGroup == null || _leftNavGroup.Count == 0) return;

            if (_lastLeftIndex != null)
            {
                _leftNavGroup.SelectAt(_lastLeftIndex, SelectionOrigin.Navigation);   
            }
            else
            {
                _leftNavGroup.SelectFirst();
            }

            _leftNavGroup.PushNavGroupToStack();
            RefreshHelpBar();
        }

        private void FocusRight()
        {
            if (_rightNavGroup.Count == 0) return;

            _focusSide = FocusSide.Right;
            _pendingHelpBarRefresh = true;

            // Remember left selection before clearing
            _lastLeftIndex = _leftNavGroup?.SelectedIndex;

            _leftNavGroup?.ClearSelection();

            if (_rightNavGroup.SelectedIndex == null)
                _rightNavGroup.SelectFirst();

            _rightNavGroup.PushNavGroupToStack();

            if (_rightScrollRect != null)
                _rightScrollRect.verticalNormalizedPosition = 1f;

            RefreshHelpBar();
        }

        private void ToggleFocusedRight()
        {
            var selected = _rightNavGroup.SelectedBehaviour;
            if (selected == null) return;

            var row = selected.GetComponent<FilterEntryRow>();
            if (row?.Toggle != null)
                row.Toggle.isOn = !row.Toggle.isOn;
        }

        private void OnRightSelectionChanged(NavigatableBehaviour selected, SelectionOrigin origin)
        {
            _pendingHelpBarRefresh = true;
            RefreshHelpBar();
        }

        private void RefreshHelpBar()
        {
            if (_leftNavGroup?.SelectedBehaviour is BaseSettingNavigatable settingRow &&
                settingRow.IsFocused)
            {
                _pendingHelpBarRefresh = true;
                return;
            }

            var state = GetHelpBarState();
            if (state.Equals(_lastHelpBarState) && !_pendingHelpBarRefresh) return;

            _pendingHelpBarRefresh = false;
            _lastHelpBarState = state;

            var entries = new List<NavigationScheme.Entry>
            {
                new(MenuAction.Green, state.GreenKey, HandleConfirm),
                new(MenuAction.Red, "Menu.Common.Back", HandleBack, hide: !state.ShowRed),
                new(MenuAction.Yellow, "Menu.Filters.ResetAllFilters", _ => { },
                    ResetAllFiltersHoldSeconds, _ => ResetAllFilters()),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown
            };

            HelpBar.Instance.SetInfoFromScheme(new NavigationScheme(entries, true));
        }

        private FilterHelpBarState GetHelpBarState()
        {
            if (_focusSide == FocusSide.Right)
            {
                if (_rightNavGroup.SelectedBehaviour is SettingsButton)
                    return new FilterHelpBarState("Menu.Common.Confirm", true);

                return new FilterHelpBarState("Menu.Common.Toggle", true);
            }

            return new FilterHelpBarState("Menu.Common.Confirm", false);
        }

        private static string NormalizeFilterKey(string text)
        {
            return StringTransformations.RemoveUnwantedWhitespace(
                StringTransformations.RemoveDiacritics(
                    RichTextUtils.StripRichTextTags(text)));
        }

        private IEnumerable<FilterDef> GetFilterDefs()
        {
            var difficultyInstrument = GetDifficultyFilterInstrument();

            yield return new FilterDef(
                FilterGroup.Genre,
                GetAllGenresCached,
                () => GetCountsFromCollections(SongContainer.Genres, key => key.ToString()),
                _genreEnabled);

            yield return new FilterDef(
                FilterGroup.Decade,
                GetAllDecadesCached,
                GetDecadeCounts,
                _decadeEnabled);

            yield return new FilterDef(
                FilterGroup.VocalParts,
                GetAllVocalPartsCached,
                GetVocalPartsCounts,
                _vocalPartsEnabled);

            yield return new FilterDef(
                FilterGroup.Source,
                GetAllSourcesCached,
                () => GetCountsFromCollections(
                    SongContainer.Sources,
                    key => SongSources.SourceToGameName(key.ToString())),
                _sourceEnabled);

            yield return new FilterDef(
                FilterGroup.Charter,
                GetAllChartersCached,
                () => GetCountsFromCollections(SongContainer.Charters, key => key.ToString()),
                _charterEnabled,
                WrapCharterLabel);

            yield return new FilterDef(
                FilterGroup.Difficulty,
                () => GetAllDifficultiesCached(difficultyInstrument),
                () => GetDifficultyCounts(difficultyInstrument),
                _difficultyEnabled);

            yield return new FilterDef(
                FilterGroup.Length,
                GetAllLengthsCached,
                GetLengthCounts,
                _lengthEnabled);
        }

        private bool TryGetFilterDef(FilterGroup group, out FilterDef def)
        {
            foreach (var candidate in GetFilterDefs())
            {
                if (candidate.Group == group)
                {
                    def = candidate;
                    return true;
                }
            }

            def = default;
            return false;
        }

        private void EnsureAllDefaults()
        {
            foreach (var def in GetFilterDefs())
                EnsureDefaults(def.Enabled, def.GetValues());
        }

        private void RestoreSavedFilters()
        {
            if (!_hasSavedFilters)
                return;

            foreach (var def in GetFilterDefs())
            {
                if (!_savedFilters.TryGetValue(def.Group, out var saved))
                    continue;

                def.Enabled.Clear();
                foreach (var kvp in saved)
                    def.Enabled[kvp.Key] = kvp.Value;
            }
        }

        private void SaveFilters()
        {
            _savedFilters.Clear();
            foreach (var def in GetFilterDefs())
            {
                _savedFilters[def.Group] = new Dictionary<string, bool>(
                    def.Enabled,
                    StringComparer.OrdinalIgnoreCase);
            }

            _hasSavedFilters = true;
        }

        private void UpdateAllSummaries()
        {
            foreach (var def in GetFilterDefs())
                UpdateSummary(def.Group, def.Enabled, def.GetValues());
        }

        private void BuildOptionsFor(FilterGroup group)
        {
            if (!TryGetFilterDef(group, out var def)) return;

            var values = def.GetValues();
            var counts = def.GetCounts();
            BuildOptions(
                values,
                def.Enabled,
                counts,
                () => UpdateSummary(def.Group, def.Enabled, def.GetValues()),
                def.LabelTransform);
        }

        private static bool TryGetSelectedSet(
            Dictionary<string, bool> enabled,
            IReadOnlyList<string> all,
            Func<string, string> normalize,
            out HashSet<string> selected)
        {
            selected = null;

            if (all.Count == 0) return false;

            EnsureDefaults(enabled, all);

            if (enabled.Count == 0) return false;

            int selectedCount = enabled.Count(kvp => kvp.Value);
            if (selectedCount == enabled.Count) return false; // no filter for this category

            selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in enabled)
            {
                if (kvp.Value)
                    selected.Add(normalize(kvp.Key));
            }
            return true;
        }

        private static string NormalizeDecade(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : text.ToLowerInvariant();

        private static IReadOnlyList<string> GetAllCached(
            ref IReadOnlyList<string> cache,
            ref int cacheCount,
            Func<IReadOnlyList<string>> build)
        {
            if (cache != null && cacheCount == SongContainer.Count) return cache;

            cacheCount = SongContainer.Count;
            cache = build();
            return cache;
        }

        private static Dictionary<string, int> GetCountsFromCollections<TKey, TValue>(
            IEnumerable<KeyValuePair<TKey, TValue>> collection,
            Func<TKey, string> keySelector)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in collection)
            {
                string key = keySelector(kvp.Key);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                int count = 0;
                if (kvp.Value is System.Collections.ICollection c)
                    count = c.Count;

                dict.TryGetValue(key, out int existing);
                dict[key] = existing + count;
            }

            return dict;
        }

        private static void EnsureDefaults(Dictionary<string, bool> enabled, IReadOnlyList<string> values)
        {
            foreach (var value in values)
            {
                if (!enabled.ContainsKey(value))
                    enabled[value] = true;
            }

            var set = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
            foreach (var key in enabled.Keys.Where(k => !set.Contains(k)).ToList())
                enabled.Remove(key);
        }

        private void UpdateSummary(
            FilterGroup group,
            Dictionary<string, bool> enabled,
            IReadOnlyList<string> values)
        {
            if (!_leftRows.TryGetValue(group, out var row)) return;

            int total = values.Count;
            int selected = enabled.Count(kvp => kvp.Value);

            string text =
                selected == 0 ? Localize.Key("Menu.Filters.None") :
                selected == total ? Localize.Key("Menu.Filters.All") :
                Localize.KeyFormat("Menu.Filters.Selected", selected);

            row.SetSecondaryText(text);
        }

        private void BuildOptions(
            IReadOnlyList<string> values,
            Dictionary<string, bool> enabled,
            Dictionary<string, int> counts,
            Action updateSummary,
            Func<string, string> labelTransform = null)
        {
            if (_rightContainer == null || _filterEntryRowPrefab == null) return;

            EnsureDefaults(enabled, values);

            if (values.Count > 0)
                AddSelectDeselectButtons(
                    () => ApplyRightToggleState(enabled, true, updateSummary),
                    () => ApplyRightToggleState(enabled, false, updateSummary));

            int rowIndex = 0;
            foreach (string value in values)
            {
                var row = Instantiate(_filterEntryRowPrefab, _rightContainer);
                _rightNavGroup.AddNavigatable(row.gameObject);

                row.AssignIndex(rowIndex++);

                bool isOn = enabled[value];
                counts.TryGetValue(value, out int count);
                var labelText = labelTransform != null ? labelTransform(value) : value;

                row.Bind(
                    labelText: labelText,
                    numberText: Localize.KeyFormat("Menu.Filters.SongCount", count),
                    isOn: isOn
                );

                row.ToggleChanged += toggleValue =>
                {
                    enabled[value] = toggleValue;
                    updateSummary?.Invoke();
                    DisableRecommendationsIfFiltered();
                };
            }
        }

        private Func<SongEntry, bool> BuildFilterPredicate()
        {
            var predicates = new List<Func<SongEntry, bool>>();

            if (TryGetSelectedSet(_genreEnabled, GetAllGenresCached(), NormalizeFilterKey, out var genres))
                predicates.Add(entry => genres.Contains(entry.Genre.SearchStr));

            if (TryGetSelectedSet(_decadeEnabled, GetAllDecadesCached(), NormalizeDecade, out var decades))
                predicates.Add(entry =>
                {
                    var decade = GetDecadeLabel(entry.YearAsNumber);
                    return decade != null && decades.Contains(decade.ToLowerInvariant());
                });

            if (TryGetSelectedSet(_sourceEnabled, GetAllSourcesCached(), NormalizeFilterKey, out var sources))
                predicates.Add(entry =>
                {
                    var display = SongSources.SourceToGameName(entry.Source);
                    var normalized = NormalizeFilterKey(display);
                    return sources.Contains(normalized);
                });

            if (TryGetSelectedSet(_charterEnabled, GetAllChartersCached(), NormalizeFilterKey, out var charters))
                predicates.Add(entry => charters.Contains(entry.Charter.SearchStr));

            if (TryGetSelectedSet(_vocalPartsEnabled, GetAllVocalPartsCached(), NormalizeFilterKey, out var vocalParts))
                predicates.Add(entry =>
                {
                    var label = GetVocalPartsLabel(entry.VocalsCount);
                    return label != null && vocalParts.Contains(NormalizeFilterKey(label));
                });

            if (TryGetSelectedSet(_lengthEnabled, GetAllLengthsCached(), NormalizeFilterKey, out var lengths))
                predicates.Add(entry =>
                {
                    var label = GetSongLengthLabel(entry.SongLengthMilliseconds);
                    return label != null && lengths.Contains(NormalizeFilterKey(label));
                });

            var difficultyInstrument = GetDifficultyFilterInstrument();
            if (TryGetSelectedSet(_difficultyEnabled, GetAllDifficultiesCached(difficultyInstrument), NormalizeFilterKey, out var difficulties))
                predicates.Add(entry =>
                {
                    var label = GetDifficultyLabel(entry, difficultyInstrument);
                    return label != null && difficulties.Contains(NormalizeFilterKey(label));
                });

            if (predicates.Count == 0) return null;

            return entry => predicates.All(predicate => predicate(entry));
        }


#region Genres
        private static IReadOnlyList<string> GetAllGenresCached()
        {
            return GetAllCached(ref _cachedGenres, ref _cachedGenreSongCount, () =>
                SongContainer.Genres.Keys
                    .Select(k => k.ToString()) // SortString -> string
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
#endregion

#region Decades
        private static IReadOnlyList<string> GetAllDecadesCached()
        {
            return GetAllCached(ref _cachedDecades, ref _cachedDecadeSongCount, () =>
                SongContainer.Songs
                    .Select(s => GetDecadeLabel(s.YearAsNumber))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }

        private static Dictionary<string, int> GetDecadeCounts()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var song in SongContainer.Songs)
            {
                var decade = GetDecadeLabel(song.YearAsNumber);
                if (string.IsNullOrWhiteSpace(decade))
                    continue;

                dict.TryGetValue(decade, out int count);
                dict[decade] = count + 1;
            }

            return dict;
        }
        private static string GetDecadeLabel(int year)
        {
            if (year < 1000 || year == int.MaxValue) return null; // invalid/unknown

            int decade = (year / 10) * 10;
            return $"The {decade}s";
        }
#endregion

#region Vocal Parts
        private static IReadOnlyList<string> GetAllVocalPartsCached()
        {
            return GetAllCached(ref _cachedVocalParts, ref _cachedVocalPartsSongCount, () =>
            {
                var counts = new HashSet<int>();
                foreach (var song in SongContainer.Songs)
                {
                    int count = Math.Clamp(song.VocalsCount, 0, 3);
                    if (count > 0)
                        counts.Add(count);
                }

                var ordered = new List<string>(counts.Count);
                for (int i = 1; i <= 3; i++)
                {
                    if (counts.Contains(i))
                        ordered.Add(GetVocalPartsLabel(i));
                }

                return ordered;
            });
        }

        private static Dictionary<string, int> GetVocalPartsCounts()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var song in SongContainer.Songs)
            {
                var label = GetVocalPartsLabel(song.VocalsCount);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                dict.TryGetValue(label, out int count);
                dict[label] = count + 1;
            }

            return dict;
        }
        private static string GetVocalPartsLabel(int vocalsCount)
        {
            return vocalsCount switch
            {
                >= 3 => Localize.Key("Menu.Filters.VocalParts.Trio"),
                2 => Localize.Key("Menu.Filters.VocalParts.Duet"),
                1 => Localize.Key("Menu.Filters.VocalParts.Solo"),
                _ => null
            };
        }
#endregion

#region Source
        private static IReadOnlyList<string> GetAllSourcesCached()
        {
            return GetAllCached(ref _cachedSources, ref _cachedSourceSongCount, () =>
                SongContainer.Sources.Keys
                    .Select(k => SongSources.SourceToGameName(k.ToString()))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
#endregion

#region Charter
        private static IReadOnlyList<string> GetAllChartersCached()
        {
            return GetAllCached(ref _cachedCharters, ref _cachedCharterSongCount, () =>
                SongContainer.Charters.Keys
                    .Select(k => k.ToString()) // keep raw text (includes <color> tags)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => RichTextUtils.StripRichTextTags(s), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
        private const int CharterWrapLimit = 30;

        private static string WrapCharterLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            string plain = RichTextUtils.StripRichTextTags(text);
            if (plain.Length <= CharterWrapLimit) return text;

            int breakVisibleIndex = FindLastWhitespaceIndex(plain, CharterWrapLimit);
            if (breakVisibleIndex < 0)
                breakVisibleIndex = CharterWrapLimit;

            return InsertLineBreakAtVisibleIndex(text, breakVisibleIndex);
        }

        private static int FindLastWhitespaceIndex(string text, int maxIndex)
        {
            int last = -1;
            int limit = Math.Min(text.Length - 1, maxIndex);
            for (int i = 0; i <= limit; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                    last = i;
            }
            return last;
        }

        private static string InsertLineBreakAtVisibleIndex(string text, int visibleIndex)
        {
            int visibleCount = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                    inTag = true;
                else if (c == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (inTag)
                    continue;

                if (visibleCount == visibleIndex)
                {
                    if (char.IsWhiteSpace(c)) return text.Remove(i, 1).Insert(i, "\n");

                    return text.Insert(i, "\n");
                }

                visibleCount++;
            }

            return text;
        }
#endregion

#region Song Lengths
        private static IReadOnlyList<string> GetAllLengthsCached()
        {
            return GetAllCached(ref _cachedLengths, ref _cachedLengthSongCount, () =>
            {
                var counts = GetLengthCounts();
                var ordered = new List<string>(LengthLabels.Count);
                foreach (var label in LengthLabels)
                {
                    if (counts.TryGetValue(label, out int count) && count > 0)
                        ordered.Add(label);
                }
                return ordered;
            });
        }

        private static Dictionary<string, int> GetLengthCounts()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var song in SongContainer.Songs)
            {
                var label = GetSongLengthLabel(song.SongLengthMilliseconds);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                dict.TryGetValue(label, out int count);
                dict[label] = count + 1;
            }

            return dict;
        }
        
        // Toggle which length buckets to use by switching this flag.
        // - true  -> Legacy labels (Short/Medium/Long/Epic)
        // - false -> Range labels (0-2, 2-5, 5-10, 10-15, 15-20, 20+)
        private const bool UseLegacyLengthLabels = true;

        private static readonly string[] LegacyLengthLabelKeys =
        {
            "Menu.Filters.Length.Short",
            "Menu.Filters.Length.Medium",
            "Menu.Filters.Length.Long",
            "Menu.Filters.Length.Epic",
        };

        private static readonly string[] RangeLengthLabels =
        {
            "00:00 - 02:00",
            "02:00 - 05:00",
            "05:00 - 10:00",
            "10:00 - 15:00",
            "15:00 - 20:00",
            "20:00+",
        };

        private static IReadOnlyList<string> LengthLabels =>
            UseLegacyLengthLabels ? GetLegacyLengthLabels() : RangeLengthLabels;

        private static IReadOnlyList<string> GetLegacyLengthLabels()
        {
            var labels = new string[LegacyLengthLabelKeys.Length];
            for (int i = 0; i < LegacyLengthLabelKeys.Length; i++)
            {
                labels[i] = Localize.Key(LegacyLengthLabelKeys[i]);
            }
            return labels;
        }

        private static string GetLegacyLengthLabel(int index)
        {
            return Localize.Key(LegacyLengthLabelKeys[index]);
        }

        private static string GetSongLengthLabel(long lengthMilliseconds)
        {
            if (UseLegacyLengthLabels)
            {
                return lengthMilliseconds switch
                {
                    < 180000 => GetLegacyLengthLabel(0),
                    < 300000 => GetLegacyLengthLabel(1),
                    < 420000 => GetLegacyLengthLabel(2),
                    _ => GetLegacyLengthLabel(3),
                };
            }

            return lengthMilliseconds switch
            {
                < 120000 => RangeLengthLabels[0],
                < 300000 => RangeLengthLabels[1],
                < 600000 => RangeLengthLabels[2],
                < 900000 => RangeLengthLabels[3],
                < 1200000 => RangeLengthLabels[4],
                _ => RangeLengthLabels[5],
            };
        }
#endregion

#region Difficulties
        private static readonly string[] DifficultyLabelKeys =
        {
            "Menu.Filters.Difficulties.WarmUp",
            "Menu.Filters.Difficulties.Apprentice",
            "Menu.Filters.Difficulties.Solid",
            "Menu.Filters.Difficulties.Moderate",
            "Menu.Filters.Difficulties.Challenging",
            "Menu.Filters.Difficulties.Nightmare",
            "Menu.Filters.Difficulties.Impossible",
        };

        private const string DifficultyLabelUnknownKey = "Menu.Filters.Difficulties.Unknown";
        private const string DifficultyLabelNoPartKey = "Menu.Filters.Difficulties.NoPart";

        private static Instrument GetDifficultyFilterInstrument()
        {
            foreach (var player in PlayerContainer.Players)
            {
                if (!player.Profile.IsBot) return player.Profile.CurrentInstrument;
            }
            return Instrument.FiveFretGuitar;
        }

        private static IReadOnlyList<string> GetAllDifficultiesCached(Instrument instrument)
        {
            if (_cachedDifficulties != null && _cachedDifficultySongCount == SongContainer.Count && _cachedDifficultyInstrument == instrument) return _cachedDifficulties;

            _cachedDifficultySongCount = SongContainer.Count;
            _cachedDifficultyInstrument = instrument;
            _cachedDifficulties = BuildDifficultyList(instrument);
            return _cachedDifficulties;
        }

        private static IReadOnlyList<string> BuildDifficultyList(Instrument instrument)
        {
            var counts = GetDifficultyCounts(instrument);
            var ordered = new List<string>(DifficultyLabelKeys.Length + 2);

            for (int i = 0; i < DifficultyLabelKeys.Length; i++)
            {
                var label = GetDifficultyLabelByIndex(i);
                if (counts.TryGetValue(label, out int count) && count > 0)
                    ordered.Add(label);
            }

            var unknownLabel = Localize.Key(DifficultyLabelUnknownKey);
            if (counts.TryGetValue(unknownLabel, out int unknownCount) && unknownCount > 0)
                ordered.Add(unknownLabel);

            var noPartLabel = Localize.Key(DifficultyLabelNoPartKey);
            if (counts.TryGetValue(noPartLabel, out int noPartCount) && noPartCount > 0)
                ordered.Add(noPartLabel);

            return ordered;
        }

        private static Dictionary<string, int> GetDifficultyCounts(Instrument instrument)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var song in SongContainer.Songs)
            {
                var label = GetDifficultyLabel(song, instrument);
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                dict.TryGetValue(label, out int count);
                dict[label] = count + 1;
            }

            return dict;
        }

        private static string GetDifficultyLabel(SongEntry entry, Instrument instrument)
        {
            var part = entry[instrument];
            if (!part.IsActive()) return Localize.Key(DifficultyLabelNoPartKey);

            int intensity = part.Intensity;
            if (intensity < 0) return Localize.Key(DifficultyLabelUnknownKey);

            if (intensity >= DifficultyLabelKeys.Length) return GetDifficultyLabelByIndex(DifficultyLabelKeys.Length - 1);

            return GetDifficultyLabelByIndex(intensity);
        }
#endregion

        private static string GetDifficultyLabelByIndex(int index)
        {
            if (index < 0) return null;
            if (index >= DifficultyLabelKeys.Length) index = DifficultyLabelKeys.Length - 1;

            return Localize.Key(DifficultyLabelKeys[index]);
        }

        public void ResetAllFilters()
        {
            EnsureAllDefaults();
            SetAllFilters(true);
            UpdateAllSummaries();

            // If right panel is visible, update toggles there too
            if (_rightContainer != null)
                foreach (var row in _rightContainer.GetComponentsInChildren<FilterEntryRow>(true))
                    row.SetToggleIsOn(true);
        }

        private static void SetAll(Dictionary<string, bool> dict, bool value)
        {
            var keys = dict.Keys.ToList();
            foreach (var key in keys)
                dict[key] = value;
        }

        private void SetAllFilters(bool value)
        {
            foreach (var def in GetFilterDefs())
                SetAll(def.Enabled, value);
        }

        private void ApplyRightToggleState(Dictionary<string, bool> dict, bool value, Action updateSummary)
        {
            SetAll(dict, value);
            updateSummary?.Invoke();
            DisableRecommendationsIfFiltered();

            if (_rightContainer == null) return;

            foreach (var row in _rightContainer.GetComponentsInChildren<FilterEntryRow>(true))
                row.SetToggleIsOn(value);
        }

        private void AddSelectDeselectButtons(Action selectAll, Action deselectAll)
        {
            if (_rightContainer == null) return;

            var prefab = GetSettingsButtonPrefab();
            if (prefab == null) return;

            var go = Instantiate(prefab, _rightContainer);
            var buttonRow = go.GetComponent<SettingsButton>();
            if (buttonRow == null) return;

            buttonRow.SetCustomButtons(new[]
            {
                new SettingsButton.CustomButton(Localize.Key("Menu.Filters.SelectAll"), selectAll),
                new SettingsButton.CustomButton(Localize.Key("Menu.Filters.DeselectAll"), deselectAll)
            });

            _rightNavGroup.AddNavigatable(buttonRow);
        }

        private static GameObject GetSettingsButtonPrefab()
        {
            if (_settingsButtonPrefab == null)
            {
                _settingsButtonPrefab = Addressables
                    .LoadAssetAsync<GameObject>("SettingTab/Button")
                    .WaitForCompletion();
            }

            return _settingsButtonPrefab;
        }

        private void DisableRecommendationsIfFiltered()
        {
            if (!SettingsManager.Settings.ShowRecommendedSongs.Value)
                return;

            foreach (var def in GetFilterDefs())
            {
                var values = def.GetValues();
                if (values.Count == 0)
                    continue;

                EnsureDefaults(def.Enabled, values);

                int total = values.Count;
                int selected = def.Enabled.Count(kvp => kvp.Value);
                if (selected != total)
                {
                    SettingsManager.Settings.ShowRecommendedSongs.Value = false;
                    if (_showRecommendationsToggle != null)
                        _showRecommendationsToggle.SetIsOnWithoutNotify(false);
                    break;
                }
            }
        }

        public void SetupSortedByDropdown(GameObject row, string label)
        {
            if (row == null)
                return;

            var visual = row.GetComponent<DropdownSettingVisual>();
            if (visual == null)
                return;

            if (_sortDropdownSetting == null)
            {
                _sortDropdownSetting = new FilterSortDropdownSetting(sort =>
                {
                    var library = FindFirstObjectByType<MusicLibrary.MusicLibraryMenu>();
                    if (library != null)
                    {
                        library.ChangeSort(sort);
                    }
                    else
                    {
                        if (sort != SortAttribute.Playcount && sort != SortAttribute.Stars)
                            SettingsManager.Settings.PreviousLibrarySort = sort;

                        SettingsManager.Settings.LibrarySort = sort;
                    }
                });
            }

            _sortDropdownSetting.UpdateValues();

            int index = FindSortIndex(_sortDropdownSetting.PossibleValues, SettingsManager.Settings.LibrarySort);
            if (index >= 0)
                _sortDropdownSetting.SetValueWithoutNotify(_sortDropdownSetting.PossibleValues[index]);

            visual.AssignPresetSetting("Filters.SortBy", false, _sortDropdownSetting);
            SetSortedByLabel(row, label);
        }

        public void SetupShowRecommendationsToggle(GameObject row, string label)
        {
            if (row == null)
                return;

            var visual = row.GetComponent<ToggleSettingVisual>();
            if (visual == null)
                return;

            visual.AssignPresetSetting("Filters.ShowRecommendations", false, SettingsManager.Settings.ShowRecommendedSongs);
            SetToggleLabel(row, label);

            _showRecommendationsToggle = row.GetComponentInChildren<Toggle>(true);
        }

        private static void SetSortedByLabel(GameObject row, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;

            var dropdown = row.GetComponentInChildren<TMP_Dropdown>(true);
            var caption = dropdown != null ? dropdown.captionText : null;
            var item = dropdown != null ? dropdown.itemText : null;

            foreach (var tmp in row.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp == null || tmp == caption || tmp == item)
                    continue;

                tmp.text = label;
                break;
            }
        }

        private static void SetToggleLabel(GameObject row, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;

            var tmp = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
                tmp.text = label;
        }

        private static int FindSortIndex(IReadOnlyList<SortAttribute> values, SortAttribute sort)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == sort)
                    return i;
            }

            return -1;
        }

        private void OnDisable()
        {
            if (!_ready) return;

            SaveFilters();
            ActiveFilterPredicate = BuildFilterPredicate();
            var library = FindFirstObjectByType<MusicLibrary.MusicLibraryMenu>();
            library?.SetSidebarDifficultiesVisible(true);
            library?.RefreshAndReselect();

            Navigator.Instance.PopScheme();
            _leftNavGroup.SelectionChanged -= OnSelectionChanged;
            _rightNavGroup.SelectionChanged -= OnRightSelectionChanged;

            MenuManager.Instance.ReactivateCurrentMenu();
        }
    }
}
