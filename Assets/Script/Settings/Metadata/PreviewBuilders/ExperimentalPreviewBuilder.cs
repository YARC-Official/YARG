using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Settings;
using YARG.Settings.Preview;

namespace YARG.Settings.Metadata
{
    public class ExperimentalPreviewBuilder : IPreviewBuilder
    {
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

        public async UniTask BuildPreviewUI(Transform uiContainer)
        {
            var go = Object.Instantiate(_experimentalPreviewUI, uiContainer);

            // Enable and wait for layouts to rebuild
            await UniTask.WaitForEndOfFrame(SettingsMenu.Instance);

            // Skip the game object was somehow destroyed
            if (go == null) return;

            // No other configuration required
        }
    }
}