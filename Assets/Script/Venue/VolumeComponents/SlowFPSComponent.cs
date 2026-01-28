using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace YARG.Venue.VolumeComponents
{
    [Serializable]
    [VolumeComponentMenu("Venue/Slow FPS")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
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