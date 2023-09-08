using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;
using YARG.Settings;
using YARG.Settings.Metadata;
using YARG.Settings.Types;

namespace YARG.Menu.Settings
{
    [DefaultExecutionOrder(-10000)]
    public class SettingsMenu : MonoSingleton<SettingsMenu>
    {
        [SerializeField]
        private HeaderTabs _headerTabs;
        [SerializeField]
        private RawImage _previewRawImage;
        [SerializeField]
        private Transform _settingsContainer;
        [SerializeField]
        private NavigationGroup _settingsNavGroup;
        [SerializeField]
        private Transform _previewContainer;
        [SerializeField]
        private ScrollRect _scrollRect;

        [Space]
        [SerializeField]
        private LocalizeStringEvent _settingName;
        [SerializeField]
        private LocalizeStringEvent _settingDescription;

        [Space]
        [SerializeField]
        private GameObject _buttonPrefab;
        [SerializeField]
        private GameObject _headerPrefab;
        [SerializeField]
        private GameObject _dropdownPrefab;

        [Space]
        [SerializeField]
        private GameObject _songManagerHeader;
        [SerializeField]
        private GameObject _songManagerDirectoryPrefab;

        private string _currentTab;

        private readonly List<BaseSettingVisual> _settingVisuals = new();
        private readonly List<SettingsPresetDropdown> _settingDropdowns = new();

        public string CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;

                UpdateSettingsForTab();
            }
        }

        public bool UpdateSongLibraryOnExit { get; set; }

        // Workaround to avoid errors when deactivating menu during startup
        private bool _ready;

        protected override void SingletonAwake()
        {
            // Settings menu defaults to active so that it will be initialized at startup
            gameObject.SetActive(false);

            _ready = true;
        }

        private void Start()
        {
            var tabs = new List<HeaderTabs.TabInfo>();

            foreach (var tab in SettingsManager.SettingsTabs)
            {
                // Load the tab sprite
                var sprite = Addressables.LoadAssetAsync<Sprite>($"TabIcons[{tab.Icon}]").WaitForCompletion();

                tabs.Add(new HeaderTabs.TabInfo
                {
                    Icon = sprite,
                    Id = tab.Name,
                    DisplayName = LocaleHelper.LocalizeString("Settings", $"Tab.{tab.Name}")
                });
            }

            _headerTabs.Tabs = tabs;
        }

        private void OnEnable()
        {
            if (!_ready)
            {
                return;
            }

            _headerTabs.RefreshTabs();
            _headerTabs.TabChanged += OnTabChanged;

            _settingsNavGroup.SelectionChanged += OnSelectionChanged;

            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", () =>
                {
                    gameObject.SetActive(false);
                }),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                _headerTabs.NavigateNextTab,
                _headerTabs.NavigatePreviousTab
            }, true));

            ReturnToFirstTab();
        }

        private void OnTabChanged(string tab)
        {
            CurrentTab = tab;
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            var settingNav = selected.GetComponent<BaseSettingNavigatable>();

            _settingName.StringReference = LocaleHelper.StringReference(
                "Settings", $"Setting.{CurrentTab}.{settingNav.SettingName}");

            _settingDescription.StringReference = LocaleHelper.StringReference(
                "Settings", $"Setting.{CurrentTab}.{settingNav.SettingName}.Description");
        }

        public void UpdateSettingsForTab()
        {
            var tabInfo = SettingsManager.GetTabByName(CurrentTab);

            UpdateSettings();
            UpdatePreview(tabInfo).Forget();
        }

        private void UpdateSettings()
        {
            _settingVisuals.Clear();
            _settingDropdowns.Clear();
            _settingsNavGroup.ClearNavigatables();

            // Destroy all previous settings
            _settingsContainer.DestroyChildren();

            // Build the settings tab
            SettingsManager.GetTabByName(CurrentTab)
                .BuildSettingTab(_settingsContainer, _settingsNavGroup);

            // Make the settings nav group the main one
            _settingsNavGroup.SelectFirst();

            // Reset scroll rect
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        private async UniTask UpdatePreview(Tab tabInfo)
        {
            // DestroyPreview();
            //
            // if (string.IsNullOrEmpty(tabInfo.PreviewPath))
            // {
            //     _previewRawImage.gameObject.SetActive(false);
            //     _previewRawImage.texture = null;
            //     _previewRawImage.color = Color.black;
            //     return;
            // }
            //
            // // Spawn prefab
            // _previewContainer.gameObject.SetActive(true);
            // var previewPrefab = Addressables.LoadAssetAsync<GameObject>(tabInfo.PreviewPath).WaitForCompletion();
            // Instantiate(previewPrefab, _previewContainer);
            //
            // // Set render texture
            // CameraPreviewTexture.SetAllPreviews();
            //
            // // Enable and wait for layouts to rebuild
            // _previewRawImage.gameObject.SetActive(true);
            // await UniTask.WaitForEndOfFrame(this);
            //
            // // Size raw image
            // _previewRawImage.texture = CameraPreviewTexture.PreviewTexture;
            // _previewRawImage.color = Color.white;
            // var rect = _previewRawImage.rectTransform.ToViewportSpaceCentered(v: false, scale: 0.9f);
            // rect.y = 0f;
            // _previewRawImage.uvRect = rect;
        }

        private void DestroyPreview()
        {
            if (_previewContainer == null) return;

            _previewContainer.DestroyChildren();
            _previewContainer.gameObject.SetActive(false);
        }

        public void ReturnToFirstTab()
        {
            CurrentTab = SettingsManager.SettingsTabs[0].Name;
        }

        public void UpdateSpecificSetting(string settingName)
        {
            // If the settings menu is not open, ignore
            if (!gameObject.activeSelf)
            {
                return;
            }

            // Refresh all of the settings with that name
            foreach (var settingVisual in _settingVisuals)
            {
                if (settingVisual.SettingName != settingName)
                {
                    continue;
                }

                settingVisual.RefreshVisual();
            }
        }

        public void UpdatePresetDropdowns(ISettingType withSetting)
        {
            // If the settings menu is not open, ignore
            if (!gameObject.activeSelf)
            {
                return;
            }

            // Refresh all of the settings with that name
            foreach (var dropdown in _settingDropdowns)
            {
                if (dropdown.ModifiedSettings.Select(SettingsManager.GetSettingByName).Contains(withSetting))
                {
                    dropdown.ForceUpdateValue();
                }
            }
        }

        private async UniTask OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            Navigator.Instance.PopScheme();
            DestroyPreview();
            _headerTabs.TabChanged -= OnTabChanged;

            _settingsNavGroup.SelectionChanged -= OnSelectionChanged;

            // Save on close
            SettingsManager.SaveSettings();

            if (UpdateSongLibraryOnExit)
            {
                UpdateSongLibraryOnExit = false;

                // Do a song refresh if requested
                LoadingManager.Instance.QueueSongRefresh(true);
                await LoadingManager.Instance.StartLoad();

                // Then refresh song select
                MusicLibraryMenu.RefreshFlag = true;
            }
        }
    }
}