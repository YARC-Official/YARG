using System;
using System.IO;
using UnityEngine;


#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace YARG.Venue
{
    public class BundleBackgroundManager : MonoBehaviour
    {
        // DO NOT CHANGE THIS! It will break existing venues
        public const string BACKGROUND_PREFAB_PATH = "Assets/_Background.prefab";
        public const string BACKGROUND_SHADER_BUNDLE_NAME = "_metal_shaders.bytes";

        // DO NOT CHANGE the name of this! I *know* it doesn't follow naming conventions, but it will also break existing
        // venues if we do change it.
        //
        // ReSharper disable once InconsistentNaming
        [SerializeField]
        private Camera mainCamera;

        public AssetBundle Bundle { get; set; }
        public AssetBundle ShaderBundle { get; set; }

        private void Awake()
        {
            // Move object out of the way, so its effects don't collide with the tracks
            transform.position += Vector3.forward * 10_000f;
        }

        private void OnDestroy()
        {
            Bundle.Unload(true);
            if (ShaderBundle != null)
            {
                ShaderBundle.Unload(true);
            }
        }

#if UNITY_EDITOR

        //
        // HUGE thanks to the people over at Trombone Champ and NyxTheShield for giving us this code.
        // This could not be done without them.
        //
        // Code to export a background from the editor.
        //

        private GameObject _backgroundReference;

        [ContextMenu("Export Background")]
        public void ExportBackground()
        {
            _backgroundReference = gameObject;
            string path = EditorUtility.SaveFilePanel("Save Background", string.Empty, "bg", "yarground");

            var selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

            GameObject clonedBackground = null;


            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                // First we'll collect all shaders and build a separate bundle out of them
                // for Mac as no other build target will include Metal shaders
                // And we want our background to work everywhere
                var shaderAssets = EditorUtility.CollectDependencies(new[] { gameObject })
                    .OfType<Shader>() // Only shader dependencices
                    .Select(shader => AssetDatabase.GetAssetPath(shader)) // Get asset path
                    .Where(assetPath => !assetPath.StartsWith("Packages/com.unity")) // Not builtins
                    .ToArray();

                if (shaderAssets.Length > 0)
                {
                    var metalAssetBundleBuild = default(AssetBundleBuild);
                    metalAssetBundleBuild.assetBundleName = BACKGROUND_SHADER_BUNDLE_NAME;
                    metalAssetBundleBuild.assetNames = shaderAssets;

                    BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
                        new[]
                        {
                            metalAssetBundleBuild
                        }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
                        BuildTarget.StandaloneOSX);

                    var filePath = Path.Combine(Application.temporaryCachePath, BACKGROUND_SHADER_BUNDLE_NAME);
                    File.Move(filePath, Path.Combine(Application.dataPath, BACKGROUND_SHADER_BUNDLE_NAME));
                    AssetDatabase.Refresh();
                }

                clonedBackground = Instantiate(_backgroundReference.gameObject);

                string fileName = Path.GetFileName(path);
                string folderPath = Path.GetDirectoryName(path);

                var assetPaths = new[]
                {
                    "Assets/" + BACKGROUND_SHADER_BUNDLE_NAME,
                    BACKGROUND_PREFAB_PATH
                };

                AssetBundleBuild assetBundleBuild = default;
                assetBundleBuild.assetBundleName = fileName;
                assetBundleBuild.assetNames = assetPaths;

                PrefabUtility.SaveAsPrefabAsset(clonedBackground.gameObject, BACKGROUND_PREFAB_PATH);

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
                if (clonedBackground != null)
                {
                    DestroyImmediate(clonedBackground);
                }
            }
        }
#endif
    }
}
