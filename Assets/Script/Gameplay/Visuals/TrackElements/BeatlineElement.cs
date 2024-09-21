using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class BeatlineElement : TrackElement<TrackPlayer>
    {
        private const float WEAK_BEAT_SCALE   = 0.04f;
        private const float STRONG_BEAT_SCALE = 0.06f;
        private const float MEASURE_SCALE     = 0.08f;

        private const float WEAK_BEAT_ALPHA   = 0.3f;
        private const float STRONG_BEAT_ALPHA = 0.4f;
        private const float MEASURE_ALPHA     = 0.8f;

        [SerializeField]
        private MeshRenderer _meshRenderer;

        public Beatline BeatlineRef;

        public override double ElementTime => BeatlineRef.Time;

        protected override void InitializeElement()
        {
            transform.localPosition = Vector3.zero;

            float yScale;
            float alpha;

            switch (BeatlineRef.Type)
            {
                case BeatlineType.Measure:
                    yScale = MEASURE_SCALE;
                    alpha = MEASURE_ALPHA;
                    break;
                case BeatlineType.Strong:
                    yScale = STRONG_BEAT_SCALE;
                    alpha = STRONG_BEAT_ALPHA;
                    break;
                case BeatlineType.Weak:
                    yScale = WEAK_BEAT_SCALE;
                    alpha = WEAK_BEAT_ALPHA;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(BeatlineRef.Type));
            }

            var cachedTransform = _meshRenderer.transform;
            cachedTransform.localScale = cachedTransform.localScale.WithY(yScale);

            var material = _meshRenderer.material;
            var color = material.color;
            color.a = alpha;
            material.color = color;
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }
    }
}