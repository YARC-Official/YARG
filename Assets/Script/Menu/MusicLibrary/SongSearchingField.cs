using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongSearchingField : MonoBehaviour
    {
        private static string _fullSearchQuery = string.Empty;

        [SerializeField]
        private TMP_InputField _searchField;
        [SerializeField]
        private GameObject _focusBorder;
        [SerializeField]
        private Image _focusBackground;
        [SerializeField]
        private TextMeshProUGUI _searchPlaceholderText;
        private readonly SongSearching _searchContext = new();
        private string _currentSearchText = string.Empty;
        private bool _searchNavigationActive;

        public bool IsSearching => !string.IsNullOrEmpty(_fullSearchQuery);
        public bool IsCurrentSearchInField => _fullSearchQuery == _searchField.text;
        public bool IsUnspecified => _searchContext.IsUnspecified();
        public string FullSearchQuery => _fullSearchQuery;

        public event Action<bool> OnSearchQueryUpdated;

        private void OnEnable()
        {
            _searchField.onSelect.AddListener(OnSearchFieldSelected);
            _searchField.onDeselect.AddListener(OnSearchFieldDeselected);
            _searchField.onSubmit.AddListener(_ => ClearSearchFocus());

            _focusBorder.SetActive(_searchField.isFocused);
            _focusBackground.enabled = _searchField.isFocused;
        }

        public void Focus()
        {
            if (_searchField.gameObject.activeSelf)
            {
                _searchField.Select();
            }
        }

        public void Restore()
        {
            _searchField.text = _fullSearchQuery;
        }

        public void SetSearchInput(SortAttribute attribute, string input)
        {
            var filter = attribute.ToString().ToLowerInvariant();
            var updatedQuery = $"{filter}:{input}";
            var queries = _searchField.text.Split(';').ToList();
            int existingIndex = queries.FindIndex(query =>
                query.TrimStart().StartsWith(filter + ":", StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                // Replace only this attribute's clause, leaving the rest of the visible query intact.
                queries[existingIndex] = updatedQuery;
            }
            else
            {
                // Keep unqualified text last: SongSearching stops parsing filters after it reaches
                // an unrestricted search clause.
                int insertionIndex = queries.FindIndex(query =>
                    !string.IsNullOrWhiteSpace(query) && !IsQualifiedSearchClause(query));
                if (insertionIndex < 0)
                {
                    insertionIndex = queries.FindLastIndex(query => !string.IsNullOrWhiteSpace(query)) + 1;
                }
                queries.Insert(insertionIndex, updatedQuery);
            }

            _fullSearchQuery = string.Join(';', queries);
            if (!_fullSearchQuery.EndsWith(';'))
            {
                _fullSearchQuery += ';';
            }

            // Search criteria are expressed directly in the visible query.
            _searchField.text = _fullSearchQuery;
            OnSearchQueryUpdated?.Invoke(true);
        }

        private static bool IsQualifiedSearchClause(string query)
        {
            int separatorIndex = query.IndexOf(':');
            if (separatorIndex < 0)
            {
                return false;
            }

            var filter = query[..separatorIndex].Trim();
            if (filter.Equals("title", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!Enum.TryParse(filter, true, out SortAttribute attribute))
            {
                return false;
            }

            return attribute is SortAttribute.Name or SortAttribute.Artist or SortAttribute.Source or
                SortAttribute.Album or SortAttribute.Charter or SortAttribute.Year or SortAttribute.Genre or
                SortAttribute.Subgenre or SortAttribute.Folder or SortAttribute.AggregateDrums ||
                attribute >= SortAttribute.FiveFretGuitar;
        }

        public void UpdateSearchText()
        {
            _currentSearchText = _searchField.text;
        }

        public void Reset()
        {
            _searchContext.Reset();
        }

        public SongCategory[] Search(SortAttribute sort)
        {
            _fullSearchQuery = _searchField.text;

            return _searchContext.Search(_fullSearchQuery, sort);
        }

        public void ClearFilterQueries()
        {
            _fullSearchQuery = string.Empty;
            _searchField.text = string.Empty;

            OnSearchQueryUpdated?.Invoke(true);
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClearFilterQueries();
            }
        }

        private void OnDisable()
        {
            _searchField.onSelect.RemoveListener(OnSearchFieldSelected);
            _searchField.onDeselect.RemoveListener(OnSearchFieldDeselected);
            _searchField.onSubmit.RemoveListener(_ => ClearSearchFocus());
            DisableSearchNavigation();
        }

        private void OnSearchFieldSelected(string _)
        {
            _focusBorder.SetActive(true);
            _focusBackground.enabled = true;

            if (_searchNavigationActive)
            {
                return;
            }

            var scheme = new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Red, "Menu.MusicLibrary.ExitSearchHold",
                    handler: null, onHoldHandler: ClearSearchFocus, holdSeconds: 0.5f, hide: false),
            }, allowsMusicPlayer: null, popCallback: () => _searchNavigationActive = false);

            _searchNavigationActive = true;
            Navigator.Instance.PushTextInputScheme(scheme);
        }

        private void OnSearchFieldDeselected(string _)
        {
            _focusBorder.SetActive(false);
            _focusBackground.enabled = false;
            DisableSearchNavigation();
        }

        private static void ClearSearchFocus()
        {
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void DisableSearchNavigation()
        {
            if (!_searchNavigationActive)
            {
                return;
            }

            Navigator.Instance.PopScheme();
            _searchNavigationActive = false;
        }

        public bool HasInstrumentFilter(Instrument instrument)
        {
            var filter = instrument.ToSortAttribute().ToString().ToLowerInvariant() + ":";
            return _fullSearchQuery.StartsWith(filter) || _fullSearchQuery.Contains(";" + filter);
        }
    }
}
