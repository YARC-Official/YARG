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
    [Serializable]
    public struct SearchToggle
    {
        public SongAttribute attribute;
        public ColoredToggle toggle;
    }

    public class SongSearchingField : MonoBehaviour
    {
        [SerializeField]
        private TMP_InputField _searchField;
        [SerializeField]
        private ToggleGroup _searchToggleGroup;
        [SerializeField]
        private List<SearchToggle> _searchToggles;
        [SerializeField]
        private TextMeshProUGUI _searchPlaceholderText;

        private readonly Song.SongSearching _searchContext = new();
        private string _currentSearchText = string.Empty;
        private bool _searchNavPushed;
        private bool _wasSearchFieldFocused;

        public bool IsSearching => !string.IsNullOrEmpty(_searchField.text);
        public bool IsCurrentSearchInField => _currentSearchText == _searchField.text;
        public bool IsUpdatedSearchLonger => _searchField.text.Length > _currentSearchText.Length;
        public bool IsUnspecified => _searchContext.IsUnspecified();

        public void Restore()
        {
            _searchField.text = _currentSearchText;
        }

        public void SetSearchInput(string query)
        {
            _searchField.text = query;
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
            return _searchContext.Search(_searchField.text, sort);
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

        private void OnEnable()
        {
            foreach (var searchToggle in _searchToggles)
            {
                var toggle = searchToggle.toggle;
                toggle.OnToggled.RemoveAllListeners();
                toggle.OnToggled.AddListener(toggle.SetBackgroundAndTextColor);
                toggle.OnToggled.AddListener(isOn => OnToggle(isOn, searchToggle.attribute));
            }
        }

        private void OnDisable()
        {
            // Make sure to also pop the search nav if that was pushed
            if (_searchNavPushed)
            {
                Navigator.Instance.PopScheme();
                _searchNavPushed = false;
            }
        }

        private void OnToggle(bool isOn, SongAttribute attribute)
        {
            if (isOn)
            {
                var filter = attribute switch
                {
                    SongAttribute.Name => "a song",
                    SongAttribute.Artist => "an artist",
                    SongAttribute.Album => "an album",
                    SongAttribute.Genre => "a genre",
                    SongAttribute.Source => "a source",
                    SongAttribute.Charter => "a charter",
                };

                _searchPlaceholderText.text = $"Search {filter}";
            }
            else
            {
                _searchPlaceholderText.text = "Search...";
            }
        }
    }
}
