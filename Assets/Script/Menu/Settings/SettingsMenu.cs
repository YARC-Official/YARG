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
            CurrentTab.OnTabEnter();
        }

        private void OnSelectionChanged(NavigatableBehaviour selected, SelectionOrigin selectionOrigin)
        {
            var settingNav = selected.GetComponent<BaseSettingNavigatable>();

            _settingName.StringReference = LocaleHelper.StringReference(
                "Settings", $"Setting.{CurrentTab.Name}.{settingNav.UnlocalizedName}");

            _settingDescription.StringReference = LocaleHelper.StringReference(
                "Settings", $"Setting.{CurrentTab.Name}.{settingNav.UnlocalizedName}.Description");
        }

        public void Refresh()
        {
            UpdateSettings();
            UpdatePreview(CurrentTab).Forget();
        }

        private void UpdateSettings()
        {
            _settingsNavGroup.ClearNavigatables();

            // Destroy all previous settings
            _settingsContainer.DestroyChildren();

            // Build the settings tab
            CurrentTab.BuildSettingTab(_settingsContainer, _settingsNavGroup);

            // Make the settings nav group the main one
            _settingsNavGroup.SelectFirst();

            // Reset scroll rect
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        private async UniTask UpdatePreview(Tab tabInfo)
        {
            DestroyPreview();

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
        }
    }
}