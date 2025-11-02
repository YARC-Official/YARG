using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using YARG.Core.Input;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Menu.Data;
using YARG.Menu.Persistent;
using YARG.Networking;
using YARG.Networking.Bookmarks;
using Cysharp.Threading.Tasks;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Lobby browser menu using YARG's ListMenu pattern.
    /// Shows discovered lobbies with favorites support and live pinging for saved servers.
    /// </summary>
    public class LobbyBrowserMenu : ListMenu<LobbyViewType, LobbyView>
    {
        [Header("UI References")]
        [SerializeField]
        private TextMeshProUGUI _statusText;
        [SerializeField]
        private LobbyBrowserSidebar _sidebar;

        [Header("Navigation")]
        [SerializeField]
        private NavigationGroup _navigationGroup;

        private bool EnsureSidebar()
        {
            if (_sidebar != null)
                return true;

            try
            {
                // Prefer a sidebar that lives under this menu's hierarchy (even if it's inactive)
                _sidebar = GetComponentInChildren<LobbyBrowserSidebar>(includeInactive: true);
                if (_sidebar != null)
                    return true;

                // Fallback: search the scene for any sidebar instance so hover behaviour still works in misconfigured prefabs
                var sidebars = Resources.FindObjectsOfTypeAll<LobbyBrowserSidebar>();
                foreach (var candidate in sidebars)
                {
                    if (candidate == null)
                        continue;

                    // Ignore prefabs/assets that are not part of the active scene hierarchy
                    var go = candidate.gameObject;
                    if (go == null || !go.scene.IsValid())
                        continue;

                    _sidebar = candidate;
                    Debug.LogWarning("[LobbyBrowserMenu] Sidebar reference was missing; auto-assigned to scene instance.");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] EnsureSidebar encountered an exception: {ex}");
            }

            Debug.LogWarning("[LobbyBrowserMenu] Sidebar reference is not assigned. Hosted presets and details panel will be unavailable.");
            return false;
        }

        private const double STALE_LOBBY_SECONDS = 18.0;
        private const float STALE_SWEEP_INTERVAL = 1.0f;

        private LobbyFavorites _favorites;
        private List<YargNetworkManager.LobbyInfo> _currentLobbies = new();
        private readonly List<int> _sectionStartIndices = new();
        private YargNetworkManager.LobbyInfo _selectedLobby;
        private bool _navigationSchemePushed;
        private string _lastNavigationHelpSignature;
        private (int favorites, int myLobbies, int recents, int discovered, int total) _lastLoggedSummary;
        private bool _hasLoggedSummary;
        // Timestamp of last SelectedIndex change (unscaled time)
        private float _lastSelectedIndexChangeTime = -1f;

        // Cache for ping results: endpointKey -> LobbyInfo (if online)
        private Dictionary<string, YargNetworkManager.LobbyInfo> _pingedLobbies = new();
        private HashSet<string> _pendingPings = new();
        private bool _isPingingSavedServers = false;
        private YargNetworkDiscovery _discovery;
        // Track the last view the sidebar was asked to show (hover or selection) so discovery updates can refresh it.
        private LobbyViewType _lastShownSidebarView;
        private float _nextStaleSweepAt;

        protected override int ExtraListViewPadding => 15;

        protected override void Awake()
        {
            // Initialize favorites before calling base.Awake so CreateViewList (invoked by base) has a valid facade.
            _favorites = new LobbyFavorites();
            _favorites.OnFavoritesChanged += RefreshList;

            if (_navigationGroup == null)
            {
                _navigationGroup = GetComponent<NavigationGroup>() ?? gameObject.AddComponent<NavigationGroup>();
            }

            base.Awake();
        }

        private void OnDestroy()
        {
            if (_favorites != null)
            {
                _favorites.OnFavoritesChanged -= RefreshList;
                _favorites.Dispose();
                _favorites = null;
            }
        }

        private void OnEnable()
        {
            SetNavigationScheme();

            EnsureSidebar();

            if (YargNetworkManager.Instance != null)
                YargNetworkManager.Instance.OnLobbyListUpdated += OnLobbyListUpdated;

            // Wire discovery callbacks so direct ping responses update saved entries
            if (YargNetworkManager.Instance != null)
            {
                _discovery = YargNetworkManager.Instance.GetComponent<YargNetworkDiscovery>();
                if (_discovery != null)
                {
                    _discovery.OnLobbyDiscovered += OnDiscoveryLobbyDiscovered;
                    _discovery.OnLobbyLost += OnDiscoveryLobbyLost;
                }
            }

            if (_sidebar != null)
            {
                _sidebar.Initialize(this);
                _sidebar.CreateLobbySubmitted += OnCreateLobbySubmitted;
                _sidebar.DirectConnectSubmitted += OnDirectConnectSubmitted;
            }

            // Build the view list first so navigatables are added to the NavigationGroup
            RefreshList(false);

            if (_navigationGroup != null)
            {
                _navigationGroup.PushNavGroupToStack();
                    if (_navigationGroup.Count > 0 && (_navigationGroup.SelectedIndex == null || _navigationGroup.SelectedIndex < 0))
                    {
                        _navigationGroup.SelectFirst();
                    }
            }

            RefreshLobbies();

            UniTask.Void(async () => await PingSavedServersAsync());

            // Debug: subscribe to navigator events to ensure inputs reach this menu
            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent += OnNavigatorEvent;
            }

            _nextStaleSweepAt = Time.unscaledTime + STALE_SWEEP_INTERVAL;
        }

        private void OnDisable()
        {
            if (_navigationSchemePushed && Navigator.Instance != null)
            {
                Navigator.Instance.PopScheme();
                _navigationSchemePushed = false;
            }
            _lastNavigationHelpSignature = null;

            if (YargNetworkManager.Instance != null)
                YargNetworkManager.Instance.OnLobbyListUpdated -= OnLobbyListUpdated;

            if (_discovery != null)
            {
                _discovery.OnLobbyDiscovered -= OnDiscoveryLobbyDiscovered;
                _discovery.OnLobbyLost -= OnDiscoveryLobbyLost;
                _discovery = null;
            }

            if (_sidebar != null)
            {
                _sidebar.CreateLobbySubmitted -= OnCreateLobbySubmitted;
                _sidebar.DirectConnectSubmitted -= OnDirectConnectSubmitted;
                _sidebar.ClearLobby();
            }
            _selectedLobby = null;

            if (Navigator.Instance != null)
            {
                Navigator.Instance.NavigationEvent -= OnNavigatorEvent;
            }

            _nextStaleSweepAt = 0f;
        }

        protected override void OnSelectedIndexChanged()
        {
            base.OnSelectedIndexChanged();
            Debug.Log($"[LobbyBrowserMenu] SelectedIndex changed -> {SelectedIndex}");
            _lastSelectedIndexChangeTime = Time.unscaledTime;
            UpdateSidebarForSelection();
        }

        protected override List<LobbyViewType> CreateViewList()
        {
            var viewTypes = new List<LobbyViewType>();

            var discoveredLookup = _currentLobbies
                .Where(lobby => IsLobbyLive(lobby))
                .GroupBy(lobby => LobbyBookmarkUtility.BuildKey(lobby.ipAddress, lobby.port))
                .ToDictionary(group => group.Key, group => group.First());

            var usedEndpointKeys = new HashSet<string>();
            foreach (var bookmark in _favorites.GetFavorites())
                usedEndpointKeys.Add(bookmark.EndpointKey);
            foreach (var bookmark in _favorites.GetRecents())
                usedEndpointKeys.Add(bookmark.EndpointKey);

            var favoritesSection = new List<LobbyViewType>();
            var favoriteEndpointKeys = new HashSet<string>();
            var usedAddresses = new HashSet<string>();

            var allFavorites = _favorites.GetFavorites();
            var favoriteBookmarks = allFavorites
                .OrderByDescending(b => (discoveredLookup.ContainsKey(b.EndpointKey) || (_pingedLobbies.TryGetValue(b.EndpointKey, out var _pinged) && _pinged != null)))
                .ThenBy(b => b.createdAt)
                .ToList();
            foreach (var bookmark in favoriteBookmarks)
            {
                string addressKey = NormalizeAddress(bookmark.address);
                if (string.IsNullOrEmpty(addressKey) || !usedAddresses.Add(addressKey))
                    continue;

                favoriteEndpointKeys.Add(bookmark.EndpointKey);
                usedEndpointKeys.Add(bookmark.EndpointKey);

                YargNetworkManager.LobbyInfo liveInfo = null;
                if (discoveredLookup.TryGetValue(bookmark.EndpointKey, out var dl) && IsLobbyLive(dl))
                {
                    liveInfo = dl;
                }

                if ((liveInfo == null || !IsLobbyLive(liveInfo)) && _pingedLobbies.TryGetValue(bookmark.EndpointKey, out var pinged) && IsLobbyLive(pinged))
                {
                    liveInfo = pinged;
                }

                if (liveInfo != null && !IsLobbyLive(liveInfo))
                {
                    liveInfo = null;
                }

                var savedView = new SavedLobbyViewType(bookmark, this, _favorites) { LiveInfo = liveInfo };
                favoritesSection.Add(savedView);
            }

            var recentsSection = new List<LobbyViewType>();
            foreach (var bookmark in _favorites.GetRecents().OrderByDescending(entry => entry.lastConnected))
            {
                if (recentsSection.Count >= 5)
                    break;

                string addressKey = NormalizeAddress(bookmark.address);
                if (string.IsNullOrEmpty(addressKey) || usedAddresses.Contains(addressKey))
                    continue;

                if (favoriteEndpointKeys.Contains(bookmark.EndpointKey))
                    continue;

                usedEndpointKeys.Add(bookmark.EndpointKey);

                YargNetworkManager.LobbyInfo liveInfo = null;
                if (discoveredLookup.TryGetValue(bookmark.EndpointKey, out var dl2) && IsLobbyLive(dl2))
                {
                    liveInfo = dl2;
                }

                if ((liveInfo == null || !IsLobbyLive(liveInfo)) && _pingedLobbies.TryGetValue(bookmark.EndpointKey, out var pinged2) && IsLobbyLive(pinged2))
                {
                    liveInfo = pinged2;
                }

                if (liveInfo != null && !IsLobbyLive(liveInfo))
                {
                    liveInfo = null;
                }

                var savedView = new SavedLobbyViewType(bookmark, this, _favorites) { LiveInfo = liveInfo };
                recentsSection.Add(savedView);
            }

            var myLobbiesSection = new List<LobbyViewType>();
            var myLobbies = LobbyBookmarkStore.Instance.MyLobbies;
            if (myLobbies != null && myLobbies.Count > 0)
            {
                foreach (var preset in myLobbies)
                {
                    if (preset == null)
                        continue;

                    myLobbiesSection.Add(new MyLobbyViewType(preset, this));
                }
            }

            var discoveredSection = new List<LobbyViewType>();
            foreach (var lobby in _currentLobbies.OrderByDescending(l => l.currentPlayers))
            {
                if (!IsLobbyLive(lobby))
                    continue;

                string endpointKey = LobbyBookmarkUtility.BuildKey(lobby.ipAddress, lobby.port);
                if (usedEndpointKeys.Contains(endpointKey)) continue;
                discoveredSection.Add(new DiscoveredLobbyViewType(lobby, this, _favorites));
            }

            viewTypes.Add(new LobbyCategoryViewType("CREATE A LOBBY"));

            viewTypes.Add(new LobbyCategoryViewType("ADD NEW CONNECTION"));

            viewTypes.Add(new LobbyCategoryViewType("★ FAVORITES"));
            if (favoritesSection.Count > 0) viewTypes.AddRange(favoritesSection);
            else viewTypes.Add(new LobbyEmptyViewType("No favorites yet. Press Yellow on a lobby to save it."));

            viewTypes.Add(new LobbyCategoryViewType("MY LOBBIES"));
            if (myLobbiesSection.Count > 0) viewTypes.AddRange(myLobbiesSection);
            else viewTypes.Add(new LobbyEmptyViewType("Create a lobby to keep reusable presets here."));

            viewTypes.Add(new LobbyCategoryViewType("RECENT CONNECTIONS"));
            if (recentsSection.Count > 0) viewTypes.AddRange(recentsSection);
            else viewTypes.Add(new LobbyEmptyViewType("Recently joined servers will show up here."));

            viewTypes.Add(new LobbyCategoryViewType("DISCOVERED LOBBIES"));
            if (discoveredSection.Count > 0) viewTypes.AddRange(discoveredSection);
            else viewTypes.Add(new LobbyEmptyViewType("No lobbies found. Press Blue to refresh or create one yourself."));

            RebuildSectionCache(viewTypes);
            UpdateStatusText(favoritesSection.Count, myLobbiesSection.Count, recentsSection.Count, discoveredSection.Count);
            LogViewSummary(favoritesSection.Count, myLobbiesSection.Count, recentsSection.Count, discoveredSection.Count, viewTypes.Count);

            try
            {
                Debug.Log($"[LobbyBrowserMenu] CreateViewList summary: favorites={favoritesSection.Count}, myLobbies={myLobbiesSection.Count}, recents={recentsSection.Count}, discovered={discoveredSection.Count}, totalViewTypes={viewTypes.Count}");
                int maxSamples = 8;
                var samples = viewTypes.Where(v => v is SavedLobbyViewType or DiscoveredLobbyViewType or MyLobbyViewType)
                                       .Take(maxSamples)
                                       .Select(v => v.GetPrimaryText(false))
                                       .ToList();
                if (samples.Count > 0)
                {
                    Debug.Log($"[LobbyBrowserMenu] CreateViewList samples: {string.Join(" | ", samples)}");
                }

                const int MAX_SAFE_VIEWTYPES = 2000;
                if (viewTypes.Count > MAX_SAFE_VIEWTYPES)
                {
                    Debug.LogWarning($"[LobbyBrowserMenu] CreateViewList produced {viewTypes.Count} items — capping to {MAX_SAFE_VIEWTYPES} for safety. Investigate discovery/bookmark sources.");
                    viewTypes = viewTypes.Take(MAX_SAFE_VIEWTYPES).ToList();
                    RebuildSectionCache(viewTypes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Exception while logging CreateViewList debug info: {ex}");
            }

            return viewTypes;
        }

        // --- List management and navigation helpers ---

        private static string NormalizeAddress(string address) => string.IsNullOrWhiteSpace(address) ? string.Empty : address.Trim().ToLowerInvariant();

        private void RefreshList() => RefreshListInternal(true);
        private void RefreshList(bool keepSection) => RefreshListInternal(keepSection);

        private void RefreshListInternal(bool keepSection)
        {
            // Try to preserve the current selection by a stable key if possible.
            string currentSelectionKey = CurrentSelection?.GetSelectionKey();
            int previousSection = keepSection ? GetSectionIndexFor(SelectedIndex) : 0;
            int priorSelectedIndex = SelectedIndex;
            RequestViewListUpdate();

            var views = ViewList;
            if (views == null || views.Count == 0)
            {
                SelectedIndex = 0;
                UpdateSidebarForSelection();
                return;
            }

            if (SelectedIndex >= views.Count) SelectedIndex = views.Count - 1;

            // If we have a stable selection key, try to reselect the same item after rebuilding the list.
            if (!string.IsNullOrEmpty(currentSelectionKey))
            {
                for (int i = 0; i < views.Count; i++)
                {
                    try
                    {
                        if (string.Equals(views[i].GetSelectionKey(), currentSelectionKey, StringComparison.Ordinal))
                        {
                            SelectedIndex = i;
                            UpdateSidebarForSelection();
                            return;
                        }
                    }
                    catch
                    {
                        // Ignore and continue
                    }
                }
            }

            // If there was no stable selection key (e.g., categories/empty rows), and we're keeping section,
            // preserve the numeric index where possible. Previously we only preserved it when the index pointed
            // to a selectable item which caused snapping when the user was hovering a category/empty row.
            // To avoid that snap, preserve the numeric index even if it points to a non-selectable row (category/empty).
            if (string.IsNullOrEmpty(currentSelectionKey) && keepSection)
            {
                if (priorSelectedIndex >= 0 && priorSelectedIndex < views.Count)
                {
                    SelectedIndex = priorSelectedIndex;
                    UpdateSidebarForSelection();
                    return;
                }
            }

            if (keepSection && _sectionStartIndices.Count > 0)
            {
                if (!SelectFirstSelectableInSection(previousSection)) SelectFirstSelectableInRange(0, views.Count);
            }
            else if (!IsSelectable(CurrentSelection))
            {
                if (!SelectFirstSelectableInRange(0, views.Count)) SelectedIndex = 0;
            }

            UpdateSidebarForSelection();
        }

        public void RefreshLobbies()
        {
            if (_statusText != null) _statusText.text = "Searching for lobbies...";
            YargNetworkManager.Instance?.RefreshLobbyList();
        }

        private void OnLobbyListUpdated(List<YargNetworkManager.LobbyInfo> lobbies)
        {
            _currentLobbies = lobbies;
            if (_statusText != null)
            {
                if (lobbies.Count == 0) _statusText.text = "No lobbies found";
                else
                {
                    int favoriteCount = lobbies.Count(l => _favorites.IsFavorited(l.ipAddress, l.port));
                    if (favoriteCount > 0) _statusText.text = $"{lobbies.Count} {(lobbies.Count == 1 ? "lobby" : "lobbies")} found ({favoriteCount} favorite{(favoriteCount == 1 ? "" : "s")})";
                    else _statusText.text = $"{lobbies.Count} {(lobbies.Count == 1 ? "lobby" : "lobbies")} found";
                }
            }

            RefreshList();
            UniTask.Void(async () => await PingSavedServersAsync());
        }

        private void OnCreateLobbySubmitted(LobbyBrowserSidebar.CreateLobbyFormData data)
        {
            try
            {
                var store = LobbyBookmarkStore.Instance;
                var preset = store.UpsertMyLobby(data.PresetId, data.LobbyName, data.MaxPlayers, data.PrivacyMode, data.Password, true);
                if (_sidebar != null)
                {
                    _sidebar.ShowHostedLobbyPreset(preset);
                }

                var password = preset.PrivacyMode == YargNetworkManager.LobbyPrivacyMode.Private ? preset.password ?? string.Empty : string.Empty;
                YargNetworkManager.Instance?.CreateLobby(preset.lobbyName, preset.maxPlayers, preset.PrivacyMode, password);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Failed to create lobby from sidebar submission: {ex}");
            }
        }

        private void OnDirectConnectSubmitted(LobbyBrowserSidebar.DirectConnectFormData data)
        {
            string address = data.Address?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogWarning("[LobbyBrowserMenu] Direct connect submission missing address.");
                return;
            }

            string displayName = string.IsNullOrWhiteSpace(data.DisplayName) ? address : data.DisplayName.Trim();
            LobbyBookmarkStore.Instance.RecordConnection(address, data.Port, displayName, data.Password ?? string.Empty);

            string endpoint;
            try
            {
                endpoint = EndpointUtility.FormatEndpoint(address, data.Port);
            }
            catch (Exception)
            {
                endpoint = string.Concat(address, ":", data.Port);
            }

            try
            {
                YargNetworkManager.Instance?.JoinLobby(endpoint, data.Password ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Direct connect join failed: {ex}");
            }
        }

        public void JoinLobby(YargNetworkManager.LobbyInfo lobby)
        {
            if (lobby == null) return;
            if (lobby.hasPassword) ShowPasswordDialog(lobby);
            else JoinLobbyWithPassword(lobby, string.Empty);
        }

        private void ShowPasswordDialog(YargNetworkManager.LobbyInfo lobby)
        {
            if (DialogManager.Instance == null)
            {
                Debug.LogWarning("[LobbyBrowserMenu] Password dialog requested but DialogManager is unavailable.");
                return;
            }

            var dialog = DialogManager.Instance.ShowRenameDialog("Password Required", value =>
            {
                var submitted = (value ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(submitted))
                {
                    return;
                }

                JoinLobbyWithPassword(lobby, submitted);
            });

            dialog.AllowEmpty = false;
            dialog.SetInitialText(string.Empty, false);

            var inputField = dialog.GetComponentInChildren<TMP_InputField>(true);
            if (inputField != null)
            {
                inputField.contentType = TMP_InputField.ContentType.Password;
                inputField.lineType = TMP_InputField.LineType.SingleLine;
                inputField.text = string.Empty;
                if (inputField.placeholder is TMP_Text placeholderText)
                {
                    placeholderText.text = "Enter password";
                }
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        private void JoinLobbyWithPassword(YargNetworkManager.LobbyInfo lobby, string password)
        {
            YargNetworkManager.Instance?.JoinDiscoveredLobby(lobby, password);
        }

        private void Back() => MenuManager.Instance.PopMenu();

        private void UpdateStatusText(int favoritesCount, int myLobbiesCount, int recentsCount, int discoveredCount)
        {
            if (_statusText == null) return;
            _statusText.text = $"Favorites: {favoritesCount} · My Lobbies: {myLobbiesCount} · Recents: {recentsCount} · Discovered: {discoveredCount}";
        }

        private void LogViewSummary(int favoritesCount, int myLobbiesCount, int recentsCount, int discoveredCount, int totalCount)
        {
            var summary = (favoritesCount, myLobbiesCount, recentsCount, discoveredCount, totalCount);
            if (_hasLoggedSummary && summary == _lastLoggedSummary) return;
            _hasLoggedSummary = true; _lastLoggedSummary = summary;
            Debug.Log($"[LobbyBrowserMenu] Built lobby list. Favorites={favoritesCount}, MyLobbies={myLobbiesCount}, Recents={recentsCount}, Discovered={discoveredCount}, ViewTypes={totalCount}");
        }

        private void SetNavigationScheme()
        {
            if (Navigator.Instance == null)
            {
                Debug.Log("[LobbyBrowserMenu] Navigator unavailable; navigation scheme not set.");
                return;
            }

            _lastNavigationHelpSignature = null;
            ApplyNavigationSchemeForCurrentView(force: true);
        }

        private void ApplyNavigationSchemeForCurrentView(bool force = false)
        {
            if (Navigator.Instance == null)
                return;

            var target = ResolveActionTargetView();
            string signature = BuildNavigationSignature(target);

            if (!force && _navigationSchemePushed && string.Equals(signature, _lastNavigationHelpSignature, StringComparison.Ordinal))
                return;

            var scheme = BuildNavigationSchemeForCurrentView(target);

            if (_navigationSchemePushed)
            {
                try
                {
                    Navigator.Instance.PopScheme();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyBrowserMenu] Failed to pop previous navigation scheme: {ex}");
                }
                _navigationSchemePushed = false;
            }

            Navigator.Instance.PushScheme(scheme);
            _navigationSchemePushed = true;
            _lastNavigationHelpSignature = signature;
        }

        private string BuildNavigationSignature(LobbyViewType target)
        {
            bool canGreen = CanPerformGreenAction(target);
            string greenKey = canGreen ? GetGreenActionLocalizationKey(target) : string.Empty;
            bool canYellow = target != null && target.ShowFavoriteButton;
            return string.Concat(canGreen ? "1" : "0", "|", greenKey, "|", canYellow ? "1" : "0");
        }

        private NavigationScheme BuildNavigationSchemeForCurrentView(LobbyViewType target)
        {
            var entries = new List<NavigationScheme.Entry>
            {
                // Use direct SelectedIndex modification so instrument inputs scroll the list even when
                // NavigationGroup does not contain navigatables for ListMenu view objects.
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up", ctx =>
                {
                    Debug.Log($"[LobbyBrowserMenu] Navigator UP event (IsRepeat={ctx.IsRepeat})");
                    SetWrapAroundState(!ctx.IsRepeat);
                    SelectedIndex--;
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down", ctx =>
                {
                    Debug.Log($"[LobbyBrowserMenu] Navigator DOWN event (IsRepeat={ctx.IsRepeat})");
                    SetWrapAroundState(!ctx.IsRepeat);
                    SelectedIndex++;
                }),
                new NavigationScheme.Entry(MenuAction.Left, "Menu.MusicLibrary.SkipSection", GoToPreviousSection),
                new NavigationScheme.Entry(MenuAction.Right, "Menu.MusicLibrary.SkipSection", GoToNextSection),
            };

            // Respect desired help-bar order: Green, Red, Yellow, Blue.
            if (CanPerformGreenAction(target))
            {
                string greenKey = GetGreenActionLocalizationKey(target);
                entries.Add(new NavigationScheme.Entry(MenuAction.Green, greenKey, TryExecuteJoinAction));
            }

            entries.Add(new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back));

            if (target != null && target.ShowFavoriteButton)
            {
                entries.Add(new NavigationScheme.Entry(MenuAction.Yellow, "Menu.MusicLibrary.AddToFavorites", TryToggleFavorite));
            }

            entries.Add(new NavigationScheme.Entry(MenuAction.Blue, "Menu.Common.Refresh", TriggerRefreshAction));

            return new NavigationScheme(entries, false);
        }

        private bool CanPerformGreenAction(LobbyViewType view)
        {
            if (view == null)
                return false;

            if (view is DiscoveredLobbyViewType d)
                return IsLobbyLive(d.LobbyInfo);

            if (view is SavedLobbyViewType saved)
                return saved.LiveInfo != null && IsLobbyLive(saved.LiveInfo);

            if (view is MyLobbyViewType)
                return true;

            return false;
        }

        private string GetGreenActionLocalizationKey(LobbyViewType view)
        {
            // Reuse the standard confirm label for both joining and starting hosted lobbies.
            return "Menu.Common.Confirm";
        }

        private void TriggerRefreshAction()
        {
            bool hadLiveInfo = InvalidateSavedLobbyLiveInfo();
            if (hadLiveInfo)
            {
                RefreshList(true);
            }
            RefreshLobbies();
            UniTask.Void(async () => await PingSavedServersAsync());
        }

        private bool InvalidateSavedLobbyLiveInfo()
        {
            if (_pingedLobbies == null || _pingedLobbies.Count == 0)
                return false;

            bool changed = false;
            var keys = _pingedLobbies.Keys.ToList();
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                if (_pingedLobbies[key] != null)
                {
                    _pingedLobbies[key] = null;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsLobbyLive(YargNetworkManager.LobbyInfo lobby)
        {
            if (lobby == null)
                return false;

            if (!lobby.isActive)
                return false;

            if (lobby.lastSeen <= 0)
                return true;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long delta = now - lobby.lastSeen;
            return delta <= STALE_LOBBY_SECONDS * 1000.0;
        }

        private static void MarkLobbyHeartbeat(YargNetworkManager.LobbyInfo lobby)
        {
            if (lobby == null)
                return;

            lobby.isActive = true;
            lobby.lastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private bool CullStalePingCache()
        {
            if (_pingedLobbies == null || _pingedLobbies.Count == 0)
                return false;

            bool changed = false;
            var keys = _pingedLobbies.Keys.ToList();
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;

                var info = _pingedLobbies[key];
                if (info != null && !IsLobbyLive(info))
                {
                    _pingedLobbies[key] = null;
                    changed = true;
                }
            }

            return changed;
        }

        private bool PruneStaleDiscoveryEntries()
        {
            if (_currentLobbies == null || _currentLobbies.Count == 0)
                return false;

            int removed = _currentLobbies.RemoveAll(lobby => !IsLobbyLive(lobby));
            return removed > 0;
        }

        // Determine which view should receive button actions, favoring the last hovered/visible sidebar entry.
        private LobbyViewType ResolveActionTargetView()
        {
            var target = TryRehydrateView(_lastShownSidebarView);
            if (target != null)
                return target;

            target = TryRehydrateView(CurrentSelection);
            if (target != null)
                return target;

            return null;
        }

        // Map a cached view reference back to the current view list when possible so actions operate on fresh data.
        private LobbyViewType TryRehydrateView(LobbyViewType view)
        {
            if (view == null)
                return null;

            var views = ViewList;
            if (views != null && views.Count > 0)
            {
                string key = null;
                try
                {
                    key = view.GetSelectionKey();
                }
                catch
                {
                    key = null;
                }

                if (!string.IsNullOrEmpty(key))
                {
                    foreach (var candidate in views)
                    {
                        if (candidate == null)
                            continue;

                        try
                        {
                            if (string.Equals(candidate.GetSelectionKey(), key, StringComparison.Ordinal))
                                return candidate;
                        }
                        catch
                        {
                            // Ignore mismatched keys
                        }
                    }
                }

                if (views.Contains(view))
                    return view;
            }

            return view;
        }

        private void TryExecuteJoinAction()
        {
            var target = ResolveActionTargetView();
            if (!CanPerformGreenAction(target))
                return;

            try
            {
                target.OnJoinClick();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Failed to execute join action for {target.GetType().Name}: {ex}");
            }
        }

        private void TryToggleFavorite()
        {
            var target = ResolveActionTargetView();
            if (target == null || !target.ShowFavoriteButton)
                return;

            try
            {
                target.OnFavoriteClick();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Failed to toggle favorite for {target.GetType().Name}: {ex}");
            }
        }

        private void UpdateSidebarForSelection()
        {
            ShowSidebarFor(CurrentSelection);
        }

        /// <summary>
        /// Public helper to show sidebar for an arbitrary view (used by view objects on pointer hover).
        /// </summary>
        public void ShowSidebarFor(LobbyViewType view)
        {
            EnsureSidebar();

            // remember last requested view for discovery-driven refreshes
            _lastShownSidebarView = view;
            ApplyNavigationSchemeForCurrentView();

            if (_sidebar == null)
                return;

            if (view is LobbyCategoryViewType category)
            {
                _selectedLobby = null;
                string name = category.CategoryName;
                if (!string.IsNullOrEmpty(name))
                {
                    if (string.Equals(name, "CREATE A LOBBY", StringComparison.OrdinalIgnoreCase))
                    {
                        _sidebar.ShowCreateLobbyForm(null, false);
                        return;
                    }

                    if (string.Equals(name, "ADD NEW CONNECTION", StringComparison.OrdinalIgnoreCase))
                    {
                        _sidebar.ShowDirectConnectForm();
                        return;
                    }
                }

                return;
            }

            if (view is MyLobbyViewType myLobby)
            {
                _selectedLobby = null;
                _sidebar.ShowHostedLobbyPreset(myLobby.Preset);
                return;
            }
            // If this is a discovered lobby and has live players, show full lobby info.
            if (view is DiscoveredLobbyViewType d)
            {
                var lobbyInfo = d.LobbyInfo;
                if (lobbyInfo != null)
                {
                    _selectedLobby = lobbyInfo;
                    _sidebar.SetLobby(_selectedLobby);
                    return;
                }
            }

            // If this is a saved bookmark, prefer showing live info when available; otherwise show bookmark details.
            if (view is SavedLobbyViewType s)
            {
                var bookmark = s.Bookmark;
                if (_sidebar != null && bookmark != null && _sidebar.IsEditingBookmark(bookmark))
                {
                    _selectedLobby = null;
                    _sidebar.SetBookmark(bookmark);
                    return;
                }

                var live = s.LiveInfo;
                if (live != null)
                {
                    _selectedLobby = live;
                    _sidebar.SetLobby(_selectedLobby);
                    return;
                }

                if (bookmark != null)
                {
                    _selectedLobby = null;
                    _sidebar.SetBookmark(bookmark);
                    return;
                }
            }

            // Fallback: for category/empty rows, do not clear the sidebar so transient hovers
            // don't remove the currently-displayed lobby/player list. Only clear when the
            // incoming view is null (no selection).
            if (view == null)
            {
                _selectedLobby = null;
                _sidebar.ClearLobby();
            }
        }

        internal void HandleActionSelection(LobbyActionViewType action)
        {
            if (action == null)
                return;

            _lastShownSidebarView = action;
            ApplyNavigationSchemeForCurrentView();

            if (_sidebar == null)
                return;

            switch (action.Kind)
            {
                case LobbyActionViewType.ActionKind.CreateLobby:
                    _sidebar.ShowCreateLobbyForm(null, true);
                    break;
                case LobbyActionViewType.ActionKind.DirectConnect:
                    _sidebar.ShowDirectConnectForm(true);
                    break;
            }
        }

        internal void StartHostedLobby(HostedLobbyPreset preset)
        {
            if (preset == null)
                return;

            var store = LobbyBookmarkStore.Instance;
            var privacy = preset.PrivacyMode;
            string password = privacy == YargNetworkManager.LobbyPrivacyMode.Private ? (preset.password ?? string.Empty) : string.Empty;
            var storedPreset = store.UpsertMyLobby(preset.id, preset.lobbyName, preset.maxPlayers, privacy, password, true);

            if (_sidebar != null)
            {
                _sidebar.ShowHostedLobbyPreset(storedPreset);
            }

            try
            {
                YargNetworkManager.Instance?.CreateLobby(storedPreset.lobbyName, storedPreset.maxPlayers, storedPreset.PrivacyMode, storedPreset.password ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Failed to create lobby from preset '{storedPreset?.lobbyName}': {ex}");
            }
        }

        internal void ShowHostedLobbyEditor(HostedLobbyPreset preset)
        {
            if (_sidebar == null || preset == null)
                return;

            _lastShownSidebarView = null;
            ApplyNavigationSchemeForCurrentView();
            _sidebar.ShowCreateLobbyForm(preset, true);
        }

        // Discovery callbacks
        private void OnDiscoveryLobbyDiscovered(YargNetworkManager.LobbyInfo lobby)
        {
            try
            {
                if (lobby == null) return;
                string key = LobbyBookmarkUtility.BuildKey(lobby.ipAddress, lobby.port);
                if (string.IsNullOrEmpty(key)) return;
                MarkLobbyHeartbeat(lobby);
                _pingedLobbies[key] = lobby;
                Debug.Log($"[LobbyBrowserMenu] Discovery found lobby for key {key}: {lobby.lobbyName}");
                // Refresh the list so SavedLobbyViewType instances pick up LiveInfo
                RefreshList(true);
                // If the sidebar is currently showing the selection (which may be a saved bookmark),
                // request it update so the sidebar displays live info immediately when discovery arrives.
                try
                {
                    // Prefer refreshing whatever view was last shown in the sidebar (hover or selection).
                    ShowSidebarFor(_lastShownSidebarView ?? CurrentSelection);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyBrowserMenu] Exception while updating sidebar after discovery: {ex}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Exception in OnDiscoveryLobbyDiscovered: {ex}");
            }
        }

        private void OnDiscoveryLobbyLost(long serverId)
        {
            try
            {
                // Try to remove any pinged entries that match this lost server via matching ip/port from discovery component
                // We don't have serverId -> endpoint mapping here, so just refresh lists (the discovery component cleans its own cache)
                Debug.Log($"[LobbyBrowserMenu] Discovery reported lobby lost: {serverId}");
                InvalidateSavedLobbyLiveInfo();
                RefreshList(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Exception in OnDiscoveryLobbyLost: {ex}");
            }
        }

        protected override void Update()
        {
            base.Update();

            if (Time.unscaledTime >= _nextStaleSweepAt)
            {
                _nextStaleSweepAt = Time.unscaledTime + STALE_SWEEP_INTERVAL;

                bool changed = false;
                changed |= CullStalePingCache();
                changed |= PruneStaleDiscoveryEntries();

                if (changed)
                {
                    RefreshList(true);
                }
            }
        }

        private void GoToPreviousSection() { if (_sectionStartIndices.Count == 0) return; int currentSection = GetSectionIndexFor(SelectedIndex); JumpToSection(Mathf.Max(0, currentSection - 1)); }
        private void GoToNextSection() { if (_sectionStartIndices.Count == 0) return; int currentSection = GetSectionIndexFor(SelectedIndex); JumpToSection(Mathf.Min(_sectionStartIndices.Count - 1, currentSection + 1)); }

        private void JumpToSection(int sectionIndex)
        {
            if (_sectionStartIndices.Count == 0 || ViewList == null || ViewList.Count == 0) return;
            sectionIndex = Mathf.Clamp(sectionIndex, 0, _sectionStartIndices.Count - 1);
            if (!SelectFirstSelectableInSection(sectionIndex)) SelectedIndex = _sectionStartIndices[sectionIndex];
        }

        private int GetSectionIndexFor(int viewIndex)
        {
            if (_sectionStartIndices.Count == 0) return 0; if (viewIndex < 0) return 0;
            for (int i = _sectionStartIndices.Count - 1; i >= 0; i--) if (viewIndex >= _sectionStartIndices[i]) return i; return 0;
        }

        private int GetSectionEndIndex(int sectionIndex)
        {
            var views = ViewList; if (views == null || views.Count == 0) return 0; if (sectionIndex + 1 < _sectionStartIndices.Count) return _sectionStartIndices[sectionIndex + 1]; return views.Count;
        }

        private bool SelectFirstSelectableInSection(int sectionIndex)
        {
            if (_sectionStartIndices.Count == 0) return false; sectionIndex = Mathf.Clamp(sectionIndex, 0, _sectionStartIndices.Count - 1); return SelectFirstSelectableInRange(_sectionStartIndices[sectionIndex], GetSectionEndIndex(sectionIndex));
        }

        private bool SelectFirstSelectableInRange(int startInclusive, int endExclusive)
        {
            var views = ViewList; if (views == null || views.Count == 0) return false; startInclusive = Mathf.Clamp(startInclusive, 0, views.Count - 1); endExclusive = Mathf.Clamp(endExclusive, startInclusive + 1, views.Count);
            for (int i = startInclusive; i < endExclusive; i++) if (IsSelectable(views[i])) { SelectedIndex = i; return true; } return false;
        }

        private static bool IsSelectable(LobbyViewType view) => view is not LobbyCategoryViewType and not LobbyEmptyViewType;

        private void RebuildSectionCache(List<LobbyViewType> viewTypes)
        {
            _sectionStartIndices.Clear(); if (viewTypes == null || viewTypes.Count == 0) return; for (int i = 0; i < viewTypes.Count; i++) if (viewTypes[i] is LobbyCategoryViewType) _sectionStartIndices.Add(i);
        }

        // --- Ping saved servers (lightweight placeholder implementation) ---
        private async UniTask PingSavedServersAsync()
        {
            if (_isPingingSavedServers) return; _isPingingSavedServers = true;
            try
            {
                var favorites = _favorites.GetFavorites();
                foreach (var bookmark in favorites)
                {
                    var key = bookmark.EndpointKey; if (string.IsNullOrEmpty(key)) continue; if (_pendingPings.Contains(key)) continue;
                    _pendingPings.Add(key);
                    try
                    {
                        // If we have a discovery component, send a direct discovery request to populate live info.
                        if (_discovery != null)
                        {
                            int port = bookmark.port > 0 ? bookmark.port : (YargNetworkManager.Instance?.SuggestedDirectConnectPort ?? NetworkTransportDefaults.DefaultTcpPort);
                            try
                            {
                                // Try to use the address directly; if it's a hostname, attempt DNS resolve
                                string sendAddress = bookmark.address;
                                try
                                {
                                    System.Net.IPAddress.Parse(sendAddress);
                                }
                                catch
                                {
                                    try
                                    {
                                        var addrs = System.Net.Dns.GetHostAddresses(sendAddress);
                                        var ipv4 = System.Array.Find(addrs, a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                                        if (ipv4 != null)
                                        {
                                            sendAddress = ipv4.ToString();
                                        }
                                    }
                                    catch (Exception dnsEx)
                                    {
                                        Debug.LogWarning($"[LobbyBrowserMenu] DNS lookup failed for {sendAddress}: {dnsEx.Message}");
                                    }
                                }

                                _discovery.SendDiscoveryRequest(sendAddress, port);
                                Debug.Log($"[LobbyBrowserMenu] Sent discovery request to {sendAddress}:{port} for bookmark '{bookmark.displayName}'");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[LobbyBrowserMenu] Failed to send discovery request to {bookmark.address}:{port}: {ex}");
                            }
                        }

                        // Mark as unknown until discovery responds
                        _pingedLobbies[key] = null;
                    }
                    finally { _pendingPings.Remove(key); }
                }
                // Also ping recents
                var recents = _favorites.GetRecents();
                foreach (var bookmark in recents)
                {
                    var key = bookmark.EndpointKey; if (string.IsNullOrEmpty(key)) continue; if (_pendingPings.Contains(key)) continue;
                    _pendingPings.Add(key);
                    try
                    {
                        if (_discovery != null)
                        {
                            int port = bookmark.port > 0 ? bookmark.port : (YargNetworkManager.Instance?.SuggestedDirectConnectPort ?? NetworkTransportDefaults.DefaultTcpPort);
                            try
                            {
                                string sendAddress = bookmark.address;
                                try
                                {
                                    System.Net.IPAddress.Parse(sendAddress);
                                }
                                catch
                                {
                                    try
                                    {
                                        var addrs = System.Net.Dns.GetHostAddresses(sendAddress);
                                        var ipv4 = System.Array.Find(addrs, a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                                        if (ipv4 != null)
                                        {
                                            sendAddress = ipv4.ToString();
                                        }
                                    }
                                    catch (Exception dnsEx)
                                    {
                                        Debug.LogWarning($"[LobbyBrowserMenu] DNS lookup failed for {sendAddress}: {dnsEx.Message}");
                                    }
                                }

                                _discovery.SendDiscoveryRequest(sendAddress, port);
                                Debug.Log($"[LobbyBrowserMenu] Sent discovery request to {sendAddress}:{port} for recent '{bookmark.displayName}'");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[LobbyBrowserMenu] Failed to send discovery request to {bookmark.address}:{port}: {ex}");
                            }
                        }

                        _pingedLobbies[key] = null;
                    }
                    finally { _pendingPings.Remove(key); }
                }
            }
            finally { _isPingingSavedServers = false; }
        }

        public void JoinSavedBookmark(LobbyBookmark bookmark)
        {
            if (bookmark == null) return; if (_pingedLobbies.TryGetValue(bookmark.EndpointKey, out var live) && live != null) { JoinLobby(live); return; }
            // Join using the normalized endpoint (address:port). YargNetworkManager does not expose JoinBookmark,
            // so use JoinLobby with a formatted endpoint and the saved password.
            try
            {
                string endpoint = EndpointUtility.FormatEndpoint(bookmark.address, bookmark.port <= 0 ? (YargNetworkManager.Instance?.SuggestedDirectConnectPort ?? NetworkTransportDefaults.DefaultUdpPort) : bookmark.port);
                YargNetworkManager.Instance?.JoinLobby(endpoint, bookmark.password ?? string.Empty);
            }
            catch (Exception)
            {
                // Fallback: attempt naive concat
                string endpoint = string.Concat(bookmark.address, ":", bookmark.port);
                YargNetworkManager.Instance?.JoinLobby(endpoint, bookmark.password ?? string.Empty);
            }
        }

        /// <summary>
        /// Edit a saved bookmark. Currently this shows a simple dialog and defers to the LobbyFavorites update API.
        /// A richer edit UI can replace this in the future.
        /// </summary>
        public void EditBookmark(LobbyBookmark bookmark)
        {
            if (bookmark == null) return;

            // For now, simply show a message and ensure the bookmark is re-saved via the favorites facade.
            if (DialogManager.Instance != null)
            {
                var dialog = DialogManager.Instance.ShowMessage("Edit Bookmark", "Bookmark editing UI is not implemented in this build. Use Direct Connect to connect or modify bookmarks in settings.");
                dialog.AddDialogButton("OK", MenuData.Colors.BrightButton, () => DialogManager.Instance.ClearDialog());
            }
            
            // Touch the bookmark via the facade to ensure store is in a consistent state.
            _favorites?.UpdateBookmark(bookmark, bookmark.displayName, bookmark.address, bookmark.port, bookmark.password);
        }

        // Debug helper: listen to Navigator events
        private void OnNavigatorEvent(Navigation.NavigationContext ctx)
        {
            Debug.Log($"[LobbyBrowserMenu] Navigator event received: Action={ctx.Action}, Player={(ctx.Player != null ? ctx.Player.Profile.Name : "null")}, IsRepeat={ctx.IsRepeat}");

            // Fallback: if our scheme didn't run for some reason but we have pushed our scheme, handle Up/Down here.
            // We avoid double-handling by only applying the fallback if SelectedIndex hasn't changed very recently.
            try
            {
                if (_navigationSchemePushed && (ctx.Action == MenuAction.Up || ctx.Action == MenuAction.Down))
                {
                    var timeSinceLastChange = Time.unscaledTime - _lastSelectedIndexChangeTime;
                    // If SelectedIndex wasn't updated in the last 0.1s, assume scheme didn't handle it and apply fallback.
                    if (timeSinceLastChange > 0.1f)
                    {
                        if (ctx.Action == MenuAction.Up)
                        {
                            Debug.Log("[LobbyBrowserMenu] Fallback handling: UP -> modifying SelectedIndex");
                            SetWrapAroundState(!ctx.IsRepeat);
                            SelectedIndex--;
                        }
                        else
                        {
                            Debug.Log("[LobbyBrowserMenu] Fallback handling: DOWN -> modifying SelectedIndex");
                            SetWrapAroundState(!ctx.IsRepeat);
                            SelectedIndex++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyBrowserMenu] Exception in OnNavigatorEvent fallback: {ex}");
            }
        }
    }

}
