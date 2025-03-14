using UnityEngine;

namespace YARG.Helpers
{
    public static class ScreenHelper
    {
        private static Resolution _bestResolution;
        private static Vector2Int _lastScreenPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate += RefreshResolutions;
        }

        private static void RefreshResolutions()
        {
            if (Screen.mainWindowPosition == _lastScreenPosition)
            {
                return;
            }

            _lastScreenPosition = Screen.mainWindowPosition;

            var screenInfo = Screen.mainWindowDisplayInfo;
            float screenAspect = screenInfo.width / screenInfo.height;

            var resolutions = Screen.resolutions;

            // Search for the highest resolution that matches the screen's aspect ratio
            var highest = new Resolution();
            int highestArea = 0;
            foreach (var r in resolutions)
            {
                int area = r.width * r.height;
                float aspect = (float) r.width / r.height;

                if (Mathf.Approximately(aspect, screenAspect) &&
                    (area > highestArea || (area == highestArea && r.refreshRate > highest.refreshRate)))
                {
                    highest = r;
                    highestArea = area;
                }
            }

            if (highest.width > 0 && highest.height > 0 && highest.refreshRate > 0)
            {
                _bestResolution = highest;
                return;
            }

            // No matching resolutions, fall back to just the highest one instead
            highest = new Resolution();
            highestArea = 0;
            foreach (var r in resolutions)
            {
                int area = r.width * r.height;

                if (area > highestArea || (area == highestArea && r.refreshRate > highest.refreshRate))
                {
                    highest = r;
                    highestArea = area;
                }
            }

            _bestResolution = highest;
        }

        /// <summary>
        /// Determines the best-fit/"default" resolution to use for the display
        /// </summary>
        public static Resolution GetDefaultResolution()
        {
            RefreshResolutions();
            return _bestResolution;
        }
    }
}