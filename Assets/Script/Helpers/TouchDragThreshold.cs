using UnityEngine;
using UnityEngine.EventSystems;

namespace YARG.Helpers
{
    /// <summary>
    /// Unity's default drag threshold is 10 pixels — under 4 points on a 3x
    /// phone — so a finger settling on a row already counts as a drag and its
    /// tap is lost. Touch devices get a finger-sized threshold instead.
    /// </summary>
    public static class TouchDragThreshold
    {
        private const float THRESHOLD_MM = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            if (!Application.isMobilePlatform || EventSystem.current == null)
            {
                return;
            }

            int pixels = Mathf.RoundToInt(Screen.dpi / 25.4f * THRESHOLD_MM);
            EventSystem.current.pixelDragThreshold = Mathf.Max(EventSystem.current.pixelDragThreshold, pixels);
        }
    }
}
