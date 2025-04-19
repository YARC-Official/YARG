using System;
using System.IO;
using UnityEngine;
using YARG.Gameplay;



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
        public const string BACKGOUND_OSX_MATERIAL_PREFIX = "_metal_";

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

        public void SetupVenueCamera(GameObject bgInstance)
        {
            mainCamera.gameObject.AddComponent<VenueCameraManager>();
            var fsrManager = mainCamera.GetComponent<FSRCameraManager>();
            if (fsrManager != null)
            {
                fsrManager.enabled = false;
                fsrManager.textureParentObject = bgInstance;
                fsrManager.enabled = true;
            }
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

            GameObject clonedBackground = null;

            AssetDatabase.DisallowAutoRefresh();

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                // First we'll collect all shaders and build a separate bundle out of them
                // for Mac as no other build target will include Metal shaders
                // And we want our background to work everywhere

                // We use materials as "anchors" to make sure all required
                // shader variants are included
                // var materialAssets = EditorUtility.CollectDependencies(new[] { gameObject })
                var materialAssets = EditorUtility.CollectDependencies(new[] { gameObject })
                    .OfType<Material>() // Only material dependencices
                    .Select((mat, i) =>
                    {
                        // Create a clone
                        var matClone = new Material(mat);
                        // Avoid name collision
                        matClone.name = BACKGOUND_OSX_MATERIAL_PREFIX + mat.name;
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

                var shaderAssets = EditorUtility.CollectDependencies(new[] { gameObject })
                    .OfType<Shader>().Select(AssetDatabase.GetAssetPath);

                if (materialAssets.Length > 0)
                {
                    var metalAssetBundleBuild = default(AssetBundleBuild);
                    metalAssetBundleBuild.assetBundleName = BACKGROUND_SHADER_BUNDLE_NAME;
                    metalAssetBundleBuild.assetNames = materialAssets.Concat(shaderAssets).ToArray();

                    BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
                        new[]
                        {
                            metalAssetBundleBuild
                        }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
                        BuildTarget.StandaloneOSX);

                    var filePath = Path.Combine(Application.temporaryCachePath, BACKGROUND_SHADER_BUNDLE_NAME);
                    var assetPath = Path.Combine(Application.dataPath, BACKGROUND_SHADER_BUNDLE_NAME);
                    File.Move(filePath, assetPath);
                    AssetDatabase.ImportAsset(Path.Combine("Assets", BACKGROUND_SHADER_BUNDLE_NAME));
                }
                // Now delete our material clones
                foreach (var assetPath in materialAssets)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                clonedBackground = Instantiate(_backgroundReference.gameObject);

                string fileName = Path.GetFileName(path);
                string folderPath = Path.GetDirectoryName(path);

                var assetPaths = new[]
                {
                    Path.Combine("Assets/", BACKGROUND_SHADER_BUNDLE_NAME),
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
                AssetDatabase.AllowAutoRefresh();
                if (clonedBackground != null)
                {
                    DestroyImmediate(clonedBackground);
                }
            }
        }
#endif
    }
}
