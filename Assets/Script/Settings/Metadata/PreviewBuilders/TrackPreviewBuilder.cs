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
    public class TrackPreviewBuilder : IPreviewBuilder
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _trackPreview = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreview")
            .WaitForCompletion();
        private static readonly GameObject _trackPreviewUI = Addressables
            .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreviewUI")
            .WaitForCompletion();

        public GameMode? StartingGameMode { get; set; }

        private readonly bool _forceShowHitWindow;

        public TrackPreviewBuilder(bool forceShowHitWindow = false)
        {
            _forceShowHitWindow = forceShowHitWindow;
        }

        public UniTask BuildPreviewWorld(Transform worldContainer)
        {
            var trackObj = Object.Instantiate(_trackPreview, worldContainer);
            var trackPreview = trackObj.GetComponentInChildren<FakeTrackPlayer>();

            trackPreview.ForceShowHitWindow = _forceShowHitWindow;

            // If null, just use the default value and skip setting it
            if (StartingGameMode is not null)
            {
                trackPreview.SelectedGameMode = StartingGameMode.Value;
            }

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