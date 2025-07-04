using System;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Menu.Settings;
using YARG.Settings;

namespace YARG.Helpers
{
    public static class ScreenHelper
    {
        private static Resolution _lastScreenResolution;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            UnityEngine.InputSystem.InputSystem.onBeforeUpdate += () =>
            {
                if (SettingsManager.Settings == null)
                {
                    return;
                }

                // Update screen resolution when the screen changes
                var screenResolution = GetScreenResolution();
                if (screenResolution.width != _lastScreenResolution.width ||
                    screenResolution.height != _lastScreenResolution.height ||
                    screenResolution.refreshRate != _lastScreenResolution.refreshRate)
                {
                    _lastScreenResolution = screenResolution;

                    YargLogger.LogFormatDebug("Updating default screen resolution to {0}", screenResolution);

                    // Automatically update resolution if set to default and we're in fullscreen
                    if (SettingsManager.Settings.Resolution.Value == null &&
                        SettingsManager.Settings.FullscreenMode.Value is FullScreenMode.FullScreenWindow
                            or FullScreenMode.ExclusiveFullScreen
                    )
                    {
                        SetResolution(screenResolution);
                    }

                    // Refresh settings display
                    if (SettingsMenu.Instance)
                    {
                        SettingsMenu.Instance.RefreshAndKeepPosition();
                    }
                }
            };
        }

        /// <summary>
        /// Retrieves the resolution of the screen currently being used for the game.
        /// </summary>
        public static Resolution GetScreenResolution()
        {
            var screenInfo = Screen.mainWindowDisplayInfo;
            return new Resolution()
            {
                width = screenInfo.width,
                height = screenInfo.height,
                refreshRate = (int) Math.Round(screenInfo.refreshRate.value),
            };
        }

        /// <summary>
        /// Sets the fullscreen resolution to that of the current screen.
        /// </summary>
        public static void SetResolution(Resolution resolution)
        {
            YargLogger.LogFormatDebug("Changing screen resolution to {0}", resolution);
            var fullscreenMode = SettingsManager.Settings?.FullscreenMode.Value ?? FullScreenMode.FullScreenWindow;
            Screen.SetResolution(resolution.width, resolution.height, fullscreenMode, resolution.refreshRate);
        }
    }
}