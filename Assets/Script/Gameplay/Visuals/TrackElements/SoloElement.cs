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
            // TODO: Here we must take an element from the pool and set the
            //  appropriate z scale for the given solosection
            //  Since we're using a plane that has a base size of 10z, scale
            //  values are a tenth of normal, I think.

            var zScale = (float) (SoloRef.EndTime - SoloRef.StartTime) * Player.NoteSpeed / 10;

            var cachedTransform = _meshRenderer.transform;
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

        // Start is called before the first frame update
        // void Start()
        // {
        //
        // }

        protected override void UpdateElement()
        {

        }

        // Update is called once per frame
        // protected override bool UpdateElementPosition()
        // {
        //
        // }
    }
}
