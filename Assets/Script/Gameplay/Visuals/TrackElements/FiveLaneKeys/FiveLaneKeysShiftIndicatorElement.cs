using UnityEngine;
using YARG.Assets.Script.Gameplay.Player;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class FiveLaneKeysShiftIndicatorElement : TrackElement<FiveLaneKeysPlayer>
    {
        private const float WIDTH_NUMERATOR = 2f;
        private const float SHIFT_INDICATOR_DEFAULT_POSITION = 1f;
        public FiveFretGuitarPlayer.RangeShiftIndicator RangeShiftIndicator;

        public override double ElementTime => RangeShiftIndicator.Time;

        protected override void InitializeElement()
        {
            var cachedTransform = transform;
            var sign = RangeShiftIndicator.RightSide ? -1f : 1f;
            var xPosition = ((WIDTH_NUMERATOR / Player.LaneCount) * RangeShiftIndicator.Offset) * sign;

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
