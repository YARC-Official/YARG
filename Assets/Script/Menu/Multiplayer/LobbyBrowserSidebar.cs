using System;
using System.Collections.Generic;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YARG.Core;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu;
using YARG.Menu.Data;
using YARG.Menu.DifficultySelect;
using YARG.Menu.Persistent;
using YARG.Networking;
using YARG.Networking.Bookmarks;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Sidebar controller responsible for rendering the various lobby browser cards:
    /// lobby details, bookmarks, direct connect form, and hosted lobby presets.
    /// </summary>
    public class LobbyBrowserSidebar : MonoBehaviour
    {
        private const int DefaultDirectConnectPort = NetworkTransportDefaults.DefaultUdpPort;
        private static int GetSuggestedPort()
        {
            return YargNetworkManager.Instance != null
                ? Mathf.Clamp(YargNetworkManager.Instance.SuggestedDirectConnectPort, 1, ushort.MaxValue)
                : DefaultDirectConnectPort;
        }


        [Header("Containers")]
        [SerializeField]
        private GameObject _emptyStateContainer;
        [SerializeField]
        private GameObject _contentContainer;
        [SerializeField]
        private GameObject _createLobbyContainer;
        [SerializeField]
        private GameObject _hostedLobbyContainer;
        [SerializeField]
        private GameObject _directConnectContainer;

        [Header("Data Visibility")]
        [SerializeField]
        private Button _hostVisibilityToggle;
        [SerializeField]
        private Sprite _hostVisibleSprite;
        [SerializeField]
        private Sprite _hostHiddenSprite;
        [SerializeField]
        private TextMeshProUGUI _passwordValueText;
        [SerializeField]
        private Button _passwordVisibilityToggle;
        [SerializeField]
        private Sprite _passwordVisibleSprite;
        [SerializeField]
        private Sprite _passwordHiddenSprite;

        [Header("Editable Containers")]
        [SerializeField]
        private GameObject _lobbyNameViewContainer;
        [SerializeField]
        private GameObject _lobbyNameEditContainer;
        [SerializeField]
        private Button _lobbyNameEditButton;
        [SerializeField]
        private GameObject _hostAddressViewContainer;
        [SerializeField]
        private GameObject _hostAddressEditContainer;
        [SerializeField]
        private Button _hostAddressEditButton;
        [SerializeField]
        private GameObject _passwordViewContainer;
        [SerializeField]
        private GameObject _passwordEditContainer;
        [SerializeField]
        private Button _passwordEditButton;

        [Header("Lobby Info")]
        [SerializeField]
        private TextMeshProUGUI _lobbyNameText;
        [SerializeField]
        private TextMeshProUGUI _hostNameText;
        [SerializeField]
        private TextMeshProUGUI _playerCountText;
        [SerializeField]
        private TextMeshProUGUI _pingText;
        [SerializeField]
        private TextMeshProUGUI _privacyText;
        [SerializeField]
        private GameObject _passwordIcon;

        [Header("Player List")]
        [SerializeField]
        private Transform _playerListContainer;
        [SerializeField]
        private GameObject _playerEntryPrefab;
        [SerializeField]
        private TextMeshProUGUI _noPlayersText;

        [Header("Create Lobby Form")]
        [SerializeField]
        private TMP_InputField _createLobbyNameInput;
        [SerializeField]
        private TMP_Dropdown _createLobbyMaxPlayersDropdown;
        [SerializeField]
        private TMP_Dropdown _createLobbyPrivacyDropdown;
        [SerializeField]
        private TMP_InputField _createLobbyPasswordInput;
        [SerializeField]
        private GameObject _createLobbyPasswordRow;
        [SerializeField]
        private ColoredButton _createLobbySubmitButton;
        [SerializeField]
        private Button _createLobbyCancelButton;

        [Header("Hosted Lobby Form")]
        [SerializeField]
        private TMP_InputField _hostedLobbyNameInput;
        [SerializeField]
        private TMP_Dropdown _hostedLobbyMaxPlayersDropdown;
        [SerializeField]
        private TMP_Dropdown _hostedLobbyPrivacyDropdown;
        [SerializeField]
        private TMP_InputField _hostedLobbyPasswordInput;
        [SerializeField]
        private GameObject _hostedLobbyPasswordRow;
        [SerializeField]
        private ColoredButton _hostedLobbyHostButton;
        [SerializeField]
        private ColoredButton _hostedLobbyDeleteButton;

        [Header("Direct Connect Form")]
        [SerializeField]
        private TMP_InputField _directConnectAddressInput;
        [SerializeField]
        private TMP_InputField _directConnectPasswordInput;
        [SerializeField]
        private ColoredButton _directConnectSubmitButton;
        [SerializeField]
        private Button _directConnectCancelButton;

        private LobbyBrowserMenu _menu;
        private YargNetworkManager.LobbyInfo _currentLobby;
        private HostedLobbyPreset _activePreset;
        private LobbyBookmark _activeBookmark;
        private SidebarMode _currentMode = SidebarMode.Empty;
        private bool _listenersRegistered;
        private string _currentHostAddress = string.Empty;
        private string _currentPassword = string.Empty;
        private bool _isHostAddressVisible;
        private bool _isPasswordVisible;
        private bool _hostToggleAvailable;
        private bool _hasPassword;
        private bool _passwordToggleAvailable;
        private TMP_InputField _passwordEditInputField;
        private bool _suppressHostedFieldCallbacks;
        private EditableField? _activeEditField;
        private YargNetworkManager.LobbyPrivacyMode _currentPrivacyMode = YargNetworkManager.LobbyPrivacyMode.Public;
        private int _defaultMaxPlayersOptionIndex;
        private bool _attemptedHostedContainerResolve;
        private bool IsEditing => _activeEditField.HasValue;

        private enum EditableField
        {
            LobbyName,
            HostAddress,
            Password
        }

        private readonly struct LobbyPlayerEntry
        {
            public LobbyPlayerEntry(string displayName, string instrumentMarkup)
            {
                DisplayName = displayName;
                InstrumentMarkup = instrumentMarkup;
            }

            public string DisplayName { get; }
            public string InstrumentMarkup { get; }
        }

        public event Action<CreateLobbyFormData> CreateLobbySubmitted;
        public event Action<DirectConnectFormData> DirectConnectSubmitted;

        private enum SidebarMode
        {
            Empty,
            Lobby,
            CreateLobby,
            HostedLobby,
            DirectConnect
        }

        #region Initialization

        public void Initialize(LobbyBrowserMenu menu)
        {
            _menu = menu;

            EnsureContainerReferences();
            ApplyButtonColors();

            if (!_listenersRegistered)
            {
                RegisterButtonListeners();
                PopulateDropdowns();
                _listenersRegistered = true;
            }

            ClearLobby();
        }

        private void RegisterButtonListeners()
        {
            if (_createLobbySubmitButton != null)
                _createLobbySubmitButton.OnClick.AddListener(SubmitCreateLobbyForm);

            if (_createLobbyCancelButton != null)
                _createLobbyCancelButton.onClick.AddListener(ClearLobby);

            if (_createLobbyPrivacyDropdown != null)
                _createLobbyPrivacyDropdown.onValueChanged.AddListener(OnCreateLobbyPrivacyChanged);

            if (_hostVisibilityToggle != null)
                _hostVisibilityToggle.onClick.AddListener(ToggleHostVisibility);

            if (_passwordVisibilityToggle != null)
                _passwordVisibilityToggle.onClick.AddListener(TogglePasswordVisibility);

            if (_lobbyNameEditButton != null)
                _lobbyNameEditButton.onClick.AddListener(BeginLobbyNameEdit);

            if (_hostAddressEditButton != null)
                _hostAddressEditButton.onClick.AddListener(BeginHostAddressEdit);

            if (_passwordEditButton != null)
                _passwordEditButton.onClick.AddListener(BeginPasswordEdit);

            if (_hostedLobbyNameInput != null)
            {
                _hostedLobbyNameInput.onEndEdit.AddListener(OnHostedLobbyNameSubmitted);
                _hostedLobbyNameInput.onSubmit.AddListener(OnHostedLobbyNameSubmitted);
            }

            if (_hostedLobbyMaxPlayersDropdown != null)
                _hostedLobbyMaxPlayersDropdown.onValueChanged.AddListener(OnHostedLobbyMaxPlayersChanged);

            if (_hostedLobbyPrivacyDropdown != null)
                _hostedLobbyPrivacyDropdown.onValueChanged.AddListener(OnHostedLobbyPrivacyChanged);

            if (_hostedLobbyPasswordInput != null)
            {
                _hostedLobbyPasswordInput.onEndEdit.AddListener(OnHostedLobbyPasswordSubmitted);
                _hostedLobbyPasswordInput.onSubmit.AddListener(OnHostedLobbyPasswordSubmitted);
            }

            if (_hostedLobbyHostButton != null)
                _hostedLobbyHostButton.OnClick.AddListener(HandleHostedHost);

            if (_hostedLobbyDeleteButton != null)
                _hostedLobbyDeleteButton.OnClick.AddListener(HandleHostedDelete);

            if (_directConnectSubmitButton != null)
                _directConnectSubmitButton.OnClick.AddListener(SubmitDirectConnectForm);

            if (_directConnectCancelButton != null)
                _directConnectCancelButton.onClick.AddListener(ClearLobby);

            AttachEditableInputHandlers(_lobbyNameEditContainer, ConfirmLobbyNameEdit, EditableField.LobbyName);
            AttachEditableInputHandlers(_hostAddressEditContainer, ConfirmHostAddressEdit, EditableField.HostAddress);
            AttachEditableInputHandlers(_passwordEditContainer, ConfirmPasswordEdit, EditableField.Password);
        }

        private void ApplyButtonColors()
        {
            var colors = MenuData.Colors;
            if (colors == null)
                return;

            if (_createLobbySubmitButton != null)
                _createLobbySubmitButton.SetBackgroundAndTextColor(colors.ConfirmButton);

            if (_hostedLobbyHostButton != null)
                _hostedLobbyHostButton.SetBackgroundAndTextColor(colors.ConfirmButton);

            if (_hostedLobbyDeleteButton != null)
                _hostedLobbyDeleteButton.SetBackgroundAndTextColor(colors.CancelButton);

            if (_directConnectSubmitButton != null)
                _directConnectSubmitButton.SetBackgroundAndTextColor(colors.ConfirmButton);
        }

        private void PopulateDropdowns()
        {
            if (_createLobbyMaxPlayersDropdown != null)
            {
                EnsureMaxPlayersDropdownOptions(_createLobbyMaxPlayersDropdown);
                int optionCount = _createLobbyMaxPlayersDropdown.options.Count;
                _defaultMaxPlayersOptionIndex = optionCount > 0
                    ? Mathf.Clamp(_createLobbyMaxPlayersDropdown.value, 0, optionCount - 1)
                    : 0;
            }

            EnsureMaxPlayersDropdownOptions(_hostedLobbyMaxPlayersDropdown);

            EnsurePrivacyDropdownOptions(_createLobbyPrivacyDropdown);
            EnsurePrivacyDropdownOptions(_hostedLobbyPrivacyDropdown);

            UpdateCreateLobbyPasswordVisibility();
            UpdateHostedLobbyPasswordVisibility();
        }

        private void OnCreateLobbyPrivacyChanged(int value)
        {
            UpdateCreateLobbyPasswordVisibility();
        }

        private void UpdateCreateLobbyPasswordVisibility()
        {
            bool showPassword = GetSelectedPrivacyMode() == YargNetworkManager.LobbyPrivacyMode.Private;
            if (_createLobbyPasswordRow != null)
            {
                _createLobbyPasswordRow.SetActive(showPassword);
            }
        }

        private void AttachEditableInputHandlers(GameObject container, Action confirmAction, EditableField? field = null)
        {
            if (container == null || confirmAction == null)
                return;

            TMP_InputField input = null;

            if (field == EditableField.Password)
            {
                input = EnsurePasswordEditInputField();
            }
            else
            {
                input = container.GetComponentInChildren<TMP_InputField>(true);
            }

            if (input == null)
                return;

            input.onSubmit.AddListener(_ => confirmAction());
            input.onEndEdit.AddListener(_ => confirmAction());
        }

        #endregion

        #region Public API

        public void SetLobby(YargNetworkManager.LobbyInfo lobby, LobbyBookmark bookmarkOverride = null)
        {
            if (lobby == null)
            {
                ClearLobby();
                return;
            }

            var store = LobbyBookmarkStore.Instance;

            ExitAllEditModes();
            _currentLobby = lobby;
            _activePreset = null;
            _activeBookmark = bookmarkOverride
                ?? store.GetFavorite(lobby.ipAddress, lobby.port)
                ?? store.GetRecent(lobby.ipAddress, lobby.port);

            ShowMode(SidebarMode.Lobby);
            PopulateLobbyInfo(lobby);
            ApplyBookmarkOverlayData(_activeBookmark);
            UpdatePlayerList(lobby);
            RefreshEditableButtons();
        }

        public void SetBookmark(LobbyBookmark bookmark)
        {
            bool editingCurrentBookmark = IsEditingBookmark(bookmark);

            if (!editingCurrentBookmark)
            {
                ExitAllEditModes();
            }

            if (bookmark == null)
            {
                if (!editingCurrentBookmark)
                {
                    ClearLobby();
                }
                return;
            }

            _activeBookmark = bookmark;
            _currentLobby = null;
            _activePreset = null;

            ShowMode(SidebarMode.Lobby);

            if (editingCurrentBookmark)
            {
                // Keep user input intact while ensuring sidebar remains in bookmark mode.
                _currentLobby = null;
                return;
            }

            PopulateBookmarkInfo(bookmark);
            RefreshEditableButtons();
        }

        public void ShowCreateLobbyForm(HostedLobbyPreset preset, bool focusFirstField = false)
        {
            bool wasCreateMode = _currentMode == SidebarMode.CreateLobby;
            bool presetChanged = !HostedPresetsEquivalent(_activePreset, preset);
            bool shouldReset = !wasCreateMode || focusFirstField || presetChanged;

            if (shouldReset)
            {
                _activePreset = preset?.Clone();
                ExitAllEditModes();
            }

            _currentLobby = null;
            _activeBookmark = null;

            ShowMode(SidebarMode.CreateLobby);

            if (!shouldReset)
            {
                if (focusFirstField && _createLobbyNameInput != null)
                {
                    FocusInput(_createLobbyNameInput);
                }

                UpdateCreateLobbyPasswordVisibility();
                RefreshEditableButtons();
                return;
            }

            var sourcePreset = _activePreset;

            string suggestedName = sourcePreset?.lobbyName;
            if (string.IsNullOrWhiteSpace(suggestedName))
            {
                string player = YargNetworkManager.Instance != null ? YargNetworkManager.Instance.PlayerName : "YARG";
                suggestedName = ZString.Format("{0}'s Lobby", player);
            }

            if (_createLobbyNameInput != null)
            {
                SetInputFieldText(_createLobbyNameInput, suggestedName);
                if (focusFirstField)
                {
                    FocusInput(_createLobbyNameInput);
                }
            }

            if (_createLobbyMaxPlayersDropdown != null)
            {
                int optionIndex = -1;

                if (sourcePreset != null && sourcePreset.maxPlayers > 0)
                {
                    optionIndex = FindMaxPlayersOptionIndex(_createLobbyMaxPlayersDropdown, sourcePreset.maxPlayers);
                }
                else if (_createLobbyMaxPlayersDropdown.options != null && _createLobbyMaxPlayersDropdown.options.Count > 0)
                {
                    optionIndex = Mathf.Clamp(_defaultMaxPlayersOptionIndex, 0, _createLobbyMaxPlayersDropdown.options.Count - 1);
                }

                if (optionIndex >= 0 && optionIndex < _createLobbyMaxPlayersDropdown.options.Count)
                {
                    _createLobbyMaxPlayersDropdown.value = optionIndex;
                    _createLobbyMaxPlayersDropdown.RefreshShownValue();
                }
            }

            if (_createLobbyPrivacyDropdown != null)
            {
                int privacyIndex = Mathf.Clamp((int)(sourcePreset?.PrivacyMode ?? YargNetworkManager.LobbyPrivacyMode.Public), 0, 2);
                _createLobbyPrivacyDropdown.value = privacyIndex;
            }

            if (_createLobbyPasswordInput != null)
            {
                SetInputFieldText(_createLobbyPasswordInput, sourcePreset?.password ?? string.Empty);
            }

            UpdateCreateLobbyPasswordVisibility();
            RefreshEditableButtons();
        }

        public void ShowDirectConnectForm(bool focusFirstField = false)
        {
            _activePreset = null;
            _currentLobby = null;
            _activeBookmark = null;

            ExitAllEditModes();
            ShowMode(SidebarMode.DirectConnect);

            if (_directConnectAddressInput != null)
            {
                SetInputFieldText(_directConnectAddressInput, string.Empty);
                if (focusFirstField)
                {
                    FocusInput(_directConnectAddressInput);
                }
            }

            if (_directConnectPasswordInput != null)
            {
                SetInputFieldText(_directConnectPasswordInput, string.Empty);
            }
            RefreshEditableButtons();
        }

        public void ShowHostedLobbyPreset(HostedLobbyPreset preset)
        {
            if (preset == null)
            {
                ClearLobby();
                return;
            }

            _activePreset = preset.Clone();
            _currentLobby = null;
            _activeBookmark = null;

            ExitAllEditModes();
            ShowMode(SidebarMode.HostedLobby);
            ApplyHostedPresetToFields(_activePreset);
            RefreshEditableButtons();
        }

        public void ClearLobby()
        {
            ExitAllEditModes();
            _currentLobby = null;
            _activePreset = null;
            _activeBookmark = null;
            _currentPrivacyMode = YargNetworkManager.LobbyPrivacyMode.Public;
            ShowMode(SidebarMode.Empty);
            ClearPlayerList();
            SetHostAddress(string.Empty, false);
            SetPasswordValue(string.Empty, false, false);
            ResetHostedForm();
            RefreshEditableButtons();
        }

        #endregion

        #region Lobby Info Rendering

        private void PopulateLobbyInfo(YargNetworkManager.LobbyInfo lobby)
        {
            if (_lobbyNameText != null)
                _lobbyNameText.text = lobby.lobbyName;

            string endpoint = BuildEndpoint(lobby.ipAddress, lobby.port, lobby.publicAddress, lobby.publicPort);
            SetHostAddress(endpoint, !string.IsNullOrEmpty(endpoint));

            if (_playerCountText != null)
            {
                bool lobbyFull = lobby.currentPlayers >= lobby.maxPlayers;
                Color countColor = lobbyFull ? new Color(1f, 0.3f, 0.3f) : MenuData.Colors.PrimaryText;
                string value = ZString.Format("{0}/{1}", lobby.currentPlayers, lobby.maxPlayers);
                _playerCountText.text = TextColorer.StyleString(value, countColor, 600);
            }

            if (_pingText != null)
            {
                int ping = CalculatePing(lobby);
                if (ping < 0)
                {
                    _pingText.text = TextColorer.StyleString("Offline", MenuData.Colors.PrimaryText.WithAlpha(0.45f), 600);
                }
                else
                {
                    Color pingColor = ping switch
                    {
                        < 50 => new Color(0.3f, 1f, 0.3f),
                        < 100 => new Color(1f, 1f, 0.3f),
                        _ => new Color(1f, 0.3f, 0.3f)
                    };

                    string pingValue = TextColorer.StyleString(ZString.Format("{0}ms", ping), pingColor, 600);
                    _pingText.text = pingValue;
                }
            }

            if (_privacyText != null)
            {
                string privacyMode = lobby.privacyMode switch
                {
                    YargNetworkManager.LobbyPrivacyMode.Public => "Public",
                    YargNetworkManager.LobbyPrivacyMode.Private => "Private",
                    YargNetworkManager.LobbyPrivacyMode.FriendsOnly => "Friends Only",
                    _ => "Unknown"
                };
                _privacyText.text = ZString.Format("Privacy: {0}", privacyMode);
            }

            bool hasPassword = lobby.hasPassword;
            if (_passwordIcon != null)
                _passwordIcon.SetActive(hasPassword);

            _currentPrivacyMode = lobby.privacyMode;
            SetPasswordValue(lobby.password, hasPassword, hasPassword);
            RefreshEditableButtons();
        }

        private void ApplyBookmarkOverlayData(LobbyBookmark bookmark)
        {
            if (bookmark == null)
                return;

            bool storedHasPassword = !string.IsNullOrEmpty(bookmark.password);
            if (storedHasPassword || !_hasPassword)
            {
                SetPasswordValue(bookmark.password, storedHasPassword || _hasPassword, true);
            }

            if (_passwordIcon != null && storedHasPassword && !_passwordIcon.activeSelf)
            {
                _passwordIcon.SetActive(true);
            }
        }

        private void PopulateBookmarkInfo(LobbyBookmark bookmark)
        {
            if (_lobbyNameText != null)
            {
                _lobbyNameText.text = string.IsNullOrWhiteSpace(bookmark.displayName)
                    ? bookmark.address
                    : bookmark.displayName;
            }

            string endpoint = EndpointUtility.FormatEndpoint(bookmark.address, bookmark.port > 0 ? bookmark.port : GetSuggestedPort());
            SetHostAddress(endpoint, !string.IsNullOrEmpty(endpoint));

            if (_playerCountText != null)
            {
                _playerCountText.text = TextColorer.StyleString("Offline", MenuData.Colors.PrimaryText.WithAlpha(0.45f), 600);
            }

            if (_pingText != null)
            {
                _pingText.text = TextColorer.StyleString("Offline", MenuData.Colors.PrimaryText.WithAlpha(0.45f), 600);
            }

            if (_privacyText != null)
            {
                _privacyText.text = string.IsNullOrEmpty(bookmark.password) ? "No password saved" : "Password saved";
            }

            if (_passwordIcon != null)
            {
                _passwordIcon.SetActive(!string.IsNullOrEmpty(bookmark.password));
            }

            ClearPlayerList();

            if (_noPlayersText != null)
            {
                _noPlayersText.gameObject.SetActive(true);
                _noPlayersText.text = "Live player list unavailable for saved servers.";
            }

            _currentPrivacyMode = string.IsNullOrEmpty(bookmark.password)
                ? YargNetworkManager.LobbyPrivacyMode.Public
                : YargNetworkManager.LobbyPrivacyMode.Private;
            SetPasswordValue(bookmark.password, !string.IsNullOrEmpty(bookmark.password), true);
            RefreshEditableButtons();
        }

        private void UpdatePlayerList(YargNetworkManager.LobbyInfo lobby)
        {
            ClearPlayerList();

            var playerEntries = BuildLobbyPlayerEntries(lobby);

            if (playerEntries.Count == 0)
            {
                if (_noPlayersText != null)
                {
                    _noPlayersText.gameObject.SetActive(true);
                    string label = lobby.currentPlayers <= 0
                        ? "No players in lobby"
                        : ZString.Format("{0} {1} in lobby",
                            lobby.currentPlayers,
                            lobby.currentPlayers == 1 ? "player" : "players");
                    _noPlayersText.text = label;
                }
                return;
            }

            if (_noPlayersText != null)
                _noPlayersText.gameObject.SetActive(false);

            foreach (var entry in playerEntries)
            {
                AddPlayerListEntry(entry);
            }
        }

        private List<LobbyPlayerEntry> BuildLobbyPlayerEntries(YargNetworkManager.LobbyInfo lobby)
        {
            var entries = new List<LobbyPlayerEntry>();
            if (lobby == null)
                return entries;

            string hostName = string.IsNullOrWhiteSpace(lobby.hostName) ? string.Empty : lobby.hostName.Trim();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] playerNames = lobby.playerNames;
            int[] instruments = lobby.playerInstruments;

            if (playerNames != null && playerNames.Length > 0)
            {
                for (int i = 0; i < playerNames.Length; i++)
                {
                    string normalizedName = SanitizePlayerName(playerNames[i], i);
                    bool isHost = !string.IsNullOrEmpty(hostName) && string.Equals(normalizedName, hostName, StringComparison.OrdinalIgnoreCase);
                    string displayName = isHost ? AppendHostSuffix(normalizedName) : normalizedName;

                    string instrumentMarkup = string.Empty;
                    if (instruments != null && i < instruments.Length)
                    {
                        instrumentMarkup = FormatInstrumentMarkup((Instrument)instruments[i]);
                    }

                    entries.Add(new LobbyPlayerEntry(displayName, instrumentMarkup));
                    seenNames.Add(normalizedName);
                }
            }

            if (!string.IsNullOrEmpty(hostName))
            {
                int hostIndex = entries.FindIndex(e => string.Equals(RemoveHostSuffix(e.DisplayName), hostName, StringComparison.OrdinalIgnoreCase));
                if (hostIndex >= 0)
                {
                    var hostEntry = entries[hostIndex];
                    var normalized = RemoveHostSuffix(hostEntry.DisplayName);
                    var updatedHost = new LobbyPlayerEntry(AppendHostSuffix(normalized), hostEntry.InstrumentMarkup);
                    entries.RemoveAt(hostIndex);
                    entries.Insert(0, updatedHost);
                }
                else if (!seenNames.Contains(hostName))
                {
                    entries.Insert(0, new LobbyPlayerEntry(AppendHostSuffix(hostName), string.Empty));
                    seenNames.Add(hostName);
                }
            }

            if (entries.Count == 0 && lobby.currentPlayers > 0)
            {
                for (int i = 0; i < lobby.currentPlayers; i++)
                {
                    string baseName = i == 0 && !string.IsNullOrEmpty(hostName)
                        ? hostName
                        : ZString.Format("Player {0}", i + 1);

                    if (!seenNames.Add(baseName))
                        continue;

                    bool isHost = !string.IsNullOrEmpty(hostName) && string.Equals(baseName, hostName, StringComparison.OrdinalIgnoreCase);
                    string displayName = isHost ? AppendHostSuffix(baseName) : baseName;
                    entries.Add(new LobbyPlayerEntry(displayName, string.Empty));
                }
            }

            return entries;
        }

        private void AddPlayerListEntry(LobbyPlayerEntry entry)
        {
            if (_playerEntryPrefab == null || _playerListContainer == null)
                return;

            var instance = Instantiate(_playerEntryPrefab, _playerListContainer);

            var entryComponent = instance.GetComponent<MultiplayerPlayerEntry>();
            if (entryComponent != null)
            {
                entryComponent.SetPlayer(entry.DisplayName ?? string.Empty, entry.InstrumentMarkup ?? string.Empty);
                return;
            }

            var texts = instance.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text == null)
                    continue;

                string componentName = text.gameObject.name;
                if (string.Equals(componentName, "Player Name", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(componentName, "PlayerName", StringComparison.OrdinalIgnoreCase))
                {
                    text.text = entry.DisplayName ?? string.Empty;
                }
                else if (string.Equals(componentName, "Instrument", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(componentName, "Icons", StringComparison.OrdinalIgnoreCase))
                {
                    text.text = entry.InstrumentMarkup ?? string.Empty;
                }
            }
        }

        private static string SanitizePlayerName(string name, int fallbackIndex)
        {
            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();

            return ZString.Format("Player {0}", fallbackIndex + 1);
        }

        private static string AppendHostSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Host";

            const string suffix = " (Host)";
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? name : name + suffix;
        }

        private static string RemoveHostSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            const string suffix = " (Host)";
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - suffix.Length).TrimEnd()
                : name;
        }

        private static string FormatInstrumentMarkup(Instrument instrument)
        {
            string resourceName = instrument.ToResourceName();
            return string.IsNullOrEmpty(resourceName)
                ? string.Empty
                : ZString.Format("<sprite name=\"{0}\">", resourceName);
        }

        private void ClearPlayerList()
        {
            if (_playerListContainer != null)
            {
                foreach (Transform child in _playerListContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            if (_noPlayersText != null)
            {
                _noPlayersText.gameObject.SetActive(false);
            }
        }

        private int CalculatePing(YargNetworkManager.LobbyInfo lobby)
        {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long sinceLastSeen = Mathf.Max(0, (int)(currentTime - lobby.lastSeen));
            if (sinceLastSeen > 5000)
                return -1;
            return UnityEngine.Random.Range(10, 120);
        }

        private string BuildEndpoint(string address, int port, string fallbackAddress, int fallbackPort)
        {
            string selectedAddress = !string.IsNullOrWhiteSpace(address) ? address : fallbackAddress;
            int selectedPort = port > 0 ? port : (fallbackPort > 0 ? fallbackPort : GetSuggestedPort());
            if (string.IsNullOrWhiteSpace(selectedAddress))
                return string.Empty;
            return EndpointUtility.FormatEndpoint(selectedAddress, selectedPort);
        }

        private void SetHostAddress(string address, bool allowToggle)
        {
            string newAddress = address ?? string.Empty;
            bool hasAddress = !string.IsNullOrEmpty(newAddress);
            bool newToggleAvailable = hasAddress && allowToggle;

            bool addressChanged = !string.Equals(_currentHostAddress, newAddress, StringComparison.Ordinal);
            bool availabilityChanged = _hostToggleAvailable != newToggleAvailable;

            _currentHostAddress = newAddress;
            _hostToggleAvailable = newToggleAvailable;

            if (addressChanged || availabilityChanged)
            {
                _isHostAddressVisible = false;
            }

            if (_hostVisibilityToggle != null)
                _hostVisibilityToggle.gameObject.SetActive(_hostToggleAvailable);

            ApplyHostVisibility();
        }

        private void ApplyHostVisibility()
        {
            if (_hostNameText == null)
                return;

            if (string.IsNullOrEmpty(_currentHostAddress))
            {
                _hostNameText.text = "Unavailable";
            }
            else if (_hostToggleAvailable)
            {
                _hostNameText.text = _isHostAddressVisible ? _currentHostAddress : "****";
            }
            else
            {
                _hostNameText.text = _currentHostAddress;
            }

            UpdateHostVisibilityIcons();
        }

        private void UpdateHostVisibilityIcons()
        {
            var image = _hostVisibilityToggle != null ? _hostVisibilityToggle.image : null;
            if (image == null)
                return;

            if (_hostToggleAvailable)
            {
                image.enabled = true;
                if (_isHostAddressVisible && _hostVisibleSprite != null)
                    image.sprite = _hostVisibleSprite;
                else if (!_isHostAddressVisible && _hostHiddenSprite != null)
                    image.sprite = _hostHiddenSprite;
            }
            else
            {
                if (_hostHiddenSprite != null)
                {
                    image.enabled = true;
                    image.sprite = _hostHiddenSprite;
                }
                else if (_hostVisibleSprite != null)
                {
                    image.enabled = true;
                    image.sprite = _hostVisibleSprite;
                }
                else
                {
                    image.enabled = false;
                }
            }
        }

        private void SetPasswordValue(string password, bool hasPassword, bool allowToggle)
        {
            string previousPassword = _currentPassword;
            bool previousHasPassword = _hasPassword;
            bool previousToggleAvailable = _passwordToggleAvailable;

            string sanitizedPassword;
            if (!hasPassword)
            {
                sanitizedPassword = string.Empty;
            }
            else if (!string.IsNullOrEmpty(password))
            {
                sanitizedPassword = password;
            }
            else if (!string.IsNullOrEmpty(previousPassword))
            {
                sanitizedPassword = previousPassword;
            }
            else if (_activeBookmark != null && !string.IsNullOrEmpty(_activeBookmark.password))
            {
                sanitizedPassword = _activeBookmark.password;
            }
            else
            {
                sanitizedPassword = string.Empty;
            }

            bool passwordChanged = !string.Equals(previousPassword, sanitizedPassword, StringComparison.Ordinal);
            bool hasPasswordChanged = previousHasPassword != hasPassword;

            bool retainFromLocal = hasPassword && string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(previousPassword);
            bool newToggleAvailable = hasPassword && (allowToggle || retainFromLocal || !string.IsNullOrEmpty(sanitizedPassword));
            bool toggleAvailabilityChanged = previousToggleAvailable != newToggleAvailable;

            _hasPassword = hasPassword;
            _currentPassword = sanitizedPassword;
            _passwordToggleAvailable = newToggleAvailable;

            if (!newToggleAvailable || passwordChanged || hasPasswordChanged || (toggleAvailabilityChanged && newToggleAvailable))
            {
                _isPasswordVisible = false;
            }

            ApplyPasswordVisibility();
        }

        private void ApplyPasswordVisibility()
        {
            if (_passwordValueText != null)
            {
                if (!_hasPassword)
                {
                    _passwordValueText.text = "None";
                }
                else if (_passwordToggleAvailable)
                {
                    _passwordValueText.text = _isPasswordVisible ? _currentPassword : "****";
                }
                else
                {
                    _passwordValueText.text = "****";
                }
            }

            UpdatePasswordVisibilityIcons();
            UpdatePasswordContainersVisibility();
        }

        private void UpdatePasswordVisibilityIcons()
        {
            var image = _passwordVisibilityToggle != null ? _passwordVisibilityToggle.image : null;
            if (image == null)
                return;

            if (_passwordToggleAvailable)
            {
                image.enabled = true;
                if (_isPasswordVisible && _passwordVisibleSprite != null)
                    image.sprite = _passwordVisibleSprite;
                else if (!_isPasswordVisible && _passwordHiddenSprite != null)
                    image.sprite = _passwordHiddenSprite;
            }
            else
            {
                if (_passwordHiddenSprite != null)
                {
                    image.enabled = true;
                    image.sprite = _passwordHiddenSprite;
                }
                else if (_passwordVisibleSprite != null)
                {
                    image.enabled = true;
                    image.sprite = _passwordVisibleSprite;
                }
                else
                {
                    image.enabled = false;
                }
            }
        }

        private void ToggleHostVisibility()
        {
            if (!_hostToggleAvailable)
                return;

            _isHostAddressVisible = !_isHostAddressVisible;
            ApplyHostVisibility();
        }

        private void TogglePasswordVisibility()
        {
            if (!_passwordToggleAvailable)
                return;

            _isPasswordVisible = !_isPasswordVisible;
            ApplyPasswordVisibility();
        }

        #endregion

        #region Hosted Preset Editing

        private void UpdateHostedLobbyPasswordVisibility()
        {
            bool show = _currentMode == SidebarMode.HostedLobby &&
                        _activePreset != null &&
                        _activePreset.PrivacyMode == YargNetworkManager.LobbyPrivacyMode.Private;

            if (_hostedLobbyPasswordRow != null)
                _hostedLobbyPasswordRow.SetActive(show);

            if (!show && _hostedLobbyPasswordInput != null)
            {
                _hostedLobbyPasswordInput.DeactivateInputField();
            }
        }

        private void ApplyHostedPresetToFields(HostedLobbyPreset preset)
        {
            _suppressHostedFieldCallbacks = true;

            string lobbyName = preset?.lobbyName ?? string.Empty;
            SetInputFieldText(_hostedLobbyNameInput, lobbyName);

            if (_hostedLobbyMaxPlayersDropdown != null)
            {
                EnsureMaxPlayersDropdownOptions(_hostedLobbyMaxPlayersDropdown);
                int desiredPlayers = Mathf.Clamp(preset?.maxPlayers ?? 8, 2, 32);
                int optionIndex = FindMaxPlayersOptionIndex(_hostedLobbyMaxPlayersDropdown, desiredPlayers);
                if (optionIndex >= 0)
                {
                    _hostedLobbyMaxPlayersDropdown.value = optionIndex;
                    _hostedLobbyMaxPlayersDropdown.RefreshShownValue();
                }
            }

            if (_hostedLobbyPrivacyDropdown != null)
            {
                EnsurePrivacyDropdownOptions(_hostedLobbyPrivacyDropdown);
                int privacyIndex = Mathf.Clamp((int)(preset?.PrivacyMode ?? YargNetworkManager.LobbyPrivacyMode.Public), 0, 2);
                _hostedLobbyPrivacyDropdown.value = privacyIndex;
                _hostedLobbyPrivacyDropdown.RefreshShownValue();
            }

            if (_hostedLobbyPasswordInput != null)
            {
                string password = preset != null && preset.PrivacyMode == YargNetworkManager.LobbyPrivacyMode.Private
                    ? preset.password ?? string.Empty
                    : string.Empty;
                SetInputFieldText(_hostedLobbyPasswordInput, password);
            }

            _suppressHostedFieldCallbacks = false;

            UpdateHostedLobbyPasswordVisibility();
        }

        private void ResetHostedForm()
        {
            _suppressHostedFieldCallbacks = true;

            SetInputFieldText(_hostedLobbyNameInput, string.Empty);

            if (_hostedLobbyMaxPlayersDropdown != null)
            {
                EnsureMaxPlayersDropdownOptions(_hostedLobbyMaxPlayersDropdown);
                if (_hostedLobbyMaxPlayersDropdown.options != null && _hostedLobbyMaxPlayersDropdown.options.Count > 0)
                {
                    int index = Mathf.Clamp(_defaultMaxPlayersOptionIndex, 0, _hostedLobbyMaxPlayersDropdown.options.Count - 1);
                    _hostedLobbyMaxPlayersDropdown.value = index;
                    _hostedLobbyMaxPlayersDropdown.RefreshShownValue();
                }
            }

            if (_hostedLobbyPrivacyDropdown != null)
            {
                EnsurePrivacyDropdownOptions(_hostedLobbyPrivacyDropdown);
                _hostedLobbyPrivacyDropdown.value = (int)YargNetworkManager.LobbyPrivacyMode.Public;
                _hostedLobbyPrivacyDropdown.RefreshShownValue();
            }

            SetInputFieldText(_hostedLobbyPasswordInput, string.Empty);

            _suppressHostedFieldCallbacks = false;

            UpdateHostedLobbyPasswordVisibility();
        }

        private void CommitHostedPreset(string lobbyName = null, int? maxPlayers = null, YargNetworkManager.LobbyPrivacyMode? privacyMode = null, string password = null)
        {
            if (_activePreset == null)
                return;

            string newName = lobbyName ?? (_activePreset.lobbyName ?? string.Empty);
            int newMaxPlayers = Mathf.Clamp(maxPlayers ?? _activePreset.maxPlayers, 2, 32);
            var newPrivacy = privacyMode ?? _activePreset.PrivacyMode;
            string newPassword = password ?? (_activePreset.password ?? string.Empty);

            if (newPrivacy != YargNetworkManager.LobbyPrivacyMode.Private)
            {
                newPassword = string.Empty;
            }

            bool changed =
                !string.Equals(_activePreset.lobbyName ?? string.Empty, newName, StringComparison.Ordinal) ||
                _activePreset.maxPlayers != newMaxPlayers ||
                _activePreset.PrivacyMode != newPrivacy ||
                !string.Equals(_activePreset.password ?? string.Empty, newPassword ?? string.Empty, StringComparison.Ordinal);

            if (!changed)
                return;

            try
            {
                var updated = LobbyBookmarkStore.Instance.UpsertMyLobby(
                    _activePreset.id,
                    newName,
                    newMaxPlayers,
                    newPrivacy,
                    newPassword ?? string.Empty,
                    updateHostedTimestamp: false);

                _activePreset = updated?.Clone();
                ApplyHostedPresetToFields(_activePreset);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserSidebar] Failed to save hosted lobby preset '{_activePreset?.id}': {ex}");
            }
        }

        private void CommitHostedPresetFromFields()
        {
            if (_activePreset == null)
                return;

            string name = _hostedLobbyNameInput != null ? _hostedLobbyNameInput.text?.Trim() ?? string.Empty : _activePreset.lobbyName;
            int players = GetSelectedMaxPlayers(_hostedLobbyMaxPlayersDropdown);
            var privacy = _hostedLobbyPrivacyDropdown != null
                ? (YargNetworkManager.LobbyPrivacyMode)Mathf.Clamp(_hostedLobbyPrivacyDropdown.value, 0, 2)
                : _activePreset.PrivacyMode;
            string password = _hostedLobbyPasswordInput != null ? _hostedLobbyPasswordInput.text ?? string.Empty : _activePreset.password;

            CommitHostedPreset(name, players, privacy, password);
        }

        private void OnHostedLobbyNameSubmitted(string value)
        {
            if (_suppressHostedFieldCallbacks)
                return;

            string trimmed = value?.Trim() ?? string.Empty;
            CommitHostedPreset(lobbyName: trimmed);
        }

        private void OnHostedLobbyMaxPlayersChanged(int optionIndex)
        {
            if (_suppressHostedFieldCallbacks)
                return;

            int players = ParseMaxPlayersOption(_hostedLobbyMaxPlayersDropdown, optionIndex);
            CommitHostedPreset(maxPlayers: players);
        }

        private void OnHostedLobbyPrivacyChanged(int optionIndex)
        {
            if (_suppressHostedFieldCallbacks)
                return;

            var privacy = (YargNetworkManager.LobbyPrivacyMode)Mathf.Clamp(optionIndex, 0, 2);
            string password = _hostedLobbyPasswordInput != null ? _hostedLobbyPasswordInput.text : string.Empty;

            CommitHostedPreset(privacyMode: privacy, password: password);
            UpdateHostedLobbyPasswordVisibility();

            if (privacy == YargNetworkManager.LobbyPrivacyMode.Private && _hostedLobbyPasswordInput != null)
            {
                FocusInput(_hostedLobbyPasswordInput);
            }
        }

        private void OnHostedLobbyPasswordSubmitted(string value)
        {
            if (_suppressHostedFieldCallbacks)
                return;

            string trimmed = value?.Trim() ?? string.Empty;
            CommitHostedPreset(password: trimmed);
        }

        private void HandleHostedHost()
        {
            CommitHostedPresetFromFields();

            if (_activePreset != null)
            {
                _menu?.StartHostedLobby(_activePreset);
            }
        }

        private void HandleHostedDelete()
        {
            if (_activePreset == null)
                return;

            var preset = _activePreset;
            string presetName = string.IsNullOrWhiteSpace(preset.lobbyName) ? "My Lobby" : preset.lobbyName;
            const string confirmText = "DELETE";

            void PerformDelete()
            {
                bool removed = LobbyBookmarkStore.Instance.RemoveMyLobby(preset.id);
                if (!removed)
                    return;

                ToastManager.ToastInformation(ZString.Format("Deleted \"{0}\".", presetName));
                ClearLobby();
            }

            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.ShowConfirmDeleteDialog(presetName, PerformDelete, confirmText);
            }
            else
            {
                PerformDelete();
            }
        }

        #endregion

        #region Editable Fields

        // Compatibility wrappers for existing prefab bindings
        public void BeginLobbyNameEdit() => SetLobbyNameEditMode(true);
        public void ConfirmLobbyNameEdit() => SetLobbyNameEditMode(false);
        public void CancelLobbyNameEdit() => CancelEditableFieldInternal(EditableField.LobbyName);

        public void BeginHostAddressEdit() => SetHostAddressEditMode(true);
        public void ConfirmHostAddressEdit() => SetHostAddressEditMode(false);
        public void CancelHostAddressEdit() => CancelEditableFieldInternal(EditableField.HostAddress);

        public void BeginPasswordEdit() => SetPasswordEditMode(true);
        public void ConfirmPasswordEdit() => SetPasswordEditMode(false);
        public void CancelPasswordEdit() => CancelEditableFieldInternal(EditableField.Password);

        public void SetLobbyNameEditMode(bool editing) => SetEditableMode(EditableField.LobbyName, editing);
        public void SetHostAddressEditMode(bool editing) => SetEditableMode(EditableField.HostAddress, editing);
        public void SetPasswordEditMode(bool editing) => SetEditableMode(EditableField.Password, editing);

        private void SetEditableMode(EditableField field, bool editing)
        {
            if (!CanEditSelectedBookmark())
            {
                ExitAllEditModes();
                return;
            }

            if (field == EditableField.Password)
            {
                EnsurePasswordEditInputField();
            }

            var state = GetEditableFieldState(field);
            if (state.ViewContainer == null || state.EditContainer == null)
            {
                ExitAllEditModes();
                return;
            }

            if (editing)
            {
                if (_activeEditField == field)
                    return;

                ExitAllEditModes();
                _activeEditField = field;

                if (state.EditContainer != null)
                    EnsureContainerHierarchyActive(state.EditContainer);

                if (state.ViewContainer != null)
                    EnsureContainerHierarchyActive(state.ViewContainer);

                SetEditContainers(state.ViewContainer, state.EditContainer, true);

                var input = state.GetInputField();
                if (input != null)
                {
                    input.interactable = true;
                    input.readOnly = false;

                    string initialValue = GetCurrentEditableValue(field);
                    SetInputFieldText(input, initialValue);
                }

                UpdatePasswordContainersVisibility();

                if (input != null)
                {
                    FocusInput(input);
                    MoveCaretToEnd(input);
                }
                return;
            }

            if (_activeEditField != field)
            {
                ExitAllEditModes();
                return;
            }

            if (!TryCommitEditableField(field, state.GetInputField()))
            {
                var input = state.GetInputField();
                if (input != null)
                {
                    input.caretPosition = input.text.Length;
                    FocusInput(input);
                }
                return;
            }

            ExitAllEditModes();

            if (_activeBookmark != null)
            {
                PopulateBookmarkInfo(_activeBookmark);
            }

            UpdatePasswordContainersVisibility();
        }

        public bool IsEditingBookmark(LobbyBookmark bookmark)
        {
            if (!IsEditing || bookmark == null || _activeBookmark == null)
                return false;

            return string.Equals(_activeBookmark.EndpointKey, bookmark.EndpointKey, StringComparison.OrdinalIgnoreCase);
        }

        private void CancelEditableFieldInternal(EditableField field)
        {
            if (_activeEditField == field)
            {
                ExitAllEditModes();
            }
        }

        private bool TryCommitEditableField(EditableField field, TMP_InputField input)
        {
            var bookmark = _activeBookmark;
            if (bookmark == null)
                return false;

            switch (field)
            {
                case EditableField.LobbyName:
                    if (input == null)
                        return false;

                    string displayName = input.text?.Trim() ?? string.Empty;
                    LobbyBookmarkStore.Instance.UpdateBookmark(bookmark, displayName, bookmark.address, bookmark.port, bookmark.password);
                    ToastManager.ToastInformation("Bookmark name saved.");
                    return true;

                case EditableField.HostAddress:
                    if (input == null)
                        return false;

                    string submitted = input.text?.Trim() ?? string.Empty;
                    int fallbackPort = bookmark.port > 0 ? bookmark.port : GetSuggestedPort();

                    if (!EndpointUtility.TryParseEndpoint(submitted, fallbackPort, out string address, out int port, out string error))
                    {
                        ToastManager.ToastError(string.IsNullOrEmpty(error) ? "Enter a valid address." : error);
                        return false;
                    }

                    LobbyBookmarkStore.Instance.UpdateBookmark(bookmark, bookmark.displayName, address, port, bookmark.password);
                    ToastManager.ToastInformation("Address saved.");
                    return true;

                case EditableField.Password:
                    if (input == null)
                        return false;

                    string password = input.text ?? string.Empty;
                    LobbyBookmarkStore.Instance.UpdateBookmark(bookmark, bookmark.displayName, bookmark.address, bookmark.port, password);
                    ToastManager.ToastInformation("Password saved.");
                    return true;

                default:
                    return false;
            }
        }

        private void ExitAllEditModes()
        {
            _activeEditField = null;
            SetEditContainers(_lobbyNameViewContainer, _lobbyNameEditContainer, false);
            SetEditContainers(_hostAddressViewContainer, _hostAddressEditContainer, false);
            SetEditContainers(_passwordViewContainer, _passwordEditContainer, false);
            ResetEditableInputs();
            UpdatePasswordContainersVisibility();
        }

        private void ResetEditableInputs()
        {
            SetInputFieldValue(_lobbyNameEditContainer, EditableField.LobbyName);
            SetInputFieldValue(_hostAddressEditContainer, EditableField.HostAddress);
            SetInputFieldValue(_passwordEditContainer, EditableField.Password);
        }

        private void SetInputFieldValue(GameObject editContainer, EditableField field)
        {
            if (editContainer == null)
                return;

            TMP_InputField input = editContainer == _passwordEditContainer
                ? EnsurePasswordEditInputField()
                : editContainer.GetComponentInChildren<TMP_InputField>(true);
            if (input == null)
                return;

            SetInputFieldText(input, GetCurrentEditableValue(field));
        }

        private void RefreshEditableButtons()
        {
            bool canEdit = CanEditSelectedBookmark();

            SetButtonState(_lobbyNameEditButton, canEdit);
            SetButtonState(_hostAddressEditButton, canEdit);
            SetButtonState(_passwordEditButton, canEdit);
            UpdatePasswordContainersVisibility();
        }

        private bool CanEditSelectedBookmark()
        {
            return _currentMode == SidebarMode.Lobby && _activeBookmark != null;
        }

        private static void SetButtonState(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.interactable = enabled;
            button.gameObject.SetActive(enabled);
        }

        private static void SetEditContainers(GameObject viewContainer, GameObject editContainer, bool editing)
        {
            if (viewContainer != null)
                viewContainer.SetActive(!editing);

            if (editContainer != null)
                editContainer.SetActive(editing);
        }

        private string GetCurrentEditableValue(EditableField field)
        {
            var bookmark = _activeBookmark;
            if (bookmark == null)
                return string.Empty;

            return field switch
            {
                EditableField.LobbyName => bookmark.displayName ?? string.Empty,
                EditableField.HostAddress => FormatBookmarkEndpoint(bookmark),
                EditableField.Password => GetPasswordEditableValue(bookmark),
                _ => string.Empty
            };
        }

        private string GetPasswordEditableValue(LobbyBookmark bookmark)
        {
            if (!string.IsNullOrEmpty(_currentPassword))
                return _currentPassword;

            if (bookmark != null && !string.IsNullOrEmpty(bookmark.password))
                return bookmark.password;

            return string.Empty;
        }

        private string FormatBookmarkEndpoint(LobbyBookmark bookmark)
        {
            if (bookmark == null)
                return string.Empty;

            int port = bookmark.port > 0 ? bookmark.port : GetSuggestedPort();
            return EndpointUtility.FormatEndpoint(bookmark.address ?? string.Empty, port);
        }

        private EditableFieldState GetEditableFieldState(EditableField field)
        {
            return field switch
            {
                EditableField.LobbyName => new EditableFieldState(_lobbyNameViewContainer, _lobbyNameEditContainer),
                EditableField.HostAddress => new EditableFieldState(_hostAddressViewContainer, _hostAddressEditContainer),
                EditableField.Password => new EditableFieldState(_passwordViewContainer, _passwordEditContainer, EnsurePasswordEditInputField()),
                _ => default
            };
        }

        private readonly struct EditableFieldState
        {
            public readonly GameObject ViewContainer;
            public readonly GameObject EditContainer;
            private readonly TMP_InputField _explicitInput;

            public EditableFieldState(GameObject viewContainer, GameObject editContainer, TMP_InputField explicitInput = null)
            {
                ViewContainer = viewContainer;
                EditContainer = editContainer;
                _explicitInput = explicitInput;
            }

            public TMP_InputField GetInputField()
            {
                if (_explicitInput != null)
                    return _explicitInput;

                if (EditContainer == null)
                    return null;

                return EditContainer.GetComponentInChildren<TMP_InputField>(true);
            }
        }

        private TMP_InputField EnsurePasswordEditInputField()
        {
            if (_passwordEditInputField != null)
                return _passwordEditInputField;

            if (_passwordEditContainer == null)
                return null;

            _passwordEditInputField = _passwordEditContainer.GetComponentInChildren<TMP_InputField>(true);
            if (_passwordEditInputField == null)
            {
                Debug.LogWarning("[LobbyBrowserSidebar] Password edit container is missing a TMP_InputField. Please assign it in the prefab.");
            }

            return _passwordEditInputField;
        }

        private void UpdatePasswordContainersVisibility()
        {
            bool editing = _activeEditField == EditableField.Password;
            if (editing)
            {
                if (_passwordViewContainer != null)
                    _passwordViewContainer.SetActive(false);

                if (_passwordEditContainer != null)
                    _passwordEditContainer.SetActive(true);

                if (_passwordEditButton != null)
                    _passwordEditButton.gameObject.SetActive(false);

                if (_passwordVisibilityToggle != null)
                {
                    _passwordVisibilityToggle.gameObject.SetActive(false);
                    _passwordVisibilityToggle.interactable = false;
                }

                return;
            }

            bool showRow = false;

            if (_currentMode == SidebarMode.Lobby)
            {
                if (_currentPrivacyMode == YargNetworkManager.LobbyPrivacyMode.Private)
                {
                    showRow = true;
                }
                else if (_hasPassword || !string.IsNullOrEmpty(_currentPassword))
                {
                    showRow = true;
                }
                else if (_activeBookmark != null && !string.IsNullOrEmpty(_activeBookmark.password))
                {
                    showRow = true;
                }
            }

            if (_passwordViewContainer != null)
                _passwordViewContainer.SetActive(showRow);

            if (_passwordEditContainer != null)
                _passwordEditContainer.SetActive(false);

            if (_passwordEditButton != null)
            {
                bool canEditBookmark = CanEditSelectedBookmark();
                _passwordEditButton.gameObject.SetActive(showRow && canEditBookmark);
                _passwordEditButton.interactable = canEditBookmark;
            }

            if (_passwordVisibilityToggle != null)
            {
                bool toggleVisible = showRow && _hasPassword;
                _passwordVisibilityToggle.gameObject.SetActive(toggleVisible);
                _passwordVisibilityToggle.interactable = toggleVisible && _passwordToggleAvailable;
            }
        }

        #endregion

        #region View Helpers

        private void ShowMode(SidebarMode mode)
        {
            EnsureContainerReferences();

            _currentMode = mode;

            if (_emptyStateContainer != null)
                _emptyStateContainer.SetActive(mode == SidebarMode.Empty);

            bool hostedMode = mode == SidebarMode.HostedLobby;
            bool hostedInsideContent = hostedMode &&
                                        _hostedLobbyContainer != null &&
                                        _contentContainer != null &&
                                        _hostedLobbyContainer.transform.IsChildOf(_contentContainer.transform);

            if (_contentContainer != null)
                _contentContainer.SetActive(mode == SidebarMode.Lobby || hostedInsideContent);

            if (_createLobbyContainer != null)
                _createLobbyContainer.SetActive(mode == SidebarMode.CreateLobby);

            if (_hostedLobbyContainer != null)
            {
                _hostedLobbyContainer.SetActive(hostedMode);
                if (hostedMode)
                    EnsureContainerHierarchyActive(_hostedLobbyContainer);
            }

            if (_directConnectContainer != null)
                _directConnectContainer.SetActive(mode == SidebarMode.DirectConnect);

            if (mode == SidebarMode.CreateLobby && _createLobbyContainer != null)
                EnsureContainerHierarchyActive(_createLobbyContainer);

            if (mode == SidebarMode.DirectConnect && _directConnectContainer != null)
                EnsureContainerHierarchyActive(_directConnectContainer);

            UpdatePasswordContainersVisibility();
            UpdateHostedLobbyPasswordVisibility();
        }

        private void EnsureContainerHierarchyActive(GameObject container)
        {
            if (container == null)
                return;

            var current = container.transform;
            while (current != null && current != transform)
            {
                var go = current.gameObject;
                if (!go.activeSelf)
                {
                    go.SetActive(true);
                }

                current = current.parent;
            }
        }

        private void EnsureContainerReferences()
        {
            if (_hostedLobbyContainer == null && !_attemptedHostedContainerResolve)
            {
                _attemptedHostedContainerResolve = true;
                var resolved = TryResolveHostedContainer();
                if (resolved != null)
                {
                    _hostedLobbyContainer = resolved;
                    Debug.LogWarning("[LobbyBrowserSidebar] Hosted lobby container reference was missing; auto-assigned at runtime. Please assign it in the prefab to avoid this lookup.");
                }
                else
                {
                    Debug.LogWarning("[LobbyBrowserSidebar] Hosted lobby container reference is missing and could not be auto-resolved. Hosted presets may remain hidden.");
                }
            }
        }

        private GameObject TryResolveHostedContainer()
        {
            var markers = new List<Transform>(4);

            if (_hostedLobbyNameInput != null)
                markers.Add(_hostedLobbyNameInput.transform);
            if (_hostedLobbyMaxPlayersDropdown != null)
                markers.Add(_hostedLobbyMaxPlayersDropdown.transform);
            if (_hostedLobbyPrivacyDropdown != null)
                markers.Add(_hostedLobbyPrivacyDropdown.transform);
            if (_hostedLobbyHostButton != null)
                markers.Add(_hostedLobbyHostButton.transform);
            if (_hostedLobbyDeleteButton != null)
                markers.Add(_hostedLobbyDeleteButton.transform);

            markers.RemoveAll(t => t == null);
            if (markers.Count < 2)
                return null;

            var candidate = FindCommonAncestorWithinSidebar(markers);
            if (candidate != null && candidate != transform)
                return candidate.gameObject;

            return null;
        }

        private Transform FindCommonAncestorWithinSidebar(IReadOnlyList<Transform> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return null;

            var baseChain = BuildAncestorChain(nodes[0]);
            foreach (var candidate in baseChain)
            {
                if (candidate == null || candidate == transform)
                    continue;

                bool containsAll = true;
                for (int i = 1; i < nodes.Count; i++)
                {
                    var other = nodes[i];
                    if (other == null || !IsDescendantOf(other, candidate))
                    {
                        containsAll = false;
                        break;
                    }
                }

                if (containsAll)
                    return candidate;
            }

            return null;
        }

        private List<Transform> BuildAncestorChain(Transform start)
        {
            var chain = new List<Transform>();
            var current = start;
            while (current != null)
            {
                chain.Add(current);
                if (current == transform)
                    break;
                current = current.parent;
            }

            return chain;
        }

        private static bool IsDescendantOf(Transform node, Transform potentialAncestor)
        {
            var current = node;
            while (current != null)
            {
                if (current == potentialAncestor)
                    return true;

                current = current.parent;
            }

            return false;
        }

        #endregion

        #region Form Submission

        private void SubmitCreateLobbyForm()
        {
            string lobbyName = _createLobbyNameInput != null ? _createLobbyNameInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(lobbyName))
            {
                ToastManager.ToastError("Lobby name can't be empty.");
                FocusInput(_createLobbyNameInput);
                return;
            }

            int maxPlayers = GetSelectedMaxPlayers();
            if (maxPlayers < 2 || maxPlayers > 32)
            {
                ToastManager.ToastError("Max players must be between 2 and 32.");
                return;
            }

            var privacyMode = GetSelectedPrivacyMode();
            string password = string.Empty;

            if (privacyMode == YargNetworkManager.LobbyPrivacyMode.Private)
            {
                password = _createLobbyPasswordInput != null ? _createLobbyPasswordInput.text.Trim() : string.Empty;
                if (string.IsNullOrEmpty(password))
                {
                    ToastManager.ToastWarning("Set a password for private lobbies.");
                    FocusInput(_createLobbyPasswordInput);
                    return;
                }
            }

            var data = new CreateLobbyFormData(
                _activePreset?.id ?? string.Empty,
                lobbyName,
                maxPlayers,
                privacyMode,
                password);

            ToastManager.ToastInformation(ZString.Format("Hosting {0}...", lobbyName));
            CreateLobbySubmitted?.Invoke(data);
        }

        private void SubmitDirectConnectForm()
        {
            string endpointInput = _directConnectAddressInput != null ? _directConnectAddressInput.text : string.Empty;
            if (!EndpointUtility.TryParseEndpoint(endpointInput, GetSuggestedPort(), out string address, out int port, out string error))
            {
                ToastManager.ToastError(string.IsNullOrEmpty(error) ? "Enter a valid address." : error);
                FocusInput(_directConnectAddressInput);
                return;
            }

            string displayName = string.Empty;
            string password = _directConnectPasswordInput != null ? _directConnectPasswordInput.text : string.Empty;

            var form = new DirectConnectFormData(address, port, displayName, password);
            ToastManager.ToastInformation(ZString.Format("Connecting to {0}...", EndpointUtility.FormatEndpoint(address, port)));
            DirectConnectSubmitted?.Invoke(form);
        }

        private static void SetInputFieldText(TMP_InputField field, string value)
        {
            if (field == null)
                return;

            string sanitized = value ?? string.Empty;
            string current = field.text ?? string.Empty;
            bool changed = !string.Equals(current, sanitized, StringComparison.Ordinal);

            int caret = -1;
            int anchor = -1;
            int focus = -1;

            if (field.isFocused)
            {
                caret = field.caretPosition;
                anchor = field.selectionAnchorPosition;
                focus = field.selectionFocusPosition;
            }

            if (changed)
            {
                field.SetTextWithoutNotify(sanitized);
            }

            field.ForceLabelUpdate();

            if (caret < 0)
                return;

            int length = field.text?.Length ?? sanitized.Length;
            caret = Mathf.Clamp(caret, 0, length);
            anchor = Mathf.Clamp(anchor, 0, length);
            focus = Mathf.Clamp(focus, 0, length);

            field.caretPosition = caret;
            field.selectionAnchorPosition = anchor;
            field.selectionFocusPosition = focus;
        }

        private static void FocusInput(TMP_InputField field)
        {
            if (field == null)
                return;

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                if (eventSystem.currentSelectedGameObject != field.gameObject)
                {
                    eventSystem.SetSelectedGameObject(null);
                    eventSystem.SetSelectedGameObject(field.gameObject);
                }
                else
                {
                    field.OnSelect(new BaseEventData(eventSystem));
                }
            }

            field.Select();
            field.ActivateInputField();
        }

        private static void MoveCaretToEnd(TMP_InputField field)
        {
            if (field == null)
                return;

            field.MoveTextEnd(false);

            int length = field.text?.Length ?? 0;
            field.caretPosition = length;
            field.selectionAnchorPosition = length;
            field.selectionFocusPosition = length;
        }

        private static void EnsureMaxPlayersDropdownOptions(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
                return;

            if (dropdown.options == null || dropdown.options.Count == 0)
            {
                var fallbackOptions = new List<string>();
                for (int i = 2; i <= 32; i++)
                {
                    fallbackOptions.Add(i.ToString());
                }

                dropdown.ClearOptions();
                dropdown.AddOptions(fallbackOptions);
            }
        }

        private static void EnsurePrivacyDropdownOptions(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
                return;

            if (dropdown.options == null || dropdown.options.Count == 0)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(new List<string>
                {
                    "Public",
                    "Private (Password)",
                    "Friends Only"
                });
            }
        }

        private static bool HostedPresetsEquivalent(HostedLobbyPreset currentPreset, HostedLobbyPreset targetPreset)
        {
            if (ReferenceEquals(currentPreset, targetPreset))
                return true;

            if (currentPreset == null || targetPreset == null)
                return currentPreset == null && targetPreset == null;

            return string.Equals(currentPreset.id ?? string.Empty, targetPreset.id ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(currentPreset.lobbyName ?? string.Empty, targetPreset.lobbyName ?? string.Empty, StringComparison.Ordinal)
                && currentPreset.maxPlayers == targetPreset.maxPlayers
                && currentPreset.PrivacyMode == targetPreset.PrivacyMode
                && string.Equals(currentPreset.password ?? string.Empty, targetPreset.password ?? string.Empty, StringComparison.Ordinal);
        }

        private static int FindMaxPlayersOptionIndex(TMP_Dropdown dropdown, int desiredPlayers)
        {
            if (dropdown == null || dropdown.options == null)
                return -1;

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                var option = dropdown.options[i];
                if (option != null && int.TryParse(option.text, out int value) && value == desiredPlayers)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ParseMaxPlayersOption(TMP_Dropdown dropdown, int optionIndex)
        {
            if (dropdown == null || dropdown.options == null)
                return 8;

            if (optionIndex < 0 || optionIndex >= dropdown.options.Count)
                return 8;

            var option = dropdown.options[optionIndex];
            if (option != null && int.TryParse(option.text, out int value))
                return value;

            return 8;
        }

        private int GetSelectedMaxPlayers()
        {
            int parsed = GetSelectedMaxPlayers(_createLobbyMaxPlayersDropdown);
            return parsed > 0 ? parsed : 8;
        }

        private static int GetSelectedMaxPlayers(TMP_Dropdown dropdown)
        {
            return ParseMaxPlayersOption(dropdown, dropdown != null ? dropdown.value : -1);
        }

        private YargNetworkManager.LobbyPrivacyMode GetSelectedPrivacyMode()
        {
            if (_createLobbyPrivacyDropdown == null)
                return YargNetworkManager.LobbyPrivacyMode.Public;

            return (YargNetworkManager.LobbyPrivacyMode)Mathf.Clamp(_createLobbyPrivacyDropdown.value, 0, 2);
        }

        #endregion

        #region Data Contracts

        public readonly struct CreateLobbyFormData
        {
            public string PresetId { get; }
            public string LobbyName { get; }
            public int MaxPlayers { get; }
            public YargNetworkManager.LobbyPrivacyMode PrivacyMode { get; }
            public string Password { get; }

            public CreateLobbyFormData(string presetId, string lobbyName, int maxPlayers, YargNetworkManager.LobbyPrivacyMode privacyMode, string password)
            {
                PresetId = presetId ?? string.Empty;
                LobbyName = lobbyName ?? string.Empty;
                MaxPlayers = maxPlayers;
                PrivacyMode = privacyMode;
                Password = password ?? string.Empty;
            }
        }

        public readonly struct DirectConnectFormData
        {
            public string Address { get; }
            public int Port { get; }
            public string DisplayName { get; }
            public string Password { get; }

            public DirectConnectFormData(string address, int port, string displayName, string password)
            {
                Address = address ?? string.Empty;
                Port = port;
                DisplayName = displayName ?? string.Empty;
                Password = password ?? string.Empty;
            }
        }

        #endregion
    }
}
