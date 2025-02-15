using UnityEngine;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class GuitarShiftIndicatorElement : TrackElement<FiveFretPlayer>
    {
        // TODO: These constants will only work for a five fret track, so work will be required to
        //  make it work with six fret if that ever becomes a thing
        private const float                              WIDTH_NUMERATOR                  = 2f;
        private const float                              WIDTH_DENOMINATOR                = 5f;
        private const float                              SHIFT_INDICATOR_DEFAULT_POSITION = 1f;
        public        FiveFretPlayer.RangeShiftIndicator RangeShiftIndicator;

        public override double ElementTime => RangeShiftIndicator.Time;

        protected override void InitializeElement()
        {
            var cachedTransform = transform;
            var sign = RangeShiftIndicator.LeftSide ? -1f : 1f;
            var xPosition = ((WIDTH_NUMERATOR / WIDTH_DENOMINATOR) * RangeShiftIndicator.Offset) * sign;

            cachedTransform.localScale = cachedTransform.localScale.WithX(sign);
            cachedTransform.localPosition = cachedTransform.localPosition.WithX(xPosition);
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}