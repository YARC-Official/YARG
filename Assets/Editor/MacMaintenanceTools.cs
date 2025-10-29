#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace YARG.EditorTools
{
    /// <summary>
    /// Utility helpers to repair common macOS-specific import issues (addressables, meta files, plugin settings).
    /// </summary>
    public static class MacMaintenanceTools
    {
        private const string TAB_ICONS_GROUP_NAME = "TabIcons";
        private static readonly string[] TabIconKeys =
        {
            "Engine",
            "Songs",
            "Sound",
            "Display",
            "Customization",
            "Generic"
        };

        [MenuItem("Tools/YARG/Fix Mac Setup")] 
        public static void FixMacSetup()
        {
            try
            {
                EditorUtility.DisplayProgressBar("YARG Mac Setup", "Updating Addressables", 0f);
                FixTabIconAddressables();

                EditorUtility.DisplayProgressBar("YARG Mac Setup", "Reimporting critical assets", 0.4f);
                ForceReimport("Assets/Prefabs/SettingPreviews/Track.prefab");
                ForceReimport("Assets/Script/Audio");

                EditorUtility.DisplayProgressBar("YARG Mac Setup", "Syncing Discord plugin settings", 0.65f);
                EnsureDiscordPluginImportSettings();

                EditorUtility.DisplayProgressBar("YARG Mac Setup", "Checking BASS dependencies", 0.8f);
                WarnIfBassPluginsMissing();

                EditorUtility.DisplayProgressBar("YARG Mac Setup", "Saving project", 0.9f);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("YARG", "Mac maintenance tasks completed. Review the Console for any warnings.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MacMaintenanceTools] Failed to complete maintenance tasks. {ex}");
                EditorUtility.DisplayDialog("YARG", "Mac maintenance failed. Check the console for details.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void FixTabIconAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[MacMaintenanceTools] AddressableAssetSettings not found; skipping tab icon repair.");
                return;
            }

            var group = settings.groups.FirstOrDefault(g => g != null && g.name == TAB_ICONS_GROUP_NAME);
            if (group == null)
            {
                Debug.LogWarning($"[MacMaintenanceTools] Addressables group '{TAB_ICONS_GROUP_NAME}' not found. Skipping tab icon fixes.");
                return;
            }

            const string tabIconFolder = "Assets/Art/UI/TabIcons";
            var spriteMap = BuildSpriteLookup(tabIconFolder);
            if (spriteMap.Count == 0)
            {
                Debug.LogWarning($"[MacMaintenanceTools] No sprites discovered under '{tabIconFolder}'. Tab icon repair skipped.");
                return;
            }

            var keys = new HashSet<string>(TabIconKeys.Select(k => k.ToLowerInvariant()));
            var entries = group.entries.ToArray();

            bool modified = false;
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                {
                    continue;
                }

                string key = ExtractKeyFromAddress(entry.address);
                if (string.IsNullOrEmpty(key) || !keys.Contains(key.ToLowerInvariant()))
                {
                    continue;
                }

                if (!spriteMap.TryGetValue(key, out var spriteGuid))
                {
                    Debug.LogWarning($"[MacMaintenanceTools] Could not find sprite matching tab key '{key}'.");
                    continue;
                }

                if (entry.guid == spriteGuid)
                {
                    continue; // Already correct
                }

                string address = entry.address;
                settings.RemoveAssetEntry(entry.guid, group, postEvent: false);
                var newEntry = settings.CreateOrMoveEntry(spriteGuid, group, readOnly: false, postEvent: false);
                newEntry.SetAddress(address);
                modified = true;
                Debug.Log($"[MacMaintenanceTools] Rebound tab icon '{address}' to sprite GUID {spriteGuid}.");
            }

            if (modified)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, postEvent: true, settingsModified: true);
                Debug.Log("[MacMaintenanceTools] Tab icon addressables updated. Consider rebuilding Addressables (Build > Clean Build > New Build).");
            }
            else
            {
                Debug.Log("[MacMaintenanceTools] Tab icon addressables already matched local sprites.");
            }
        }

        private static Dictionary<string, string> BuildSpriteLookup(string folderPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return result;
            }

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                result[fileName] = guid;
            }

            return result;
        }

        private static string ExtractKeyFromAddress(string address)
        {
            int start = address.IndexOf('[');
            int end = address.IndexOf(']');
            if (start < 0 || end < 0 || end <= start + 1)
            {
                return string.Empty;
            }

            return address.Substring(start + 1, end - start - 1);
        }

        private static void ForceReimport(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                Debug.LogWarning($"[MacMaintenanceTools] Asset '{assetPath}' not found. Skipping reimport.");
                return;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[MacMaintenanceTools] Reimported '{assetPath}'.");
        }

        private static void EnsureDiscordPluginImportSettings()
        {
            const string dylibPath = "Assets/Plugins/DiscordGameSDK/Plugins/aarch64/discord_game_sdk.dylib";
            const string bundlePath = "Assets/Plugins/DiscordGameSDK/Plugins/aarch64/discord_game_sdk.bundle";

            var dylibImporter = AssetImporter.GetAtPath(dylibPath) as PluginImporter;
            if (dylibImporter != null)
            {
                if (dylibImporter.GetCompatibleWithEditor())
                {
                    dylibImporter.SetCompatibleWithEditor(false);
                    dylibImporter.SaveAndReimport();
                    Debug.Log("[MacMaintenanceTools] Disabled Editor compatibility on discord_game_sdk.dylib.");
                }
            }
            else
            {
                Debug.LogWarning("[MacMaintenanceTools] discord_game_sdk.dylib importer not found.");
            }

            var bundleImporter = AssetImporter.GetAtPath(bundlePath) as PluginImporter;
            if (bundleImporter != null)
            {
                if (!bundleImporter.GetCompatibleWithEditor())
                {
                    bundleImporter.SetCompatibleWithEditor(true);
                    bundleImporter.SaveAndReimport();
                    Debug.Log("[MacMaintenanceTools] Enabled Editor compatibility on discord_game_sdk.bundle.");
                }
            }
            else
            {
                Debug.LogWarning("[MacMaintenanceTools] discord_game_sdk.bundle importer not found.");
            }
        }

        private static void WarnIfBassPluginsMissing()
        {
            string[] expectedPlugins =
            {
                "Assets/Plugins/macOS/libbass.dylib",
                "Assets/Plugins/macOS/libbass_fx.dylib",
                "Assets/Plugins/macOS/libbassmix.dylib",
                "Assets/Plugins/macOS/libbassopus.dylib"
            };

            var missing = expectedPlugins.Where(path => !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path))).ToList();
            if (missing.Count == 0)
            {
                Debug.Log("[MacMaintenanceTools] All expected macOS BASS plugins are present.");
                return;
            }

            Debug.LogWarning($"[MacMaintenanceTools] Missing macOS BASS plugins: {string.Join(", ", missing)}. Add the dylibs to restore drum SFX playback.");
        }
    }
}
#endif
