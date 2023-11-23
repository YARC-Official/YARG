using UnityEditor;
using UnityEngine;

namespace YARG.Editor
{
    [InitializeOnLoad]
    public static class ThreadPoolKiller
    {
        // TODO: The saved state doesn't seem to get re-loaded when restarting Unity
        // Leaving this as-is for now, since the default is to not force-kill which is less obstructive
        [FilePath("UserSettings/YARG/ThreadPoolKiller.asset", FilePathAttribute.Location.ProjectFolder)]
        private class ThreadPoolKillerState : ScriptableSingleton<ThreadPoolKillerState>
        {
            [field: SerializeField]
            public bool Enabled { get; private set; }

            public bool Toggle()
            {
                Enabled = !Enabled;
                Save(true);
                return Enabled;
            }
        }

        private const string FORCE_KILL = "YARG/Force-Reload Scripts";
        private const string FORCE_KILL_ON_EXIT = "YARG/Force-Reload Scripts on Play Exit (to kill thread pool)";

        static ThreadPoolKiller()
        {
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
            Menu.SetChecked(FORCE_KILL_ON_EXIT, ThreadPoolKillerState.instance.Enabled);
        }

        [MenuItem(FORCE_KILL, false, 100)]
        private static void ForceKillThreadPool()
        {
            Debug.Log($"Forcing script reload to kill active thread pool threads...");
            EditorUtility.RequestScriptReload();
        }

        [MenuItem(FORCE_KILL_ON_EXIT, false, 100)]
        private static void SetEnabled()
        {
            Menu.SetChecked(FORCE_KILL_ON_EXIT, ThreadPoolKillerState.instance.Toggle());
        }

        private static void PlayModeStateChanged(PlayModeStateChange newState)
        {
            if (!ThreadPoolKillerState.instance.Enabled || newState != PlayModeStateChange.ExitingPlayMode)
                return;

            ForceKillThreadPool();
        }
    }
}