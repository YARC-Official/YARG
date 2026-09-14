#if UNITY_IOS || UNITY_ANDROID
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YARG.Helpers.UI
{
    /// <summary>
    ///     The menu canvases scale to match their 1920-unit reference width,
    ///     which on phone aspect ratios wider than 16:9 leaves fewer than the
    ///     designed 1080 vertical canvas units — bottom-anchored bars then
    ///     ride up over layouts authored for the full height. Matching height
    ///     instead preserves the designed vertical space and spreads the
    ///     extra width. Rechecked on scene loads (new canvases) and on
    ///     resolution changes: the app boots in portrait, where the aspect
    ///     test fails, and only settles into landscape a few frames later.
    /// </summary>
    internal class WideScreenCanvasScale : MonoBehaviour
    {
        private int _lastWidth;
        private int _lastHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject(nameof(WideScreenCanvasScale));
            Object.DontDestroyOnLoad(host);
            host.AddComponent<WideScreenCanvasScale>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            // Small epsilon so exact 16:9 screens keep their current scaling.
            // Once wide-landscape is seen the scalers stay switched — the
            // game plays landscape-only, so a transient portrait reading must
            // never flip them back.
            if ((float) Screen.width / Screen.height <= 16f / 9f + 0.01f)
            {
                return;
            }

            var scalers = FindObjectsByType<CanvasScaler>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var scaler in scalers)
            {
                if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    scaler.matchWidthOrHeight = 1f;
                }
            }
        }
    }
}
#endif
