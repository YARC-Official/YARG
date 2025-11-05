using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Core.Input;
using YARG.Networking;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Lobby waiting room where players gather before browsing songs.
    /// Host can start song selection when ready.
    /// </summary>
    public class LobbyRoomMenu : MonoBehaviour
    {
        private static bool _isQuitting = false;
        
        [Header("Lobby Info")]
        [SerializeField] private TextMeshProUGUI lobbyNameText;
        [SerializeField] private TextMeshProUGUI hostNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI lobbyCodeText;
        [SerializeField] private TextMeshProUGUI connectionInfoText;
        [SerializeField] private RectTransform hostAddressPanelParent;
        [SerializeField] private GameObject hostAddressPanel;
        [SerializeField] private GameObject lanAddressRow;
        [SerializeField] private TextMeshProUGUI lanAddressValueText;
        [SerializeField] private Button lanVisibilityToggleButton;
        [SerializeField] private TextMeshProUGUI lanVisibilityToggleLabel;
        [SerializeField] private Button lanCopyButton;
        [SerializeField] private GameObject wanAddressRow;
        [SerializeField] private TextMeshProUGUI wanAddressValueText;
        [SerializeField] private Button wanVisibilityToggleButton;
        [SerializeField] private TextMeshProUGUI wanVisibilityToggleLabel;
        [SerializeField] private Button wanCopyButton;
        [Header("Visibility Icons")]
        [SerializeField] private Sprite visibilityVisibleSprite;
        [SerializeField] private Sprite visibilityHiddenSprite;
        
        [Header("Player List")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerEntryPrefab;
        
        [Header("Controls")]
        [SerializeField] private Button browseSongsButton; // Host only
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private TextMeshProUGUI waitingForHostText; // Client only

        private bool isHost = false;
        private NetworkPlayerData selectedPlayer = null;
        private PlayerView selectedPlayerView = null;
        private System.Collections.Generic.Dictionary<NetworkPlayerData, PlayerView> playerViews = new System.Collections.Generic.Dictionary<NetworkPlayerData, PlayerView>();
        private bool _waitingForSongSync;
        private string _defaultWaitingForHostText;
        private bool _hostPanelListenersBound;
        private string _lanAddress = string.Empty;
        private string _wanAddress = string.Empty;
        private bool _lanVisible;
        private bool _wanVisible;

        private void Start()
        {
            Debug.Log("[LobbyRoomMenu] Start called - subscribing to events");

            EnsureHostAddressPanel();
            
            // Wire up button onClick events
            if (browseSongsButton != null)
            {
                browseSongsButton.onClick.AddListener(OnBrowseSongsClicked);
                Debug.Log("[LobbyRoomMenu] Wired up BrowseSongsButton onClick");
            }
            else
            {
                Debug.LogWarning("[LobbyRoomMenu] browseSongsButton is NULL!");
            }
            
            if (leaveLobbyButton != null)
            {
                leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
                Debug.Log("[LobbyRoomMenu] Wired up LeaveLobbyButton onClick");
            }
            else
            {
                Debug.LogWarning("[LobbyRoomMenu] leaveLobbyButton is NULL!");
            }

            if (waitingForHostText != null)
            {
                _defaultWaitingForHostText = waitingForHostText.text;
            }
            
            // Subscribe to network events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyLeft += OnLobbyLeft;
                YargNetworkManager.Instance.OnNetworkError += OnNetworkError;
                YargNetworkManager.Instance.OnLobbyJoined += OnLobbyInfoUpdated;
                YargNetworkManager.Instance.OnSharedSongSyncStateChanged += OnSharedSongSyncStateChanged;
                Debug.Log("[LobbyRoomMenu] Subscribed to YargNetworkManager events");
                // TODO: Subscribe to player joined/left events when implemented

                OnSharedSongSyncStateChanged(YargNetworkManager.Instance.IsSharedSongSyncComplete);
            }
            else
            {
                Debug.LogWarning("[LobbyRoomMenu] YargNetworkManager.Instance is NULL in Start!");
            }

            // Check if we have a valid lobby before trying to refresh
            // This can happen if the GameObject is active by default in the scene
            if (YargNetworkManager.Instance == null || YargNetworkManager.Instance.CurrentLobby == null)
            {
                Debug.Log("[LobbyRoomMenu] LobbyRoomMenu started without a lobby (probably during scene init). This is normal if the GameObject is active by default.");
                // Just return silently - OnEnable will call RefreshLobbyInfo when menu is actually opened
                return;
            }

            RefreshLobbyInfo();
        }

        private void OnEnable()
        {
            Debug.Log("[LobbyRoomMenu] OnEnable called");

            _waitingForSongSync = false;
            
            // Subscribe to player join/leave events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnPlayerJoined += OnPlayerJoinedLobby;
                YargNetworkManager.Instance.OnPlayerLeft += OnPlayerLeftLobby;
            }
            
            // RefreshLobbyInfo will call UpdateNavigationScheme after setting isHost
            RefreshLobbyInfo();

            if (YargNetworkManager.Instance != null)
            {
                OnSharedSongSyncStateChanged(YargNetworkManager.Instance.IsSharedSongSyncComplete);
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from player events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnPlayerJoined -= OnPlayerJoinedLobby;
                YargNetworkManager.Instance.OnPlayerLeft -= OnPlayerLeftLobby;
            }
            
            // Pop scheme - try/catch in case stack is empty
            try
            {
                Navigator.Instance?.PopScheme();
            }
            catch (System.InvalidOperationException)
            {
                // Stack was empty, this is fine
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyLeft -= OnLobbyLeft;
                YargNetworkManager.Instance.OnNetworkError -= OnNetworkError;
                YargNetworkManager.Instance.OnLobbyJoined -= OnLobbyInfoUpdated;
                YargNetworkManager.Instance.OnSharedSongSyncStateChanged -= OnSharedSongSyncStateChanged;
            }

            if (lanVisibilityToggleButton != null)
            {
                lanVisibilityToggleButton.onClick.RemoveListener(ToggleLanVisibility);
            }

            if (lanCopyButton != null)
            {
                lanCopyButton.onClick.RemoveListener(CopyLanAddress);
            }

            if (wanVisibilityToggleButton != null)
            {
                wanVisibilityToggleButton.onClick.RemoveListener(ToggleWanVisibility);
            }

            if (wanCopyButton != null)
            {
                wanCopyButton.onClick.RemoveListener(CopyWanAddress);
            }
        }
        
        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }
        
        private void UpdateNavigationScheme()
        {
            // Pop existing scheme if there is one (not on first call)
            try
            {
                Navigator.Instance?.PopScheme();
            }
            catch (System.InvalidOperationException)
            {
                // Stack was empty, this is fine on first call
            }

            var entries = new System.Collections.Generic.List<NavigationScheme.Entry>
            {
                NavigationScheme.Entry.NavigateSelect,
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                new NavigationScheme.Entry(MenuAction.Red, "Leave Lobby", OnLeaveLobbyClicked)
            };
            
            if (isHost)
            {
                entries.Add(new NavigationScheme.Entry(MenuAction.Yellow, "Browse Songs", OnBrowseSongsClicked));
                if (selectedPlayer != null && !selectedPlayer.IsLocalUser)
                {
                    entries.Add(new NavigationScheme.Entry(MenuAction.Blue, "Kick Player", OnKickPlayerClicked));
                }
            }
            
            Navigator.Instance?.PushScheme(new NavigationScheme(entries, true));
        }
        
        public void OnPlayerSelected(NetworkPlayerData player)
        {
            // Deselect previous player view
            if (selectedPlayerView != null)
            {
                selectedPlayerView.SetSelected(false);
            }
            
            selectedPlayer = player;
            
            // Select new player view
            if (playerViews.TryGetValue(player, out var playerView))
            {
                selectedPlayerView = playerView;
                selectedPlayerView.SetSelected(true);
            }
            
            UpdateNavigationScheme();
        }
        
        public void OnPlayerDeselected()
        {
            // Deselect current player view
            if (selectedPlayerView != null)
            {
                selectedPlayerView.SetSelected(false);
                selectedPlayerView = null;
            }
            
            selectedPlayer = null;
            UpdateNavigationScheme();
        }
        
        private void OnLobbyInfoUpdated(YargNetworkManager.LobbyInfo lobby)
        {
            Debug.Log($"[LobbyRoomMenu] Lobby info updated: {lobby.lobbyName}");
            RefreshLobbyInfo();
        }
        
        private void OnPlayerJoinedLobby(NetworkPlayerData player)
        {
            Debug.Log($"[LobbyRoomMenu] Player joined: {player.PlayerName}");
            
            // Update player count from actual connection count
            if (YargNetworkManager.Instance.CurrentLobby != null && Mirror.NetworkServer.active)
            {
                YargNetworkManager.Instance.CurrentLobby.currentPlayers = Mirror.NetworkServer.connections.Count;
                Debug.Log($"[LobbyRoomMenu] Updated player count from connections to: {YargNetworkManager.Instance.CurrentLobby.currentPlayers}");
            }
            
            // Refresh for both host and clients
            RefreshLobbyInfo();
        }
        
        private void OnPlayerLeftLobby(NetworkPlayerData player)
        {
            Debug.Log($"[LobbyRoomMenu] Player left: {player.PlayerName}");
            
            // Update player count from actual connection count
            if (YargNetworkManager.Instance.CurrentLobby != null && Mirror.NetworkServer.active)
            {
                YargNetworkManager.Instance.CurrentLobby.currentPlayers = Mirror.NetworkServer.connections.Count;
                Debug.Log($"[LobbyRoomMenu] Updated player count from connections to: {YargNetworkManager.Instance.CurrentLobby.currentPlayers}");
            }
            
            // Delay refresh slightly to allow the NetworkPlayerData object to be destroyed
            StartCoroutine(RefreshAfterDelay());
        }
        
        private System.Collections.IEnumerator RefreshAfterDelay()
        {
            // Wait one frame for the player object to be destroyed
            yield return null;
            RefreshLobbyInfo();
        }

        private void OnSharedSongSyncStateChanged(bool ready)
        {
            if (isHost && !ready)
            {
                _waitingForSongSync = true;
            }

            bool disableBrowse = !ready || _waitingForSongSync;

            if (browseSongsButton != null)
            {
                browseSongsButton.interactable = isHost && !disableBrowse;
            }

            if (waitingForHostText != null)
            {
                if (isHost)
                {
                    if (_waitingForSongSync && !ready)
                    {
                        waitingForHostText.text = "Syncing shared song library...";
                        waitingForHostText.gameObject.SetActive(true);
                    }
                    else
                    {
                        waitingForHostText.text = _defaultWaitingForHostText;
                        waitingForHostText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    waitingForHostText.text = !string.IsNullOrEmpty(_defaultWaitingForHostText)
                        ? _defaultWaitingForHostText
                        : "Waiting for host...";
                    waitingForHostText.gameObject.SetActive(!ready);
                }
            }

            if (ready && _waitingForSongSync)
            {
                _waitingForSongSync = false;
                Debug.Log("[LobbyRoomMenu] Shared song sync complete; music library will open momentarily.");
            }
        }

        private void RefreshLobbyInfo()
        {
            Debug.Log("[LobbyRoomMenu] RefreshLobbyInfo called");
            EnsureHostAddressPanel();
            
            // Don't crash if called during scene init when LobbyRoomMenu is active by default
            if (YargNetworkManager.Instance == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] YargNetworkManager not ready yet, skipping refresh");
                return;
            }
            
            var lobby = YargNetworkManager.Instance.CurrentLobby;
            if (lobby == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] No current lobby, skipping refresh");
                return;
            }

            Debug.Log($"[LobbyRoomMenu] Lobby data: {lobby.lobbyName}, Host: {lobby.hostName}, Players: {lobby.currentPlayers}/{lobby.maxPlayers}");
            
            isHost = YargNetworkManager.Instance.IsHosting;
            Debug.Log($"[LobbyRoomMenu] IsHost: {isHost}");

            // Capture the network manager reference for reuse
            var networkManager = YargNetworkManager.Instance;

            // Update lobby info display
            if (lobbyNameText != null)
            {
                lobbyNameText.text = lobby.lobbyName;
                Debug.Log($"[LobbyRoomMenu] Set lobbyNameText to: {lobby.lobbyName}");
            }
            else
            {
                Debug.LogWarning("[LobbyRoomMenu] lobbyNameText is NULL!");
            }
            
            if (hostNameText != null)
            {
                hostNameText.text = $"Host: {lobby.hostName}";
                Debug.Log($"[LobbyRoomMenu] Set hostNameText to: Host: {lobby.hostName}");
            }
            else
            {
                Debug.LogWarning("[LobbyRoomMenu] hostNameText is NULL!");
            }
            
            if (playerCountText != null)
            {
                playerCountText.text = $"{lobby.currentPlayers}/{lobby.maxPlayers} Players";
                Debug.Log($"[LobbyRoomMenu] Set playerCountText to: {lobby.currentPlayers}/{lobby.maxPlayers} Players");
            }
            else
            {
                Debug.LogWarning("[LobbyRoomMenu] playerCountText is NULL!");
            }

            var connectionTextTarget = connectionInfoText != null ? connectionInfoText : lobbyCodeText;

            int fallbackPort = lobby.port > 0
                ? lobby.port
                : (YargNetworkManager.Instance != null ? YargNetworkManager.Instance.DefaultPort : NetworkTransportDefaults.DefaultUdpPort);

            string lanSourceAddress = lobby.ipAddress;
            if (isHost && networkManager != null && !string.IsNullOrWhiteSpace(networkManager.networkAddress))
            {
                lanSourceAddress = networkManager.networkAddress;
            }

            string wanSourceAddress = lobby.publicAddress;
            if (isHost && networkManager != null && networkManager.CurrentLobby != null &&
                !string.IsNullOrWhiteSpace(networkManager.CurrentLobby.publicAddress))
            {
                wanSourceAddress = networkManager.CurrentLobby.publicAddress;
            }

            string lanEndpoint = FormatEndpoint(lanSourceAddress, lobby.port, fallbackPort);
            string wanEndpoint = FormatEndpoint(wanSourceAddress, lobby.publicPort, fallbackPort);

            bool hostPanelVisible = UpdateHostAddressPanel(lanEndpoint, wanEndpoint);

            if (connectionTextTarget != null)
            {
                string connectLabel = string.Empty;

                if (!string.IsNullOrEmpty(lanEndpoint))
                {
                    connectLabel = $"Direct Connect (LAN): {lanEndpoint}";
                }

                if (!string.IsNullOrEmpty(wanEndpoint) && !wanEndpoint.Equals(lanEndpoint))
                {
                    if (!string.IsNullOrEmpty(connectLabel))
                    {
                        connectLabel += "\n";
                    }

                    connectLabel += $"Public Address: {wanEndpoint}";
                }

                if (string.IsNullOrEmpty(connectLabel))
                {
                    connectLabel = "Direct Connect: Resolving...";
                }

                string finalLabel = connectLabel;
                if (isHost && hostPanelVisible)
                {
                    finalLabel = "Direct connect details are shown below.";
                }
                if (connectionTextTarget == playerCountText && playerCountText != null)
                {
                    string baseText = playerCountText.text;
                    if (!string.IsNullOrEmpty(baseText))
                    {
                        finalLabel = string.Concat(baseText, "\n", connectLabel);
                    }
                }

                connectionTextTarget.text = finalLabel;
                Debug.Log($"[LobbyRoomMenu] Set connection info text to: {finalLabel.Replace('\n', ' ')}");
            }
            else
            {
                Debug.LogWarning("[LobbyRoomMenu] Connection info text target is NULL!");
            }

            // Show/hide controls based on role
            UpdateControlsForRole();

            if (YargNetworkManager.Instance != null)
            {
                OnSharedSongSyncStateChanged(YargNetworkManager.Instance.IsSharedSongSyncComplete);
            }
            
            RefreshPlayerList();
            
            // Update navigation scheme now that isHost is set
            UpdateNavigationScheme();
            
            // Force UI update
            UnityEngine.Canvas.ForceUpdateCanvases();
        }

        private static string FormatEndpoint(string address, int port, int fallbackPort)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return string.Empty;
            }

            int finalPort = port > 0 ? port : fallbackPort;
            return finalPort > 0 ? $"{address}:{finalPort}" : address;
        }

        private bool EnsureHostAddressPanel()
        {
            if (!HasPrefabHostPanel())
            {
                Debug.LogWarning("[LobbyRoomMenu] Host address UI references are missing. The prefab now owns this layout.");
                return false;
            }

            BindHostPanelButtons();
            return true;
        }

        private bool HasPrefabHostPanel()
        {
            return hostAddressPanel != null &&
                   lanAddressRow != null &&
                   lanAddressValueText != null &&
                   lanVisibilityToggleButton != null &&
                   lanCopyButton != null &&
                   wanAddressRow != null &&
                   wanAddressValueText != null &&
                   wanVisibilityToggleButton != null &&
                   wanCopyButton != null;
        }

        private void BindHostPanelButtons()
        {
            if (_hostPanelListenersBound)
            {
                return;
            }

            if (lanVisibilityToggleButton != null)
            {
                lanVisibilityToggleButton.onClick.AddListener(ToggleLanVisibility);
            }
            if (lanCopyButton != null)
            {
                lanCopyButton.onClick.AddListener(CopyLanAddress);
            }
            if (wanVisibilityToggleButton != null)
            {
                wanVisibilityToggleButton.onClick.AddListener(ToggleWanVisibility);
            }
            if (wanCopyButton != null)
            {
                wanCopyButton.onClick.AddListener(CopyWanAddress);
            }

            _hostPanelListenersBound = true;
        }

        private bool UpdateHostAddressPanel(string lanEndpoint, string wanEndpoint)
        {
            if (!EnsureHostAddressPanel())
            {
                return false;
            }

            if (!isHost)
            {
                SetHostSidebarActive(false);
                return false;
            }

            ApplyEndpointRow(ref _lanAddress, lanEndpoint, ref _lanVisible, lanAddressRow, lanAddressValueText, lanVisibilityToggleButton, lanVisibilityToggleLabel, lanCopyButton);
            ApplyEndpointRow(ref _wanAddress, wanEndpoint, ref _wanVisible, wanAddressRow, wanAddressValueText, wanVisibilityToggleButton, wanVisibilityToggleLabel, wanCopyButton);

            SetHostSidebarActive(true);

            return true;
        }

        private void SetHostSidebarActive(bool active)
        {
            if (hostAddressPanel != null)
            {
                hostAddressPanel.SetActive(active);
            }

            var container = hostAddressPanelParent != null
                ? hostAddressPanelParent.gameObject
                : hostAddressPanel != null ? hostAddressPanel.transform.parent?.gameObject : null;

            if (container != null && container != hostAddressPanel)
            {
                if (active)
                {
                    container.SetActive(true);
                }
            }
        }

        private bool ApplyEndpointRow(ref string cachedValue, string incomingValue, ref bool isVisible, GameObject row, TextMeshProUGUI valueLabel, Button toggleButton, TextMeshProUGUI toggleLabel, Button copyButton)
        {
            string previousValue = cachedValue ?? string.Empty;
            string sanitized = SanitizeEndpoint(incomingValue, previousValue);

            bool valueChanged = !string.Equals(previousValue, sanitized, StringComparison.Ordinal);
            cachedValue = sanitized;

            bool hasValue = !string.IsNullOrEmpty(sanitized);

            if (row != null)
            {
                row.SetActive(true);
            }

            if (copyButton != null)
            {
                copyButton.interactable = hasValue;
            }

            if (!hasValue || valueChanged)
            {
                isVisible = false;
            }

            RefreshEndpointDisplay(sanitized, isVisible, valueLabel, toggleLabel, toggleButton, hasValue);

            return hasValue;
        }

        private static string SanitizeEndpoint(string incomingValue, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(incomingValue))
            {
                return incomingValue.Trim();
            }

            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
        }

        private void RefreshEndpointDisplay(string endpoint, bool isVisible, TextMeshProUGUI valueLabel, TextMeshProUGUI toggleLabel, Button toggleButton, bool toggleAvailable)
        {
            if (valueLabel != null)
            {
                if (!toggleAvailable)
                {
                    valueLabel.text = "Resolving...";
                }
                else
                {
                    valueLabel.text = isVisible ? endpoint : "****";
                }
            }

            if (toggleButton != null)
            {
                toggleButton.interactable = toggleAvailable;
            }

            if (toggleLabel != null)
            {
                toggleLabel.text = toggleAvailable
                    ? (isVisible ? "Hide" : "Show")
                    : "Show";
            }

            UpdateVisibilityToggleIcon(toggleButton, toggleAvailable ? (bool?) isVisible : null);
        }

        private void UpdateVisibilityToggleIcon(Button toggleButton, bool? isVisible)
        {
            if (toggleButton == null)
                return;

            var image = toggleButton.image;
            if (image == null || (visibilityVisibleSprite == null && visibilityHiddenSprite == null))
                return;

            if (!isVisible.HasValue)
            {
                if (visibilityHiddenSprite != null)
                {
                    image.enabled = true;
                    image.sprite = visibilityHiddenSprite;
                }
                else if (visibilityVisibleSprite != null)
                {
                    image.enabled = true;
                    image.sprite = visibilityVisibleSprite;
                }
                else
                {
                    image.enabled = false;
                }

                return;
            }

            if (isVisible.Value)
            {
                if (visibilityVisibleSprite != null)
                {
                    image.enabled = true;
                    image.sprite = visibilityVisibleSprite;
                }
                else
                {
                    image.enabled = false;
                }
            }
            else
            {
                if (visibilityHiddenSprite != null)
                {
                    image.enabled = true;
                    image.sprite = visibilityHiddenSprite;
                }
                else
                {
                    image.enabled = false;
                }
            }
        }

        private void ToggleEndpointVisibility(ref bool visibility, string endpoint, TextMeshProUGUI valueLabel, TextMeshProUGUI toggleLabel, Button toggleButton)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                ToastManager.ToastWarning("No address is available yet to show.");
                return;
            }

            visibility = !visibility;
            RefreshEndpointDisplay(endpoint, visibility, valueLabel, toggleLabel, toggleButton, true);
        }

        private void ToggleLanVisibility()
        {
            ToggleEndpointVisibility(ref _lanVisible, _lanAddress, lanAddressValueText, lanVisibilityToggleLabel, lanVisibilityToggleButton);
        }

        private void ToggleWanVisibility()
        {
            ToggleEndpointVisibility(ref _wanVisible, _wanAddress, wanAddressValueText, wanVisibilityToggleLabel, wanVisibilityToggleButton);
        }

        private void CopyLanAddress()
        {
            CopyEndpointToClipboard(_lanAddress, "LAN");
        }

        private void CopyWanAddress()
        {
            CopyEndpointToClipboard(_wanAddress, "WAN");
        }

        private void CopyEndpointToClipboard(string endpoint, string label)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                ToastManager.ToastWarning($"No {label} address available yet.");
                return;
            }

            GUIUtility.systemCopyBuffer = endpoint;
            ToastManager.ToastInformation($"{label} address copied to clipboard.");
        }

        private void UpdateControlsForRole()
        {
            // Only host can browse songs
            if (browseSongsButton != null)
            {
                browseSongsButton.gameObject.SetActive(isHost);
                Debug.Log($"[LobbyRoomMenu] Browse songs button visible: {isHost}");
            }
            
            // Only clients see "waiting for host" message
            if (waitingForHostText != null)
            {
                if (!isHost && string.IsNullOrEmpty(waitingForHostText.text))
                {
                    waitingForHostText.text = !string.IsNullOrEmpty(_defaultWaitingForHostText)
                        ? _defaultWaitingForHostText
                        : "Waiting for host...";
                }
            }
        }
        
        private void MoveWaitingTextToContainer()
        {
            if (waitingForHostText == null || playerListContainer == null)
            {
                return;
            }
            
            // Move the waiting text to be the last child in the player list container
            waitingForHostText.transform.SetParent(playerListContainer, false);
            waitingForHostText.transform.SetAsLastSibling();
            
            // Ensure proper layout settings for centered positioning
            var rectTransform = waitingForHostText.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
                // Center horizontally, positioned normally in vertical layout
                rectTransform.anchorMin = new Vector2(0, 0.5f);
                rectTransform.anchorMax = new Vector2(1, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                // Add some top padding to separate from last player
                var layoutElement = waitingForHostText.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = waitingForHostText.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                layoutElement.minHeight = 40; // Set minimum height for spacing
                layoutElement.preferredHeight = 40;
            }
        }

        private void RefreshPlayerList()
        {
            if (playerListContainer == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] playerListContainer is null!");
                return;
            }

            // NOTE: Make sure playerListContainer has a VerticalLayoutGroup component
            // with spacing set to a small value (e.g., 5-10) for proper stacking
            
            // Clear the player views dictionary
            playerViews.Clear();
            selectedPlayerView = null;
            
            // Clear existing player entries (but don't destroy the waiting text)
            foreach (Transform child in playerListContainer)
            {
                // Skip destroying the waiting for host text
                if (waitingForHostText != null && child.gameObject == waitingForHostText.gameObject)
                {
                    continue;
                }
                Destroy(child.gameObject);
            }

            if (YargNetworkManager.Instance == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] YargNetworkManager is null!");
                return;
            }

            // Get all connected players
            var allPlayers = YargNetworkManager.Instance.GetAllPlayers();
            
            if (allPlayers == null || allPlayers.Count == 0)
            {
                Debug.Log("[LobbyRoomMenu] No players to display yet");
                return;
            }

            Debug.Log($"[LobbyRoomMenu] Refreshing player list with {allPlayers.Count} players");

            // Create a view for each player
            for (int i = 0; i < allPlayers.Count; i++)
            {
                CreatePlayerView(allPlayers[i]);
                
                // Add divider after each player except the last one
                if (i < allPlayers.Count - 1)
                {
                    CreateDivider();
                }
            }
            
            // Move waiting for host text to end of player list container
            MoveWaitingTextToContainer();
        }

        private void CreatePlayerView(NetworkPlayerData playerData)
        {
            if (playerEntryPrefab == null || playerListContainer == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] PlayerEntry prefab or container not assigned!");
                return;
            }

            if (playerData == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] PlayerData is null!");
                return;
            }

            // Instantiate the player entry prefab
            var entry = Instantiate(playerEntryPrefab, playerListContainer);
            
            // Ensure the entry is active
            entry.SetActive(true);
            
            // Set up RectTransform for proper layout
            var rectTransform = entry.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
                // Stretch horizontally to fill container width
                rectTransform.anchorMin = new Vector2(0, 0.5f);
                rectTransform.anchorMax = new Vector2(1, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            
            // Get or add PlayerView component
            var playerView = entry.GetComponent<PlayerView>();
            if (playerView == null)
            {
                playerView = entry.AddComponent<PlayerView>();
            }

            // Check if this is the local player
            bool isLocalPlayer = playerData.IsLocalUser;
            
            // Set up selection for host (or for all players for future features)
            var button = entry.GetComponent<Button>();
            if (button == null)
            {
                button = entry.AddComponent<Button>();
            }
            
            // Configure button navigation to work with up/down controls
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.Automatic;
            button.navigation = navigation;
            
            // Add click listener to select this player (host only functionality)
            if (isHost)
            {
                button.onClick.AddListener(() => OnPlayerSelected(playerData));
                
                // Add visual feedback on selection
                var colorBlock = button.colors;
                colorBlock.highlightedColor = new Color(1f, 1f, 1f, 0.3f);
                colorBlock.selectedColor = new Color(0.2f, 0.9f, 0.2f, 0.3f); // Green tint when selected
                button.colors = colorBlock;
            }
            else
            {
                // For non-host, make button interactable but don't add click functionality
                button.interactable = true;
            }
            
            // Initialize the view
            playerView.Initialize(playerData, isLocalPlayer, isHost);
            
            // Store the player view in the dictionary for selection tracking
            playerViews[playerData] = playerView;
            
            Debug.Log($"[LobbyRoomMenu] Created player view for: {playerData.PlayerName}");
        }

        private void CreateDivider()
        {
            // Create a simple divider (horizontal line)
            GameObject divider = new GameObject("Divider");
            divider.transform.SetParent(playerListContainer, false);
            
            // Add Image component for the line
            var image = divider.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(1, 1, 1, 0.2f); // Semi-transparent white
            
            // Set RectTransform to be a thin horizontal line
            var rectTransform = divider.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 0.5f);
            rectTransform.anchorMax = new Vector2(1, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(0, 1); // 1 pixel height, full width
        }

        // Button callbacks
        public void OnBrowseSongsClicked()
        {
            if (!isHost)
            {
                Debug.LogWarning("Only host can start song selection!");
                return;
            }

            Debug.Log("Host starting song selection...");

            if (YargNetworkManager.Instance == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] Cannot start song selection - network manager missing");
                return;
            }

            if (_waitingForSongSync)
            {
                Debug.Log("[LobbyRoomMenu] Already waiting for shared song sync to complete.");
                return;
            }

            if (!YargNetworkManager.Instance.IsSharedSongSyncComplete)
            {
                Debug.Log("[LobbyRoomMenu] Shared song sync in progress; queuing music library navigation until ready.");
                _waitingForSongSync = true;
                OnSharedSongSyncStateChanged(false);
            }

            YargNetworkManager.Instance.StartSongSelection();
        }

        public void OnLeaveLobbyClicked()
        {
            // Both host and client show confirmation dialog when leaving lobby entirely
            if (YargNetworkManager.Instance != null && YargNetworkManager.Instance.isNetworkActive)
            {
                ShowLeaveLobbyDialog();
            }
            else
            {
                // Not in multiplayer - just go back
                LeaveLobby();
            }
        }
        
        public void OnKickPlayerClicked()
        {
            if (!isHost)
            {
                Debug.LogWarning("[LobbyRoomMenu] Only host can kick players!");
                return;
            }
            
            if (selectedPlayer == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] No player selected to kick!");
                return;
            }
            
            // Don't allow kicking yourself
            if (selectedPlayer.IsLocalUser)
            {
                Debug.LogWarning("[LobbyRoomMenu] Cannot kick yourself!");
                return;
            }
            
            ShowKickPlayerDialog();
        }
        
        private void ShowKickPlayerDialog()
        {
            if (DialogManager.Instance == null || selectedPlayer == null) return;
            
            string playerName = selectedPlayer.PlayerName;
            
            var dialog = DialogManager.Instance.ShowMessage(
                "Kick Player?",
                $"Are you sure you want to kick {playerName} from the lobby?");
            
            dialog.ClearButtons();
            dialog.AddDialogButton("Cancel", MenuData.Colors.BrightButton, () => DialogManager.Instance.ClearDialog());
            dialog.AddDialogButton("Kick Player", MenuData.Colors.CancelButton, () =>
            {
                DialogManager.Instance.ClearDialog();
                KickPlayer(selectedPlayer);
            });
        }
        
        private void KickPlayer(NetworkPlayerData player)
        {
            if (!isHost || player == null)
            {
                Debug.LogWarning("[LobbyRoomMenu] Cannot kick player - not host or player is null");
                return;
            }
            
            Debug.Log($"[LobbyRoomMenu] Kicking player: {player.PlayerName}");
            
            // Disconnect the specific player's connection
            if (Mirror.NetworkServer.active && player.connectionToClient != null)
            {
                player.connectionToClient.Disconnect();
            }
            
            selectedPlayer = null;
            UpdateNavigationScheme();
        }
        
        private void ShowLeaveLobbyDialog()
        {
            if (DialogManager.Instance == null) return;
            
            bool isHost = YargNetworkManager.Instance != null && YargNetworkManager.Instance.IsHosting;
            
            string title = isHost ? "Close Lobby?" : "Leave Lobby?";
            string message = isHost
                ? "Are you sure you want to close the lobby? All connected players will be disconnected."
                : "Are you sure you want to leave the lobby? You will be disconnected from the host.";
            
            var dialog = DialogManager.Instance.ShowMessage(title, message);
            
            dialog.ClearButtons();
            dialog.AddDialogButton("Cancel", MenuData.Colors.BrightButton, () => DialogManager.Instance.ClearDialog());
            dialog.AddDialogButton(isHost ? "Close Lobby" : "Leave Lobby", MenuData.Colors.CancelButton, () =>
            {
                DialogManager.Instance.ClearDialog();
                LeaveLobby();
            });
        }

        private void LeaveLobby()
        {
            Debug.Log("[LobbyRoomMenu] LeaveLobby called");
            
            // If host, sync menu navigation to clients before disconnecting
            if (YargNetworkManager.Instance != null && 
                YargNetworkManager.Instance.isNetworkActive &&
                YargNetworkManager.Instance.IsHosting)
            {
                Debug.Log("[LobbyRoomMenu] Host closing lobby - syncing clients to lobby browser");
                YargNetworkManager.Instance.SyncMenuNavigation(popMenu: true);
            }
            
            // Return to menu
            MenuManager.Instance?.PopMenu();
            
            // Then disconnect from network
            if (YargNetworkManager.Instance != null)
            {
                Debug.Log($"[LobbyRoomMenu] NetworkServer.active: {Mirror.NetworkServer.active}, NetworkClient.isConnected: {Mirror.NetworkClient.isConnected}");
                
                // Call LeaveLobby which handles stopping host or client
                YargNetworkManager.Instance.LeaveLobby();
            }
        }

        private void OnLobbyLeft()
        {
            Debug.Log("Lobby left, returning to lobby browser");
            
            // Safety checks: Don't run during application quit or if MenuManager is gone
            if (_isQuitting)
            {
                Debug.Log("[LobbyRoomMenu] Application is quitting, skipping OnLobbyLeft");
                return;
            }
            
            if (MenuManager.Instance == null) 
            {
                Debug.Log("[LobbyRoomMenu] MenuManager is null, skipping OnLobbyLeft");
                return;
            }
            
            // Additional safety: Check if this is still the active menu scene instance
            // If the current menu is OnlineMultiplayer or MainMenu, we're already in the right place
            if (MenuManager.Instance.CurrentMenu == MenuManager.Menu.OnlineMultiplayer ||
                MenuManager.Instance.CurrentMenu == MenuManager.Menu.MainMenu)
            {
                Debug.Log($"[LobbyRoomMenu] Already at {MenuManager.Instance.CurrentMenu}, skipping navigation");
                return;
            }
            
            // Pop back to lobby browser (OnlineMultiplayer) no matter where we are
            // This handles cases where client is in MusicLibrary, DifficultySelect, or even Gameplay
            while (MenuManager.Instance != null &&
                   MenuManager.Instance.CurrentMenu != MenuManager.Menu.OnlineMultiplayer && 
                   MenuManager.Instance.MenuStackCount > 1)
            {
                Debug.Log($"[LobbyRoomMenu] Popping menu: {MenuManager.Instance.CurrentMenu}");
                MenuManager.Instance.PopMenu();
            }
            
            if (MenuManager.Instance != null)
            {
                Debug.Log($"[LobbyRoomMenu] Returned to lobby browser. Current menu: {MenuManager.Instance.CurrentMenu}");
            }
        }

        private void OnNetworkError(string error)
        {
            Debug.LogError($"[LobbyRoomMenu] Network error in lobby: {error}");
            
            // Only show dialog if one isn't already showing
            if (DialogManager.Instance != null && !DialogManager.Instance.IsDialogShowing)
            {
                DialogManager.Instance.ShowMessage(
                    "Connection Error", 
                    error);
            }
            
            // Leave lobby after showing error
            LeaveLobby();
        }
    }
}
