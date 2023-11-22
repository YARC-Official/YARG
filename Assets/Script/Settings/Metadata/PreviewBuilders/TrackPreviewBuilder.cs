using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Settings;

namespace YARG.Settings.Metadata
{
    public class TrackPreviewBuilder : IPreviewBuilder
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _trackPreview = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreview")
            .WaitForCompletion();
        private static readonly GameObject _trackPreviewUI = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreviewUI")
            .WaitForCompletion();

        public UniTask BuildPreviewWorld(Transform worldContainer)
        {
            Object.Instantiate(_trackPreview, worldContainer);
            return UniTask.CompletedTask;
        }

        public async UniTask BuildPreviewUI(Transform uiContainer)
        {
            var go = Object.Instantiate(_trackPreviewUI, uiContainer);

            // Enable and wait for layouts to rebuild
            await UniTask.WaitForEndOfFrame(SettingsMenu.Instance);

            // Skip the game object was somehow destroyed
            if (go == null) return;

            // Show the raw image
            var previewTexture = go.GetComponentInChildren<RawImage>();
            previewTexture.texture = CameraPreviewTexture.PreviewTexture;
            previewTexture.color = Color.white;

            // Size raw image
            var rect = previewTexture.rectTransform.ToViewportSpaceCentered(v: false, scale: 0.9f);
            rect.y = 0f;
            previewTexture.uvRect = rect;
        }
    }
}