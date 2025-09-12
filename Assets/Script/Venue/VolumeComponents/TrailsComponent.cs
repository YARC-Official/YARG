using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VolumeComponents
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("Venue/Trails", typeof(UniversalRenderPipeline))]
    public class TrailsComponent : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter length = new(0.5f, 0.35f, 0.85f);

        public bool IsActive() => length.value > 0 && length.overrideState;
        public bool IsTileCompatible() => true;

        public float Length => 1 - length.value;
    }
}