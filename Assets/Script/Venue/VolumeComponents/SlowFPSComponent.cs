using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VolumeComponents
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("Venue/Slow FPS", typeof(UniversalRenderPipeline))]
    public class SlowFPSComponent: VolumeComponent, IPostProcessComponent
    {
        public ClampedIntParameter SkipFrames = new(1, 0, 10);
        [NonSerialized]
        public RenderTexture IntermediateTexture;
        [NonSerialized]
        public int LastFrame;

        public bool IsActive() => SkipFrames.overrideState && SkipFrames.value > 0;
        public bool IsTileCompatible() => true;
    }
}