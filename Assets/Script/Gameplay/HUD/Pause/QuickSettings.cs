﻿using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Settings.Types;

namespace YARG.Gameplay.HUD
{
    public partial class QuickSettings : GenericPause
    {
        [SerializeField]
        private Transform _quickSettingsContainer;
        [SerializeField]
        private Transform _subSettingsContainer;

        [Space]
        [SerializeField]
        private NavigationGroup _subSettingsNavGroup;
        [SerializeField]
        private GameObject _editHudButton;
        [SerializeField]
        private Transform _subSettingsBackButton;

        [Space]
        [SerializeField]
        private PauseVolumeSetting _pauseVolumeSettingPrefab;

        protected override void OnSongStarted()
        {
            _editHudButton.gameObject.SetActive(GameManager.Players.Count <= 1);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _quickSettingsContainer.gameObject.SetActive(true);
            _subSettingsContainer.gameObject.SetActive(false);
        }

        public override void Back()
        {
            PauseMenuManager.PopMenu();
        }

        public void EditHUD()
        {
            GameManager.SetEditHUD(true);
        }

        public void OpenAudioSettings()
        {
            OpenSubSettings(_soundSettings);
        }

        private void OpenSubSettings(List<string> settings)
        {
            // Destroy all of the options (except for the back button)
            foreach (Transform child in _subSettingsContainer)
            {
                if (child == _subSettingsBackButton)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }

            foreach (var settingName in settings)
            {
                var setting = SettingsManager.GetSettingByName(settingName);

                switch (setting)
                {
                    case VolumeSetting volumeSetting:
                    {
                        var settingObject = Instantiate(_pauseVolumeSettingPrefab, _subSettingsContainer);
                        settingObject.Initialize(settingName, volumeSetting);

                        _subSettingsNavGroup.AddNavigatable(settingObject.gameObject);

                        break;
                    }
                    default:
                        YargLogger.LogError("Didn't implement setting prefab for this setting type.");
                        break;
                }
            }

            _quickSettingsContainer.gameObject.SetActive(false);
            _subSettingsContainer.gameObject.SetActive(true);
        }
    }
}