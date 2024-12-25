using UnityEngine;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class ProKeysShiftIndicatorElement : TrackElement<ProKeysPlayer>
    {
        public ProKeysPlayer.RangeShiftIndicator RangeShiftIndicator;

        public override double ElementTime => RangeShiftIndicator.Time;

        protected override void InitializeElement()
        {
            var cachedTransform = transform;
            cachedTransform.localScale = cachedTransform.localScale
                .WithX(RangeShiftIndicator.LeftSide ? -1f : 1f);
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}