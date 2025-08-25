using UnityEngine;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public abstract class VocalElement : BaseElement
    {
        private const float SING_LINE_POS = -5f;
        private const float REMOVE_POINT = -15f;

        protected VocalTrack VocalTrack { get; private set; }

        protected override void GameplayAwake()
        {
            VocalTrack = GetComponentInParent<VocalTrack>();
        }

        protected override bool UpdateElementPosition()
        {
            float x =
                SING_LINE_POS                                    // Shift origin to the sing line
                + (float) (ElementTime - GameManager.VisualTime) // Get time of note relative to now
                * VocalTrack.TrackSpeed;                         // Adjust speed (units/s)

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithX(x);

            if (x < REMOVE_POINT - RemovePointOffset)
            {
                ParentPool.Return(this);
                return false;
            }

            return true;
        }
    }
}