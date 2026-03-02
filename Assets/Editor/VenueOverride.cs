using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using YARG.Venue;

namespace YARG.Editor
{
    public class VenueOverrideMenu
    {
        private const string MENU_ROOT = "YARG/Venue/";

        [MenuItem(MENU_ROOT + "Select Venue Scene", priority = 10)]
        private static void UseSelectedScene()
        {
            var scene = Selection.activeObject as SceneAsset;
            if (scene == null)
            {
                EditorUtility.DisplayDialog("Error", "No scene selected\nSelect a scene to use as the venue!", "OK");
                return;
            }

            var path = AssetDatabase.GetAssetPath(scene);
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Venue Error", "Could not find venue scene path", "OK");
                return;
            }

            if (!IsSceneValid(path))
            {
                EditorUtility.DisplayDialog("Venue Error", "Selected scene does not contain a BundleBackgroundManager", "OK");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(path);

            var settings = VenueOverrideSettings.instance;
            settings.SceneGuid = guid;
            settings.Enabled = true;
            settings.SaveSettings();

            VenueEditorHelper.SetScenePath(path, true);
        }

        [MenuItem(MENU_ROOT + "Enable Editor Venue", priority = 1)]
        private static void ToggleEnabled()
        {
            var settings = VenueOverrideSettings.instance;
            settings.Enabled = !settings.Enabled;
            settings.SaveSettings();

            VenueEditorHelper.SetSceneEnabled(settings.Enabled);
        }

        [MenuItem(MENU_ROOT + "Enable Editor Venue", true)]
        private static bool ValidateToggleEnabled()
        {
            UnityEditor.Menu.SetChecked(MENU_ROOT + "Enable Editor Venue", VenueOverrideSettings.instance.Enabled);
            return true;
        }

        private static bool IsSceneValid(string scenePath)
        {
            var previousScene = SceneManager.GetActiveScene();

            // If the selected scene is already loaded, we can just check if it contains the venue manager
            if (previousScene.IsValid() && previousScene.isLoaded && previousScene.path == scenePath)
            {
                foreach (var root in previousScene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<BundleBackgroundManager>() != null)
                    {
                        return true;
                    }
                }

                return false;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.GetComponentInChildren<BundleBackgroundManager>() != null)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid())
                {
                    SceneManager.SetActiveScene(previousScene);
                }
            }
        }
    }

    internal sealed class VenueOverrideSettings : ScriptableSingleton<VenueOverrideSettings>
    {
        public bool   Enabled;
        public string SceneGuid;

        public void SaveSettings()
        {
            Save(true);
        }
    }

    internal static class VenueOverride
    {
        public static bool Enabled => VenueOverrideSettings.instance.Enabled;
        public static string SceneGuid => VenueOverrideSettings.instance.SceneGuid;

        public static bool TryGetVenuePath(out string path)
        {
            if (!Enabled || string.IsNullOrEmpty(SceneGuid))
            {
                path = null;
                return false;
            }

            path = AssetDatabase.GUIDToAssetPath(SceneGuid);
            return !string.IsNullOrEmpty(path);
        }
    }
}