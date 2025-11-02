using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// View component for displaying lobby information in the browser list.
    /// Follows YARG's ViewObject pattern.
    /// </summary>
    public class LobbyView : ViewObject<LobbyViewType>, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public enum DisplayState
        {
            Normal,
            Category,
            Empty,
            Offline,
            Hosted,
            Loading
        }

        // Tracks the display state last applied by ApplyDisplayState so visibility
        // helpers can decide whether enabling a target's nearest state container
        // is appropriate. This prevents enabling a mismatched container (e.g.,
        // the Offline container) when the view is supposed to show Normal.
        private DisplayState _appliedDisplayState = DisplayState.Normal;

        // Set text on a named child inside a state container. This searches recursively
        // and writes to the first TextMeshProUGUI found on the named child or its descendants.
        // Attempts to set text on a named child inside a state container.
        // Returns true when a TextMeshProUGUI was found and written; false otherwise.
        private bool SetTextInContainer(Transform container, string childName, string text)
        {
            if (container == null || string.IsNullOrEmpty(childName)) return false;

            try
            {
                var target = FindChildRecursive(container, childName);
                if (target == null)
                {
                    // Try a few common alternate name variations
                    var altNames = new[] { childName + " Text", childName + "Label", childName.Replace(" ", ""), childName + "_Text" };
                    foreach (var alt in altNames)
                    {
                        target = FindChildRecursive(container, alt);
                        if (target != null) break;
                    }
                }

                if (target != null)
                {
                    var tmps = target.GetComponentsInChildren<TextMeshProUGUI>(true);
                    if (tmps != null && tmps.Length > 0)
                    {
                        foreach (var t in tmps)
                        {
                            if (t != null)
                                t.text = text ?? string.Empty;
                        }
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning($"[LobbyView] Found child '{target.name}' for '{childName}' but it has no TextMeshProUGUI components.");
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"[LobbyView] Could not find child named '{childName}' (or its alternates) inside container '{container.name}'.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Failed to set text for '{childName}' in container '{container.name}': {ex}");
                return false;
            }
        }

        // If the active container doesn't contain the expected named children, attempt
        // a pragmatic fallback: map the found TextMeshProUGUI components under the
        // container to the expected fields in a common order. This keeps the prefab
        // as the source of truth while tolerating alternate layouts.
        private void PopulateFallbackFromContainer(Transform container, LobbyViewType viewType, bool selected, System.Collections.Generic.List<string> missingFields)
        {
            if (container == null) return;

            var tmps = container.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (tmps == null || tmps.Length == 0)
            {
                Debug.LogWarning($"[LobbyView] Fallback: no TMPs found under container '{container.name}' to map missing fields.");
                return;
            }

            Debug.Log($"[LobbyView] Fallback: mapping {tmps.Length} TMPs in '{container.name}' to missing fields: {string.Join(", ", missingFields)}");

            // Simple ordering mapping: fill missing fields in this priority order using available tmps
            var priority = new string[] { "Lobby Name", "Host", "Player Count", "Ping Text", "Last Connected", "Privacy", "Message", "Title" };

            int idx = 0;
            foreach (var p in priority)
            {
                if (idx >= tmps.Length) break;
                if (!missingFields.Contains(p)) continue;

                // Write into the TMP found at idx
                try
                {
                    tmps[idx].text = p switch
                    {
                        "Lobby Name" => viewType.GetPrimaryText(selected) ?? string.Empty,
                        "Host" => viewType.GetSecondaryText(selected) ?? string.Empty,
                        "Player Count" => viewType is DiscoveredLobbyViewType d ? d.GetPlayerCountText() : (viewType is SavedLobbyViewType s ? s.GetStatusBadge() : string.Empty),
                        "Ping Text" => viewType is DiscoveredLobbyViewType dd ? dd.GetPingText() : (viewType is SavedLobbyViewType ss ? ss.GetInfoBadge() : string.Empty),
                        "Last Connected" => viewType.GetSecondaryText(selected) ?? string.Empty,
                        "Privacy" => "SAVED BOOKMARK",
                        "Message" => viewType.GetPrimaryText(selected) ?? string.Empty,
                        "Title" => viewType.GetPrimaryText(selected) ?? string.Empty,
                        _ => string.Empty
                    };
                    Debug.Log($"[LobbyView] Fallback: wrote '{tmps[idx].text}' to TMP '{tmps[idx].name}' for field '{p}'");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyView] Fallback write failed for '{p}' in '{container.name}': {ex}");
                }

                idx++;
            }

            // Also set any global assigned TMPs as a fallback (player/ping)
            try
            {
                if (_playerCountText != null && missingFields.Contains("Player Count"))
                {
                    _playerCountText.text = viewType is DiscoveredLobbyViewType dd2 ? dd2.GetPlayerCountText() : (viewType is SavedLobbyViewType ss2 ? ss2.GetStatusBadge() : string.Empty);
                }

                if (_pingText != null && missingFields.Contains("Ping Text"))
                {
                    _pingText.text = viewType is DiscoveredLobbyViewType dd3 ? dd3.GetPingText() : (viewType is SavedLobbyViewType ss3 ? ss3.GetInfoBadge() : string.Empty);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Error while populating global fallback fields: {ex}");
            }
        }

        [Header("Prefab-driven state containers (optional)")]
        [SerializeField]
        private GameObject _stateContainerNormal;
        [SerializeField]
        private GameObject _stateContainerEmpty;
        [SerializeField]
        private GameObject _stateContainerCategory;
        [SerializeField]
        private GameObject _stateContainerOffline;
        [SerializeField]
        private GameObject _stateContainerHosted;
        [SerializeField]
        private GameObject _stateContainerLoading;

        // Fields specific to lobby rows (may be wired on the prefab)
        [Space]
        [SerializeField]
        private TextMeshProUGUI _playerCountText;
        [SerializeField]
        private TextMeshProUGUI _pingText;
        [SerializeField]
        private GameObject _passwordIcon;
        
        [Header("Explicit per-state fields (wire these on the prefab)")]
        [Header("Normal / Discovered")]
        [SerializeField]
        private TextMeshProUGUI _normalLobbyName;
        [SerializeField]
        private TextMeshProUGUI _normalHost;
        [SerializeField]
        private TextMeshProUGUI _normalPlayerCount;
        [SerializeField]
        private TextMeshProUGUI _normalPing;
        [SerializeField]
        private TextMeshProUGUI _normalPrivacy;

        [Header("Offline / Saved")]
        [SerializeField]
        private TextMeshProUGUI _offlineLobbyName;
        [SerializeField]
        private TextMeshProUGUI _offlineLastConnected;
        [SerializeField]
        private TextMeshProUGUI _offlinePlayerCount;
        [SerializeField]
        private TextMeshProUGUI _offlinePing;
        [SerializeField]
        private TextMeshProUGUI _offlinePrivacy;

        [Header("Hosted / Presets")]
        [SerializeField]
        private TextMeshProUGUI _hostedLobbyName;
        [SerializeField]
        private TextMeshProUGUI _hostedLastHosted;
        [SerializeField]
        private TextMeshProUGUI _hostedMaxPlayers;
        [SerializeField]
        private TextMeshProUGUI _hostedPrivacyText;
        [SerializeField]
        private UnityEngine.UI.Image _hostedPrivacyIcon;

        [Header("Category / Empty")]
        [SerializeField]
        private TextMeshProUGUI _categoryTitle;
        [SerializeField]
        private TextMeshProUGUI _emptyMessage;
        
        [Header("Privacy icons (preferred over text)")]
        [SerializeField]
        private UnityEngine.UI.Image _normalPrivacyIcon;
        [SerializeField]
        private UnityEngine.UI.Image _offlinePrivacyIcon;
        [SerializeField]
        private Sprite _privacyLocked;
        [SerializeField]
        private Sprite _privacyUnlocked;
        // Colors applied to the privacy icon when using icon-based indication.
        // These are serialized so designers can tweak them in the prefab Inspector.
        [SerializeField]
        private Color _privacyLockedColor = new Color32(220, 20, 60, 255); // crimson-ish
        [SerializeField]
        private Color _privacyUnlockedColor = new Color32(94, 215, 110, 255); // green-ish
        /// <summary>
        /// If any of the state container fields are assigned on the prefab, this will
        /// enable only the requested container and disable the others. Returns true
        /// when the prefab-driven behavior was applied; false when no containers are
        /// wired (caller should fall back to legacy behavior).
        /// </summary>
        public bool ApplyDisplayState(DisplayState state)
        {
            // Record the state we're about to apply so other helpers know which
            // container should be considered the "active" one for visibility
            // decisions. This prevents enabling unrelated container ancestors.
            _appliedDisplayState = state;

            // Require explicit wiring in the prefab. Auto-wiring is disabled to avoid
            // fragile runtime heuristics — please assign the state container fields
            // on the LobbyView prefab in the Inspector.
            // Include the category container in the wiring check as well. If no
            // containers are assigned, the prefab-driven behavior isn't available.
            bool any = _stateContainerNormal != null
                || _stateContainerCategory != null
                || _stateContainerEmpty != null
                || _stateContainerOffline != null
                || _stateContainerHosted != null
                || _stateContainerLoading != null;
            if (!any)
            {
                Debug.LogWarning("[LobbyView] No state container references are assigned on the LobbyView prefab. Please wire the state container GameObjects in the LobbyView prefab inspector.");
                return false;
            }

            // Helper to safely set active only when container exists
            void Set(GameObject obj, bool active)
            {
                if (obj != null)
                {
                    if (obj.activeSelf != active)
                        obj.SetActive(active);
                }
            }

            // Force exclusivity: disable all known containers first, then enable
            // only the one matching the requested state. This protects against
            // prefabs that accidentally have multiple containers active by default
            // or left active in the editor.
            Set(_stateContainerNormal, false);
            Set(_stateContainerCategory, false);
            Set(_stateContainerEmpty, false);
            Set(_stateContainerOffline, false);
            Set(_stateContainerHosted, false);
            Set(_stateContainerLoading, false);

            switch (state)
            {
                case DisplayState.Normal: Set(_stateContainerNormal, true); break;
                case DisplayState.Category: Set(_stateContainerCategory, true); break;
                case DisplayState.Empty: Set(_stateContainerEmpty, true); break;
                case DisplayState.Offline: Set(_stateContainerOffline, true); break;
                case DisplayState.Hosted: Set(_stateContainerHosted, true); break;
                case DisplayState.Loading: Set(_stateContainerLoading, true); break;
            }

            // Ensure the parent wrappers of the active container are enabled so
            // the container's children can become activeInHierarchy. We do this
            // here instead of force-enabling arbitrary ancestors elsewhere to
            // keep container toggling centralized in ApplyDisplayState. Use the
            // safe EnableAncestorsUpToViewRoot which will not toggle other
            // state containers.
            try
            {
                var activeContainer = GetActiveStateContainer(state);
                if (activeContainer != null)
                {
                    EnableAncestorsUpToViewRoot(activeContainer);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Failed to enable parent wrappers for active container: {ex}");
            }

            // NOTE: ancestor enabling intentionally not forced here. Per-instance
            // fallbacks will enable ancestors when needed. Avoid enabling ancestors
            // globally here to prevent inadvertently activating other state
            // containers which previously produced duplicated visuals.

            // Debug log to help diagnose prefab-driven behavior at runtime. Log the
            // actual active state and name of each container (or null) so it's
            // easier to see what's visible in the prefab at runtime.
            string NameOrNull(GameObject go) => go == null ? "<null>" : go.name;
            string ActiveState(GameObject go) => go == null ? "<null>" : (go.activeSelf ? "Active" : "Inactive");
            Debug.Log($"[LobbyView] ApplyDisplayState -> {state}. Containers -> Normal={NameOrNull(_stateContainerNormal)}:{ActiveState(_stateContainerNormal)}, Category={NameOrNull(_stateContainerCategory)}:{ActiveState(_stateContainerCategory)}, Empty={NameOrNull(_stateContainerEmpty)}:{ActiveState(_stateContainerEmpty)}, Offline={NameOrNull(_stateContainerOffline)}:{ActiveState(_stateContainerOffline)}, Hosted={NameOrNull(_stateContainerHosted)}:{ActiveState(_stateContainerHosted)}, Loading={NameOrNull(_stateContainerLoading)}:{ActiveState(_stateContainerLoading)}");

            // Also guard against stray per-state UI fields that were wired outside
            // of the expected state containers. When views are recycled by the
            // scrolling list, it's possible for these targets to remain active if
            // they don't live under the toggled container. Explicitly normalize
            // per-state active flags here so only UI elements for the current
            // display state remain visible.
            NormalizePerStateActive(state);

            // Dump quick diagnostics so it's easy to see at runtime whether the
            // important per-state targets (privacy icons, player count, etc.) are
            // assigned and visible. This helps debug prefab wiring vs runtime
            // activation issues.
            DumpPerStateDiagnostics(state);

            return true;
        }

        private void DumpPerStateDiagnostics(DisplayState state)
        {
            try
            {
                void DumpImage(string friendly, UnityEngine.UI.Image img)
                {
                    if (img == null)
                    {
                        Debug.Log($"[LobbyView][Diag] {friendly}: <null>");
                        return;
                    }

                    var spriteName = img.sprite != null ? img.sprite.name : "<null>";
                    var nearest = FindNearestStateContainerAncestor(img.gameObject);
                    var nearestName = nearest == null ? "<none>" : nearest.name;
                    Debug.Log($"[LobbyView][Diag] {friendly}: go.activeSelf={img.gameObject.activeSelf}, activeInHierarchy={img.gameObject.activeInHierarchy}, enabled={img.enabled}, nearestContainer={nearestName}, sprite={spriteName}, color.a={img.color.a}");
                }

                void DumpTMP(string friendly, TextMeshProUGUI tmp)
                {
                    if (tmp == null)
                    {
                        Debug.Log($"[LobbyView][Diag] {friendly}: <null>");
                        return;
                    }
                    var nearest = FindNearestStateContainerAncestor(tmp.gameObject);
                    var nearestName = nearest == null ? "<none>" : nearest.name;
                    Debug.Log($"[LobbyView][Diag] {friendly}: go.activeSelf={tmp.gameObject.activeSelf}, activeInHierarchy={tmp.gameObject.activeInHierarchy}, enabled={tmp.enabled}, nearestContainer={nearestName}, text='{tmp.text}'");
                }

                void DumpGO(string friendly, GameObject go)
                {
                    if (go == null)
                    {
                        Debug.Log($"[LobbyView][Diag] {friendly}: <null>");
                        return;
                    }
                    var nearest = FindNearestStateContainerAncestor(go);
                    var nearestName = nearest == null ? "<none>" : nearest.name;
                    Debug.Log($"[LobbyView][Diag] {friendly}: go.activeSelf={go.activeSelf}, activeInHierarchy={go.activeInHierarchy}, nearestContainer={nearestName}");
                }

                Debug.Log($"[LobbyView][Diag] State={state}");
                DumpImage("_normalPrivacyIcon", _normalPrivacyIcon);
                DumpImage("_offlinePrivacyIcon", _offlinePrivacyIcon);
                DumpImage("_hostedPrivacyIcon", _hostedPrivacyIcon);
                DumpTMP("_normalPlayerCount", _normalPlayerCount);
                DumpTMP("_offlinePlayerCount", _offlinePlayerCount);
                DumpTMP("_hostedLobbyName", _hostedLobbyName);
                DumpTMP("_hostedLastHosted", _hostedLastHosted);
                DumpTMP("_hostedMaxPlayers", _hostedMaxPlayers);
                DumpTMP("_hostedPrivacyText", _hostedPrivacyText);
                DumpTMP("_playerCountText", _playerCountText);
                DumpTMP("_pingText", _pingText);
                DumpGO("_passwordIcon", _passwordIcon);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView][Diag] DumpPerStateDiagnostics failed: {ex}");
            }
        }

        // Ensure per-state UI targets are only active for their matching state.
        // This prevents wired fields that live outside container GameObjects from
        // remaining visible when the view is recycled for a different state.
        private void NormalizePerStateActive(DisplayState state)
        {
            try
            {
                // Normal / Discovered
                if (_normalPlayerCount != null)
                {
                    _normalPlayerCount.gameObject.SetActive(state == DisplayState.Normal);
                    if (state == DisplayState.Normal) EnsureUIVisible(_normalPlayerCount);
                }
                if (_normalPing != null)
                {
                    _normalPing.gameObject.SetActive(state == DisplayState.Normal);
                    if (state == DisplayState.Normal) EnsureUIVisible(_normalPing);
                }
                if (_normalPrivacyIcon != null)
                {
                    _normalPrivacyIcon.gameObject.SetActive(state == DisplayState.Normal);
                    if (state == DisplayState.Normal) EnsureUIVisible(_normalPrivacyIcon);
                }

                // Offline / Saved
                if (_offlinePlayerCount != null)
                {
                    _offlinePlayerCount.gameObject.SetActive(state == DisplayState.Offline);
                    if (state == DisplayState.Offline) EnsureUIVisible(_offlinePlayerCount);
                }
                if (_offlinePing != null)
                {
                    _offlinePing.gameObject.SetActive(state == DisplayState.Offline);
                    if (state == DisplayState.Offline) EnsureUIVisible(_offlinePing);
                }
                if (_offlinePrivacyIcon != null)
                {
                    _offlinePrivacyIcon.gameObject.SetActive(state == DisplayState.Offline);
                    if (state == DisplayState.Offline) EnsureUIVisible(_offlinePrivacyIcon);
                }

                // Hosted / Presets
                bool hostedState = state == DisplayState.Hosted;
                if (_hostedLobbyName != null)
                {
                    _hostedLobbyName.gameObject.SetActive(hostedState);
                    if (hostedState) EnsureUIVisible(_hostedLobbyName);
                }
                if (_hostedLastHosted != null)
                {
                    _hostedLastHosted.gameObject.SetActive(hostedState);
                    if (hostedState) EnsureUIVisible(_hostedLastHosted);
                }
                if (_hostedMaxPlayers != null)
                {
                    _hostedMaxPlayers.gameObject.SetActive(hostedState);
                    if (hostedState) EnsureUIVisible(_hostedMaxPlayers);
                }
                if (_hostedPrivacyText != null)
                {
                    _hostedPrivacyText.gameObject.SetActive(false);
                }
                if (_hostedPrivacyIcon != null)
                {
                    _hostedPrivacyIcon.gameObject.SetActive(hostedState);
                    if (hostedState) EnsureUIVisible(_hostedPrivacyIcon);
                }

                // Category / Empty
                if (_categoryTitle != null)
                {
                    _categoryTitle.gameObject.SetActive(state == DisplayState.Category);
                    if (state == DisplayState.Category) EnsureUIVisible(_categoryTitle);
                }
                if (_emptyMessage != null)
                {
                    _emptyMessage.gameObject.SetActive(state == DisplayState.Empty);
                    if (state == DisplayState.Empty) EnsureUIVisible(_emptyMessage);
                }

                // Global player/ping fallbacks: show only for Normal, and only when
                // there isn't a per-state target wired (avoid duplicate visuals).
                if (_playerCountText != null)
                {
                    bool showGlobalPlayer = state == DisplayState.Normal && _normalPlayerCount == null;
                    _playerCountText.gameObject.SetActive(showGlobalPlayer);
                    if (showGlobalPlayer) EnsureUIVisible(_playerCountText);
                }
                if (_pingText != null)
                {
                    bool showGlobalPing = state == DisplayState.Normal && _normalPing == null && _offlinePing == null;
                    _pingText.gameObject.SetActive(showGlobalPing);
                    if (showGlobalPing) EnsureUIVisible(_pingText);
                }

                // Password icon is used for discovered normal rows; hide it otherwise
                if (_passwordIcon != null)
                {
                    _passwordIcon.SetActive(state == DisplayState.Normal);
                    if (state == DisplayState.Normal) EnsureUIVisible(_passwordIcon);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] NormalizePerStateActive failed: {ex}");
            }
        }

        // Make sure a UI component and its GameObject are visible. If the target
        // GameObject was turned off in the prefab, this will enable it and ensure
        // the UI component (Image/TextMeshProUGUI) is enabled so it renders.
        private void EnsureUIVisible(Component comp)
        {
            if (comp == null) return;

            try
            {
                var go = comp.gameObject;
                // If the target's nearest state container does not match the
                // currently-applied display state, do not enable that container.
                // This avoids enabling the wrong state container (e.g., turning
                // on Offline while showing Normal) when a serialized field is
                // mistakenly wired to a GameObject under the wrong container.
                var targetContainer = FindNearestStateContainerAncestor(go);
                var expectedContainer = GetActiveStateContainer(_appliedDisplayState);
                if (targetContainer != null && expectedContainer != null && targetContainer != expectedContainer)
                {
                    Debug.Log($"[LobbyView] EnsureUIVisible: target '{go.name}' belongs to container '{targetContainer.name}' which is not the active container '{expectedContainer.name}' for state {_appliedDisplayState}. Skipping ancestor enabling.");
                    return;
                }

                // Ensure this object and its ancestors are active so activeInHierarchy
                // becomes true. Some prefab elements are nested under a disabled root
                // (e.g., "Offline Lobby") which prevents rendering even when the
                // component's own GameObject is enabled. Walk parents up to this
                // view's GameObject and enable them.
                EnableAncestorsUpToViewRoot(go);

                // If this is a Graphic (Image/TMP), make sure it's enabled and
                // the CanvasRenderer alpha isn't zeroed out.
                var graphic = comp as UnityEngine.UI.Graphic;
                if (graphic != null)
                {
                    if (!graphic.enabled) graphic.enabled = true;
                    var cr = graphic.canvasRenderer;
                    if (cr != null)
                    {
                        try { cr.SetAlpha(1f); } catch { }
                    }
                }

                // Also handle TextMeshPro separately since it doesn't derive from Graphic
                var tmp = comp as TextMeshProUGUI;
                if (tmp != null)
                {
                    if (!tmp.enabled) tmp.enabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] EnsureUIVisible failed for '{comp?.name}': {ex}");
            }
        }

        // Overload that accepts a GameObject target (some wired fields are GameObjects)
        private void EnsureUIVisible(GameObject go)
        {
            if (go == null) return;

            try
            {
                // If the target's nearest state container does not match the
                // currently-applied display state, skip enabling that container to
                // avoid turning on the wrong UI (see comment in EnsureUIVisible(comp)).
                var targetContainer = FindNearestStateContainerAncestor(go);
                var expectedContainer = GetActiveStateContainer(_appliedDisplayState);
                if (targetContainer != null && expectedContainer != null && targetContainer != expectedContainer)
                {
                    Debug.Log($"[LobbyView] EnsureUIVisible(GameObject): target '{go.name}' belongs to container '{targetContainer.name}' which is not the active container '{expectedContainer.name}' for state {_appliedDisplayState}. Skipping ancestor enabling.");
                    return;
                }

                // Enable the object and its ancestors to ensure it becomes
                // active in the hierarchy.
                EnableAncestorsUpToViewRoot(go);

                var graphic = go.GetComponent<UnityEngine.UI.Graphic>();
                if (graphic != null)
                {
                    if (!graphic.enabled) graphic.enabled = true;
                    var cr = graphic.canvasRenderer;
                    if (cr != null)
                    {
                        try { cr.SetAlpha(1f); } catch { }
                    }
                }

                var tmp = go.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (!tmp.enabled) tmp.enabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] EnsureUIVisible failed for GameObject '{go.name}': {ex}");
            }
        }

        // Force-enable ancestors up to this view root. This is an unconditional fallback
        // used to recover visibility for deeply-nested prefab elements. It walks the
        // parent chain and SetActive(true) on each GameObject until it reaches this
        // view's root GameObject. It will NOT enable known state container GameObjects
        // (Normal/Offline/Category/Empty/Loading); ApplyDisplayState is responsible
        // for toggling containers.
        private void ForceEnableAncestorsUpToViewRootUnconditional(GameObject go)
        {
            if (go == null) return;

            try
            {
                var nearest = FindNearestStateContainerAncestor(go);
                var nearestName = nearest == null ? "<none>" : nearest.name;
                Debug.Log($"[LobbyView] ForceEnableAncestorsUnconditional called for '{go.name}', nearestStateContainer={nearestName}");

                Transform t = go.transform;
                while (t != null)
                {
                    var current = t.gameObject;
                    if (!current.activeSelf)
                    {
                        bool isStateContainer = current == _stateContainerNormal || current == _stateContainerOffline || current == _stateContainerCategory || current == _stateContainerEmpty || current == _stateContainerHosted || current == _stateContainerLoading;
                        if (!isStateContainer)
                        {
                            current.SetActive(true);
                            Debug.Log($"[LobbyView] Force-enabled ancestor '{current.name}' to make '{go.name}' visible (unconditional fallback).");
                        }
                        else
                        {
                            Debug.Log($"[LobbyView] ForceEnableUnconditional: skipping activation of state container '{current.name}' for '{go.name}'.");
                        }
                    }

                    if (current == this.gameObject) break;
                    t = t.parent;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] ForceEnableAncestorsUpToViewRootUnconditional failed for '{go.name}': {ex}");
            }
        }

        // Find the nearest state container ancestor for a GameObject, if any.
        // Returns the container GameObject (Normal/Offline/Category/Empty/Loading) or null.
        private GameObject FindNearestStateContainerAncestor(GameObject go)
        {
            if (go == null) return null;

            var t = go.transform;
            while (t != null)
            {
                if (t.gameObject == _stateContainerNormal) return _stateContainerNormal;
                if (t.gameObject == _stateContainerOffline) return _stateContainerOffline;
                if (t.gameObject == _stateContainerCategory) return _stateContainerCategory;
                if (t.gameObject == _stateContainerEmpty) return _stateContainerEmpty;
                if (t.gameObject == _stateContainerHosted) return _stateContainerHosted;
                if (t.gameObject == _stateContainerLoading) return _stateContainerLoading;
                t = t.parent;
            }

            return null;
        }

        // Search for an Image under the given container that likely corresponds to
        // the provided target by matching name or a keyword. Returns the Image if
        // found, otherwise null.
        private UnityEngine.UI.Image FindImageInContainerByNameOrKeyword(GameObject container, string targetName, string keyword)
        {
            if (container == null) return null;
            try
            {
                var imgs = container.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                if (imgs == null || imgs.Length == 0) return null;
                foreach (var img in imgs)
                {
                    if (img == null || img.gameObject == null) continue;
                    if (string.Equals(img.gameObject.name, targetName, StringComparison.OrdinalIgnoreCase)) return img;
                    if (!string.IsNullOrEmpty(keyword) && img.gameObject.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return img;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] FindImageInContainerByNameOrKeyword failed: {ex}");
            }
            return null;
        }

        // Conditional ancestor enabler: like the unconditional variant but it will
        // skip activating any state container that is not the expected one. This
        // prevents accidentally turning on the Offline/Normal containers when the
        // view is supposed to show a different state.
        private void ForceEnableAncestorsUpToViewRoot(GameObject go, GameObject expectedContainer = null)
        {
            if (go == null) return;

            try
            {
                Transform t = go.transform;
                while (t != null)
                {
                    var current = t.gameObject;

                    bool isStateContainer = current == _stateContainerNormal || current == _stateContainerOffline || current == _stateContainerCategory || current == _stateContainerEmpty || current == _stateContainerHosted || current == _stateContainerLoading;
                    if (isStateContainer && current != expectedContainer)
                    {
                        Debug.Log($"[LobbyView] ForceEnableAncestorsConditional: encountered other state container '{current.name}' while enabling ancestors for '{go.name}'; skipping activation of this container.");
                    }
                    else
                    {
                        // Do not enable state containers here unless it's explicitly the expected container.
                        if (!isStateContainer)
                        {
                            if (!current.activeSelf)
                            {
                                current.SetActive(true);
                                Debug.Log($"[LobbyView] Force-enabled ancestor '{current.name}' to make '{go.name}' visible (conditional fallback).");
                            }
                        }
                        else
                        {
                            Debug.Log($"[LobbyView] ForceEnableAncestorsConditional: skipping activation of state container '{current.name}' (will not toggle containers here).");
                        }
                    }

                    if (expectedContainer != null)
                    {
                        if (current == expectedContainer) break;
                    }
                    else
                    {
                        if (current == this.gameObject) break;
                    }

                    t = t.parent;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] ForceEnableAncestorsUpToViewRoot (conditional) failed for '{go.name}': {ex}");
            }
        }

        // Walk the transform.parent chain and SetActive(true) on each GameObject
        // until we reach the nearest state container (if the target lives under
        // one) or this view's root GameObject. This prevents enabling unrelated
        // sibling containers (e.g., enabling the Offline container when showing
        // Normal) which produced overlapping visuals.
        private void EnableAncestorsUpToViewRoot(GameObject go)
        {
            if (go == null) return;

            try
            {
                var container = FindNearestStateContainerAncestor(go);
                Transform t = go.transform;
                while (t != null)
                {
                    var current = t.gameObject;
                    if (!current.activeSelf)
                    {
                        // Do not enable known state container GameObjects here; ApplyDisplayState
                        // should be the only code that toggles containers. Enable intermediate
                        // wrappers only so children can become activeInHierarchy.
                        bool isStateContainer = current == _stateContainerNormal || current == _stateContainerOffline || current == _stateContainerCategory || current == _stateContainerEmpty || current == _stateContainerHosted || current == _stateContainerLoading;
                        if (!isStateContainer)
                        {
                            current.SetActive(true);
                            Debug.Log($"[LobbyView] Enabled ancestor '{current.name}' to make '{go.name}' visible.");
                        }
                        else
                        {
                            Debug.Log($"[LobbyView] EnableAncestorsUpToViewRoot: skipping activation of state container '{current.name}' for '{go.name}'.");
                        }
                    }

                    if (container != null)
                    {
                        if (current == container) break;
                    }
                    else
                    {
                        if (current == this.gameObject) break;
                    }

                    t = t.parent;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] EnableAncestorsUpToViewRoot failed for '{go.name}': {ex}");
            }
        }

        // Auto-wiring is intentionally disabled. Please wire the state container
        // references and any per-state TMP fields (player count, ping, password icon)
        // directly in the LobbyView prefab inspector. This avoids fragile runtime
        // heuristics and keeps prefab/layout changes explicit.
        private void TryAutoWireStateContainers()
        {
            Debug.LogWarning("[LobbyView] Auto-wiring is disabled. Please assign state container and text fields on the LobbyView prefab in the Inspector.");
        }

        // Return the GameObject that corresponds to the given DisplayState (if wired).
        private GameObject GetActiveStateContainer(DisplayState state)
        {
            return state switch
            {
                DisplayState.Normal => _stateContainerNormal,
                DisplayState.Category => _stateContainerCategory ?? _stateContainerEmpty,
                DisplayState.Empty => _stateContainerEmpty,
                DisplayState.Offline => _stateContainerOffline,
                DisplayState.Hosted => _stateContainerHosted ?? _stateContainerNormal,
                DisplayState.Loading => _stateContainerLoading,
                _ => _stateContainerNormal
            };
        }

        // Recursively find a child by name (case-insensitive) and set its TextMeshProUGUI text.
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name)) return null;
            foreach (Transform child in parent)
            {
                if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // Attempt to write a text value into a named child under the active container
        // and ensure that child's GameObject is visible. Returns true when successful.
        // Improved: after writing, search all TMPs under the active container and
        // prefer the component that contains the written value (covers nested
        // layouts and shared child names across containers).
        private bool TryWriteAndEnsureToActiveContainer(GameObject activeContainer, string childName, string value)
        {
            if (activeContainer == null) return false;
            try
            {
                if (SetTextInContainer(activeContainer.transform, childName, value))
                {
                    // Look for TMPs under the active container that contain the
                    // value we just wrote. This is robust against formatting/colour
                    // tags and nested container wrappers.
                    var tmps = activeContainer.GetComponentsInChildren<TextMeshProUGUI>(true);
                    if (tmps != null && tmps.Length > 0)
                    {
                        foreach (var t in tmps)
                        {
                            if (t == null) continue;
                            try
                            {
                                if (!string.IsNullOrEmpty(value) && t.text != null && t.text.Contains(value))
                                {
                                    EnsureUIVisible(t);
                                    return true;
                                }
                            }
                            catch { }
                        }
                    }

                    // Fallback: try to find the named child recursively and enable it.
                    var target = FindChildRecursive(activeContainer.transform, childName);
                    if (target != null)
                    {
                        var t = target.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (t != null)
                        {
                            EnsureUIVisible(t);
                            return true;
                        }
                        else
                        {
                            EnsureUIVisible(target.gameObject);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] TryWriteAndEnsureToActiveContainer failed for '{childName}' in '{activeContainer?.name}': {ex}");
            }

            return false;
        }
        
        // Additional serialized fields to support prefab-driven visuals for category/selection
        // Note: UI fields (canvas group, backgrounds, icon, primary/secondary text lists)
        // are declared in the base `ViewObject` class as protected members to avoid
        // duplicate serialized field names. Do not re-declare them here.
        
        [Header("Favorite Button")]
        [SerializeField]
        private GameObject _favoriteButtonContainer;
        [SerializeField]
        private GameObject _favoriteButtonContainerSelected;
        [SerializeField]
        private Image[] _favoriteButtons;
        
        [Header("Sprites")]
        [SerializeField]
        private Sprite _favoriteUnfilled;
        [SerializeField]
        private Sprite _favoriteFilled;

        private LobbyBrowserMenu _cachedMenu;
        
        public override void Show(bool selected, LobbyViewType viewType)
        {
            base.Show(selected, viewType);

            ApplyFavoriteButtonState(viewType, selected);
            
            // Determine desired prefab-driven display state for this view type. If the
            // prefab has state containers wired, ApplyDisplayState will toggle them.
            // Determine desired prefab-driven display state for this view type. Declared
            // outside the try block so it is available to later container population.
            DisplayState desiredState = DisplayState.Normal;
            try
            {
                if (viewType is LobbyCategoryViewType) desiredState = DisplayState.Category;
                else if (viewType is LobbyEmptyViewType) desiredState = DisplayState.Empty;
                else if (viewType is SavedLobbyViewType saved)
                {
                    // If saved bookmark has live info, treat as normal; otherwise offline
                    desiredState = saved.LiveInfo != null ? DisplayState.Normal : DisplayState.Offline;
                }
                else if (viewType is MyLobbyViewType)
                {
                    desiredState = DisplayState.Hosted;
                }
                else if (viewType is DiscoveredLobbyViewType) desiredState = DisplayState.Normal;

                var applied = ApplyDisplayState(desiredState);
                Debug.Log($"[LobbyView] ApplyDisplayState returned {applied} for {desiredState}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Exception while applying prefab display state: {ex}");
            }
            switch (viewType)
            {
                case LobbyCategoryViewType category:
                    // Category rows typically only show a single primary label. If the prefab
                    // provides primary/secondary text fields, populate them via the base view
                    // (ViewObject) and let the prefab visuals handle layout. We don't mutate
                    // the text fields here because the ListMenu/ViewObject plumbing sets them.
                    break;

                case DiscoveredLobbyViewType discovered:
                    if (_playerCountText != null)
                    {
                        _playerCountText.text = discovered.GetPlayerCountText();
                    }

                    if (_pingText != null)
                    {
                        _pingText.text = discovered.GetPingText();
                    }

                    if (_passwordIcon != null)
                    {
                        _passwordIcon.SetActive(discovered.HasPassword());
                        if (discovered.HasPassword()) EnsureUIVisible(_passwordIcon);
                    }

                    break;

                case SavedLobbyViewType saved:
                    if (_playerCountText != null)
                    {
                        _playerCountText.text = saved.GetStatusBadge();
                    }

                    if (_pingText != null)
                    {
                        _pingText.text = saved.GetInfoBadge();
                    }

                    if (_passwordIcon != null)
                    {
                        _passwordIcon.SetActive(false);
                    }

                    break;

                case MyLobbyViewType hosted:
                    if (_playerCountText != null)
                    {
                        _playerCountText.text = string.Empty;
                        _playerCountText.gameObject.SetActive(false);
                    }

                    if (_pingText != null)
                    {
                        _pingText.text = string.Empty;
                        _pingText.gameObject.SetActive(false);
                    }

                    if (_passwordIcon != null)
                    {
                        _passwordIcon.SetActive(false);
                    }

                    break;

                default:
                    if (_playerCountText != null)
                    {
                        _playerCountText.text = string.Empty;
                    }

                    if (_pingText != null)
                    {
                        _pingText.text = string.Empty;
                    }

                    if (_passwordIcon != null)
                    {
                        _passwordIcon.SetActive(false);
                    }

                    break;
            }
            // Populate per-state, explicitly-wired fields first. This lets the prefab
            // owner wire the exact TMP fields for each state (recommended). If a
            // per-state field isn't assigned, fall back to the previous named-child
            // lookup to preserve compatibility with older prefabs.
            try
            {
                GameObject activeContainer = GetActiveStateContainer(desiredState);
                Debug.Log($"[LobbyView] Active container for {desiredState}: {(activeContainer == null ? "<null>" : activeContainer.name)}");
                if (activeContainer != null)
                {
                    // Category rows
                    if (viewType is LobbyCategoryViewType)
                    {
                        var title = viewType.GetPrimaryText(selected) ?? string.Empty;
                        if (_categoryTitle != null)
                        {
                            _categoryTitle.text = title;
                        }
                        else
                        {
                            // Backwards compatibility: write into a child named "Title"
                            if (!SetTextInContainer(activeContainer.transform, "Title", title))
                                Debug.LogWarning($"[LobbyView] Category title field not wired and fallback failed for '{activeContainer.name}'.");
                        }
                    }

                    // Empty placeholder rows
                    else if (viewType is LobbyEmptyViewType)
                    {
                        var msg = viewType.GetPrimaryText(selected) ?? string.Empty;
                        if (_emptyMessage != null)
                        {
                            _emptyMessage.text = msg;
                        }
                        else
                        {
                            if (!SetTextInContainer(activeContainer.transform, "Message", msg))
                                Debug.LogWarning($"[LobbyView] Empty message field not wired and fallback failed for '{activeContainer.name}'.");
                        }
                    }

                    // Discovered / Normal rows
                    else if (viewType is DiscoveredLobbyViewType d)
                    {
                        // Lobby name
                        if (_normalLobbyName != null) _normalLobbyName.text = d.GetPrimaryText(selected) ?? string.Empty;
                        else if (!SetTextInContainer(activeContainer.transform, "Lobby Name", d.GetPrimaryText(selected)))
                            Debug.LogWarning($"[LobbyView] Lobby Name not wired for normal view and fallback failed in '{activeContainer.name}'.");

                        // Host / secondary
                        if (_normalHost != null) _normalHost.text = d.GetSecondaryText(selected) ?? string.Empty;
                        else if (!SetTextInContainer(activeContainer.transform, "Host", d.GetSecondaryText(selected)))
                            Debug.LogWarning($"[LobbyView] Host not wired for normal view and fallback failed in '{activeContainer.name}'.");

                        // Player count (prefer per-state target, fallback to global _playerCountText)
                        if (_normalPlayerCount != null)
                        {
                            _normalPlayerCount.gameObject.SetActive(true);
                            _normalPlayerCount.text = d.GetPlayerCountText();
                            // Ensure the newly-enabled TMP and its ancestors are visible
                            EnsureUIVisible(_normalPlayerCount);
                        }
                        else if (_playerCountText != null)
                        {
                            // If the serialized global player-count TMP points to a child
                            // under a different state container, prefer writing into the
                            // active container's "Player Count" child instead of
                            // enabling an unrelated container.
                            if (!TryWriteAndEnsureToActiveContainer(activeContainer, "Player Count", d.GetPlayerCountText()))
                            {
                                _playerCountText.gameObject.SetActive(true);
                                _playerCountText.text = d.GetPlayerCountText();
                                EnsureUIVisible(_playerCountText);
                            }
                        }
                        else if (!SetTextInContainer(activeContainer.transform, "Player Count", d.GetPlayerCountText()))
                            Debug.LogWarning($"[LobbyView] Player Count not wired for normal view and fallback failed in '{activeContainer.name}'.");

                        // Ping
                        if (_normalPing != null) _normalPing.text = d.GetPingText();
                        else if (_pingText != null)
                        {
                            if (!TryWriteAndEnsureToActiveContainer(activeContainer, "Ping Text", d.GetPingText()))
                            {
                                _pingText.text = d.GetPingText();
                            }
                        }
                        else if (!SetTextInContainer(activeContainer.transform, "Ping Text", d.GetPingText()))
                            Debug.LogWarning($"[LobbyView] Ping Text not wired for normal view and fallback failed in '{activeContainer.name}'.");

                        // Ensure ping target is visible when enabling
                        if (_normalPing != null) EnsureUIVisible(_normalPing);
                        else if (_pingText != null)
                        {
                            // Only ensure the global ping TMP is made visible if it's
                            // actually part of the active container; otherwise the
                            // TryWriteAndEnsureToActiveContainer above will have
                            // created/ensured the correct child instead.
                            var targetContainer = FindNearestStateContainerAncestor(_pingText.gameObject);
                            var expectedContainer = GetActiveStateContainer(_appliedDisplayState);
                            if (targetContainer == null || expectedContainer == null || targetContainer == expectedContainer)
                                EnsureUIVisible(_pingText);
                        }

                        // Privacy: prefer icon if provided, otherwise fallback to TMP label
                        bool hasPwd = d.HasPassword();
                        if (_normalPrivacyIcon != null)
                        {
                            try
                            {
                                // Ensure the GameObject is active and the Image component is enabled
                                _normalPrivacyIcon.gameObject.SetActive(true);
                                if (!_normalPrivacyIcon.enabled) _normalPrivacyIcon.enabled = true;

                                // Assign sprite if available and set a color for locked/unlocked states
                                _normalPrivacyIcon.sprite = hasPwd ? _privacyLocked : _privacyUnlocked;
                                _normalPrivacyIcon.color = hasPwd ? _privacyLockedColor : _privacyUnlockedColor;

                                // Force CanvasRenderer alpha to 1 so it isn't transparent
                                try { _normalPrivacyIcon.canvasRenderer.SetAlpha(1f); } catch { }

                                // Ensure ancestors/graphic visibility
                                EnsureUIVisible(_normalPrivacyIcon);

                                // If the safe EnsureUIVisible path didn't make the icon active
                                // try to find an equivalent Image under the active container
                                // before falling back to enabling ancestors. This avoids
                                // activating the wrong state container and showing both
                                // Normal and Offline visuals simultaneously.
                                if (!_normalPrivacyIcon.gameObject.activeInHierarchy || !_normalPrivacyIcon.enabled)
                                {
                                    var expectedContainer = GetActiveStateContainer(_appliedDisplayState);
                                    var targetContainer = FindNearestStateContainerAncestor(_normalPrivacyIcon.gameObject);

                                    // If this icon lives under a different container than the
                                    // one we're currently showing, prefer to find a matching
                                    // icon under the active container and use that instead.
                                    if (targetContainer != expectedContainer && expectedContainer != null)
                                    {
                                        var found = FindImageInContainerByNameOrKeyword(expectedContainer, _normalPrivacyIcon.gameObject.name, "Privacy");
                                        if (found != null)
                                        {
                                            Debug.Log($"[LobbyView] Found equivalent normal privacy Image '{found.gameObject.name}' under active container '{expectedContainer.name}'; using it.");
                                            found.gameObject.SetActive(true);
                                            if (!found.enabled) found.enabled = true;
                                            found.sprite = hasPwd ? _privacyLocked : _privacyUnlocked;
                                            found.color = hasPwd ? _privacyLockedColor : _privacyUnlockedColor;
                                            try { found.canvasRenderer.SetAlpha(1f); } catch { }
                                            EnsureUIVisible(found);
                                        }
                                        else
                                        {
                                            Debug.Log("[LobbyView] _normalPrivacyIcon not visible after EnsureUIVisible — applying conditional fallback.");
                                            ForceEnableAncestorsUpToViewRoot(_normalPrivacyIcon.gameObject, expectedContainer);
                                            _normalPrivacyIcon.gameObject.SetActive(true);
                                            _normalPrivacyIcon.enabled = true;
                                            try { _normalPrivacyIcon.canvasRenderer.SetAlpha(1f); } catch { }
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log("[LobbyView] _normalPrivacyIcon not visible after EnsureUIVisible — applying conditional fallback.");
                                        ForceEnableAncestorsUpToViewRoot(_normalPrivacyIcon.gameObject, expectedContainer);
                                        _normalPrivacyIcon.gameObject.SetActive(true);
                                        _normalPrivacyIcon.enabled = true;
                                        try { _normalPrivacyIcon.canvasRenderer.SetAlpha(1f); } catch { }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[LobbyView] Failed to show _normalPrivacyIcon: {ex}");
                            }
                        }
                        else
                        {
                            // fall back to the password icon GameObject or text fields
                            if (_passwordIcon != null)
                            {
                                _passwordIcon.SetActive(hasPwd);
                                if (hasPwd) EnsureUIVisible(_passwordIcon);
                            }
                            else if (!SetTextInContainer(activeContainer.transform, "Privacy", hasPwd ? "LOCKED" : "UNLOCKED"))
                                Debug.LogWarning($"[LobbyView] Privacy target not wired for normal view and fallback failed in '{activeContainer.name}'.");
                        }
                    }

                    // Saved / Offline rows
                    else if (viewType is SavedLobbyViewType s)
                    {
                        if (_offlineLobbyName != null) _offlineLobbyName.text = s.GetPrimaryText(selected) ?? string.Empty;
                        else if (!SetTextInContainer(activeContainer.transform, "Lobby Name", s.GetPrimaryText(selected)))
                            Debug.LogWarning($"[LobbyView] Lobby Name not wired for offline view and fallback failed in '{activeContainer.name}'.");

                        if (_offlineLastConnected != null) _offlineLastConnected.text = s.GetSecondaryText(selected) ?? string.Empty;
                        else if (!SetTextInContainer(activeContainer.transform, "Last Connected", s.GetSecondaryText(selected)))
                            Debug.LogWarning($"[LobbyView] Last Connected not wired for offline view and fallback failed in '{activeContainer.name}'.");

                        // Offline rows should not show a live player count. Hide any player-count targets
                        if (_offlinePlayerCount != null)
                        {
                            _offlinePlayerCount.gameObject.SetActive(false);
                        }
                        if (_playerCountText != null)
                        {
                            _playerCountText.gameObject.SetActive(false);
                        }

                        // For saved/offline rows: show a simple "OFFLINE" indicator in the
                        // ping slot when there's no live info. The secondary text
                        // (Last Connected) already contains the timestamp/age.
                        string pingValue = (s.LiveInfo != null) ? s.GetInfoBadge() : "OFFLINE";
                        if (_offlinePing != null) {
                            _offlinePing.text = pingValue;
                            EnsureUIVisible(_offlinePing);
                        }
                        else if (_pingText != null) {
                            if (!TryWriteAndEnsureToActiveContainer(activeContainer, "Ping Text", pingValue))
                            {
                                _pingText.text = pingValue;
                                // Check container match before enabling
                                var targetContainer = FindNearestStateContainerAncestor(_pingText.gameObject);
                                var expectedContainer = GetActiveStateContainer(_appliedDisplayState);
                                if (targetContainer == null || expectedContainer == null || targetContainer == expectedContainer)
                                    EnsureUIVisible(_pingText);
                            }
                        }
                        else if (!SetTextInContainer(activeContainer.transform, "Ping Text", pingValue))
                            Debug.LogWarning($"[LobbyView] Ping Text not wired for offline view and fallback failed in '{activeContainer.name}'.");


                        // Privacy: use icon (locked/unlocked) for saved/offline rows. If no icon wired
                        // fall back to a TMP or a simple label.
                        bool savedHasPassword = false;
                        // SavedLobbyViewType may contain LiveInfo (from discovery) or a stored bookmark with a password
                        if (s != null)
                        {
                            if (s.LiveInfo != null)
                                savedHasPassword = s.LiveInfo.hasPassword;
                            else
                                savedHasPassword = s.Bookmark != null && !string.IsNullOrEmpty(s.Bookmark.password);
                        }
                        if (_offlinePrivacyIcon != null)
                        {
                            try
                            {
                                _offlinePrivacyIcon.gameObject.SetActive(true);
                                if (!_offlinePrivacyIcon.enabled) _offlinePrivacyIcon.enabled = true;

                                _offlinePrivacyIcon.sprite = savedHasPassword ? _privacyLocked : _privacyUnlocked;
                                _offlinePrivacyIcon.color = savedHasPassword ? _privacyLockedColor : _privacyUnlockedColor;

                                try { _offlinePrivacyIcon.canvasRenderer.SetAlpha(1f); } catch { }

                                EnsureUIVisible(_offlinePrivacyIcon);

                                if (!_offlinePrivacyIcon.gameObject.activeInHierarchy || !_offlinePrivacyIcon.enabled)
                                {
                                    var expectedContainer = GetActiveStateContainer(_appliedDisplayState);
                                    var targetContainer = FindNearestStateContainerAncestor(_offlinePrivacyIcon.gameObject);

                                    if (targetContainer != expectedContainer && expectedContainer != null)
                                    {
                                        var found = FindImageInContainerByNameOrKeyword(expectedContainer, _offlinePrivacyIcon.gameObject.name, "Privacy");
                                        if (found != null)
                                        {
                                            Debug.Log($"[LobbyView] Found equivalent offline privacy Image '{found.gameObject.name}' under active container '{expectedContainer.name}'; using it.");
                                            found.gameObject.SetActive(true);
                                            if (!found.enabled) found.enabled = true;
                                            found.sprite = savedHasPassword ? _privacyLocked : _privacyUnlocked;
                                            found.color = savedHasPassword ? _privacyLockedColor : _privacyUnlockedColor;
                                            try { found.canvasRenderer.SetAlpha(1f); } catch { }
                                            EnsureUIVisible(found);
                                        }
                                        else
                                        {
                                            Debug.Log("[LobbyView] _offlinePrivacyIcon not visible after EnsureUIVisible — applying conditional fallback.");
                                            ForceEnableAncestorsUpToViewRoot(_offlinePrivacyIcon.gameObject, expectedContainer);
                                            _offlinePrivacyIcon.gameObject.SetActive(true);
                                            _offlinePrivacyIcon.enabled = true;
                                            try { _offlinePrivacyIcon.canvasRenderer.SetAlpha(1f); } catch { }
                                        }
                                    }
                                    else
                                    {
                                        Debug.Log("[LobbyView] _offlinePrivacyIcon not visible after EnsureUIVisible — applying conditional fallback.");
                                        ForceEnableAncestorsUpToViewRoot(_offlinePrivacyIcon.gameObject, expectedContainer);
                                        _offlinePrivacyIcon.gameObject.SetActive(true);
                                        _offlinePrivacyIcon.enabled = true;
                                        try { _offlinePrivacyIcon.canvasRenderer.SetAlpha(1f); } catch { }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[LobbyView] Failed to show _offlinePrivacyIcon: {ex}");
                            }
                        }
                        else if (_offlinePrivacy != null)
                        {
                            // keep backwards-compatible TMP if present
                            _offlinePrivacy.text = savedHasPassword ? "PRIVATE" : "PUBLIC";
                        }
                        else if (!SetTextInContainer(activeContainer.transform, "Privacy", savedHasPassword ? "PRIVATE" : "PUBLIC"))
                        {
                            Debug.LogWarning($"[LobbyView] Privacy target not wired for offline view and fallback failed in '{activeContainer.name}'.");
                        }
                    }

                    // Hosted presets (My Lobbies)
                    else if (viewType is MyLobbyViewType m)
                    {
                        var nameText = m.GetPrimaryText(selected) ?? string.Empty;
                        if (_hostedLobbyName != null)
                        {
                            _hostedLobbyName.text = nameText;
                            EnsureUIVisible(_hostedLobbyName);
                        }
                        else if (!SetTextInContainer(activeContainer.transform, "Lobby Name", nameText))
                        {
                            Debug.LogWarning($"[LobbyView] Lobby Name not wired for hosted view and fallback failed in '{activeContainer.name}'.");
                        }

                        var hostedRecency = m.GetHostedRecencyText();
                        if (_hostedLastHosted != null)
                        {
                            _hostedLastHosted.text = hostedRecency;
                            EnsureUIVisible(_hostedLastHosted);
                        }
                        else if (!SetTextInContainer(activeContainer.transform, "Last Connected", hostedRecency))
                        {
                            Debug.LogWarning($"[LobbyView] Last Connected not wired for hosted view and fallback failed in '{activeContainer.name}'.");
                        }

                        var maxPlayers = m.GetMaxPlayersLabel();
                        if (_hostedMaxPlayers != null)
                        {
                            _hostedMaxPlayers.text = maxPlayers;
                            EnsureUIVisible(_hostedMaxPlayers);
                        }
                        else if (!SetTextInContainer(activeContainer.transform, "Player Count", maxPlayers))
                        {
                            Debug.LogWarning($"[LobbyView] Player Count not wired for hosted view and fallback failed in '{activeContainer.name}'.");
                        }

                        if (_hostedPrivacyText != null)
                        {
                            _hostedPrivacyText.text = string.Empty;
                            _hostedPrivacyText.gameObject.SetActive(false);
                        }

                        if (_hostedPrivacyIcon != null)
                        {
                            try
                            {
                                _hostedPrivacyIcon.gameObject.SetActive(true);
                                if (!_hostedPrivacyIcon.enabled) _hostedPrivacyIcon.enabled = true;

                                bool locked = m.IsPasswordProtected();
                                _hostedPrivacyIcon.sprite = locked ? _privacyLocked : _privacyUnlocked;
                                _hostedPrivacyIcon.color = locked ? _privacyLockedColor : _privacyUnlockedColor;

                                try { _hostedPrivacyIcon.canvasRenderer.SetAlpha(1f); } catch { }

                                EnsureUIVisible(_hostedPrivacyIcon);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[LobbyView] Failed to show _hostedPrivacyIcon: {ex}");
                            }
                        }
                    }

                    // Generic fallback for unknown types: try to populate a Lobby Name/Host pair
                    else
                    {
                        if (_normalLobbyName != null) _normalLobbyName.text = viewType.GetPrimaryText(selected) ?? string.Empty;
                        else if (!SetTextInContainer(activeContainer.transform, "Lobby Name", viewType.GetPrimaryText(selected)))
                            Debug.LogWarning($"[LobbyView] Generic Lobby Name not wired and fallback failed in '{activeContainer.name}'.");

                        if (_normalHost != null) _normalHost.text = viewType.GetSecondaryText(selected) ?? string.Empty;
                        else if (!SetTextInContainer(activeContainer.transform, "Host", viewType.GetSecondaryText(selected)))
                            Debug.LogWarning($"[LobbyView] Generic Host not wired and fallback failed in '{activeContainer.name}'.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[LobbyView] No active container found for state {desiredState}. Ensure state container fields are wired on the prefab.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Exception while populating per-state fields: {ex}");
            }

            // Post-population sanity check: if a player-count target was populated
            // but remains inactive in the hierarchy, force-enable it and its
            // ancestors so the text becomes visible. This guards against cases
            // where the prefab has nested inactive wrappers that weren't
            // previously enabled (or where prior code skipped enabling).
            try
            {
                if (desiredState == DisplayState.Normal)
                {
                    if (_normalPlayerCount != null)
                    {
                        var txt = _normalPlayerCount.text ?? string.Empty;
                        if (!string.IsNullOrEmpty(txt) && !_normalPlayerCount.gameObject.activeInHierarchy)
                        {
                            Debug.Log($"[LobbyView] Post-population: forcing visibility for _normalPlayerCount (text='{txt}').");
                            _normalPlayerCount.gameObject.SetActive(true);
                            EnsureUIVisible(_normalPlayerCount);
                        }
                    }

                    // Also handle the global fallback in case the prefab wired only
                    // the global TMP but it wasn't under the active container.
                    if (_normalPlayerCount == null && _playerCountText != null)
                    {
                        var gtxt = _playerCountText.text ?? string.Empty;
                        if (!string.IsNullOrEmpty(gtxt) && !_playerCountText.gameObject.activeInHierarchy)
                        {
                            Debug.Log($"[LobbyView] Post-population: forcing visibility for _playerCountText (text='{gtxt}').");
                            _playerCountText.gameObject.SetActive(true);
                            EnsureUIVisible(_playerCountText);
                        }
                    }

                    // Ping: mirror the same post-population behaviour we added for
                    // player count. If a per-state ping was populated but remains
                    // inactive, force it visible. If no per-state ping exists but
                    // the global ping TMP was used and is inactive, force that.
                    if (_normalPing != null)
                    {
                        var ptxt = _normalPing.text ?? string.Empty;
                        if (!string.IsNullOrEmpty(ptxt) && !_normalPing.gameObject.activeInHierarchy)
                        {
                            Debug.Log($"[LobbyView] Post-population: forcing visibility for _normalPing (text='{ptxt}').");
                            _normalPing.gameObject.SetActive(true);
                            EnsureUIVisible(_normalPing);
                        }
                    }
                    else if (_pingText != null)
                    {
                        var gptxt = _pingText.text ?? string.Empty;
                        if (!string.IsNullOrEmpty(gptxt) && !_pingText.gameObject.activeInHierarchy)
                        {
                            Debug.Log($"[LobbyView] Post-population: forcing visibility for _pingText (text='{gptxt}').");
                            _pingText.gameObject.SetActive(true);
                            EnsureUIVisible(_pingText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Post-population visibility check failed: {ex}");
            }

            // Toggle background variants based on selection and category state (if wired).
            try
            {
                if (_normalBackground != null)
                    _normalBackground.SetActive(!(viewType is LobbyCategoryViewType) && !selected);
                if (_selectedBackground != null)
                    _selectedBackground.SetActive(selected);
                if (_categoryBackground != null)
                    _categoryBackground.SetActive(viewType is LobbyCategoryViewType && !selected);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Exception while toggling background variants: {ex}");
            }

            UpdateFavoriteSprite(viewType.IsFavorited);

            // Populate primary and secondary text fields from the view type. Many prefabs
            // provide multiple TMP fields or grouped text elements; write to all configured
            // targets so prefab layouts get consistent values.
            try
            {
                var primary = viewType.GetPrimaryText(selected) ?? string.Empty;
                var secondary = viewType.GetSecondaryText(selected) ?? string.Empty;

                if (_primaryText != null)
                {
                    foreach (var t in _primaryText)
                    {
                        if (t != null) t.text = primary;
                    }
                }

                if (_secondaryText != null)
                {
                    foreach (var t in _secondaryText)
                    {
                        if (t != null) t.text = secondary;
                    }
                }

                // Icon visibility: hide icon for category/empty rows if prefab supports it
                if (_icon != null)
                {
                    bool showIcon = !(viewType is LobbyCategoryViewType) && !(viewType is LobbyEmptyViewType);
                    _icon.gameObject.SetActive(showIcon);
                }

                // CanvasGroup enable/disable to block interactions on category rows
                if (_canvasGroup != null)
                {
                    _canvasGroup.interactable = !(viewType is LobbyCategoryViewType || viewType is LobbyEmptyViewType);
                    _canvasGroup.blocksRaycasts = !(viewType is LobbyCategoryViewType || viewType is LobbyEmptyViewType);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Exception while populating text fields: {ex}");
            }
        }

        // Ensure this view can receive pointer events by having a transparent Image as a raycast target.
        private void EnsureRaycastTarget()
        {
            // If there's already an Image on this GameObject, enable raycast target.
            var img = GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.raycastTarget = true;
                return;
            }

            // Otherwise, add a transparent Image so pointer events are received.
            try
            {
                img = gameObject.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LobbyView] Failed to add Image for raycast target: {ex}");
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!Showing)
            {
                return;
            }

            EnsureRaycastTarget();

            // Mouse hover should not change list selection or sidebar content.
            // Leave visual state controlled exclusively by the active selection.
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!Showing)
            {
                return;
            }

            // Hover exit intentionally does nothing; selection visuals remain
            // governed by the menu's active index.
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Showing || eventData == null)
            {
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            EnsureRaycastTarget();

            var menu = ResolveMenu();
            menu?.HandleViewPointerClick(ViewType);
        }

        private void ApplyFavoriteButtonState(LobbyViewType viewType, bool selected)
        {
            bool showFavorite = viewType.ShowFavoriteButton;

            if (_favoriteButtonContainer != null)
            {
                _favoriteButtonContainer.SetActive(showFavorite && !selected);
            }

            if (_favoriteButtonContainerSelected != null)
            {
                _favoriteButtonContainerSelected.SetActive(showFavorite && selected);
            }
        }
        
        private void UpdateFavoriteSprite(bool isFavorited)
        {
            if (_favoriteButtons == null)
            {
                Debug.LogWarning("[LobbyView] UpdateFavoriteSprite: _favoriteButtons is null or not assigned on prefab.");
                return;
            }

            Debug.Log($"[LobbyView] UpdateFavoriteSprite: isFavorited={isFavorited}, buttons={_favoriteButtons.Length}, filledAssigned={_favoriteFilled!=null}, unfilledAssigned={_favoriteUnfilled!=null}");

            foreach (var button in _favoriteButtons)
            {
                if (button == null)
                {
                    Debug.LogWarning("[LobbyView] UpdateFavoriteSprite: encountered null Image in _favoriteButtons array.");
                    continue;
                }

                var target = isFavorited ? _favoriteFilled : _favoriteUnfilled;
                if (target == null)
                {
                    Debug.LogWarning($"[LobbyView] UpdateFavoriteSprite: target sprite is null for isFavorited={isFavorited}. Skipping.");
                    continue;
                }

                try
                {
                    button.sprite = target;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LobbyView] Failed to set favorite sprite on Image '{button.name}': {ex}");
                }
            }
        }
        
        /// <summary>
        /// Called when the join button is clicked (or main action is triggered).
        /// </summary>
        public void JoinClick()
        {
            if (!Showing) return;
            
            ViewType.OnJoinClick();
        }
        
        /// <summary>
        /// Called when the favorite button is clicked.
        /// </summary>
        public void FavoriteClick()
        {
            if (!Showing) return;
            
            ViewType.OnFavoriteClick();
            
            // Update the sprite after in case the state changed
            UpdateFavoriteSprite(ViewType.IsFavorited);
        }

        private LobbyBrowserMenu ResolveMenu()
        {
            if (ViewType != null)
            {
                var owner = ViewType.MenuOwner;
                if (owner != null)
                {
                    _cachedMenu = owner;
                    return owner;
                }
            }

            if (_cachedMenu == null)
            {
                _cachedMenu = FindObjectOfType<LobbyBrowserMenu>();
            }

            return _cachedMenu;
        }
    }
}
