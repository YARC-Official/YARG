using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Helpers;

namespace YARG.Gameplay.Visuals
{
    public class GuitarRangeIndicatorElement : TrackElement<FiveFretPlayer>
    {
        public        FiveFretRangeShift RangeShift;

        private const float SCALE_DENOMINATOR    = 5f;
        private const float RANGE_Y_SCALE        = 0.12f;

        private static readonly int _color = Shader.PropertyToID("_Color");

        public override double ElementTime => RangeShift.Time;

        [SerializeField]
        private MeshRenderer _meshRenderer;

        protected override void InitializeElement()
        {
            transform.localPosition = Vector3.zero;

            var cachedTransform = _meshRenderer.transform;
            var newXScale = (RangeShift.Size / SCALE_DENOMINATOR) * 2;

            // TODO: There has got to be a better way to calculate this
            var sign = RangeShift.Position < 2 ? -1 : 1;
            var xPos = ((2 - newXScale) / 2) * (RangeShift.Position == 2 && RangeShift.Size == 3 ? 0f : sign);

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