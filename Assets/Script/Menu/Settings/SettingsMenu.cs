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
        private HeaderTabs _headerTabs;
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
        private int _previewVersion;


        public string CurrentSection { get; private set; } = string.Empty;
        private bool IsSearching => SearchQuery.Length > 0;

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
            var categories = new List<CategorySidebar.CategoryInfo>();

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

                categories.Add(new CategorySidebar.CategoryInfo
                {
                    Icon = sprite,
                    Id = tab.Name,
                    DisplayName = Localize.Key("Settings.Tab", tab.Name)
                });
            }

            if (_headerTabs != null)
            {
                _headerTabs.Tabs = tabs;
            }

            if (_categorySidebar != null)
            {
                _categorySidebar.SetCategories(categories);
            }

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

            if (_headerTabs != null)
            {
                _headerTabs.RefreshTabs();
                _headerTabs.TabChanged += OnTabChanged;
            }

            if (_categorySidebar != null)
            {
                _categorySidebar.CategoryChanged += OnTabChanged;
                _categorySidebar.SetCollapsed(true, animate: false);
            }

            _settingsNavGroup.SelectionChanged += OnSelectionChanged;
            _sectionsNavGroup.SelectionChanged += OnSectionChanged;

            // Set navigation scheme
            PushNavigationScheme();

            if (CurrentTab == null)
            {
                var tabId = !string.IsNullOrEmpty(_pendingTabName)
                    ? _pendingTabName
                    : _categorySidebar != null
                        ? _categorySidebar.SelectedCategoryId
                        : _headerTabs != null
                            ? _headerTabs.SelectedTabId
                            : null;

                if (!string.IsNullOrEmpty(tabId))
                {
                    SelectTabByName(tabId);
                }
                else
                {
                    SelectTab(SettingsManager.DisplayedSettingsTabs[0]);
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
            _searchBar.SetTextWithoutNotify(string.Empty);
            CurrentTab = tab;
            BuildSections();
            Refresh();
            FocusSections();
            CurrentTab.OnTabEnter();
        }

        private void BuildSections()
        {
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

        private void OnSectionChanged(NavigatableBehaviour selected, SelectionOrigin origin)
        {
            if (selected is SettingsSectionView view)
            {
                if (origin == SelectionOrigin.Mouse)
                {
                    _settingsNavGroup.SelectLastNavGroup();
                }

                if (view.Section == CurrentSection)
                {
                    return;
                }

                CurrentSection = view.Section;
                _searchBar.SetTextWithoutNotify(string.Empty);
                Refresh();
            }
        }

        public void EnterSettings()
        {
            _sectionsNavGroup.SelectLastNavGroup();
            _settingsNavGroup.PushNavGroupToStack();
            _settingsNavGroup.SelectFirst(SelectionOrigin.Navigation);
        }

        private void FocusSections()
        {
            if (_sectionsPanel.activeSelf)
            {
                _settingsNavGroup.SelectLastNavGroup();
                _sectionsNavGroup.PushNavGroupToStack();
                var index = CurrentTab.Sections.ToList().FindIndex(section => section.HeaderName == CurrentSection);
                _sectionsNavGroup.SelectAt(index: index, selectionOrigin: SelectionOrigin.Navigation);
            }
            else
            {
                EnterSettings();
            }
        }

        private void Back()
        {
            if (IsSearching)
            {
                _searchBar.SetTextWithoutNotify(string.Empty);
                Refresh();
                FocusSections();
            }
            else if (_sectionsPanel.activeSelf && NavigationGroup.CurrentNavigationGroup != _sectionsNavGroup)
            {
                FocusSections();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void OnCategoryConfirmed(string categoryId)
        {
            if (CurrentTab?.Name != categoryId)
            {
                SelectTabByName(categoryId);
            }

            if (_categorySidebar != null)
            {
                _categorySidebar.SetCollapsed(true);
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

            if (_headerTabs != null)
            {
                _headerTabs.SelectTabById(name);
            }

            if (_categorySidebar != null)
            {
                _categorySidebar.SelectCategory(name);
            }

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
            var section = string.Empty;
            var index = 0;
            foreach (var metadata in tab.Settings)
            {
                // Since the header can't be selected, we gotta skip that
                // for the navigation index.
                if (metadata is HeaderMetadata header)
                {
                    section = header.HeaderName;
                    index = 0;
                    continue;
                }

                if (!metadata.IsVisible)
                {
                    continue;
                }

                if (metadata.UnlocalizedSearchNames?.Contains(searchName) == true)
                {
                    var sectionIndex = tab.Sections.ToList().FindIndex(header => header.HeaderName == section);
                    if (section.Length > 0)
                    {
                        _sectionsNavGroup.SelectAt(sectionIndex);
                    }
                    EnterSettings();
                    // Force it to be the navigation selection type so the scroll view properly updates
                    _settingsNavGroup.SelectAt(index: index, selectionOrigin: SelectionOrigin.Navigation);
                    return;
                }

                if (metadata is FieldMetadata || metadata is ButtonRowMetadata)
                {
                    index++;
                }
            }
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null)
            {
                return;
            }

            if (selectionOrigin == SelectionOrigin.Mouse)
            {
                _sectionsNavGroup.SelectLastNavGroup();
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

            foreach (var view in _sectionsContainer.GetComponentsInChildren<SettingsSectionView>())
            {
                view.ShowCurrent(view.Section == CurrentSection);
            }

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

            if (tab is MetadataTab)
            {
                foreach (var visual in visuals)
                {
                    var row = (RectTransform) visual.transform;
                    row.sizeDelta = new Vector2(x: row.sizeDelta.x, y: 84f);
                    var width = visual is DMXChannelsSettingVisual
                        ? row.rect.width * 0.7f
                        : Mathf.Min(a: 440f, b: row.rect.width * 0.45f);
                    var controls = (RectTransform) row.Find("Container");
                    controls.anchorMin = new Vector2(x: 1f, y: 0f);
                    controls.anchorMax = Vector2.one;
                    controls.pivot = new Vector2(x: 1f, y: 0.5f);
                    controls.anchoredPosition = new Vector2(x: -25f, y: 0f);
                    controls.sizeDelta = new Vector2(x: width, y: 0f);
                    var label = visual.SettingLabel;
                    label.rectTransform.anchorMin = Vector2.zero;
                    label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(x: 25f, y: 8f);
                    label.rectTransform.offsetMax = new Vector2(x: -width - 50f, y: -8f);
                    label.enableAutoSizing = true;
                    label.fontSizeMax = 24f;
                    label.fontSizeMin = 18f;
                    foreach (var slider in visual.GetComponentsInChildren<ValueSlider>())
                    {
                        var track = (RectTransform) slider.GetComponentInChildren<Slider>().transform;
                        track.sizeDelta = new Vector2(x: -165f, y: track.sizeDelta.y);
                        track.anchoredPosition = new Vector2(x: -82.5f, y: 0f);
                        var value = slider.GetComponentInChildren<TMP_InputField>();
                        var entry = (RectTransform) value.transform.parent;
                        entry.anchorMin = new Vector2(x: 1f, y: 0f);
                        entry.anchorMax = Vector2.one;
                        entry.pivot = new Vector2(x: 1f, y: 0.5f);
                        entry.anchoredPosition = Vector2.zero;
                        entry.sizeDelta = new Vector2(x: 140f, y: 0f);
                        value.textComponent.enableAutoSizing = true;
                        value.textComponent.fontSizeMax = 28f;
                        value.textComponent.fontSizeMin = 18f;
                    }
                    if (visual is DMXChannelsSettingVisual)
                    {
                        foreach (var input in visual.GetComponentsInChildren<TMP_InputField>())
                        {
                            input.textViewport.offsetMin = new Vector2(x: 8f, y: 7f);
                            input.textViewport.offsetMax = new Vector2(x: -8f, y: -7f);
                            input.textComponent.enableAutoSizing = true;
                            input.textComponent.fontSizeMax = 24f;
                            input.textComponent.fontSizeMin = 18f;
                        }
                    }
                    foreach (var dropdown in visual.GetComponentsInChildren<TMP_Dropdown>())
                    {
                        var caption = dropdown.captionText;
                        caption.enableAutoSizing = true;
                        caption.fontSizeMax = 28f;
                        caption.fontSizeMin = 18f;
                        caption.overflowMode = TextOverflowModes.Ellipsis;
                    }
                }
                Canvas.ForceUpdateCanvases();
            }

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
            _searchHeaderText.transform.parent.gameObject.SetActive(IsSearching || CurrentTab is AllSettingsTab);
            if (IsSearching)
            {
                _settingsNavGroup.PushNavGroupToStack();
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

            if (_headerTabs != null)
            {
                entries.Add(_headerTabs.NavigateNextTab);
                entries.Add(_headerTabs.NavigatePreviousTab);
            }

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

            if (_headerTabs != null)
            {
                _headerTabs.TabChanged -= OnTabChanged;
            }

            if (_categorySidebar != null)
            {
                _categorySidebar.CategoryChanged -= OnTabChanged;
            }

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

        protected override void SingletonDestroy()
        {
            SceneManager.sceneLoaded -= HideAfterSceneTransition;
        }
    }
}
