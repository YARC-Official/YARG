using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Playlists;

namespace YARG.Menu.MusicLibrary
{
    public class PlaylistFilterPopup : MonoBehaviour
    {
        [SerializeField]
        private PlaylistFilterPopupItem _menuItemPrefab;
        [SerializeField]
        private GameObject _header;
        [SerializeField]
        private TextMeshProUGUI _headerText;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private NavigationGroup _navGroup;

        private HashSet<string> _selectedPlaylists = new();
        private List<PlaylistFilterPopupItem> _menuItems = new();

        public event System.Action<HashSet<string>> OnSelectionChanged;

        private void OnEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () =>
                {
                    gameObject.SetActive(false);
                })
            }, false));

            PopulatePlaylistList();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public void Show(HashSet<string> currentSelection)
        {
            _selectedPlaylists = new HashSet<string>(currentSelection);
            gameObject.SetActive(true);
        }

        private void PopulatePlaylistList()
        {
            // Clear existing items
            _navGroup.ClearNavigatables();
            _container.DestroyChildren();
            _menuItems.Clear();

            // Set header
            SetHeader(Localize.Key("Menu.MusicLibrary.Popup.Header", "SelectPlaylists"));

            int playlistCount = 0;

            // Add Favorites playlist first (only if it has songs)
            if (PlaylistContainer.FavoritesPlaylist != null && PlaylistContainer.FavoritesPlaylist.Count > 0)
            {
                var favItem = Instantiate(_menuItemPrefab, _container);
                bool isFavSelected = _selectedPlaylists.Contains(PlaylistContainer.FavoritesPlaylist.Name);
                favItem.Initialize(PlaylistContainer.FavoritesPlaylist.Name, isFavSelected, OnPlaylistToggled);
                _navGroup.AddNavigatable(favItem.Button);
                _menuItems.Add(favItem);
                playlistCount++;
            }

            // Add all other playlists
            foreach (var playlist in PlaylistContainer.Playlists)
            {
                var item = Instantiate(_menuItemPrefab, _container);
                bool isSelected = _selectedPlaylists.Contains(playlist.Name);
                item.Initialize(playlist.Name, isSelected, OnPlaylistToggled);
                _navGroup.AddNavigatable(item.Button);
                _menuItems.Add(item);
                playlistCount++;
            }

            // If no playlists exist, show a message item
            if (playlistCount == 0)
            {
                var emptyItem = Instantiate(_menuItemPrefab, _container);
                emptyItem.Initialize("No playlists found. Favorite songs or create a playlist first.", false, (name, selected) => { });
                _navGroup.AddNavigatable(emptyItem.Button);
                _menuItems.Add(emptyItem);
            }

            _navGroup.SelectFirst();
        }

        private void OnPlaylistToggled(string playlistName, bool isSelected)
        {
            if (isSelected)
            {
                _selectedPlaylists.Add(playlistName);
            }
            else
            {
                _selectedPlaylists.Remove(playlistName);
            }

            // Notify listeners that the selection changed
            OnSelectionChanged?.Invoke(_selectedPlaylists);
        }

        private void SetHeader(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                _header.SetActive(false);
            }
            else
            {
                _header.SetActive(true);
                _headerText.text = text;
            }
        }

        public void ClearSelection()
        {
            _selectedPlaylists.Clear();
            foreach (var item in _menuItems)
            {
                item.SetSelected(false);
            }
            OnSelectionChanged?.Invoke(_selectedPlaylists);
        }
    }
}
