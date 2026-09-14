#if UNITY_IOS && !UNITY_EDITOR
using UnityEngine;

namespace YARG.Helpers
{
    /// <summary>
    ///     Phones play landscape only; iPads keep every orientation. Applied
    ///     before the splash screen so the app never spends its first frames
    ///     in portrait, where canvas scaling and safe-area insets would be
    ///     computed for the wrong orientation.
    /// </summary>
    internal static class IOSOrientation
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Apply()
        {
            bool isPad = UnityEngine.iOS.Device.generation.ToString().StartsWith("iPad") ||
                SystemInfo.deviceModel.StartsWith("iPad");
            if (isPad)
            {
                return;
            }

            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
    }
}
#endif
