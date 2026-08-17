using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;
using YARG.Settings;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings
{
    [DefaultExecutionOrder(-10000)]
    public class SettingsMenu : MonoSingleton<SettingsMenu>
    {
        [SerializeField]
        private HeaderTabs _headerTabs;
        [SerializeField]
        private Transform _settingsContainer;
        [SerializeField]
        private NavigationGroup _settingsNavGroup;
        [SerializeField]
        private ScrollRect _scrollRect;

        [Space]
        [SerializeField]
        private GameObject _searchBarContainer;
        [SerializeField]
        private TMP_InputField _searchBar;
        [SerializeField]
        private TextMeshProUGUI _searchHeaderText;

        [Space]
        [SerializeField]
        private Transform _previewContainerWorld;
        [SerializeField]
        private Transform _previewContainerUI;

        /// <summary>
        /// Public access so tabs (e.g. PresetSubTab) can locate the preview
        /// sidebar and add controls to its header area.
        /// </summary>
        public Transform PreviewContainerUI => _previewContainerUI;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _settingName;
        [SerializeField]
        private TextMeshProUGUI _settingDescription;

        public Tab CurrentTab { get; private set; }
        public string SearchQuery => _searchBar.text;

        public event Action SettingChanged;

        private static bool _openOnNextMenuLoad;
        private static bool _skipMenuReactivationOnDisable;

        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;
        private bool _tabsInitialized;
        private string _pendingTabName;

        private bool _showAdvanced;

        public bool ShowAdvanced
        {
            get => SettingsManager.Settings?.ShowAdvancedSettings?.Value ?? _showAdvanced;
            private set
            {
                var setting = SettingsManager.Settings?.ShowAdvancedSettings;
                if (setting != null)
                {
                    setting.SetValueWithoutNotify(value);
                }
                else
                {
                    _showAdvanced = value;
                }
            }
        }

        public static void OpenOnNextMenuLoad()
        {
            _openOnNextMenuLoad = true;
        }

        public static bool ConsumeOpenOnNextMenuLoad()
        {
            if (!_openOnNextMenuLoad)
            {
                return false;
            }

            _openOnNextMenuLoad = false;
            return true;
        }

        public void PrepareForSceneTransition()
        {
            _skipMenuReactivationOnDisable = true;
            SceneManager.sceneLoaded -= HideAfterSceneTransition;
            SceneManager.sceneLoaded += HideAfterSceneTransition;
        }

        private void HideAfterSceneTransition(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= HideAfterSceneTransition;
            gameObject.SetActive(false);
        }

        protected override void SingletonAwake()
        {
            // Settings menu defaults to active so that it will be initialized at startup
            gameObject.SetActive(false);

            _ready = true;
        }

        private void Start()
        {
            // Long setting names (e.g. preset activation-note colors) can exceed
            // the sidebar width; shrink-to-fit on one line instead of wrapping,
            // with an ellipsis as the last resort below the minimum size.
            // TODO: bake these five properties into the SettingsMenu prefab's
            // Setting Name TMP text and delete this block. They're set in code
            // only because prefab edits happen in the Unity runtime tree, not here.
            _settingName.textWrappingMode = TextWrappingModes.NoWrap;
            _settingName.overflowMode = TextOverflowModes.Ellipsis;
            _settingName.fontSizeMax = _settingName.fontSize;
            _settingName.fontSizeMin = 18f;
            _settingName.enableAutoSizing = true;

            var tabs = new List<HeaderTabs.TabInfo>();

            // Add the main tabs
            foreach (var tab in SettingsManager.DisplayedSettingsTabs)
            {
                // Load the tab sprite
                var sprite = Addressables.LoadAssetAsync<Sprite>($"TabIcons[{tab.Icon}]").WaitForCompletion();

                tabs.Add(new HeaderTabs.TabInfo
                {
                    Icon = sprite,
                    Id = tab.Name,
                    DisplayName = Localize.Key("Settings.Tab", tab.Name)
                });
            }

            _headerTabs.Tabs = tabs;
            _tabsInitialized = true;

            if (!string.IsNullOrEmpty(_pendingTabName))
            {
                var pending = _pendingTabName;
                _pendingTabName = null;
                SelectTabByName(pending);
            }
        }

        private void OnEnable()
        {
            if (!_ready)
            {
                return;
            }

            _showAdvanced = ShowAdvanced;

            _headerTabs.RefreshTabs();
            _headerTabs.TabChanged += OnTabChanged;

            _settingsNavGroup.SelectionChanged += OnSelectionChanged;

            // Set navigation scheme
            PushNavigationScheme();

            if (CurrentTab == null)
            {
                var tabId = !string.IsNullOrEmpty(_pendingTabName)
                    ? _pendingTabName
                    : _headerTabs.SelectedTabId;

                if (!string.IsNullOrEmpty(tabId))
                {
                    SelectTabByName(tabId);
                }
                else
                {
                    CurrentTab = SettingsManager.DisplayedSettingsTabs[0];
                    _searchBarContainer.SetActive(false);
                    Refresh();
                }
            }
        }

        private void OnTabChanged(string tab)
        {
            SelectTab(SettingsManager.GetTabByName(tab));
        }

        private void SelectTab(Tab tab)
        {
            CurrentTab?.OnTabExit();

            CurrentTab = tab;
            Refresh();

            CurrentTab?.OnTabEnter();

            _searchBarContainer.SetActive(CurrentTab?.ShowSearchBar ?? false);
            _searchBar.text = string.Empty;
            OnSearchBarChanged();
        }

        public void SelectTabByName(string name)
        {
            if (!_tabsInitialized)
            {
                _pendingTabName = name;
                return;
            }

            _headerTabs.SelectTabById(name);

            // If the header tab does not exist, then force update to that tab
            if (_headerTabs.SelectedTabId is null)
            {
                SelectTab(SettingsManager.GetTabByName(name));
                return;
            }

            // Selecting the already-selected header tab does not fire TabChanged.
            // This matters when reopening settings after CurrentTab was cleared on close.
            if (CurrentTab?.Name != _headerTabs.SelectedTabId)
            {
                SelectTab(SettingsManager.GetTabByName(_headerTabs.SelectedTabId));
            }
        }

        public void SelectSettingByIndex(int index)
        {
            // Force it to be the navigation selection type so the scroll view properly updates
            _settingsNavGroup.SelectAt(index, SelectionOrigin.Navigation);
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null || CurrentTab == null)
            {
                _settingName.text = string.Empty;
                _settingDescription.text = string.Empty;
                return;
            }

            // Most setting rows carry a BaseSettingNavigatable, but some (e.g. the
            // preset color rows, whose confirm opens a color picker instead of the
            // stock edit scheme) swap in a RuntimeNavigatable. Either way the
            // BaseSettingVisual lives on the same GameObject, so fall back to it.
            var settingNav = selected.GetComponent<BaseSettingNavigatable>();
            var settingVisual = settingNav != null
                ? settingNav.BaseSettingVisual
                : selected.GetComponent<BaseSettingVisual>();

            // If we're not selecting a setting (for example, buttons or headers) skip
            if (settingVisual == null)
            {
                _settingName.text = string.Empty;
                _settingDescription.text = string.Empty;
                return;
            }

            // Set the setting name and description
            var unlocalized = settingVisual.UnlocalizedName;
            string baseKey = !settingVisual.IsPresetSetting
                ? "Settings.Setting"
                : "Settings.PresetSetting";

            // Some preset names carry manual line breaks for the narrow editor
            // column labels; the sidebar header is one auto-sized line.
            _settingName.text = Localize.Key(baseKey, unlocalized, "Name").Replace('\n', ' ');
            _settingDescription.text = settingVisual.HasDescription
                ? Localize.Key(baseKey, unlocalized, "Description")
                : string.Empty;

            // Let the tab react to the selection (e.g. the preset color editor
            // spotlights the lane whose color field was just selected).
            CurrentTab?.OnSettingSelected(unlocalized);
        }

        public void RefreshPreview(bool waitForResolution = false)
        {
            // Prevent errors if this gets called when the settings aren't opened
            if (!_ready || !gameObject.activeSelf) return;

            UpdatePreview(CurrentTab, waitForResolution).Forget();
        }

        public void Refresh()
        {
            UpdateSettings(true);
            RefreshPreview();
        }

        public void RefreshAndKeepPosition()
        {
            // Everything gets recreated, so we must cache the index before hand
            int? beforeIndex = _settingsNavGroup.SelectedIndex;

            UpdateSettings(false);
            RefreshPreview();

            // Restore selection
            _settingsNavGroup.SelectAt(beforeIndex);
        }

        /// <summary>
        /// Rebuilds only the settings list (not the preview). Use for UI-only
        /// changes like collapsing/expanding group headers where the 3D preview
        /// doesn't need to restart.
        /// </summary>
        public void RefreshSettingsKeepPosition()
        {
            int? beforeIndex = _settingsNavGroup.SelectedIndex;

            UpdateSettings(false);

            _settingsNavGroup.SelectAt(beforeIndex);
        }

        private void UpdateSettings(bool resetScroll)
        {
            _showAdvanced = ShowAdvanced;

            _settingsNavGroup.ClearNavigatables();

            // Destroy all previous settings
            _settingsContainer.DestroyChildren();

            // Build the settings tab
            CurrentTab?.BuildSettingTab(_settingsContainer, _settingsNavGroup);

            if (resetScroll)
            {
                // Make the settings nav group the main one
                _settingsNavGroup.SelectFirst();

                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void SmoothScrollToTop()
        {
            _scrollRect.DOKill();
            _scrollRect
                .DOVerticalNormalizedPos(1f, 0.4f)
                .SetEase(Ease.OutCubic);
        }

        private async UniTask UpdatePreview(Tab tabInfo, bool waitForResolution)
        {
            // When Unity changes resolution, it takes two frames to apply it correctly.
            if (waitForResolution)
            {
                await UniTask.WaitForEndOfFrame(this);
                await UniTask.WaitForEndOfFrame(this);
            }

            DestroyPreview();

            if (CurrentTab == null)
                return;

            // Spawn world preview
            _previewContainerWorld.gameObject.SetActive(true);
            await tabInfo.BuildPreviewWorld(_previewContainerWorld);

            // Set render texture(s)
            CameraPreviewTexture.SetAllPreviews();

            // Spawn UI preview
            await tabInfo.BuildPreviewUI(_previewContainerUI);
        }

        private void DestroyPreview()
        {
            _previewContainerWorld.DestroyChildren();
            _previewContainerWorld.gameObject.SetActive(false);

            _previewContainerUI.DestroyChildren();
        }

        public void OnSettingChanged()
        {
            if (!_ready || !gameObject.activeSelf) return;

            CurrentTab?.OnSettingChanged();
            SettingChanged?.Invoke();
        }

        public void OnSearchBarChanged()
        {
            // Update header
            if (string.IsNullOrEmpty(_searchBar.text))
            {
                _searchHeaderText.text = Localize.Key("Menu.Settings.SearchHeader.AllCategories");
            }
            else
            {
                _searchHeaderText.text = Localize.Key("Menu.Settings.SearchHeader.Results");
            }

            // Refresh on search
            if (CurrentTab?.ShowSearchBar ?? false)
            {
                Refresh();
            }
        }

        private void PushNavigationScheme()
        {
            string advancedKey = ShowAdvanced
                ? "Menu.Settings.HideAdvanced"
                : "Menu.Settings.ShowAdvanced";

            _ = Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", () =>
                {
                    gameObject.SetActive(false);
                }, hide: true),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                _headerTabs.NavigateNextTab,
                _headerTabs.NavigatePreviousTab,
                new NavigationScheme.Entry(MenuAction.Blue, advancedKey, ToggleAdvanced)
            }, true));
        }

        public void EnableAdvanced(bool isEnabled)
        {
            if (isEnabled == ShowAdvanced)
            {
                return;
            }

            ShowAdvanced = isEnabled;
        }

        public void RefreshNavigationScheme()
        {
            Navigator.Instance.PopScheme();
            PushNavigationScheme();
        }

        private void ToggleAdvanced()
        {
            EnableAdvanced(!ShowAdvanced);
            RefreshNavigationScheme();
            RefreshAndKeepPosition();
            SmoothScrollToTop();
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            // Set the current tab back to null to avoid calling OnTabExit twice
            CurrentTab?.OnTabExit();
            CurrentTab = null;

            Navigator.Instance.PopScheme();
            DestroyPreview();
            _headerTabs.TabChanged -= OnTabChanged;

            _settingsNavGroup.SelectionChanged -= OnSelectionChanged;

            // Save on close
            SettingsManager.SaveSettings();
            CustomContentManager.SaveAll();

            if (_skipMenuReactivationOnDisable)
            {
                _skipMenuReactivationOnDisable = false;
                return;
            }

            // The settings menu overlays the current menu, so avoid toggling an already-active menu.
            MenuManager.Instance.ReactivateCurrentMenu(false);
        }

        protected override void SingletonDestroy()
        {
            SceneManager.sceneLoaded -= HideAfterSceneTransition;
        }
    }
}
