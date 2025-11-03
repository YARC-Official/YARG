using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Playlists;

namespace YARG.Menu.MusicLibrary
{
    public class PlaylistFilterDropdown : MonoBehaviour
    {
        [SerializeField]
        private GameObject _dropdownPanel;
        [SerializeField]
        private Transform _dropdownContent;
        [SerializeField]
        private GameObject _playlistItemPrefab;

        private readonly List<PlaylistItem> _playlistItems = new();
        private readonly HashSet<string> _selectedPlaylists = new();

        public event System.Action<HashSet<string>> OnSelectionChanged;

        private void Start()
        {
            _dropdownPanel.SetActive(false);
            PopulatePlaylistList();
        }

        public void ToggleDropdown()
        {
            bool newState = !_dropdownPanel.activeSelf;
            _dropdownPanel.SetActive(newState);

            if (newState)
            {
                RefreshPlaylistList();
            }
        }

        public void ShowDropdown()
        {
            _dropdownPanel.SetActive(true);
            RefreshPlaylistList();
        }

        public void HideDropdown()
        {
            _dropdownPanel.SetActive(false);
        }

        private void PopulatePlaylistList()
        {
            // Clear existing items
            foreach (var item in _playlistItems)
            {
                if (item != null && item.GameObject != null)
                {
                    Destroy(item.GameObject);
                }
            }
            _playlistItems.Clear();

            // Add all playlists
            foreach (var playlist in PlaylistContainer.Playlists)
            {
                CreatePlaylistItem(playlist);
            }
        }

        private void RefreshPlaylistList()
        {
            // Check if playlists have changed
            if (_playlistItems.Count != PlaylistContainer.Playlists.Count)
            {
                PopulatePlaylistList();
                return;
            }

            // Update checkboxes to match current selection
            foreach (var item in _playlistItems)
            {
                item.Checkbox.isOn = _selectedPlaylists.Contains(item.PlaylistName);
            }
        }

        private void CreatePlaylistItem(Playlist playlist)
        {
            var itemObject = Instantiate(_playlistItemPrefab, _dropdownContent);
            var toggle = itemObject.GetComponentInChildren<Toggle>();
            var text = itemObject.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = playlist.Name;
            }

            if (toggle != null)
            {
                toggle.isOn = _selectedPlaylists.Contains(playlist.Name);
                toggle.onValueChanged.AddListener((isOn) => OnPlaylistToggled(playlist.Name, isOn));
            }

            _playlistItems.Add(new PlaylistItem
            {
                GameObject = itemObject,
                PlaylistName = playlist.Name,
                Checkbox = toggle
            });
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

            OnSelectionChanged?.Invoke(_selectedPlaylists);
        }

        public HashSet<string> GetSelectedPlaylists()
        {
            return new HashSet<string>(_selectedPlaylists);
        }

        public void ClearSelection()
        {
            _selectedPlaylists.Clear();
            foreach (var item in _playlistItems)
            {
                if (item.Checkbox != null)
                {
                    item.Checkbox.isOn = false;
                }
            }
            OnSelectionChanged?.Invoke(_selectedPlaylists);
        }

        public void SetSelectedPlaylists(IEnumerable<string> playlists)
        {
            _selectedPlaylists.Clear();
            foreach (var playlist in playlists)
            {
                _selectedPlaylists.Add(playlist);
            }

            foreach (var item in _playlistItems)
            {
                if (item.Checkbox != null)
                {
                    item.Checkbox.isOn = _selectedPlaylists.Contains(item.PlaylistName);
                }
            }
        }

        public bool HasSelection => _selectedPlaylists.Count > 0;

        private class PlaylistItem
        {
            public GameObject GameObject;
            public string PlaylistName;
            public Toggle Checkbox;
        }
    }
}
