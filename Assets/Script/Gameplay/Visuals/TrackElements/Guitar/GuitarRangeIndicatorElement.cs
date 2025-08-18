using System;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Helpers;

namespace YARG.Gameplay.Visuals
{
    public class GuitarRangeIndicatorElement : TrackElement<FiveFretPlayer>
    {
        public        FiveFretRangeShift RangeShift;

        private const float SCALE_DENOMINATOR = 5f;
        private const float TRACK_WIDTH       = 2f;
        private const float FRET_SIZE         = TRACK_WIDTH / SCALE_DENOMINATOR;
        private const float TRACK_MIDDLE      = 0f;
        private const float RANGE_Y_SCALE     = 0.12f;

        private static readonly int _color = Shader.PropertyToID("_Color");

        public override double ElementTime => RangeShift.Time;

        [SerializeField]
        private MeshRenderer _meshRenderer;

        protected override void InitializeElement()
        {
            transform.localPosition = Vector3.zero;

            var cachedTransform = _meshRenderer.transform;
            var newXScale = (RangeShift.Size / SCALE_DENOMINATOR) * 2;
            var xPos = -1 + (RangeShift.Size * (FRET_SIZE / 2)) + (RangeShift.Position - 1) * FRET_SIZE;

            cachedTransform.localScale = new Vector3(newXScale, RANGE_Y_SCALE, transform.localScale.z);
            cachedTransform.localPosition = new Vector3(xPos, 0.002f, cachedTransform.localPosition.z);
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }

    }
}