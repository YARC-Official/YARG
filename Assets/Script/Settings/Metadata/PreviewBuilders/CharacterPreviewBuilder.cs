using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Settings;
using YARG.Settings.Preview;
using YARG.Venue;

namespace YARG.Settings.Metadata
{
    public class CharacterPreviewBuilder : IPreviewBuilder
    {
        private static Transform          _worldContainer;
        private static GameObject         _previewWorld;
        private static GameObject         _previewUI;
        private static GameObject         _characterPrefab;

        private static TextMeshProUGUI _nameText;
        private static TextMeshProUGUI _authorText;

        private static GameObject         _characterInstance;
        private static CharacterPreview   _previewScriptInstance;
        private static GameObject         _worldInstance;
        private static GameObject         _uiInstance;
        private static CharacterPreviewUI _uiScriptInstance;

        public static  string             CharacterFile;

        public CharacterPreviewBuilder()
        {
        }

        public UniTask BuildPreviewWorld(Transform worldContainer)
        {
            _worldContainer = worldContainer;

            // Instantiate the world first since we may need it if the user subsequently selects a character
            if (_previewWorld == null)
            {
                _previewWorld = Addressables
                    .LoadAssetAsync<GameObject>("SettingPreviews/CharacterPreview")
                    .WaitForCompletion();
            }

            // Instantiate the preview prefab
            var go = Object.Instantiate(_previewWorld, worldContainer);
            _worldInstance = go;
            _previewScriptInstance = go.GetComponent<CharacterPreview>();

            if (string.IsNullOrEmpty(CharacterFile))
            {
                return UniTask.CompletedTask;
            }

            if (_characterPrefab != null)
            {
                Object.Destroy(_characterPrefab);
                _characterPrefab = null;
            }

            AssetBundle bundle = null;

            if (!string.IsNullOrEmpty(CharacterFile))
            {
                bundle = AssetBundle.LoadFromFile(CharacterFile);
            }

            if (bundle != null)
            {
                _characterPrefab =
                    bundle.LoadAsset<GameObject>(BundleBackgroundManager.CHARACTER_PREFAB_PATH.ToLowerInvariant());

                if (_characterPrefab == null)
                {
                    bundle.Unload(true);
                }
            }

            // It is expected that the character bundle may not have loaded
            if (_characterPrefab != null)
            {
                _characterInstance = _previewScriptInstance.Initialize(_characterPrefab);
            }

            if (bundle != null)
            {
                bundle.Unload(false);
            }

            return UniTask.CompletedTask;
        }

        public async UniTask BuildPreviewUI(Transform uiContainer)
        {
            if (_previewUI == null)
            {
                _previewUI = Addressables
                    .LoadAssetAsync<GameObject>("SettingPreviews/CharacterPreviewUI")
                    .WaitForCompletion();
            }

            var go = Object.Instantiate(_previewUI, uiContainer);
            _uiInstance = go;

            _uiScriptInstance = go.GetComponent<CharacterPreviewUI>();
            _uiScriptInstance.Initialize(_characterInstance);

            // Enable and wait for layouts to rebuild
            await UniTask.WaitForEndOfFrame(SettingsMenu.Instance);

            // Skip the game object was somehow destroyed
            if (go == null)
            {
                return;
            }

            // Show the raw image
            var previewTexture = go.GetComponentInChildren<RawImage>();
            previewTexture.texture = CameraPreviewTexture.PreviewTexture;
            previewTexture.color = Color.white;

            // Size raw image
            var rect = previewTexture.rectTransform.ToViewportSpaceCentered(v: false, scale: 0.9f);
            rect.y = 0f;
            previewTexture.uvRect = rect;
        }

        public static void ChangeCharacter(string path)
        {
            CharacterFile = path;

            // If the world instance doesn't exist, just skip
            if (_worldInstance == null)
            {
                return;
            }

            AssetBundle bundle = null;

            if (!string.IsNullOrEmpty(CharacterFile))
            {
                bundle = AssetBundle.LoadFromFile(CharacterFile);
                if (bundle == null)
                {
                    YargLogger.LogFormatError("Failed to load character bundle from {0}", CharacterFile);
                    return;
                }
                _characterPrefab = bundle.LoadAsset<GameObject>(BundleBackgroundManager.CHARACTER_PREFAB_PATH.ToLowerInvariant());
            }
            else
            {
                // Despawn the character preview
                _previewScriptInstance.Disable();
                _uiScriptInstance.Disable();
                _uiInstance = null;
                _characterPrefab = null;
                return;
            }

            if (_characterPrefab == null)
            {
                YargLogger.LogError("Failed to load character from bundle!");
                if (bundle != null)
                {
                    bundle.Unload(true);
                }

                return;
            }

            if (_previewWorld == null)
            {
                _previewWorld = Addressables
                    .LoadAssetAsync<GameObject>("SettingPreviews/CharacterPreview")
                    .WaitForCompletion();

                // Instantiate the preview prefab
                var go = Object.Instantiate(_previewWorld, _worldContainer);
                _previewScriptInstance = go.GetComponent<CharacterPreview>();
                _worldInstance = go;

                _characterInstance = _previewScriptInstance.Initialize(_characterPrefab);
            }
            else
            {
                _characterInstance = _previewScriptInstance.Reinitialize(_characterPrefab);
            }

            _uiScriptInstance.Initialize(_characterInstance);

            if (bundle != null)
            {
                bundle.Unload(false);
            }
        }
    }
}