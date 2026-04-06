using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Venue;

namespace YARG.Helpers
{
    public static class BackgroundHelper
    {
        public static async UniTask<AssetBundle> LoadMetalShaders(AssetBundle bundle, GameObject bg, ExportType type)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            AssetBundle shaderBundle = null;
            var renderers = bg.GetComponentsInChildren<Renderer>(true);
            var metalShaders = new Dictionary<string, Shader>();

            var shaderBundleName = type switch
            {
                ExportType.Character  => "Assets/" + BundleBackgroundManager.CHARACTER_SHADER_BUNDLE_NAME,
                ExportType.Background => "Assets/" + BundleBackgroundManager.BACKGROUND_SHADER_BUNDLE_NAME,
                _                     => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            var shaderBundleData = (TextAsset)await bundle.LoadAssetAsync<TextAsset>(
                shaderBundleName
            );

            if (shaderBundleData == null)
            {
                shaderBundleData = (TextAsset)await bundle.LoadAssetAsync<TextAsset>(
                    bundle.name + BundleBackgroundManager.BUNDLE_OSX_SUFFIX
                );
            }

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

        public enum ExportType
        {
            Character,
            Background
        }
    }
}