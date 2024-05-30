using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Extensions;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongSearchingField : MonoBehaviour
    {
        private static SortAttribute _currentSearchFilter = SortAttribute.Unspecified;
        private static Dictionary<SortAttribute, string> _searchQueries;
        private static string _fullSearchQuery = string.Empty;

        static SongSearchingField()
        {
            _searchQueries = new Dictionary<SortAttribute, string>();
            foreach (var sort in EnumExtensions<SortAttribute>.Values)
            {
                if (sort != SortAttribute.Artist_Album &&
                    sort != SortAttribute.Playlist &&
                    sort != SortAttribute.SongLength &&
                    sort != SortAttribute.DateAdded &&
                    sort != SortAttribute.Playable &&
                    sort != SortAttribute.Instrument)
                {
                    _searchQueries.Add(sort, string.Empty);
                }
            }
        }

        [SerializeField]
        private TMP_InputField _searchField;
        [SerializeField]
        private TextMeshProUGUI _searchPlaceholderText;
        [SerializeField]
        private ColoredButtonGroup _searchFilters;

        private readonly SongSearching _searchContext = new();
        private string _currentSearchText = string.Empty;

        public bool IsSearching => !string.IsNullOrEmpty(_fullSearchQuery);
        public bool IsCurrentSearchInField => _searchQueries[_currentSearchFilter] == _searchField.text;
        public bool IsUpdatedSearchLonger => _searchField.text.Length > _currentSearchText.Length;
        public bool IsUnspecified => _searchContext.IsUnspecified();

        public event Action<bool> OnSearchQueryUpdated;

        private void OnEnable()
        {
            _searchFilters.ClickedButton += OnClickedSearchFilter;
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
            _searchField.text = _searchQueries[_currentSearchFilter];

            _searchFilters.DeactivateAllButtons();
            ActivateFilterButton(_currentSearchFilter);
        }

        public void SetSearchInput(SortAttribute attribute, string input)
        {
            if (attribute == SortAttribute.Unspecified)
            {
                _fullSearchQuery = input;
            }
            else
            {
                var filter = attribute.ToString().ToLowerInvariant();
                var updatedQuery = $"{filter}:{input}";

                if (string.IsNullOrEmpty(_fullSearchQuery) || _currentSearchFilter == SortAttribute.Unspecified)
                {
                    _fullSearchQuery = updatedQuery;
                }
                else
                {
                    if (!_fullSearchQuery.Contains(filter))
                    {
                        _fullSearchQuery += ";" + updatedQuery;
                    }
                    else
                    {
                        // Regex pattern: The filter specified and the search query tagged with that filter
                        var currentQuery = $"{filter}:{_searchQueries[attribute]}";
                        _fullSearchQuery = Regex.Replace(_fullSearchQuery, currentQuery, updatedQuery,
                            RegexOptions.IgnoreCase);
                    }
                }
            }

            _currentSearchFilter = attribute;
            _searchQueries[_currentSearchFilter] = input;
            _searchField.text = _searchQueries[_currentSearchFilter];

            ActivateFilterButton(_currentSearchFilter);

            OnSearchQueryUpdated?.Invoke(true);
        }

        public void UpdateSearchText()
        {
            _currentSearchText = _searchField.text;
        }

        public void ClearList()
        {
            _searchContext.ClearList();
        }

        public IReadOnlyList<SongCategory> Search(SortAttribute sort)
        {
            if (_currentSearchFilter == SortAttribute.Unspecified)
            {
                _searchQueries[_currentSearchFilter] = _searchField.text;
                _fullSearchQuery = _searchQueries[_currentSearchFilter];
            }
            else
            {
                var filter = _currentSearchFilter.ToString().ToLowerInvariant();

                // Regex pattern representing a word boundary around the filter value
                string filterFoundPattern = $@"\b{filter}\b";

                if (Regex.IsMatch(_fullSearchQuery, filterFoundPattern))
                {
                    var currentQuery = $"{filter}:{_searchQueries[_currentSearchFilter]}";
                    var updatedQuery = $"{filter}:{_searchField.text}";
                    _fullSearchQuery = _fullSearchQuery.Replace(currentQuery, updatedQuery, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_searchField.text))
                    {
                        _fullSearchQuery = $"{filter}:{_searchField.text}";
                    }
                    else if (string.IsNullOrEmpty(_fullSearchQuery))
                    {
                        _fullSearchQuery = $"{filter}:";
                    }
                    else
                    {
                        _fullSearchQuery += $";{filter}:";
                    }
                }

                _searchQueries[_currentSearchFilter] = _searchField.text;
            }

            return _searchContext.Search(_fullSearchQuery, sort);
        }

        public void ClearFilterQueries()
        {
            _currentSearchFilter = SortAttribute.Unspecified;
            foreach (var filter in _searchQueries.Keys.ToArray())
            {
                _searchQueries[filter] = string.Empty;
            }
            _fullSearchQuery = string.Empty;
            _searchField.text = string.Empty;

            _searchFilters.DeactivateAllButtons();
            OnSearchQueryUpdated?.Invoke(true);
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClearFilterQueries();
            }
        }

        private void OnClickedSearchFilter()
        {
            var button = _searchFilters.ActiveButton;
            if (button == null)
            {
                ClearSearchQuery(_currentSearchFilter);
                return;
            }

            var previousSearchFilter = _currentSearchFilter;

            _currentSearchFilter = button.Text.text.ToLowerInvariant() switch
            {
                "track"   => SortAttribute.Name,
                "artist"  => SortAttribute.Artist,
                "album"   => SortAttribute.Album,
                "genre"   => SortAttribute.Genre,
                "source"  => SortAttribute.Source,
                "charter" => SortAttribute.Charter,
                "year"    => SortAttribute.Year,
                _         => SortAttribute.Unspecified
            };

            if (previousSearchFilter == SortAttribute.Unspecified)
            {
                _searchQueries[SortAttribute.Unspecified] = string.Empty;
                _searchQueries[_currentSearchFilter] = _searchField.text;
            }
            else
            {
                _searchField.text = _searchQueries[_currentSearchFilter];
            }


            OnSearchQueryUpdated?.Invoke(true);
        }

        private void ActivateFilterButton(SortAttribute attribute)
        {
            var toggleName = attribute switch
            {
                SortAttribute.Name    => "track",
                SortAttribute.Artist  => "artist",
                SortAttribute.Album   => "album",
                SortAttribute.Genre   => "genre",
                SortAttribute.Source  => "source",
                SortAttribute.Charter => "charter",
                SortAttribute.Year    => "year",
                _                     => string.Empty,
            };

            if (!string.IsNullOrEmpty(toggleName))
            {
                _searchFilters.ActivateButton(toggleName);
            }
        }

        private void ClearSearchQuery(SortAttribute attribute)
        {
            var filter = attribute.ToString().ToLowerInvariant();
            string currentQuery = $"{filter}:{_searchQueries[attribute]}";
            if (_fullSearchQuery.Contains($";{filter}"))
            {
                currentQuery = $";{filter}:{_searchQueries[attribute]}";
            }
            else if (_fullSearchQuery.Contains($"{_searchQueries[attribute]};"))
            {
                currentQuery = $"{filter}:{_searchQueries[attribute]};";
            }

            // Include the special characters in the removal of the current query
            currentQuery = Regex.Escape(currentQuery);
            _fullSearchQuery = Regex.Replace(_fullSearchQuery, currentQuery, string.Empty);

            _searchQueries[attribute] = string.Empty;
            foreach (var query in _searchQueries)
            {
                filter = query.Key.ToString().ToLowerInvariant();
                if (!_fullSearchQuery.Contains(filter))
                {
                    continue;
                }

                _currentSearchFilter = query.Key;
                _searchField.text = query.Value;

                ActivateFilterButton(_currentSearchFilter);

                OnSearchQueryUpdated?.Invoke(true);
                return;
            }

            _currentSearchFilter = SortAttribute.Unspecified;
            _searchField.text = string.Empty;

            OnSearchQueryUpdated?.Invoke(true);
        }

        private void OnDisable()
        {
            _searchFilters.ClickedButton -= OnClickedSearchFilter;
        }
    }
}
