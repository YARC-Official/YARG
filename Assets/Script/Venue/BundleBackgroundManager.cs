using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using YARG.Core.Logging;
#endif

namespace YARG.Venue
{
    public class BundleBackgroundManager : MonoBehaviour
    {
        // DO NOT CHANGE THIS! It will break existing venues
        public const string BACKGROUND_PREFAB_PATH = "Assets/_Background.prefab";

        // DO NOT CHANGE the name of this! I *know* it doesn't follow naming conventions, but it will also break existing
        // venues if we do change it.
        //
        // ReSharper disable once InconsistentNaming
        [SerializeField]
        private Camera mainCamera;

        public AssetBundle Bundle { get; set; }

        private void Awake()
        {
            // Move object out of the way, so its effects don't collide with the tracks
            transform.position += Vector3.forward * 10_000f;

            // TODO: FIX
            // Destroy the default camera (venue has its own)
            // Destroy(GameManager.Instance.DefaultCamera.gameObject);
        }

        private void OnDestroy()
        {
            Bundle.Unload(true);
        }

#if UNITY_EDITOR

        //
        // HUGE thanks to the people over at Trombone Champ and NyxTheShield for giving us this code.
        // This could not be done without them.
        //
        // Code to export a background from the editor.
        //

        private GameObject _backgroundReference;
        public enum ExportType {
            CurrentPlatform,
            AllPlatforms
        }

        [ContextMenu("Export Background")]
        public void ExportBackground(ExportType type)
        {
            _backgroundReference = gameObject;
            string path = EditorUtility.SaveFilePanel("Save Background", string.Empty, "bg", "yarground");
            GameObject clonedBackground = null;

            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                clonedBackground = Instantiate(_backgroundReference.gameObject);

                string fileName = Path.GetFileNameWithoutExtension(path);
                string extension = Path.GetExtension(path);
                string folderPath = Path.GetDirectoryName(path);

                var assetPaths = new[]
                {
                    BACKGROUND_PREFAB_PATH
                };

                PrefabUtility.SaveAsPrefabAsset(clonedBackground.gameObject, BACKGROUND_PREFAB_PATH);
                AssetBundleBuild assetBundleBuild = default;
                assetBundleBuild.assetBundleName = fileName;
                assetBundleBuild.assetNames = assetPaths;

                List<BuildTarget> platforms = type == ExportType.AllPlatforms ? new() {BuildTarget.StandaloneLinux64, BuildTarget.StandaloneWindows, BuildTarget.StandaloneOSX} : new() {EditorUserBuildSettings.activeBuildTarget};

                foreach (var target in platforms) {
                    YargLogger.LogInfo("Exporting for :" + target);

                    try {
                        string outputPath = Path.Join(folderPath, fileName + "_" + target + extension);

                        BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
                            new[]
                            {
                                assetBundleBuild
                            }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
                            target);

                        // If the file exists, delete it (to replace it)
                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                        }


                        // Unity seems to save the file in lower case, which is a problem on Linux, as file systems are case sensitive there
                        File.Move(Path.Combine(Application.temporaryCachePath, fileName.ToLowerInvariant()), outputPath);
                    }  catch (Exception e)
                    {
                        YargLogger.LogException(e, "Failed to bundle background/venue for " + target);
                    }
                }

                foreach (var asset in assetPaths)
                {
                    AssetDatabase.DeleteAsset(asset);
                }

                AssetDatabase.Refresh();


                EditorUtility.DisplayDialog("Export Successful!", "Export Successful!", "OK");
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to bundle background/venue.");
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
