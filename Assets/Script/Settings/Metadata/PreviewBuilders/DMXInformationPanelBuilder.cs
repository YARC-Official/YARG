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
        private static GameObject _information;

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
            if (_information == null)
            {
                 _information = Addressables
                    .LoadAssetAsync<GameObject>("SettingPreviews/DMXInformationPanelUI")
                    .WaitForCompletion();
            }
            var go = Object.Instantiate(_information, uiContainer);
            return UniTask.CompletedTask;
        }
    }
}
