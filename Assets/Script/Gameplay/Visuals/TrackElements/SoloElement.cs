using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class SoloElement : TrackElement<TrackPlayer>
    {
        [SerializeField]
        private MeshRenderer _meshRenderer;
        public TrackPlayer.Solo SoloRef { get; set; }
        public override double ElementTime => SoloRef.StartTime;
        public double MiddleTime => SoloRef.StartTime + ((SoloRef.EndTime - SoloRef.StartTime) / 2);
        private float ZLength => (float) (SoloRef.EndTime - SoloRef.StartTime * Player.NoteSpeed);
        // not sure that we really need the +3.5
        protected new float RemovePointOffset => (float) ((SoloRef.EndTime - SoloRef.StartTime) * Player.NoteSpeed + 3.5);

        protected override void InitializeElement()
        {
            // More correctly, this would get the unscaled size of the object
            // Since we currently use Unity's plane, this works
            const float zSize = 10.0f;
            float childZBasePosition = zSize / 2;
            var zScale = (float) (SoloRef.EndTime - SoloRef.StartTime) * Player.NoteSpeed / zSize;

            var cachedTransform = _meshRenderer.transform;

            // A bit of hackery is necessary to avoid the rescaling of the
            // parent from messing up the scaling of the children
            var children = cachedTransform.GetComponentsInChildren<Transform>();
            var scaleFactor = zScale / zSize;
            foreach (var child in children)
            {
                if (child == cachedTransform)
                {
                    continue;
                }
                // Change the child's scale such that their world size remains the same after the parent scales
                var originalScale = 0.005f; // this should be child.localScale.z, but that causes issues if the object gets reused
                var newScale = originalScale / scaleFactor;
                child.localScale = child.localScale.WithZ(newScale);
                // Adjust the child's position to reflect the new scale
                var signFactor = Math.Sign(child.localPosition.z);
                var newZ = (childZBasePosition + newScale * childZBasePosition) * signFactor;
                // This fudge shouldn't be necessary, but without it there is sometimes
                // a visible gap in the rail between the transition and main section
                // I assume this is because of rounding errors with small float values
                newZ += 0.001f * -signFactor;

                child.localPosition = child.localPosition.WithZ(newZ);
            }
            // With the adjustments to the children made, we can scale the
            // parent and have everything end up in the right place
            cachedTransform.localScale = cachedTransform.localScale.WithZ(zScale);
        }

        protected override bool UpdateElementPosition()
        {
            // Calibration is not taken into consideration here, as that is instead handled in more
            // critical areas such as the game manager and players
            float z =
                TrackPlayer.STRIKE_LINE_POS                          // Shift origin to the strike line
                + (float) (MiddleTime - GameManager.RealVisualTime) // Get time of note relative to now
                * Player.NoteSpeed;                                  // Adjust speed (units/s)

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithZ(z);

            if (z < REMOVE_POINT - RemovePointOffset)
            {
                ParentPool.Return(this);
                return false;
            }

            return true;
        }


        protected override void HideElement()
        {

        }

        protected override void UpdateElement()
        {

        }
    }
}
