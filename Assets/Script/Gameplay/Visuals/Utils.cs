using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public static class Utils
    {
        public static Vector2 FadeSettingsToScreenParams(Vector3 trackOrigin, float screenHeight, Camera trackCamera, float zeroFadePosition, float fadeSize)
        {
            var trackZeroFadePosition = new Vector3(trackOrigin.x, trackOrigin.y, zeroFadePosition - fadeSize);
            var trackFullFadePosition = new Vector3(trackOrigin.x, trackOrigin.y, zeroFadePosition);
            var fadeStart = trackCamera.WorldToScreenPoint(trackZeroFadePosition).y / screenHeight;
            var fadeEnd = trackCamera.WorldToScreenPoint(trackFullFadePosition).y / screenHeight;

            return new Vector2(fadeStart, fadeEnd);
        }

    }
}
