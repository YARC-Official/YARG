using UnityEditor;
using UnityEngine;

namespace YARG.Editor
{
    [InitializeOnLoad]
    public static class ThreadPoolKiller
    {
        static ThreadPoolKiller()
        {
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        private static void PlayModeStateChanged(PlayModeStateChange newState)
        {
            if (newState != PlayModeStateChange.ExitingPlayMode)
                return;

            Debug.Log($"Forcing script reload to kill active thread pool threads...");
            EditorUtility.RequestScriptReload();
        }
    }
}