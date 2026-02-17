namespace YARG.Venue
{
    public static class VenueEditorHelper
    {
        private const string ENABLED_KEY = "YARG_VENUE_SCENE_ENABLED";
        private const string SCENE_PATH_KEY = "YARG_VENUE_SCENE_PATH";

        public static bool TryGetScenePath(out string scenePath)
        {
#if UNITY_EDITOR
            if (!UnityEditor.EditorPrefs.GetBool(ENABLED_KEY, false))
            {
                scenePath = null;
                return false;
            }

            scenePath = UnityEditor.EditorPrefs.GetString(SCENE_PATH_KEY, string.Empty);
            return !string.IsNullOrEmpty(scenePath);
#else
            scenePath = null;
            return false;
#endif
        }

#if UNITY_EDITOR
        public static void SetScenePath(string scenePath, bool enabled = true)
        {
            UnityEditor.EditorPrefs.SetString(SCENE_PATH_KEY, scenePath);
            UnityEditor.EditorPrefs.SetBool(ENABLED_KEY, enabled);
        }

        public static void ClearScenePath()
        {
            UnityEditor.EditorPrefs.DeleteKey(SCENE_PATH_KEY);
            UnityEditor.EditorPrefs.DeleteKey(ENABLED_KEY);
        }

        public static void SetSceneEnabled(bool enabled)
        {
            UnityEditor.EditorPrefs.SetBool(ENABLED_KEY, enabled);
        }

        public static bool IsSceneEnabled()
        {
            return UnityEditor.EditorPrefs.GetBool(ENABLED_KEY, false);
        }
#endif
    }
}