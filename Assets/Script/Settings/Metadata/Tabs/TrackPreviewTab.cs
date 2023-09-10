using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Settings;

namespace YARG.Settings.Metadata
{
    public class TrackPreviewTab : MetadataTab
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _trackPreview = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreview")
            .WaitForCompletion();
        private static readonly GameObject _trackPreviewUI = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreviewUI")
            .WaitForCompletion();

        public TrackPreviewTab(string name, string icon = "Generic") : base(name, icon)
        {
        }

        public override UniTask BuildPreviewWorld(Transform container)
        {
            Object.Instantiate(_trackPreview, container);

            return UniTask.CompletedTask;
        }

        public override async UniTask BuildPreviewUI(Transform container)
        {
            var go = Object.Instantiate(_trackPreviewUI, container);

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