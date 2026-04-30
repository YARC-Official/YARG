using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using static YARG.Themes.ThemeManager;

#if UNITY_EDITOR
using System.IO;
using System.IO.Compression;
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
        private const string THEME_PREFAB_PATH = "Assets/_Theme.prefab";
        private const string BUNDLE_OSX_SUFFIX = "_metal.bytes";
        private const string BACKGOUND_OSX_MATERIAL_PREFIX = "_metal_";

        [ContextMenu("Export Theme")]
        public void ExportTheme()
        {
            string path = EditorUtility.SaveFilePanel("Export Theme", string.Empty, "theme", "yargtheme");
            if (string.IsNullOrEmpty(path)) return;

            string fileName = Path.GetFileNameWithoutExtension(path);
            GameObject clonedTheme = null;

            AssetDatabase.DisallowAutoRefresh();

            try
            {
                // 1. Build Metal shader sub-bundle (macOS target)
                var metalAssetBundleName = fileName + BUNDLE_OSX_SUFFIX;
                var materialAssets = EditorUtility.CollectDependencies(new[] { gameObject })
                    .OfType<Material>()
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

                var shaderAssets = EditorUtility.CollectDependencies(new[] { gameObject })
                    .OfType<Shader>().Select(AssetDatabase.GetAssetPath);

                if (materialAssets.Length > 0)
                {
                    var metalBuild = new AssetBundleBuild
                    {
                        assetBundleName = metalAssetBundleName,
                        assetNames = materialAssets.Concat(shaderAssets).ToArray()
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

                    var assetPath = Path.Combine(Application.dataPath, metalAssetBundleName);
                    File.Move(filePath, assetPath);
                    AssetDatabase.ImportAsset(Path.Combine("Assets", metalAssetBundleName));
                }

                // Delete temp material clones
                foreach (var assetPath in materialAssets)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                // 2. Save theme prefab
                clonedTheme = Instantiate(gameObject);
                PrefabUtility.SaveAsPrefabAsset(clonedTheme, THEME_PREFAB_PATH);

                // 3. Build main AssetBundle (Windows target)
                var metalBundleAssetPath = Path.Combine("Assets/", metalAssetBundleName);
                var assetPaths = File.Exists(metalBundleAssetPath)
                    ? new[] { metalBundleAssetPath, THEME_PREFAB_PATH }
                    : new[] { THEME_PREFAB_PATH };

                var mainBuild = new AssetBundleBuild
                {
                    assetBundleName = "theme.bundle",
                    assetNames = assetPaths
                };

                BuildPipeline.BuildAssetBundles(
                    Application.temporaryCachePath,
                    new[] { mainBuild },
                    BuildAssetBundleOptions.ForceRebuildAssetBundle,
                    BuildTarget.StandaloneWindows);

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

                // 5. Create theme.json
                var preset = new ThemePreset(fileName, false)
                {
                    Type = "ThemePreset",
                    Id = Guid.NewGuid(),
                    CustomBundlePath = "theme.bundle",
                    SupportedStyles = supportedStyles,
                    FormatVersion = 1
                };

                string jsonText = JsonConvert.SerializeObject(preset, Formatting.Indented);

                // 6. Create ZIP: theme.json + theme.bundle
                if (File.Exists(path)) File.Delete(path);

                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
                {
                    var jsonEntry = zip.CreateEntry("theme.json");
                    using (var writer = new StreamWriter(jsonEntry.Open()))
                    {
                        writer.Write(jsonText);
                    }

                    var bundleSource = Path.Combine(Application.temporaryCachePath, "theme.bundle");
                    if (File.Exists(bundleSource))
                    {
                        zip.CreateEntryFromFile(bundleSource, "theme.bundle");
                    }
                }

                // 7. Cleanup
                foreach (var asset in assetPaths)
                {
                    if (File.Exists(asset)) AssetDatabase.DeleteAsset(asset);
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
                if (clonedTheme != null)
                {
                    DestroyImmediate(clonedTheme);
                }
            }
        }
#endif
    }
}