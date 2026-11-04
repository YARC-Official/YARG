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
        private const string PRESET_TYPE_NAME = "Preset Type";
        private const string PRESET_TYPE_DESC = "Select which category of presets to configure.";
        private const string PRESET_NAME = "Preset";
        private const string PRESET_DESC = "Select a preset to load, or choose a custom preset to edit.";

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
        [SerializeField]
        private RectTransform _previewHeader;

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
        private CareerHeader _careerActionRow;

        public string CurrentSection { get; private set; } = string.Empty;
        private bool IsSearching => SearchQuery.Length > 0;

        public bool IsNavigationLevelActive(NavigationGroup group)
        {
            var current = NavigationGroup.CurrentNavigationGroup;
            if (current?.transform.IsChildOf(_previewContainerUI) == true)
            {
                // The career editor is one level deeper than the main settings
                // list. Keep every selected ancestor level highlighted, just as
                // category -> section -> setting navigation does elsewhere.
                return group == current ||
                    group == _settingsNavGroup ||
                    group == _sectionsNavGroup ||
                    group == _categorySidebar.NavigationGroup;
            }
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

                categories.Add(new CategorySidebar.CategoryInfo(id: tab.Name,
                    displayName: Localize.Key("Settings.Tab", tab.Name), icon: sprite));
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
            _categorySidebar.CategoryClicked += OnCategoryClicked;
            _categorySidebar.CategoryConfirmed += OnCategoryConfirmed;
            _categorySidebar.SidebarClicked += OnSidebarClicked;
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

        private void SelectTab(Tab tab, string initialSection = null)
        {
            if (tab == null)
            {
                return;
            }

            if (CurrentTab == tab)
            {
                if (!string.IsNullOrEmpty(initialSection) && CurrentSection != initialSection)
                {
                    OnSectionClicked(initialSection);
                }
                return;
            }

            CurrentTab?.OnTabExit();
            _searchBar.SetTextWithoutNotify(string.Empty);
            CurrentTab = tab;
            BuildSections(initialSection);
            Refresh();
            CurrentTab.OnTabEnter();
        }

        private void BuildSections(string initialSection = null)
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
                view.Initialize(section.HeaderName,
                    onClicked: OnSectionClicked,
                    onConfirmed: EnterSettings);
                _sectionsNavGroup.AddNavigatable(view);
                _sectionViews.Add(view);
            }

            Canvas.ForceUpdateCanvases();
            var scroll = _sectionsNavGroup.GetComponent<ScrollRect>();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;

            if (sections.Count > 0)
            {
                var targetSection = !string.IsNullOrEmpty(initialSection) && sections.Any(s => s.HeaderName == initialSection)
                    ? initialSection
                    : sections[0].HeaderName;
                CurrentSection = targetSection;
                var targetIndex = _sectionViews.FindIndex(view => view.Section == targetSection);
                _sectionsNavGroup.SelectAt(Math.Max(0, targetIndex));
            }
        }

        private void UpdateSectionMarkers()
        {
            foreach (var view in _sectionViews)
            {
                view.SetCurrent(view.Section == CurrentSection);
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
            if (target == null)
            {
                return;
            }

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
            var selected = _settingsNavGroup.SelectedBehaviour;
            _settingsNavGroup.SelectFirst(SelectionOrigin.Navigation);
            if (selected == _settingsNavGroup.SelectedBehaviour)
            {
                OnSelectionChanged(_settingsNavGroup.SelectedBehaviour, SelectionOrigin.Programmatically);
            }
        }

        public void FocusSections()
        {
            if (IsSearching || _sectionViews.Count == 0)
            {
                EnterSettings();
                return;
            }

            ClearSettingDetails();
            FocusNavigation(_sectionsNavGroup);
            var index = _sectionViews.FindIndex(view => view.Section == CurrentSection);
            _sectionsNavGroup.SelectAt(index: index, selectionOrigin: SelectionOrigin.Navigation);
        }

        public void FocusCategories(bool animate = true)
        {
            ClearSettingDetails();
            FocusNavigation(target: _categorySidebar.NavigationGroup, animate: animate);
            _categorySidebar.SelectCategory(CurrentTab.Name);
        }

        private void ClearSettingDetails()
        {
            _settingName.text = string.Empty;
            _settingDescription.text = string.Empty;
        }

        public void ShowSettingDetails(string name, string description = "")
        {
            _settingName.text = name;
            _settingDescription.text = description;
        }

        public void SetPreviewHeaderVisible(bool visible)
        {
            _previewHeader.gameObject.SetActive(visible);

            var previewRect = (RectTransform) _previewContainerUI;
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.zero;
            float headerHeight = visible
                ? Mathf.Max(LayoutUtility.GetPreferredHeight(_previewHeader), _previewHeader.rect.height)
                : 0f;
            previewRect.offsetMax = new Vector2(0f, -headerHeight);
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
            else if (NavigationGroup.CurrentNavigationGroup?.transform.IsChildOf(_previewContainerUI) == true)
            {
                FocusNavigation(_settingsNavGroup);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnSidebarClicked() => FocusCategories();

        private void OnCategoryClicked(string categoryId)
        {
            if (CurrentTab?.Name != categoryId)
            {
                SelectTabByName(categoryId);
            }

            FocusCategories();
        }

        public void OnSettingClicked(NavigatableBehaviour selected)
        {
            // Settings visuals are also reused by other menus (for example, the
            // Filters menu). Ignore clicks from those foreign navigation trees.
            if (selected == null || selected.NavigationGroup == null ||
                !selected.transform.IsChildOf(transform))
            {
                return;
            }

            // The career editor is a child/detail navigation level of the selected tier or
            // song. Keep that parent row selected when mouse focus enters an editor field.
            // FocusNavigation() normally pops and clears sibling navigation groups, which
            // erased the parent selection and caused the generic preview header to return.
            if (selected.transform.IsChildOf(_previewContainerUI) && _careerActionRow != null)
            {
                selected.NavigationGroup.PushNavGroupToStack();
                selected.SetSelected(true, SelectionOrigin.Mouse);
                SetPreviewHeaderVisible(false);
                return;
            }

            FocusNavigation(selected.NavigationGroup);
            selected.SetSelected(true, SelectionOrigin.Mouse);
        }

        private void OnSectionClicked(string section)
        {
            if (CurrentSection != section)
            {
                CurrentSection = section;
                _searchBar.SetTextWithoutNotify(string.Empty);
                Refresh();
            }

            FocusSections();
        }

        private void OnCategoryConfirmed(string categoryId)
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
            var tab = (MetadataTab) SettingsManager.GetTabByName(tabName);
            var position = tab.GetSettingPosition(searchName);

            _categorySidebar.SelectCategory(tabName);
            SelectTab(tab, position.Section);

            EnterSettings();
            _settingsNavGroup.SelectAt(index: position.Index, selectionOrigin: SelectionOrigin.Navigation);
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null)
            {
                SetPreviewHeaderVisible(true);
                if (!IsSearching)
                {
                    ClearSettingDetails();
                }
                return;
            }

            // A recycled setting row may belong to another menu. SettingsMenu
            // must not handle its selection or prevent that menu's listener from
            // receiving the event.
            if (!selected.transform.IsChildOf(transform))
            {
                return;
            }

            // Career tier/song controls live in the preview container, but are
            // presented as a headerless editor. Selecting or editing one must
            // not restore the normal setting-details header.
            if (selected.transform.IsChildOf(_previewContainerUI))
            {
                SetPreviewHeaderVisible(false);
                return;
            }

            SetPreviewHeaderVisible(true);

            if (selectionOrigin == SelectionOrigin.Mouse)
            {
                OnSettingClicked(selected);
            }

            if (selected.TryGetComponent<CareerHeader>(out var careerRow) &&
                !string.IsNullOrEmpty(careerRow.DetailsTitle))
            {
                SetCareerActionRow(careerRow);
                SetPreviewHeaderVisible(false);
                careerRow.ShowDetails();
                return;
            }

            if (CurrentTab is PresetsTab presetsTab)
            {
                presetsTab.ClearCareerEditor();
            }

            SetCareerActionRow(null);

            if (careerRow != null && careerRow.IsEditorActionRow)
            {
                SetPreviewHeaderVisible(false);
                return;
            }

            if (selected.TryGetComponent<PresetTypeDropdown>(out _))
            {
                _settingName.text = PRESET_TYPE_NAME;
                _settingDescription.text = PRESET_TYPE_DESC;
                return;
            }

            if (selected.TryGetComponent<PresetDropdown>(out _))
            {
                _settingName.text = PRESET_NAME;
                _settingDescription.text = PRESET_DESC;
                return;
            }

            var settingVisual = selected.GetComponent<BaseSettingVisual>();

            // If we're not selecting a setting (for example, buttons or headers) skip
            if (settingVisual == null)
            {
                ClearSettingDetails();
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
            if (beforeIndex.HasValue && _settingsNavGroup.Count > 0)
            {
                _settingsNavGroup.SelectAt(Math.Min(beforeIndex.Value, _settingsNavGroup.Count - 1));
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

            if (beforeIndex.HasValue && _settingsNavGroup.Count > 0)
            {
                _settingsNavGroup.SelectAt(Math.Min(beforeIndex.Value, _settingsNavGroup.Count - 1));
            }
        }

        public void RefreshSettingsAndSelectCareerRow(object rowIdentity)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            UpdateSettings(false);

            for (int i = 0; i < _settingsNavGroup.Navigatables.Count; i++)
            {
                var navigatable = _settingsNavGroup.Navigatables[i];
                if (navigatable.TryGetComponent<CareerHeader>(out var row) &&
                    ReferenceEquals(row.RowIdentity, rowIdentity))
                {
                    _settingsNavGroup.SelectAt(i, SelectionOrigin.Navigation);
                    return;
                }
            }
        }

        private void UpdateSettings(bool resetScroll)
        {
            _settingName.text = IsSearching
                ? Localize.Key("Menu.Settings.SearchHeader.Results")
                : string.Empty;
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
                if (IsSearching || NavigationGroup.CurrentNavigationGroup == _settingsNavGroup)
                {
                    _settingsNavGroup.SelectFirst();
                }
            }
        }

        private void UpdateLayout()
        {
            var hasPreview = !IsSearching && CurrentTab != null && CurrentTab.HasPreview;
            var previewHeader = (RectTransform) _previewPanel.transform.Find("Header");
            var previewLayout = _previewPanel.GetComponent<LayoutElement>();
            bool equalCareerSplit = !IsSearching &&
                CurrentTab is PresetsTab presetsTab && presetsTab.IsCareerPresetSelected;
            previewHeader.gameObject.SetActive(true);
            _previewPanel.SetActive(true);
            previewLayout.preferredWidth = equalCareerSplit
                ? 0f
                : CurrentTab is PresetsTab
                    ? 800f
                    : hasPreview
                        ? 640f
                        : 440f;
            previewLayout.flexibleWidth = equalCareerSplit ? 1f : 0f;
            _previewContainerUI.gameObject.SetActive(hasPreview);

            if (hasPreview)
            {
                previewHeader.anchorMin = new Vector2(0f, 1f);
                previewHeader.anchorMax = Vector2.one;
                previewHeader.offsetMin = new Vector2(0f, -125f);
                previewHeader.offsetMax = Vector2.zero;
            }
            else
            {
                previewHeader.anchorMin = Vector2.zero;
                previewHeader.anchorMax = Vector2.one;
                previewHeader.offsetMin = Vector2.zero;
                previewHeader.offsetMax = Vector2.zero;
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

        private void SetCareerActionRow(CareerHeader row)
        {
            if (_careerActionRow == row)
            {
                return;
            }

            _careerActionRow = row;
            if (isActiveAndEnabled && Navigator.Instance != null)
            {
                Navigator.Instance.PopScheme();
                PushNavigationScheme();
            }
        }

        private void PushNavigationScheme()
        {
            var entries = new List<NavigationScheme.Entry>
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up", context =>
                {
                    if (_careerActionRow != null &&
                        Navigator.Instance.IsActionHeld(context.Player, MenuAction.Yellow))
                    {
                        Navigator.Instance.CancelHold(context.Player, MenuAction.Yellow);
                        _careerActionRow.MoveUp?.Invoke();
                    }
                    else
                    {
                        NavigationGroup.CurrentNavigationGroup.SelectPrevious(context.IsRepeat);
                    }
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down", context =>
                {
                    if (_careerActionRow != null &&
                        Navigator.Instance.IsActionHeld(context.Player, MenuAction.Yellow))
                    {
                        Navigator.Instance.CancelHold(context.Player, MenuAction.Yellow);
                        _careerActionRow.MoveDown?.Invoke();
                    }
                    else
                    {
                        NavigationGroup.CurrentNavigationGroup.SelectNext(context.IsRepeat);
                    }
                }),
            };

            if (_careerActionRow != null)
            {
                string moveKey = _careerActionRow.IsSongRow
                    ? "Menu.Settings.HoldMoveSong"
                    : "Menu.Settings.HoldMoveTier";
                string deleteKey = _careerActionRow.IsSongRow
                    ? "Menu.Settings.HoldDeleteSong"
                    : "Menu.Settings.HoldDeleteTier";

                // Register Yellow with the navigation hold tracker so modifier chords can
                // inspect its physical held state. ShowHeldState gives it a pressed visual
                // without displaying the timed hold-progress fill or performing an action.
                entries.Add(new NavigationScheme.Entry(MenuAction.Yellow, moveKey, () => { },
                    holdSeconds: 1f, onHoldHandler: () => { }, showHeldState: true));
                entries.Add(new NavigationScheme.Entry(MenuAction.Blue, deleteKey, () => { },
                    holdSeconds: 1f, onHoldHandler: () => _careerActionRow?.Delete?.Invoke()));

                string directionalMoveKey = _careerActionRow.IsSongRow
                    ? "Menu.MusicLibrary.MoveInPlaylist"
                    : "Menu.Settings.MoveTier";
                entries.Add(new NavigationScheme.Entry(MenuAction.Left, directionalMoveKey,
                    () => _careerActionRow?.MoveUp?.Invoke()));
                entries.Add(new NavigationScheme.Entry(MenuAction.Right, directionalMoveKey,
                    () => _careerActionRow?.MoveDown?.Invoke()));
            }

            var scheme = new NavigationScheme(entries, true);
            if (_careerActionRow != null)
            {
                scheme.VerticalDirectionsModifier = MenuAction.Yellow;
            }
            _ = Navigator.Instance.PushScheme(scheme);
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            _previewVersion++;
            _careerActionRow = null;
            CurrentTab?.OnTabExit();
            CurrentTab = null;

            Navigator.Instance.PopScheme();
            DestroyPreview();

            _categorySidebar.CategoryChanged -= OnTabChanged;
            _categorySidebar.CategoryClicked -= OnCategoryClicked;
            _categorySidebar.CategoryConfirmed -= OnCategoryConfirmed;
            _categorySidebar.SidebarClicked -= OnSidebarClicked;

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
