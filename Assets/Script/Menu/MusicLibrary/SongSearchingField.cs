using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Input;
using YARG.Core.Song;
using YARG.Menu.Navigation;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class SongSearchingField : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField _searchField;
        [SerializeField]
        private TextMeshProUGUI _searchPlaceholderText;
        [SerializeField]
        private ColoredButtonGroup _searchFilters;

        private readonly SongSearching _searchContext = new();
        private string _currentSearchText = string.Empty;
        private bool _searchNavPushed;
        private bool _wasSearchFieldFocused;

        public bool IsSearching => !string.IsNullOrEmpty(_fullSearchQuery);
        public bool IsCurrentSearchInField => _searchQueries[_currentSearchFilter] == _searchField.text;
        public bool IsUpdatedSearchLonger => _searchField.text.Length > _currentSearchText.Length;
        public bool IsUnspecified => _searchContext.IsUnspecified();

        public event Action<bool> OnSearchQueryUpdated;

        private static SongAttribute _currentSearchFilter = SongAttribute.Unspecified;
        private static Dictionary<SongAttribute, string> _searchQueries = new()
        {
            {SongAttribute.Unspecified, string.Empty},
            {SongAttribute.Name, string.Empty},
            {SongAttribute.Artist, string.Empty},
            {SongAttribute.Album, string.Empty},
            {SongAttribute.Genre, string.Empty},
            {SongAttribute.Source, string.Empty},
            {SongAttribute.Charter, string.Empty},
            {SongAttribute.Instrument, string.Empty},
            {SongAttribute.Year, string.Empty},
        };

        private static string _fullSearchQuery = string.Empty;

        /// <summary>
        /// Regex pattern to match a whole word
        /// - \b: Asserts a word boundary, matching the position between a word character (i.e., a letter,
        ///       digit, or underscore) and a non-word character (or vice versa).
        /// - \w+: Matches one or more word characters (i.e., letters, digits, or underscores).
        /// - \b: Asserts another word boundary.
        /// </summary>
        private static readonly Regex WholeWordRegex = new(@"\b\w+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void OnEnable()
        {
            _searchFilters.ClickedButton += OnClickedSearchFilter;
        }

        public void Focus()
        {
            _searchField.Select();
        }

        public void Restore()
        {
            _searchField.text = _searchQueries[_currentSearchFilter];

            _searchFilters.DeactivateAllButtons();
            ActivateFilterButton(_currentSearchFilter);
        }

        public void SetSearchInput(SongAttribute attribute, string input)
        {
            var filter = attribute.ToString().ToLowerInvariant();
            var updatedQuery = $"{filter}:{input}";

            if (string.IsNullOrEmpty(_fullSearchQuery) || _currentSearchFilter == SongAttribute.Unspecified)
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

        public IReadOnlyList<SongCategory> Refresh(SongAttribute sort)
        {
            _currentSearchText = _searchField.text = string.Empty;
            return _searchContext.Refresh(sort);
        }

        public IReadOnlyList<SongCategory> Search(SongAttribute sort)
        {
            if (_currentSearchFilter == SongAttribute.Unspecified)
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
                    _fullSearchQuery = Regex.Replace(_fullSearchQuery, currentQuery, updatedQuery, RegexOptions.IgnoreCase);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_searchField.text))
                    {
                        _fullSearchQuery = WholeWordRegex.Replace(_fullSearchQuery, $"{filter}:{_searchField.text}");
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
            _currentSearchFilter = SongAttribute.Unspecified;
            _searchQueries = new Dictionary<SongAttribute, string>
            {
                {SongAttribute.Unspecified, string.Empty},
                {SongAttribute.Name, string.Empty},
                {SongAttribute.Artist, string.Empty},
                {SongAttribute.Album, string.Empty},
                {SongAttribute.Genre, string.Empty},
                {SongAttribute.Source, string.Empty},
                {SongAttribute.Charter, string.Empty},
                {SongAttribute.Instrument, string.Empty},
                {SongAttribute.Year, string.Empty},
            };
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

            // Update the search bar pushing the empty navigation scheme.
            // We can't use the "OnSelect" event because for some reason it isn't called
            // if the user reselected the input field after pressing enter.
            if (_wasSearchFieldFocused != _searchField.isFocused)
            {
                _wasSearchFieldFocused = _searchField.isFocused;

                if (_wasSearchFieldFocused)
                {
                    if (_searchNavPushed) return;

                    _searchNavPushed = true;
                    Navigator.Instance.PushScheme(new NavigationScheme(new()
                    {
                        new NavigationScheme.Entry(MenuAction.Blue, "Cancel Search",
                            () => _searchField.DeactivateInputField()),
                    }, false));
                }
                else
                {
                    if (!_searchNavPushed) return;

                    _searchNavPushed = false;
                    Navigator.Instance.PopScheme();
                }
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
                "track"   => SongAttribute.Name,
                "artist"  => SongAttribute.Artist,
                "album"   => SongAttribute.Album,
                "genre"   => SongAttribute.Genre,
                "source"  => SongAttribute.Source,
                "charter" => SongAttribute.Charter,
                "instrument" => SongAttribute.Instrument,
                "year"       => SongAttribute.Year,
                _            => SongAttribute.Unspecified
            };

            if (previousSearchFilter == SongAttribute.Unspecified)
            {
                _searchQueries[SongAttribute.Unspecified] = string.Empty;
                _searchQueries[_currentSearchFilter] = _searchField.text;
            }
            else
            {
                _searchField.text = _searchQueries[_currentSearchFilter];
            }


            OnSearchQueryUpdated?.Invoke(true);
        }

        private void ActivateFilterButton(SongAttribute attribute)
        {
            var toggleName = attribute switch
            {
                SongAttribute.Name    => "track",
                SongAttribute.Artist  => "artist",
                SongAttribute.Album   => "album",
                SongAttribute.Genre   => "genre",
                SongAttribute.Source  => "source",
                SongAttribute.Charter => "charter",
                SongAttribute.Instrument => "instrument",
                SongAttribute.Year => "year",
                _                     => string.Empty,
            };

            if (!string.IsNullOrEmpty(toggleName))
            {
                _searchFilters.ActivateButton(toggleName);
            }
        }

        private void ClearSearchQuery(SongAttribute attribute)
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

            _currentSearchFilter = SongAttribute.Unspecified;
            _searchField.text = string.Empty;

            OnSearchQueryUpdated?.Invoke(true);
        }

        private void OnDisable()
        {
            _searchFilters.ClickedButton -= OnClickedSearchFilter;

            // Make sure to also pop the search nav if that was pushed
            if (Navigator.Instance == null)
            {
                _searchNavPushed = false;
            }

            if (!_searchNavPushed)
            {
                return;
            }

            Navigator.Instance.PopScheme();
            _searchNavPushed = false;
        }
    }
}
