using UnityEngine;
using YARG.Core.Engine.Track;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class HitWindowDisplay : MonoBehaviour
    {
        public void SetHitWindowInfo(TrackEngineParameters param, float noteSpeed)
        {
            if (!SettingsManager.Settings.ShowHitWindow.Data)
            {
                Destroy(gameObject);
                return;
            }

            // Offsetting is done based on half of the size
            float baseOffset = (float) (-param.FrontEnd - param.BackEnd) / 2f;

            var transformCache = transform;
            transformCache.localScale = transformCache.localScale
                .WithY((float) param.HitWindow * noteSpeed);
            transformCache.localPosition = transformCache.localPosition
                .AddZ(baseOffset);
        }
    }
}
