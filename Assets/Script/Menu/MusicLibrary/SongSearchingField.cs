using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Core.Song;
using YARG.Menu.Navigation;
using YARG.Song;

namespace YARG.Menu.SongSearching
{
    public class SongSearchingField : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField _searchField;
        [SerializeField]
        private TextMeshProUGUI _searchPlaceholderText;
        [SerializeField]
        private ColoredButtonGroup _searchFilters;

        private readonly Song.SongSearching _searchContext = new();
        private string _currentSearchText = string.Empty;
        private bool _searchNavPushed;
        private bool _wasSearchFieldFocused;

        public bool IsSearching => !string.IsNullOrEmpty(_searchField.text);
        public bool IsCurrentSearchInField => _currentSearchText == _searchField.text;
        public bool IsUpdatedSearchLonger => _searchField.text.Length > _currentSearchText.Length;
        public bool IsUnspecified => _searchContext.IsUnspecified();

        public delegate void OnSearchFilterClicked(bool forceUpdate);
        public OnSearchFilterClicked ClickedSearchFilter;

        private SongAttribute _currentSearchFilter = SongAttribute.Unspecified;
        private Dictionary<SongAttribute, string> _searchQueries;
        private string _fullSearchQuery = string.Empty;

        private void OnEnable()
        {
            _searchFilters.ClickedButton += OnClickedSearchFilter;
            _searchQueries = new Dictionary<SongAttribute, string>
            {
                {SongAttribute.Unspecified, string.Empty},
                {SongAttribute.Name, string.Empty},
                {SongAttribute.Artist, string.Empty},
                {SongAttribute.Album, string.Empty},
                {SongAttribute.Genre, string.Empty},
                {SongAttribute.Source, string.Empty},
                {SongAttribute.Charter, string.Empty},
            };
        }

        public void Restore()
        {
            _searchField.text = _currentSearchText;
        }

        public void SetSearchInput(string query)
        {
            _currentSearchFilter = SongAttribute.Unspecified;
            _searchField.text = query;
        }

        public void SetSearchInput(SongAttribute attribute, string input)
        {
            _currentSearchFilter = attribute;
            _searchQueries[_currentSearchFilter] = input;
            _searchField.text = _searchQueries[_currentSearchFilter];

            var filter = _currentSearchFilter.ToString().ToLowerInvariant();

            if (string.IsNullOrEmpty(_fullSearchQuery))
            {
                _fullSearchQuery = $"{filter}:{input}";
            }
            else
            {
                _fullSearchQuery += $";{filter}:{input}";
            }

            var toggleName = _currentSearchFilter switch
            {
                SongAttribute.Name    => "track",
                SongAttribute.Artist  => "artist",
                SongAttribute.Album   => "album",
                SongAttribute.Genre   => "genre",
                SongAttribute.Source  => "source",
                SongAttribute.Charter => "charter",
                _ => string.Empty,
            };

            if (!string.IsNullOrEmpty(toggleName))
            {
                _searchFilters.ActivateButton(toggleName);
            }

            ClickedSearchFilter?.Invoke(true);
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
                var currentQuery = $"{filter}:{_searchQueries[_currentSearchFilter]}";
                var updatedQuery = $"{filter}:{_searchField.text}";

                _searchQueries[_currentSearchFilter] = _searchField.text;

                if (_fullSearchQuery.Contains(filter))
                {
                    _fullSearchQuery = _fullSearchQuery.Replace(currentQuery, updatedQuery);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_searchField.text))
                    {
                        _fullSearchQuery = _fullSearchQuery.Replace(_searchField.text, $"{filter}:{_searchField.text}");
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(_fullSearchQuery))
                        {
                            _fullSearchQuery = $"{filter}:";
                        }
                        else
                        {
                            _fullSearchQuery += $";{filter}:";
                        }
                    }
                }
            }

            Debug.Log(_fullSearchQuery);
            return _searchContext.Search(_fullSearchQuery, sort);
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _searchField.text = string.Empty;
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
                    Navigator.Instance.PushScheme(NavigationScheme.Empty);
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
                var filter = _currentSearchFilter.ToString().ToLowerInvariant();
                string currentQuery = $"{filter}:{_searchQueries[_currentSearchFilter]}";
                if (_fullSearchQuery.Contains($";{filter}"))
                {
                    currentQuery = $";{filter}:{_searchQueries[_currentSearchFilter]}";
                }
                else if (_fullSearchQuery.Contains($"{_searchQueries[_currentSearchFilter]};"))
                {
                    currentQuery = $"{filter}:{_searchQueries[_currentSearchFilter]};";
                }

                _fullSearchQuery = _fullSearchQuery.Replace(currentQuery, string.Empty);
                _searchQueries[_currentSearchFilter] = string.Empty;
                _currentSearchFilter = SongAttribute.Unspecified;
                foreach (var query in _searchQueries)
                {
                    filter = query.Key.ToString().ToLowerInvariant();
                    if (!_fullSearchQuery.Contains(filter))
                    {
                        continue;
                    }

                    _currentSearchFilter = query.Key;
                    _searchField.text = query.Value;

                    var toggleName = _currentSearchFilter switch
                    {
                        SongAttribute.Name    => "track",
                        SongAttribute.Artist  => "artist",
                        SongAttribute.Album   => "album",
                        SongAttribute.Genre   => "genre",
                        SongAttribute.Source  => "source",
                        SongAttribute.Charter => "charter",
                        _ => string.Empty,
                    };

                    if (!string.IsNullOrEmpty(toggleName))
                    {
                        _searchFilters.ActivateButton(toggleName);
                    }

                    break;
                }

                ClickedSearchFilter?.Invoke(true);
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
                _         => SongAttribute.Unspecified
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


            ClickedSearchFilter?.Invoke(true);
        }

        private void OnDisable()
        {
            // Make sure to also pop the search nav if that was pushed
            if (_searchNavPushed)
            {
                Navigator.Instance.PopScheme();
                _searchNavPushed = false;
            }

            _searchFilters.ClickedButton -= OnClickedSearchFilter;
        }
    }
}
