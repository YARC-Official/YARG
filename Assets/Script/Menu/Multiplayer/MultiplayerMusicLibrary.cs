using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Core.Song;
using YARG.Networking;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Handles multiplayer-specific music library functionality.
    /// Shows selected song and "Start Song" button for host.
    /// </summary>
    public class MultiplayerMusicLibrary : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject multiplayerPanel;
        [SerializeField] private TextMeshProUGUI selectedSongText;
        [SerializeField] private Button startSongButton;
        [SerializeField] private TextMeshProUGUI waitingText;

        private SongEntry _selectedSong;
        private bool _isHost;

        private void Start()
        {
            // Check if we're in multiplayer mode
            if (YargNetworkManager.Instance == null || !YargNetworkManager.Instance.isNetworkActive)
            {
                // Not in multiplayer, hide panel
                if (multiplayerPanel != null)
                {
                    multiplayerPanel.SetActive(false);
                }
                return;
            }

            _isHost = YargNetworkManager.Instance.IsHosting;

            // Show multiplayer panel
            if (multiplayerPanel != null)
            {
                multiplayerPanel.SetActive(true);
            }

            // Wire up start button
            if (startSongButton != null)
            {
                startSongButton.onClick.AddListener(OnStartSongClicked);
                startSongButton.gameObject.SetActive(_isHost);
            }

            // Show appropriate text
            if (waitingText != null)
            {
                waitingText.gameObject.SetActive(!_isHost);
            }

            if (selectedSongText != null)
            {
                selectedSongText.text = "No song selected";
            }

            // Subscribe to song selection events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnSongSelected += OnSongSelected;
            }
        }

        private void OnDestroy()
        {
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnSongSelected -= OnSongSelected;
            }
        }

        /// <summary>
        /// Called when host or any player selects a song.
        /// </summary>
        public void OnSongSelected(SongEntry song)
        {
            _selectedSong = song;

            if (selectedSongText != null)
            {
                selectedSongText.text = $"Selected: {song.Name} by {song.Artist}";
            }

            // Enable start button if we're host
            if (startSongButton != null && _isHost)
            {
                startSongButton.interactable = true;
            }
        }

        private void OnStartSongClicked()
        {
            if (!_isHost || _selectedSong == null)
            {
                Debug.LogWarning("[MultiplayerMusicLibrary] Cannot start song - not host or no song selected");
                return;
            }

            Debug.Log($"[MultiplayerMusicLibrary] Host starting song: {_selectedSong.Name}");

            // Set global state for local host
            GlobalVariables.State.CurrentSong = _selectedSong;
            GlobalVariables.State.ShowSongs.Clear();
            GlobalVariables.State.ShowSongs.Add(_selectedSong);
            GlobalVariables.State.PlayingAShow = false;

            // Navigate host to difficulty select
            MenuManager.Instance.PushMenu(MenuManager.Menu.DifficultySelect);

            // Tell network manager to start song for all clients
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.StartMultiplayerSong(_selectedSong);
            }
        }
    }
}
