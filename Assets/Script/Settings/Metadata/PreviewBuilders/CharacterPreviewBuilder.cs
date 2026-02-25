using System.Collections.Generic;
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

        public async UniTask BuildPreviewWorld(Transform worldContainer)
        {
            _worldContainer = worldContainer;

            // Instantiate the world first since we may need it if the user subsequently selects a character
            if (_previewWorld == null)
            {
                _previewWorld = Addressables
                    .LoadAssetAsync<GameObject>("SettingPreviews/CharacterPreview")
                    .WaitForCompletion();
            }

            if (_previewWorld == null)
            {
                YargLogger.LogError("Failed to load addressable character preview world prefab!");
                return;
            }

            // Instantiate the preview prefab
            var go = Object.Instantiate(_previewWorld, worldContainer);
            _worldInstance = go;
            _previewScriptInstance = go.GetComponent<CharacterPreview>();

            if (string.IsNullOrEmpty(CharacterFile))
            {
                return;
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

            AssetBundle shaderBundle = null;
            // It is expected that the character bundle may not have loaded
            if (_characterPrefab != null)
            {
                // Replace shaders if necessary
                shaderBundle = await LoadMetalShaders(bundle, _characterPrefab);
                _characterInstance = _previewScriptInstance.Initialize(_characterPrefab);
            }

            if (shaderBundle != null)
            {
                shaderBundle.Unload(false);
            }

            if (bundle != null)
            {
                bundle.Unload(false);
            }

            return;
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

        public static async UniTask ChangeCharacter(string path)
        {
            CharacterFile = path;

            // If the world instance doesn't exist, just skip
            if (_worldInstance == null)
            {
                return;
            }

            AssetBundle bundle = null;
            AssetBundle shaderBundle = null;

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

            shaderBundle = await LoadMetalShaders(bundle, _characterPrefab);

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

            if (shaderBundle != null)
            {
                shaderBundle.Unload(false);
            }

            if (bundle != null)
            {
                bundle.Unload(false);
            }
        }

        // TODO: Refactor the AssetBundle loading such that BackgroundManager and this can share code
        private static async UniTask<AssetBundle> LoadMetalShaders(AssetBundle bundle, GameObject bg)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            AssetBundle shaderBundle = null;
            var renderers = bg.GetComponentsInChildren<Renderer>(true);
            var metalShaders = new Dictionary<string, Shader>();

            var shaderBundleName = "Assets/" + BundleBackgroundManager.CHARACTER_SHADER_BUNDLE_NAME;

            var shaderBundleData = (TextAsset)await bundle.LoadAssetAsync<TextAsset>(
                shaderBundleName
            );

            if (shaderBundleData != null && shaderBundleData.bytes.Length > 0)
            {
                YargLogger.LogInfo("Loading Metal shader bundle");
                shaderBundle = await AssetBundle.LoadFromMemoryAsync(shaderBundleData.bytes);
                var allAssets = shaderBundle.LoadAllAssets<Shader>();
                foreach (var shader in allAssets)
                {
                    metalShaders.Add(shader.name, shader);
                }
            }
            else
            {
                YargLogger.LogInfo("Did not find Metal shader bundle");
            }

            // Yarground comes with shaders for dx11/dx12/glcore/vulkan
            // Metal shaders used on OSX come in this separate bundle
            // Update our renderers to use them

            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    var shaderName = material.shader.name;
                    if (metalShaders.TryGetValue(shaderName, out var shader))
                    {
                        YargLogger.LogFormatDebug("Found bundled shader {0}", shaderName);
                        // We found shader from Yarground
                        material.shader = shader;
                    }
                    else
                    {
                        YargLogger.LogFormatDebug("Did not find bundled shader {0}", shaderName);
                        // Fallback to try to find among builtin shaders
                        material.shader = Shader.Find(shaderName);
                    }
                }
            }

            return shaderBundle;
#endif
            // Fallback if we're not running on OSX
            return null;
        }
    }
}