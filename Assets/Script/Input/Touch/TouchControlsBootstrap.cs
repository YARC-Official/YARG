using UnityEngine;
using UnityEngine.SceneManagement;

namespace YARG.Input.Touch
{
    /// <summary>
    /// Installs the Android touch controls without touching any scene or
    /// prefab: when the Gameplay scene loads on a mobile platform, a
    /// <see cref="TouchControlsManager"/> is added to a fresh GameObject.
    /// On desktop / editor builds this does nothing.
    /// </summary>
    public static class TouchControlsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Landscape lock + never sleep on Android (belt and braces next to
            // the PlayerSettings default orientation, which is already landscape).
            if (Application.platform == RuntimePlatform.Android)
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (!Application.isMobilePlatform)
                return;

            if (scene.name != "Gameplay")
                return;

            if (Object.FindAnyObjectByType<TouchControlsManager>() != null)
                return;

            var host = new GameObject("TouchControls");
            host.AddComponent<TouchControlsManager>();
        }
    }
}
