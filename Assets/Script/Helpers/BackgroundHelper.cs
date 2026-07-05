using UnityEngine;
using YARG.Venue.Characters;

#if UNITY_EDITOR || UNITY_STANDALONE_OSX
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using YARG.Core.Logging;
using System.Collections.Generic;
#endif

#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace YARG.Helpers
{
    public static class BackgroundHelper
    {
        // DO NOT CHANGE THIS! It will break existing venues
        public const string BACKGROUND_PREFAB_PATH = "Assets/_Background.prefab";
        public const string GENERIC_PREFAB_PATH = "Assets/_Bundled.prefab";
        public const string CHARACTER_PREFAB_PATH = "Assets/_Character.prefab";
        public const string BACKGROUND_SHADER_BUNDLE_NAME = "_metal_shaders.bytes";
        public const string CHARACTER_SHADER_BUNDLE_NAME = "_character_metal_shaders.bytes";
        public const string BACKGOUND_OSX_MATERIAL_PREFIX = "_metal_";
        public const string BUNDLE_OSX_SUFFIX = "_metal.bytes";
        public const string AUDIO_PATH = "__YARG_AudioBundle";
        public static readonly string[] AUDIO_FILE_EXTENSIONS =
        {
            ".ogg", ".mogg", ".wav", ".mp3", ".aiff", ".opus",
        };

        public static AssetBundle LoadMetalShaders(AssetBundle bundle, GameObject bg, ExportType type)
        {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            AssetBundle shaderBundle = null;
            var renderers = bg.GetComponentsInChildren<Renderer>(true);
            var metalShaders = new Dictionary<string, Shader>();

            var shaderBundleName = type switch
            {
                ExportType.Character  => "Assets/" + BackgroundHelper.CHARACTER_SHADER_BUNDLE_NAME,
                ExportType.Background => "Assets/" + BackgroundHelper.BACKGROUND_SHADER_BUNDLE_NAME,
                ExportType.Generic    => bundle.name + BackgroundHelper.BUNDLE_OSX_SUFFIX,
                _                     => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };

            var shaderBundleData = (TextAsset) bundle.LoadAsset<TextAsset>(
                shaderBundleName
            );

            if (shaderBundleData == null)
            {
                shaderBundleData = (TextAsset) bundle.LoadAsset<TextAsset>(
                    bundle.name + BackgroundHelper.BUNDLE_OSX_SUFFIX
                );
            }

            if (shaderBundleData != null && shaderBundleData.bytes.Length > 0)
            {
                YargLogger.LogInfo("Loading Metal shader bundle");
                shaderBundle = AssetBundle.LoadFromMemory(shaderBundleData.bytes);
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
#else
            // Fallback if we're not running on OSX
            return null;
#endif
        }

        public enum ExportType
        {
            Character,
            Background,
            Generic
        }



#if UNITY_EDITOR
        // Code to export a background from the editor.

        public static void Export(GameObject root, BackgroundHelper.ExportType type, string[] additionalAssets)
        {
            string defaultName = type == BackgroundHelper.ExportType.Character ? "character" : "bg";
            string extension = type == BackgroundHelper.ExportType.Character ? "yargchar" : "yarground";
            string title = type == BackgroundHelper.ExportType.Character ? "Export Character" : "Export Background";
            string path = EditorUtility.SaveFilePanel(title, string.Empty, defaultName, extension);
            Export(root, type, additionalAssets, path);
        }

        public static void Export(GameObject root, BackgroundHelper.ExportType type, string[] additionalAssets, string path)
        {
            GameObject clonedObject = null;

            AssetDatabase.DisallowAutoRefresh();

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }
                string fileName = Path.GetFileName(path);
                string folderPath = Path.GetDirectoryName(path);

                // First we'll collect all shaders and build a separate bundle out of them
                // for Mac as no other build target will include Metal shaders
                // And we want our background to work everywhere

                // We use materials as "anchors" to make sure all required
                // shader variants are included
                var metalAssetBundleName = fileName + BUNDLE_OSX_SUFFIX;
                var materialAssets = EditorUtility.CollectDependencies(new[] { root })
                    .OfType<Material>() // Only material dependencices
                    .Select((mat, i) =>
                    {
                        // Create a clone
                        var matClone = new Material(mat);
                        // Avoid name collision
                        matClone.name = BACKGOUND_OSX_MATERIAL_PREFIX + i.ToString() + mat.name;
                        // Drop all textures to not double resulting yarground in size
                        if (matClone.mainTexture != null)
                        {
                            matClone.mainTexture = Texture2D.whiteTexture;
                        }
                        foreach (var id in matClone.GetTexturePropertyNameIDs())
                        {
                            if (matClone.GetTexture(id) != null)
                            {
                                matClone.SetTexture(id, Texture2D.whiteTexture);
                            }
                        }
                        var assetPath = Path.Combine("Assets", matClone.name + ".mat");
                        AssetDatabase.CreateAsset(matClone, assetPath);

                        return assetPath;
                    })
                    .ToArray();

                var shaderAssets = EditorUtility.CollectDependencies(new[] { root })
                    .OfType<Shader>().Select(AssetDatabase.GetAssetPath);

                if (materialAssets.Length > 0)
                {
                    var metalAssetBundleBuild = default(AssetBundleBuild);
                    metalAssetBundleBuild.assetBundleName = metalAssetBundleName;
                    metalAssetBundleBuild.assetNames = materialAssets.Concat(shaderAssets).ToArray();

                    BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
                        new[]
                        {
                            metalAssetBundleBuild
                        }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
                        BuildTarget.StandaloneOSX);

                    var filePath = Path.Combine(Application.temporaryCachePath, metalAssetBundleName);

                    if (!File.Exists(filePath))
                    {
                        EditorUtility.DisplayDialog("Export Unsuccessful", "Failed to build MacOS Shader bundle. See console for more info.", "OK");
                        throw new FileNotFoundException("MacOS Shader bundle failed to build. <a href=\"https://wiki.yarg.in/wiki/Venue_Creation\">Please ensure you have the \"MacOS Build Support (Mono)\" module installed.</a>");
                    }

                    var assetPath = Path.Combine(Application.dataPath, metalAssetBundleName);
                    File.Move(filePath, assetPath);
                    AssetDatabase.ImportAsset(Path.Combine("Assets", metalAssetBundleName));
                }
                // Now delete our material clones
                foreach (var assetPath in materialAssets)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                clonedObject = UnityEngine.Object.Instantiate(root.gameObject);

                var objectPath = type switch
                {
                    BackgroundHelper.ExportType.Character => CHARACTER_PREFAB_PATH,
                    BackgroundHelper.ExportType.Background => BACKGROUND_PREFAB_PATH,
                    _ => GENERIC_PREFAB_PATH
                };

                var bundledAudioAssets = Array.Empty<string>();

                if (type == BackgroundHelper.ExportType.Background)
                {
                    bundledAudioAssets = BundleAudioAssets(root);
                }

                var assetPaths = new[]
                {
                    Path.Combine("Assets/", metalAssetBundleName),
                    objectPath
                }
                .Concat(additionalAssets)
                .Concat(bundledAudioAssets)
                .ToArray();

                AssetBundleBuild assetBundleBuild = default;
                assetBundleBuild.assetBundleName = fileName;
                assetBundleBuild.assetNames = assetPaths;

                // We must examine anything that has the VenueCharacter component so we can deal with animations
                // properly. First we find them, then check for an AnimatorController and extract the layers and
                // animation states contained within. Then we assign them to a SerializedField on the VenueCharacter,
                // which will hopefully end up on the character when the AssetBundle is built.

                var characterComponents = clonedObject.GetComponentsInChildren<VenueCharacter>();

                foreach (var character in characterComponents)
                {
                    var animator = character.GetComponent<Animator>();
                    if (animator == null)
                    {
                        continue;
                    }

                    // This should work since we're in the editor
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    if (controller == null)
                    {
                        continue;
                    }

                    var layerStates = new AnimationDictionary();
                    foreach (var layer in controller.layers)
                    {
                        var layerName = layer.name;
                        foreach (var state in layer.stateMachine.states)
                        {
                            layerStates.Add(layerName, state.state.name);
                        }
                    }

                    character.LayerStates = layerStates;
                }

                PrefabUtility.SaveAsPrefabAsset(clonedObject.gameObject, objectPath);

                BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
                    new[]
                    {
                        assetBundleBuild
                    }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.StandaloneWindows);

                foreach (var asset in assetPaths)
                {
                    AssetDatabase.DeleteAsset(asset);
                }

                if (AssetDatabase.IsValidFolder("Assets/__YARG_AudioBundle"))
                {
                    AssetDatabase.DeleteAsset("Assets/__YARG_AudioBundle");
                }

                // If the file exists, delete it (to replace it)
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                // Unity seems to save the file in lower case, which is a problem on Linux, as file systems are case sensitive there
                File.Move(Path.Combine(Application.temporaryCachePath, fileName.ToLowerInvariant()), path);

                EditorUtility.DisplayDialog("Export Successful!", "Export Successful!", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();

                if (AssetDatabase.IsValidFolder("Assets/__YARG_AudioBundle"))
                {
                    AssetDatabase.DeleteAsset("Assets/__YARG_AudioBundle");
                }

                if (clonedObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(clonedObject);
                }
            }
        }

        private static string GetRootAssetDirectory(GameObject root)
        {
            string assetPath = string.Empty;

            if (root.scene.IsValid())
            {
                assetPath = root.scene.path;
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = AssetDatabase.GetAssetPath(root);
            }

            return string.IsNullOrEmpty(assetPath) ? null : Path.GetDirectoryName(assetPath);
        }

        private static string[] BundleAudioAssets(GameObject root)
        {
            var directory = GetRootAssetDirectory(root);
            if (string.IsNullOrEmpty(directory) || !AssetDatabase.IsValidFolder(directory))
            {
                return Array.Empty<string>();
            }

            var tempDirectory = Path.Combine("Assets", "__YARG_AudioBundle");
            if (AssetDatabase.IsValidFolder(tempDirectory))
            {
                AssetDatabase.DeleteAsset(tempDirectory);
            }

            AssetDatabase.CreateFolder("Assets", "__YARG_AudioBundle");

            var fullRoot = Path.GetFullPath(directory);
            var fullTemp = Path.GetFullPath(tempDirectory);

            var audioFiles = Directory
                .EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Where(file => AUDIO_FILE_EXTENSIONS.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .ToArray();

            var audioAssets = new List<string>();
            foreach (var audioFile in audioFiles)
            {
                var fullAudioPath = Path.GetFullPath(audioFile);

                // Skip files that are in the temp directory
                if (fullAudioPath.StartsWith(fullTemp, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(fullRoot, fullAudioPath);
                var tempAssetPath = Path.Combine(tempDirectory, relativePath + ".bytes");
                var tempAssetDirectory = Path.GetDirectoryName(tempAssetPath);

                if (!AssetDatabase.IsValidFolder(tempAssetDirectory))
                {
                    Directory.CreateDirectory(tempAssetDirectory);
                }

                File.Copy(audioFile, tempAssetPath, true);
                AssetDatabase.ImportAsset(tempAssetPath);
                audioAssets.Add(tempAssetPath);
            }

            return audioAssets.ToArray();
        }

#endif

    }
}
