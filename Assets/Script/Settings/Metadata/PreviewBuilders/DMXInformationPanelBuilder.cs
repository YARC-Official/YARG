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
        private static readonly GameObject _information = Addressables
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

        public UniTask BuildPreviewUI(Transform uiContainer)
        {
            var go = Object.Instantiate(_information, uiContainer);
            return UniTask.CompletedTask;
        }
    }
}