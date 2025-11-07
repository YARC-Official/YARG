using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace YARG.Venue.VolumeComponents
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("Venue/Slow FPS", typeof(UniversalRenderPipeline))]
    public class SlowFPSComponent: VolumeComponent, IPostProcessComponent
    {
        [FormerlySerializedAs("SkipFrames")]
        public ClampedIntParameter Divisor = new(1, 0, 10);
        [NonSerialized]
        public RenderTexture IntermediateTexture;
        [NonSerialized]
        public int LastFrame;

        public bool IsActive() => Divisor.overrideState && Divisor.value > 0;
        public bool IsTileCompatible() => true;
    }
}