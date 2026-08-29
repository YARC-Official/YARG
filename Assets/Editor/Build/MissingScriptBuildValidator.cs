using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor.Build
{
    /// <summary>
    /// Scans all scenes and prefabs for missing C# scripts, throwing an error if any are found.
    /// Runs automatically at the start of standalone player builds in the Editor and CI (never in Play Mode).
    /// </summary>
    public class MissingScriptBuildValidator : IPreprocessBuildWithReport
    {
        // Runs early (before default order 0) to fail fast before asset packaging and player compilation.
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateScenes();
            ValidatePrefabs();
        }

        private static void ValidateScenes()
        {
            var scenesToValidate = new List<string>();

            foreach (var sceneSetting in EditorBuildSettings.scenes)
            {
                if (sceneSetting.enabled && !string.IsNullOrEmpty(sceneSetting.path))
                {
                    scenesToValidate.Add(sceneSetting.path);
                }
            }

            foreach (var scenePath in scenesToValidate)
            {
                var scene = SceneManager.GetSceneByPath(scenePath);
                bool wasLoaded = scene.isLoaded;

                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        ValidateGameObject(root, scenePath);
                    }
                }
                finally
                {
                    if (!wasLoaded && scene.IsValid())
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }

        private static void ValidatePrefabs()
        {
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    ValidateGameObject(prefab, path);
                }
            }
        }

        private static void ValidateGameObject(GameObject targetObject, string assetPath)
        {
            var transforms = targetObject.GetComponentsInChildren<Transform>(true);
            foreach (var transform in transforms)
            {
                var current = transform.gameObject;
                int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current);
                if (count > 0)
                {
                    throw new BuildFailedException($"Build aborted: Found {count} missing script(s) on '{current.name}' in '{assetPath}'.");
                }
            }
        }
    }
}