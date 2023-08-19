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

        [Space]
        [SerializeField]
        private GameObject _buttonPrefab;
        [SerializeField]
        private GameObject _headerPrefab;
        [SerializeField]
        private GameObject _directoryPrefab;
        [SerializeField]
        private GameObject _dropdownPrefab;

        private string _currentTab;

        private readonly List<ISettingVisual> _settingVisuals = new();
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
                // Skip tabs that aren't shown in game, if we are in game
                if (!tab.ShowInPlayMode && GlobalVariables.Instance.CurrentScene == SceneIndex.Gameplay)
                {
                    continue;
                }

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

            _headerTabs.TabChanged += OnTabChanged;

            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Back", () => { gameObject.SetActive(false); }),
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

        public void UpdateSettingsForTab()
        {
            if (CurrentTab == SettingsManager.SONG_FOLDER_MANAGER_TAB)
            {
                UpdateSongFolderManager();

                return;
            }

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

            foreach (var tab in SettingsManager.SettingsTabs)
            {
                // Look for the tab
                if (tab.Name != CurrentTab)
                {
                    continue;
                }

                // Once we've found the tab, add the settings
                foreach (var settingMetadata in tab.Settings)
                {
                    if (settingMetadata is HeaderMetadata header)
                    {
                        // Spawn in the header
                        SpawnHeader(_settingsContainer, $"Header.{header.HeaderName}");
                    }
                    else if (settingMetadata is ButtonRowMetadata buttonRow)
                    {
                        // Spawn the button
                        var go = Instantiate(_buttonPrefab, _settingsContainer);
                        go.GetComponent<SettingsButton>().SetInfo(buttonRow.Buttons);
                    }
                    else if (settingMetadata is FieldMetadata field)
                    {
                        var setting = SettingsManager.GetSettingByName(field.FieldName);

                        // Spawn the setting
                        var settingPrefab = Addressables.LoadAssetAsync<GameObject>(setting.AddressableName)
                            .WaitForCompletion();
                        var go = Instantiate(settingPrefab, _settingsContainer);

                        // Set the setting, and cache the object
                        var visual = go.GetComponent<ISettingVisual>();
                        visual.SetSetting(field.FieldName);
                        _settingVisuals.Add(visual);
                        _settingsNavGroup.AddNavigatable(go);
                    }
                    else if (settingMetadata is PresetDropdownMetadata dropdown)
                    {
                        // Spawn the dropdown
                        var go = Instantiate(_dropdownPrefab, _settingsContainer);

                        // Set the setting, and cache the object
                        var settingsDropdown = go.GetComponent<SettingsPresetDropdown>();
                        settingsDropdown.SetInfo(dropdown);
                        _settingDropdowns.Add(settingsDropdown);
                    }
                }

                // Then we're good!
                break;
            }

            // Make the settings nav group the main one
            _settingsNavGroup.SelectFirst();
        }

        private void SpawnHeader(Transform container, string localizationKey)
        {
            // Spawn the header
            var go = Instantiate(_headerPrefab, container);

            // Set header text
            go.GetComponentInChildren<LocalizeStringEvent>().StringReference =
                LocaleHelper.StringReference("Settings", localizationKey);
        }

        public void UpdateSongFolderManager()
        {
            UpdateSongLibraryOnExit = true;

            // Destroy all previous settings
            _settingsContainer.DestroyChildren();

            // Spawn header
            SpawnHeader(_settingsContainer, "Header.Cache");

            // Spawn refresh all button
            {
                var go = Instantiate(_buttonPrefab, _settingsContainer);
                go.GetComponent<SettingsButton>().SetCustomCallback(async () =>
                {
                    LoadingManager.Instance.QueueSongRefresh(false);
                    await LoadingManager.Instance.StartLoad();
                }, "RefreshCache");
            }

            // Spawn header
            SpawnHeader(_settingsContainer, "Header.SongFolders");

            // Spawn add folder button
            {
                var go = Instantiate(_buttonPrefab, _settingsContainer);
                go.GetComponent<SettingsButton>().SetCustomCallback(() =>
                {
                    SettingsManager.Settings.SongFolders.Add(string.Empty);

                    // Refresh everything
                    UpdateSongFolderManager();
                }, "AddFolder");
            }

            // Create all of the directories
            for (int i = 0; i < SettingsManager.Settings.SongFolders.Count; i++)
            {
                var go = Instantiate(_directoryPrefab, _settingsContainer);
                go.GetComponent<SettingsDirectory>().SetIndex(i);
            }
        }

        private async UniTask UpdatePreview(SettingsManager.Tab tabInfo)
        {
            DestroyPreview();

            if (string.IsNullOrEmpty(tabInfo.PreviewPath))
            {
                _previewRawImage.gameObject.SetActive(false);
                _previewRawImage.texture = null;
                _previewRawImage.color = Color.black;
                return;
            }

            // Spawn prefab
            _previewContainer.gameObject.SetActive(true);
            var previewPrefab = Addressables.LoadAssetAsync<GameObject>(tabInfo.PreviewPath).WaitForCompletion();
            Instantiate(previewPrefab, _previewContainer);

            // Set render texture
            CameraPreviewTexture.SetAllPreviews();

            // Enable and wait for layouts to rebuild
            _previewRawImage.gameObject.SetActive(true);
            await UniTask.WaitForEndOfFrame(this);

            // Size raw image
            _previewRawImage.texture = CameraPreviewTexture.PreviewTexture;
            _previewRawImage.color = Color.white;
            var rect = _previewRawImage.rectTransform.ToViewportSpaceCentered(v: false, scale: 0.7f);
            rect.y = 0f;
            _previewRawImage.uvRect = rect;
        }

        private void DestroyPreview()
        {
            if (_previewContainer == null) return;

            _previewContainer.DestroyChildren();
            _previewContainer.gameObject.SetActive(false);
        }

        public void ReturnToFirstTab()
        {
            // Select the first tab
            foreach (var tab in SettingsManager.SettingsTabs)
            {
                // Skip tabs that aren't shown in game, if we are in game
                if (!tab.ShowInPlayMode && GlobalVariables.Instance.CurrentScene == SceneIndex.Gameplay)
                {
                    continue;
                }

                CurrentTab = tab.Name;
                break;
            }
        }

        public void UpdateSpecificSetting(string settingName)
        {
            // If the settings menu is not open, ignore
            if (!gameObject.activeSelf)
            {
                return;
            }

            // Nothing in the song folder manager we can update
            if (CurrentTab == SettingsManager.SONG_FOLDER_MANAGER_TAB)
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