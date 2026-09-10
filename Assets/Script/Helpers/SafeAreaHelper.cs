using UnityEngine;

namespace YARG.Helpers
{
    /// <summary>
    ///     <see cref="Screen.safeArea"/> helpers for edge-anchored UI, so it
    ///     clears notches, the Dynamic Island and the home indicator on
    ///     phones. All canvases scale with screen size, so insets are
    ///     converted from screen pixels to canvas units.
    /// </summary>
    public static class SafeAreaHelper
    {
        /// <summary>
        ///     Safe-area insets in canvas units for the canvas containing
        ///     <paramref name="ui"/>: x=left, y=bottom, z=right, w=top.
        ///     Call from Start or later, after the CanvasScaler has applied
        ///     the real scale factor.
        /// </summary>
        public static Vector4 GetInsets(Component ui)
        {
            var canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return Vector4.zero;
            }

            float scale = canvas.rootCanvas.scaleFactor;
            if (scale <= 0f)
            {
                return Vector4.zero;
            }

            var safeArea = Screen.safeArea;
            return new Vector4(
                safeArea.xMin,
                safeArea.yMin,
                Screen.width - safeArea.xMax,
                Screen.height - safeArea.yMax) / scale;
        }
    }
}
