using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Core.Song;
using YARG.Menu.Dialogs;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
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

        public bool IsSearching => !string.IsNullOrEmpty(_fullSearchQuery);
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
            ClearFilterQueries();
        }

        public void Restore()
        {
            _searchField.text = _currentSearchText;
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
                    var currentQuery = $"{filter}:{_searchQueries[attribute]}";
                    _fullSearchQuery = _fullSearchQuery.Replace(currentQuery, updatedQuery);
                }
            }

            _currentSearchFilter = attribute;
            _searchQueries[_currentSearchFilter] = input;
            _searchField.text = _searchQueries[_currentSearchFilter];


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
        }

        public void OpenMoreFilters()
        {
            var filtersList = DialogManager.Instance.ShowList("More Filters");
            CreateInstrumentFilterButton(filtersList);
            CreateYearFilterButton(filtersList);
        }

        private void CreateInstrumentFilterButton(ListDialog list)
        {
            list.AddListButton(SongAttribute.Instrument.ToString(), () =>
            {
                DialogManager.Instance.ClearDialog();
                var instrumentFilerList = DialogManager.Instance.ShowList(SongAttribute.Instrument + "s");

                foreach (var instrument in GlobalVariables.Instance.SongContainer.Instruments.Keys)
                {
                    instrumentFilerList.AddListButton(instrument, () =>
                    {
                        SetSearchInput(SongAttribute.Instrument, instrument);
                        DialogManager.Instance.ClearDialog();
                    });
                }

                if (_fullSearchQuery.Contains(SongAttribute.Instrument.ToString().ToLowerInvariant()))
                {
                    instrumentFilerList.AddDialogButton("Remove Filter", () =>
                    {
                        ClearSearchQuery(SongAttribute.Instrument);
                        DialogManager.Instance.ClearDialog();
                    });
                }
            });
        }

        private void CreateYearFilterButton(ListDialog list)
        {
            list.AddListButton(SongAttribute.Year.ToString(), () =>
            {
                DialogManager.Instance.ClearDialog();
                var yearFilterList = DialogManager.Instance.ShowList(SongAttribute.Year + "s");
                var years = new List<int>();
                foreach (var song in GlobalVariables.Instance.SongContainer.Songs)
                {
                    if (years.Contains(song.YearAsNumber) || song.YearAsNumber == 0 || song.YearAsNumber == int.MaxValue)
                    {
                        continue;
                    }

                    years.Add(song.YearAsNumber);
                }

                years.Sort();
                foreach (var year in years)
                {
                    yearFilterList.AddListButton(year.ToString(), () =>
                    {
                        SetSearchInput(SongAttribute.Year, year.ToString());
                        DialogManager.Instance.ClearDialog();
                    });
                }

                if (_fullSearchQuery.Contains(SongAttribute.Year.ToString().ToLowerInvariant()))
                {
                    yearFilterList.AddDialogButton("Remove Filter", () =>
                    {
                        ClearSearchQuery(SongAttribute.Year);
                        DialogManager.Instance.ClearDialog();
                    });
                }
            });
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

            _fullSearchQuery = _fullSearchQuery.Replace(currentQuery, string.Empty);
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

                var toggleName = _currentSearchFilter switch
                {
                    SongAttribute.Name    => "track",
                    SongAttribute.Artist  => "artist",
                    SongAttribute.Album   => "album",
                    SongAttribute.Genre   => "genre",
                    SongAttribute.Source  => "source",
                    SongAttribute.Charter => "charter",
                    _                     => string.Empty,
                };

                if (!string.IsNullOrEmpty(toggleName))
                {
                    _searchFilters.ActivateButton(toggleName);
                }

                ClickedSearchFilter?.Invoke(true);
                return;
            }

            _currentSearchFilter = SongAttribute.Unspecified;
            _searchField.text = string.Empty;

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
