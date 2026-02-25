using UnityEngine;
using YARG.Assets.Script.Gameplay.Player;
using YARG.Core.Chart;
using YARG.Helpers;

namespace YARG.Gameplay.Visuals
{
    public class FiveLaneKeysRangeIndicatorElement : TrackElement<FiveLaneKeysPlayer>
    {
        public FiveFretRangeShift RangeShift;

        private const float TRACK_WIDTH = 2f;
        private const float TRACK_MIDDLE = 0f;
        private const float RANGE_Y_SCALE = 0.12f;

        private static readonly int _color = Shader.PropertyToID("_Color");

        public override double ElementTime => RangeShift.Time;

        [SerializeField]
        private MeshRenderer _meshRenderer;

        protected override void InitializeElement()
        {
            float scaleDenominator = Player.LaneCount;
            var fretSize = TRACK_WIDTH / scaleDenominator;

            transform.localPosition = Vector3.zero;

            var cachedTransform = _meshRenderer.transform;

            // When in open lane mode, treat GRY[B] ranges as if they contain P
            var rangeIncludesOpen = (Player.UsingOpenLane && RangeShift.Position is (int) FiveFretGuitarFret.Green);
            var rangeShiftSize = RangeShift.Size + (rangeIncludesOpen ? 1 : 0);

            var newXScale = (rangeShiftSize / scaleDenominator) * 2;

            int positionOffset;
            if (Player.UsingOpenLane)
            {
                positionOffset = rangeIncludesOpen ? -1 : 0;
            }
            else
            {
                positionOffset = -1;
            }

            var xPos = -1 + (rangeShiftSize * (fretSize / 2)) + (RangeShift.Position + positionOffset) * fretSize;

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
