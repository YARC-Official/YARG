using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using static YARG.Themes.ThemeManager;

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
#endif

namespace YARG.Themes
{
    // WARNING: Changing this could break themes or venues!
    //
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class ThemeComponent : MonoBehaviour
    {
        [SerializeField]
        private GameObject _fiveFretNotes;
        [SerializeField]
        private GameObject _fourLaneNotes;
        [SerializeField]
        private GameObject _fiveLaneNotes;
        [SerializeField]
        private GameObject _proKeysNotes;

        [Space]
        [SerializeField]
        private GameObject _fiveFretFret;
        [SerializeField]
        private GameObject _fourLaneFret;
        [SerializeField]
        private GameObject _fiveLaneFret;

        [Space]
        [SerializeField]
        private GameObject _whiteKey;
        [SerializeField]
        private GameObject _blackKey;

        [Space]
        [SerializeField]
        private GameObject _kickFret;

        public Dictionary<ThemeNoteType, GameObject> GetNoteModelsForVisualStyle(VisualStyle style, bool starPower)
        {
            var parent = style switch
            {
                VisualStyle.FiveFretGuitar or
                VisualStyle.FiveLaneKeys   => _fiveFretNotes,
                VisualStyle.FourLaneDrums  => _fourLaneNotes,
                VisualStyle.FiveLaneDrums  => _fiveLaneNotes,
                VisualStyle.ProKeys        => _proKeysNotes,
                _ => null // future VisualStyle values — caller falls back to default
            };

            if (parent == null) return new Dictionary<ThemeNoteType, GameObject>();

            var dict = new Dictionary<ThemeNoteType, GameObject>();

            // Fetch all of the theme notes
            var themeNotes = parent.GetComponentsInChildren<ThemeNote>();
            foreach (var themeNote in themeNotes)
            {
                // Make sure we choose the correct variant
                if (themeNote.StarPowerVariant != starPower) continue;

                dict.Add(themeNote.NoteType, themeNote.gameObject);
            }

            return dict;
        }

        public GameObject GetModelForVisualStyle(VisualStyle style, string name)
        {
            return name switch
            {
                ThemeManager.FRET_PREFAB_NAME      => GetFretModelForVisualStyle(style),
                ThemeManager.KICK_FRET_PREFAB_NAME => _kickFret,
                ThemeManager.WHITE_KEY_PREFAB_NAME => _whiteKey,
                ThemeManager.BLACK_KEY_PREFAB_NAME => _blackKey,
                _                                  => null
            };
        }

        private GameObject GetFretModelForVisualStyle(VisualStyle style)
        {
            return style switch
            {
                VisualStyle.FiveFretGuitar or
                VisualStyle.FiveLaneKeys => _fiveFretFret,
                VisualStyle.FourLaneDrums  => _fourLaneFret,
                VisualStyle.FiveLaneDrums  => _fiveLaneFret,
                _  => null // future VisualStyle values — caller falls back to default
            };
        }

#if UNITY_EDITOR
        private const string THEME_PREFAB_NAME = "ThemeRoot";
        private const string THEME_META_NAME = "theme_meta";
        private const string THEME_PREFAB_PATH = "Assets/" + THEME_PREFAB_NAME + ".prefab";
        private const string THEME_META_PATH = "Assets/" + THEME_META_NAME + ".asset";
        private const string BUNDLE_OSX_SUFFIX = "_metal.bytes";
        private const string BACKGOUND_OSX_MATERIAL_PREFIX = "_metal_";

        [ContextMenu("Export Theme")]
        public void ExportTheme()
        {
            string path = EditorUtility.SaveFilePanel("Export Theme", string.Empty, "theme", "yargtheme");
            if (string.IsNullOrEmpty(path)) return;

            string fileName = Path.GetFileNameWithoutExtension(path);
            GameObject clonedTheme = null;
            TextAsset metaAsset = null;

            AssetDatabase.DisallowAutoRefresh();

            try
            {
                // 1. Collect all dependencies in a single pass
                var allDeps = EditorUtility.CollectDependencies(new[] { gameObject });
                var depsMaterials = allDeps.OfType<Material>().ToArray();
                var depsShaderAssets = allDeps.OfType<Shader>().Select(AssetDatabase.GetAssetPath).ToArray();

                // 2. Build Metal shader sub-bundle (macOS target)
                var metalAssetBundleName = fileName + BUNDLE_OSX_SUFFIX;
                string metalBundleAssetPath = null;

                var materialAssets = depsMaterials
                    .Select((mat, i) =>
                    {
                        var matClone = new Material(mat);
                        matClone.name = BACKGOUND_OSX_MATERIAL_PREFIX + i.ToString() + mat.name;
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

                if (materialAssets.Length > 0)
                {
                    var metalBuild = new AssetBundleBuild
                    {
                        assetBundleName = metalAssetBundleName,
                        assetNames = materialAssets.Concat(depsShaderAssets).ToArray()
                    };

                    BuildPipeline.BuildAssetBundles(
                        Application.temporaryCachePath,
                        new[] { metalBuild },
                        BuildAssetBundleOptions.ForceRebuildAssetBundle,
                        BuildTarget.StandaloneOSX);

                    var filePath = Path.Combine(Application.temporaryCachePath, metalAssetBundleName);
                    if (!File.Exists(filePath))
                    {
                        EditorUtility.DisplayDialog("Export Unsuccessful",
                            "Failed to build MacOS Shader bundle. Ensure you have the \"MacOS Build Support (Mono)\" module installed.", "OK");
                        throw new FileNotFoundException("MacOS Shader bundle failed to build.");
                    }

                    metalBundleAssetPath = Path.Combine(Application.dataPath, metalAssetBundleName);
                    File.Move(filePath, metalBundleAssetPath);
                    AssetDatabase.ImportAsset(Path.Combine("Assets", metalAssetBundleName));
                }

                // Delete temp material clones
                foreach (var assetPath in materialAssets)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                // 3. Save theme prefab
                clonedTheme = Instantiate(gameObject);
                clonedTheme.name = THEME_PREFAB_NAME;
                PrefabUtility.SaveAsPrefabAsset(clonedTheme, THEME_PREFAB_PATH);

                // 4. Determine SupportedStyles from non-null fields
                var supportedStyles = new List<VisualStyle>();
                if (_fiveFretNotes != null)
                {
                    supportedStyles.Add(VisualStyle.FiveFretGuitar);
                    supportedStyles.Add(VisualStyle.FiveLaneKeys);
                }
                if (_fourLaneNotes != null) supportedStyles.Add(VisualStyle.FourLaneDrums);
                if (_fiveLaneNotes != null) supportedStyles.Add(VisualStyle.FiveLaneDrums);
                if (_proKeysNotes != null) supportedStyles.Add(VisualStyle.ProKeys);

                // 5. Create metadata TextAsset
                var preset = new ThemePreset(fileName, false)
                {
                    Type = "ThemePreset",
                    Id = Guid.NewGuid(),
                    CustomBundlePath = "theme.bundle",
                    SupportedStyles = supportedStyles,
                    FormatVersion = 1
                };

                string jsonText = JsonConvert.SerializeObject(preset, Formatting.Indented);
                metaAsset = new TextAsset(jsonText);
                metaAsset.name = THEME_META_NAME;
                AssetDatabase.CreateAsset(metaAsset, THEME_META_PATH);

                // 6. Build AssetBundle (Windows target) with prefab + meta (+ metal if any)
                var bundleAssetPaths = new List<string> { THEME_PREFAB_PATH, THEME_META_PATH };
                if (!string.IsNullOrEmpty(metalBundleAssetPath))
                {
                    bundleAssetPaths.Add(Path.Combine("Assets/", metalAssetBundleName));
                }

                var mainBuild = new AssetBundleBuild
                {
                    assetBundleName = fileName.ToLowerInvariant() + ".yargtheme",
                    assetNames = bundleAssetPaths.ToArray()
                };

                BuildPipeline.BuildAssetBundles(
                    Application.temporaryCachePath,
                    new[] { mainBuild },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.StandaloneWindows);

                // 7. Copy output to user-chosen path
                if (File.Exists(path)) File.Delete(path);

                var bundleOutput = Path.Combine(Application.temporaryCachePath, mainBuild.assetBundleName);
                File.Move(bundleOutput, path);

                // 8. Cleanup
                foreach (var assetPath in bundleAssetPaths)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                if (!string.IsNullOrEmpty(metalBundleAssetPath) && File.Exists(metalBundleAssetPath))
                {
                    AssetDatabase.DeleteAsset(Path.Combine("Assets", metalAssetBundleName));
                }

                EditorUtility.DisplayDialog("Export Successful!",
                    $"Theme \"{fileName}\" exported to:\n{path}", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                if (clonedTheme != null) DestroyImmediate(clonedTheme);
                if (metaAsset != null) DestroyImmediate(metaAsset);
            }
        }
#endif
    }
}