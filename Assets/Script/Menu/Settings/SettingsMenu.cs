using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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
        private CategorySidebar _categorySidebar;
        [SerializeField]
        private Transform _settingsContainer;
        [SerializeField]
        private NavigationGroup _settingsNavGroup;
        [SerializeField]
        private ScrollRect _scrollRect;

        [Space]
        [SerializeField]
        private Transform _sectionsContainer;
        [SerializeField]
        private NavigationGroup _sectionsNavGroup;
        [SerializeField]
        private SettingsSectionView _sectionPrefab;
        [SerializeField]
        private GameObject _sectionsPanel;
        [SerializeField]
        private GameObject _previewPanel;

        [Space]
        [SerializeField]
        private TMP_InputField _searchBar;

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
        private int _previewVersion;
        private readonly List<SettingsSectionView> _sectionViews = new();

        public string CurrentSection { get; private set; } = string.Empty;
        private bool IsSearching => SearchQuery.Length > 0;

        public bool IsNavigationLevelActive(NavigationGroup group)
        {
            var current = NavigationGroup.CurrentNavigationGroup;
            if (current == _settingsNavGroup || current?.transform.IsChildOf(_settingsContainer) == true)
            {
                return true;
            }

            if (current == _sectionsNavGroup)
            {
                return group == _sectionsNavGroup || group == _categorySidebar.NavigationGroup;
            }

            return current == _categorySidebar.NavigationGroup && group == current;
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
            EnsureTabsInitialized();

            if (!string.IsNullOrEmpty(_pendingTabName))
            {
                var pending = _pendingTabName;
                _pendingTabName = null;
                SelectTabByName(pending);
            }
        }

        private void EnsureTabsInitialized()
        {
            if (_tabsInitialized)
            {
                return;
            }

            var categories = new List<CategorySidebar.CategoryInfo>();

            // Add the main tabs
            foreach (var tab in SettingsManager.AllSettingsTabs.Where(tab => tab is not AllSettingsTab))
            {
                // Load the tab sprite
                var sprite = Addressables.LoadAssetAsync<Sprite>($"TabIcons[{tab.Icon}]").WaitForCompletion();

                categories.Add(new CategorySidebar.CategoryInfo
                {
                    Icon = sprite,
                    Id = tab.Name,
                    DisplayName = Localize.Key("Settings.Tab", tab.Name)
                });
            }

            _categorySidebar.SetCategories(categories);

            _tabsInitialized = true;
        }

        private void OnEnable()
        {
            if (!_ready)
            {
                return;
            }

            EnsureTabsInitialized();

            _categorySidebar.CategoryChanged += OnTabChanged;
            _categorySidebar.SetCollapsed(collapsed: false, animate: false);

            _settingsNavGroup.SelectionChanged += OnSelectionChanged;
            _sectionsNavGroup.SelectionChanged += OnSectionChanged;

            // Set navigation scheme
            PushNavigationScheme();

            if (CurrentTab == null)
            {
                var tabId = !string.IsNullOrEmpty(_pendingTabName)
                    ? _pendingTabName
                    : _categorySidebar.SelectedCategoryId;

                _pendingTabName = null;

                if (!string.IsNullOrEmpty(tabId))
                {
                    SelectTabByName(tabId);
                }
                else
                {
                    SelectTab(SettingsManager.DisplayedSettingsTabs[0]);
                }
            }

            FocusCategories(animate: false);
        }

        private void OnTabChanged(string tab)
        {
            SelectTab(SettingsManager.GetTabByName(tab));
        }

        private void SelectTab(Tab tab)
        {
            if (tab == null || CurrentTab == tab)
            {
                return;
            }

            CurrentTab?.OnTabExit();
            _searchBar.SetTextWithoutNotify(string.Empty);
            CurrentTab = tab;
            BuildSections();
            Refresh();
            CurrentTab.OnTabEnter();
        }

        private void BuildSections()
        {
            _sectionViews.Clear();
            _sectionsNavGroup.ClearNavigatables();
            foreach (Transform child in _sectionsContainer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            var sections = CurrentTab.Sections;
            _sectionsPanel.SetActive(sections.Count > 0);
            CurrentSection = string.Empty;
            foreach (var section in sections)
            {
                var view = Instantiate(_sectionPrefab, _sectionsContainer);
                view.Initialize(section.HeaderName);
                _sectionsNavGroup.AddNavigatable(view);
                _sectionViews.Add(view);
            }

            Canvas.ForceUpdateCanvases();
            var scroll = _sectionsNavGroup.GetComponent<ScrollRect>();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;

            if (sections.Count > 0)
            {
                CurrentSection = sections[0].HeaderName;
                _sectionsNavGroup.SelectFirst();
            }
        }

        private void UpdateSectionMarkers()
        {
            for (var i = 0; i < _sectionViews.Count; i++)
            {
                var isCurrent = _sectionViews[i].Section == CurrentSection;
                _sectionViews[i].GetComponent<SettingsItemVisual>().ShowCurrent(isCurrent);
            }
        }

        private void OnSectionChanged(NavigatableBehaviour selected, SelectionOrigin origin)
        {
            if (selected is SettingsSectionView view)
            {
                if (origin == SelectionOrigin.Mouse)
                {
                    FocusNavigation(_sectionsNavGroup);
                }

                if (view.Section == CurrentSection)
                {
                    return;
                }

                CurrentSection = view.Section;
                _searchBar.SetTextWithoutNotify(string.Empty);
                Refresh();
                UpdateSectionMarkers();
            }
        }

        private void FocusNavigation(NavigationGroup target, bool animate = true)
        {
            _categorySidebar.SetCollapsed(collapsed: target != _categorySidebar.NavigationGroup, animate: animate);

            var current = NavigationGroup.CurrentNavigationGroup;
            while (current != target && current != null && current.transform.IsChildOf(transform) &&
                !target.transform.IsChildOf(current.transform))
            {
                current.SelectLastNavGroup();
                current = NavigationGroup.CurrentNavigationGroup;
            }

            target.PushNavGroupToStack();
            UpdateSectionMarkers();
        }

        public void EnterSettings()
        {
            FocusNavigation(_settingsNavGroup);
            _settingsNavGroup.SelectFirst(SelectionOrigin.Navigation);
        }

        public void FocusSections()
        {
            if (IsSearching || _sectionViews.Count == 0)
            {
                EnterSettings();
                return;
            }

            FocusNavigation(_sectionsNavGroup);
            var index = _sectionViews.FindIndex(view => view.Section == CurrentSection);
            _sectionsNavGroup.SelectAt(index: index, selectionOrigin: SelectionOrigin.Navigation);
        }

        public void FocusCategories(bool animate = true)
        {
            FocusNavigation(target: _categorySidebar.NavigationGroup, animate: animate);
            _categorySidebar.SelectCategory(CurrentTab.Name);
        }

        private void Back()
        {
            if (IsSearching)
            {
                _searchBar.SetTextWithoutNotify(string.Empty);
                Refresh();
                FocusSections();
            }
            else if (NavigationGroup.CurrentNavigationGroup == _settingsNavGroup)
            {
                if (_sectionsPanel.activeSelf)
                {
                    FocusSections();
                }
                else
                {
                    FocusCategories();
                }
            }
            else if (NavigationGroup.CurrentNavigationGroup == _sectionsNavGroup)
            {
                FocusCategories();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void OnCategoryClicked(string categoryId)
        {
            if (CurrentTab?.Name != categoryId)
            {
                SelectTabByName(categoryId);
            }

            FocusCategories();
        }

        public void OnSettingClicked(NavigatableBehaviour selected)
        {
            FocusNavigation(selected.NavigationGroup);
            selected.SetSelected(true, SelectionOrigin.Mouse);
        }

        public void OnSectionClicked(string section)
        {
            if (CurrentSection != section)
            {
                CurrentSection = section;
                _searchBar.SetTextWithoutNotify(string.Empty);
                Refresh();
            }

            FocusSections();
        }

        public void OnCategoryConfirmed(string categoryId)
        {
            if (CurrentTab?.Name != categoryId)
            {
                SelectTabByName(categoryId);
            }

            FocusSections();
        }

        public void SelectTabByName(string name)
        {
            if (!_tabsInitialized)
            {
                _pendingTabName = name;
                return;
            }

            _categorySidebar.SelectCategory(name);

            if (CurrentTab?.Name != name)
            {
                SelectTab(SettingsManager.GetTabByName(name));
            }
        }

        public void SelectSetting(string tabName, string searchName)
        {
            SelectTabByName(tabName);
            if (IsSearching)
            {
                _searchBar.SetTextWithoutNotify(string.Empty);
                Refresh();
            }
            var tab = (MetadataTab) CurrentTab;
            // Since the header can't be selected, we gotta skip that
            // for the navigation index.
            var position = tab.GetSettingPosition(searchName);
            if (position.Section.Length > 0)
            {
                OnSectionClicked(position.Section);
            }
            EnterSettings();
            // Force it to be the navigation selection type so the scroll view properly updates
            _settingsNavGroup.SelectAt(index: position.Index, selectionOrigin: SelectionOrigin.Navigation);
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null)
            {
                return;
            }

            if (selectionOrigin == SelectionOrigin.Mouse)
            {
                OnSettingClicked(selected);
            }

            var settingVisual = selected.GetComponent<BaseSettingVisual>();

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
            if (!_ready || !gameObject.activeSelf)
            {
                return;
            }

            UpdatePreview(tabInfo: CurrentTab, waitForResolution: waitForResolution,
                version: ++_previewVersion).Forget();
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            UpdateLayout();
            UpdateSettings(true);
            RefreshPreview();
        }

        public void RefreshAndKeepPosition()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            // Everything gets recreated, so we must cache the index before hand
            var beforeIndex = _settingsNavGroup.SelectedIndex;

            UpdateLayout();
            UpdateSettings(false);
            RefreshPreview();

            // Restore selection
            if (_settingsNavGroup.Count > 0)
            {
                _settingsNavGroup.SelectAt(Math.Min(beforeIndex ?? 0, _settingsNavGroup.Count - 1));
            }
        }

        /// <summary>
        /// Rebuilds only the settings list (not the preview). Use for UI-only
        /// changes like collapsing/expanding group headers where the 3D preview
        /// doesn't need to restart.
        /// </summary>
        public void RefreshSettingsKeepPosition()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            var beforeIndex = _settingsNavGroup.SelectedIndex;

            UpdateSettings(false);

            if (_settingsNavGroup.Count > 0)
            {
                _settingsNavGroup.SelectAt(Math.Min(beforeIndex ?? 0, _settingsNavGroup.Count - 1));
            }
        }

        private void UpdateSettings(bool resetScroll)
        {
            _settingName.text = IsSearching
                ? Localize.Key("Menu.Settings.SearchHeader.Results")
                : CurrentSection.Length > 0
                    ? Localize.Key("Settings.Header", CurrentSection)
                    : Localize.Key("Settings.Tab", CurrentTab.Name);
            _settingDescription.text = string.Empty;

            UpdateSectionMarkers();

            _settingsNavGroup.ClearNavigatables();

            // Destroy all previous settings
            foreach (Transform child in _settingsContainer)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            // Build the settings tab
            var tab = IsSearching ? SettingsManager.GetTabByName("AllSettings") : CurrentTab;
            tab?.BuildSettingTab(_settingsContainer, _settingsNavGroup);
            var visuals = _settingsContainer.GetComponentsInChildren<BaseSettingVisual>();
            _settingName.transform.parent.gameObject.SetActive(visuals.Length > 0);
            Canvas.ForceUpdateCanvases();

            if (resetScroll)
            {
                _scrollRect.StopMovement();
                _scrollRect.verticalNormalizedPosition = 1f;
                _settingsNavGroup.SelectFirst();
            }
        }

        private void UpdateLayout()
        {
            var hasPreview = !IsSearching && CurrentTab != null && CurrentTab.HasPreview;
            _previewPanel.SetActive(true);
            _previewPanel.GetComponent<LayoutElement>().preferredWidth = CurrentTab is PresetsTab
                ? 800f
                : hasPreview
                    ? 640f
                    : 440f;
            _previewContainerUI.gameObject.SetActive(hasPreview);

            var header = (RectTransform) _previewPanel.transform.Find("Header");
            if (header != null)
            {
                if (hasPreview)
                {
                    header.anchorMin = new Vector2(0f, 1f);
                    header.anchorMax = Vector2.one;
                    header.offsetMin = new Vector2(0f, -125f);
                    header.offsetMax = Vector2.zero;
                }
                else
                {
                    header.anchorMin = Vector2.zero;
                    header.anchorMax = Vector2.one;
                    header.offsetMin = Vector2.zero;
                    header.offsetMax = Vector2.zero;
                }
            }

            _sectionsPanel.SetActive(!IsSearching && CurrentTab?.Sections.Count > 0);
            if (IsSearching)
            {
                FocusNavigation(_settingsNavGroup);
            }
        }

        private async UniTask UpdatePreview(Tab tabInfo, bool waitForResolution, int version)
        {
            // When Unity changes resolution, it takes two frames to apply it correctly.
            if (waitForResolution)
            {
                await UniTask.WaitForEndOfFrame(this);
                await UniTask.WaitForEndOfFrame(this);
            }

            if (version != _previewVersion)
            {
                return;
            }

            DestroyPreview();

            if (IsSearching || tabInfo == null || !tabInfo.HasPreview)
            {
                return;
            }

            // Spawn world preview
            _previewContainerWorld.gameObject.SetActive(true);
            await tabInfo.BuildPreviewWorld(_previewContainerWorld);
            if (version != _previewVersion)
            {
                return;
            }

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
            if (!_ready || !gameObject.activeSelf)
            {
                return;
            }

            CurrentTab?.OnSettingChanged();
            SettingChanged?.Invoke();
        }

        public void OnSearchBarChanged()
        {
            // Refresh on search
            if (CurrentTab != null)
            {
                Refresh();
            }
        }

        private void PushNavigationScheme()
        {
            var entries = new List<NavigationScheme.Entry>
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            };

            _ = Navigator.Instance.PushScheme(new NavigationScheme(entries, true));
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            _previewVersion++;
            CurrentTab?.OnTabExit();
            CurrentTab = null;

            Navigator.Instance.PopScheme();
            DestroyPreview();

            _categorySidebar.CategoryChanged -= OnTabChanged;

            _settingsNavGroup.SelectionChanged -= OnSelectionChanged;
            _sectionsNavGroup.SelectionChanged -= OnSectionChanged;

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

        private void OnApplicationQuit()
        {
            gameObject.SetActive(false);
        }

        protected override void SingletonDestroy()
        {
            SceneManager.sceneLoaded -= HideAfterSceneTransition;
        }
    }
}
