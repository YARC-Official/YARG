using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Menu.Settings;

namespace YARG.Settings.Metadata
{
    public class DMXInformationPanelBuilder : IPreviewBuilder
    {
        // Prefabs needed for this tab type
        private static readonly GameObject Information = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/DMXInformationPanelUI")
            .WaitForCompletion();

        public GameMode? StartingGameMode { get; set; }

        public DMXInformationPanelBuilder()
        {

        }

        public UniTask BuildPreviewWorld(Transform worldContainer)
        {
            return UniTask.CompletedTask;
        }

        public async UniTask BuildPreviewUI(Transform uiContainer)
        {
            var go = Object.Instantiate(Information, uiContainer);

            // Enable and wait for layouts to rebuild
            await UniTask.WaitForEndOfFrame(SettingsMenu.Instance);

            // Skip the game object was somehow destroyed
            if (go == null) return;

        }
    }
}