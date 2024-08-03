﻿using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;
using YARG.Settings.Preview;

namespace YARG.Settings.Metadata
{
    public class ExperimentalPreviewBuilder : IPreviewBuilder
    {
        // Whether experimental dialog has been shown
        private static bool _experimentalDialogShown;

        // Prefabs needed for this tab type
        private static readonly GameObject _experimentalPreviewUI = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/ExperimentalPreviewUI")
            .WaitForCompletion();

        public ExperimentalPreviewBuilder()
        {
        }

        public UniTask BuildPreviewWorld(Transform worldContainer)
        {
            return UniTask.CompletedTask;
        }

        public UniTask BuildPreviewUI(Transform uiContainer)
        {
            // Instantiate the warning UI
            Object.Instantiate(_experimentalPreviewUI, uiContainer);

            // Show the experimental warning dialog if it hasn't been shown already
            // Also only show it once per game launch
            if (!_experimentalDialogShown && SettingsManager.Settings.ShowExperimentalWarningDialog)
            {
                DialogManager.Instance.ShowOneTimeMessage(
                    Localize.Key("Menu.Dialog.Experimental.Title"),
                    Localize.Key("Menu.Dialog.Experimental.Description"),
                    () =>
                    {
                        SettingsManager.Settings.ShowExperimentalWarningDialog = false;
                        SettingsManager.SaveSettings();
                    });

                _experimentalDialogShown = true;
            }

            return UniTask.CompletedTask;
        }
    }
}