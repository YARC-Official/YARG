using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
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
        [SerializeField]
        private Transform _previewContainerWorld;
        [SerializeField]
        private Transform _previewContainerUI;

        [Space]
        [SerializeField]
        private LocalizeStringEvent _settingName;
        [SerializeField]
        private LocalizeStringEvent _settingDescription;

        private Tab _currentTab;
        public Tab CurrentTab
        {
            get => _currentTab;
            set
            {
                _currentTab = value;

                Refresh();
            }
        }

        public event Action SettingChanged;

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
            CurrentTab?.OnTabExit();
            CurrentTab = SettingsManager.GetTabByName(tab);
            CurrentTab?.OnTabEnter();
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            if (selected == null || CurrentTab == null)
            {
                _settingName.StringReference = LocaleHelper.EmptyString;
                _settingDescription.StringReference = LocaleHelper.EmptyString;
                return;
            }

            var settingNav = selected.GetComponent<BaseSettingNavigatable>();

            _settingName.StringReference = LocaleHelper.StringReference(
                "Settings", $"Setting.{CurrentTab.Name}.{settingNav.UnlocalizedName}");

            _settingDescription.StringReference = LocaleHelper.StringReference(
                "Settings", $"Setting.{CurrentTab.Name}.{settingNav.UnlocalizedName}.Description");
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

        private void UpdateSettings(bool resetScroll)
        {
            _settingsNavGroup.ClearNavigatables();

            // Destroy all previous settings
            _settingsContainer.DestroyChildren();

            // Build the settings tab
            CurrentTab?.BuildSettingTab(_settingsContainer, _settingsNavGroup);

            if (resetScroll)
            {
                // Make the settings nav group the main one
                _settingsNavGroup.SelectFirst();

                // Reset scroll rect
                _scrollRect.verticalNormalizedPosition = 1f;
            }
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

        public void ReturnToFirstTab()
        {
            CurrentTab = SettingsManager.SettingsTabs[0];
        }

        public void OnSettingChanged()
        {
            if (!_ready || !gameObject.activeSelf) return;

            CurrentTab?.OnSettingChanged();
            SettingChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            // Set the current tab back to null to avoid calling OnTabExit twice
            CurrentTab?.OnTabExit();
            _currentTab = null;

            Navigator.Instance.PopScheme();
            DestroyPreview();
            _headerTabs.TabChanged -= OnTabChanged;

            _settingsNavGroup.SelectionChanged -= OnSelectionChanged;

            // Save on close
            SettingsManager.SaveSettings();
            CustomContentManager.SaveAll();

            //This is a bit of a hack to update the CurrentNavigationGroup again.
            //ideally the settings menu should work just like every other menu so this isn't needed
            MenuManager.Instance.ReactivateCurrentMenu();
        }
    }
}